using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.Core;
using NAV.Gameplay.Player;
using NAV.Presentation.Map;

namespace NAV.UI
{
    /// <summary>
    /// The map (developer's request 2026-11-10): a minimap top right under the day sign - north
    /// up, the player an arrow in the middle turning with them, other players dots with their
    /// names - and the world map on M: a wooden window over the explored land (MapExploration -
    /// only what this player has seen, the rest under the fog), mouse wheel to zoom, hold a mouse
    /// button and drag to move, M or Esc to close. Both in the HUD kit's style (MapPanel.uss). Adds the console command "reveal".
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private StyleSheet _style;
        [SerializeField] private MapExploration _exploration;
        [SerializeField] private PlayerInputHandler _input;
        [Tooltip("Minimap pixels per metre (its view is ~164 px wide).")]
        [SerializeField, Min(0.05f)] private float _miniScale = 0.6f;
        [SerializeField] private Vector2 _worldZoomRange = new(0.04f, 2f);

        private sealed class View
        {
            public VisualElement Field;
            public VisualElement Layer;
            public readonly Dictionary<Vector2Int, VisualElement> Tiles = new();
            public readonly List<(MapExplorers.Explorer explorer, VisualElement element)> Others = new();
            public float Scale;
            public ArrowElement Player;
        }

        private View _mini;
        private View _world;
        private VisualElement _worldRoot;
        private VisualElement _miniFrame;
        private bool _worldOpen;
        private Vector2 _worldFocus;
        private bool _dragging;
        private Vector2 _dragStart;
        private Vector2 _focusAtDragStart;

        /// <summary>The world map is open (other keys wait).</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_panelSettings == null || _exploration == null)
            {
                Debug.LogError($"{nameof(MapUI)} on '{name}' is missing its Panel Settings or Map Exploration.", this);
                enabled = false;
                return;
            }

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            document.sortingOrder = 40;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (_style != null)
            {
                root.styleSheets.Add(_style);
            }

            // Minimap.
            _miniFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            VisualElement frame = _miniFrame;
            frame.AddToClassList("minimap");
            root.Add(frame);
            _mini = MakeView(frame, "minimap__view", _miniScale);
            var north = new Label(NAV.Core.Localization.Loc.T("N")) { pickingMode = PickingMode.Ignore };
            north.AddToClassList("minimap__north");
            frame.Add(north);
            var key = new Label("M") { pickingMode = PickingMode.Ignore };
            key.AddToClassList("minimap__key");
            frame.Add(key);

            // World map.
            _worldRoot = new VisualElement();
            _worldRoot.AddToClassList("worldmap");
            _worldRoot.AddToClassList("worldmap--hidden");
            root.Add(_worldRoot);
            var window = new VisualElement();
            window.AddToClassList("worldmap__window");
            _worldRoot.Add(window);
            var title = new Label(NAV.Core.Localization.Loc.T("Map"));
            title.AddToClassList("worldmap__title");
            window.Add(title);
            _world = MakeView(window, "worldmap__field", 0.35f);
            _world.Field.pickingMode = PickingMode.Position;
            var hint = new Label(NAV.Core.Localization.Loc.T("Hold the mouse button and drag - move  ·  Mouse wheel - zoom  ·  M - close"));
            hint.AddToClassList("worldmap__hint");
            window.Add(hint);
            _world.Field.RegisterCallback<WheelEvent>(HandleWheel);
            _world.Field.RegisterCallback<PointerDownEvent>(e => { _dragging = true; _dragStart = e.position; _focusAtDragStart = _worldFocus; _world.Field.CapturePointer(e.pointerId); });
            _world.Field.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragging)
                {
                    Vector2 delta = (Vector2)e.position - _dragStart;
                    _worldFocus = ClampToExplored(_focusAtDragStart + new Vector2(-delta.x, delta.y) / _world.Scale);
                }
            });
            _world.Field.RegisterCallback<PointerUpEvent>(e => { _dragging = false; _world.Field.ReleasePointer(e.pointerId); });
            // The button released somewhere the field never heard of (alt-tab, the window lost focus).
            _world.Field.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);

            DevConsole.Register("reveal", "reveal [radius]", "Lifts the map's fog round the player (default 1000 m).", args =>
            {
                float radius = args.Length > 0 ? DevConsole.Float(args, 0, "the radius") : 1000f;
                if (_exploration.Player == null)
                {
                    return "No player yet.";
                }

                _exploration.Reveal(_exploration.Player.position, Mathf.Clamp(radius, 10f, 4000f));
                return $"Revealed {radius:F0} m round the player.";
            });
        }

        private static View MakeView(VisualElement parent, string fieldClass, float scale)
        {
            var view = new View { Scale = scale };
            view.Field = new VisualElement { pickingMode = PickingMode.Ignore };
            view.Field.AddToClassList(fieldClass);
            parent.Add(view.Field);
            view.Layer = new VisualElement { pickingMode = PickingMode.Ignore };
            view.Layer.AddToClassList("map-layer");
            view.Field.Add(view.Layer);
            view.Player = new ArrowElement { pickingMode = PickingMode.Ignore };
            view.Player.AddToClassList("map-marker");
            view.Field.Add(view.Player);
            return view;
        }

        private void OnEnable()
        {
            if (_exploration != null)
            {
                _exploration.TileChanged += HandleTileChanged;
            }
        }

        private void OnDisable()
        {
            if (_exploration != null)
            {
                _exploration.TileChanged -= HandleTileChanged;
            }

            if (_worldOpen)
            {
                SetWorldOpen(false);
            }
        }

        private void HandleTileChanged(MapExploration.Tile tile)
        {
            foreach (View view in new[] { _mini, _world })
            {
                if (!view.Tiles.TryGetValue(tile.Key, out VisualElement element))
                {
                    element = new VisualElement { pickingMode = PickingMode.Ignore };
                    element.AddToClassList("map-tile");
                    element.style.backgroundImage = new StyleBackground(Background.FromTexture2D(tile.Texture));
                    view.Layer.Add(element);
                    view.Tiles[tile.Key] = element;
                    PlaceTile(view, tile.Key, element);
                }

                element.MarkDirtyRepaint();
            }
        }

        private static void PlaceTile(View view, Vector2Int key, VisualElement element)
        {
            float size = MapExploration.TileSize * view.Scale;
            element.style.left = key.x * size;
            element.style.top = -(key.y + 1) * size;
            element.style.width = size;
            element.style.height = size;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.mKey.wasPressedThisFrame && !ChatUI.IsTyping && !DevConsoleUI.IsOpen
                    && (_worldOpen || _input == null || !_input.MenuOpen))
                {
                    SetWorldOpen(!_worldOpen);
                }
                else if (_worldOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    SetWorldOpen(false);
                }
            }

            Transform player = _exploration.Player;
            if (player == null)
            {
                return;
            }

            Vector2 here = _exploration.ToMap(player.position);
            Layout(_mini, here, player);
            if (_worldOpen)
            {
                Layout(_world, _worldFocus, player);
            }
        }

        /// <summary>Puts the layer so focus (map metres) is in the middle of the field, and the markers.</summary>
        private void Layout(View view, Vector2 focus, Transform player)
        {
            Rect field = view.Field.contentRect;
            if (field.width <= 0f)
            {
                return;
            }

            Vector2 centre = new Vector2(field.width * 0.5f, field.height * 0.5f);
            view.Layer.style.left = centre.x - focus.x * view.Scale;
            view.Layer.style.top = centre.y + focus.y * view.Scale;

            Vector2 me = _exploration.ToMap(player.position);
            PlaceMarker(view.Player, centre + new Vector2(me.x - focus.x, -(me.y - focus.y)) * view.Scale);
            view.Player.style.rotate = new Rotate(player.eulerAngles.y);

            // Other players.
            IReadOnlyList<MapExplorers.Explorer> others = MapExplorers.All;
            while (view.Others.Count < others.Count)
            {
                var dot = new DotElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("map-marker"); dot.AddToClassList(NAV.Core.Localization.Loc.NoLocClass);
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("map-marker__name");
                dot.Add(label);
                view.Field.Add(dot);
                view.Others.Add((null, dot));
            }

            for (int i = 0; i < view.Others.Count; i++)
            {
                VisualElement dot = view.Others[i].element;
                bool used = i < others.Count && others[i].Transform != null;
                dot.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used)
                {
                    continue;
                }

                Vector2 at = _exploration.ToMap(others[i].Transform.position);
                PlaceMarker(dot, centre + new Vector2(at.x - focus.x, -(at.y - focus.y)) * view.Scale);
                ((Label)dot[0]).text = others[i].Name;
            }
        }

        private static void PlaceMarker(VisualElement marker, Vector2 position)
        {
            marker.style.left = position.x - 8f;
            marker.style.top = position.y - 8f;
        }

        /// <summary>Keeps the middle of the world map inside the box of the explored land - it
        /// can't be dragged away into the fog and lost.</summary>
        private Vector2 ClampToExplored(Vector2 focus)
        {
            if (!_exploration.HasExplored)
            {
                return focus;
            }

            Rect bounds = _exploration.ExploredBounds;
            return Vector2.Max(bounds.min, Vector2.Min(bounds.max, focus));
        }

        private void HandleWheel(WheelEvent evt)
        {
            float factor = evt.delta.y > 0f ? 0.8f : 1.25f;
            float scale = Mathf.Clamp(_world.Scale * factor, _worldZoomRange.x, _worldZoomRange.y);
            if (!Mathf.Approximately(scale, _world.Scale))
            {
                _world.Scale = scale;
                foreach (KeyValuePair<Vector2Int, VisualElement> pair in _world.Tiles)
                {
                    PlaceTile(_world, pair.Key, pair.Value);
                }
            }

            evt.StopPropagation();
        }

        private void SetWorldOpen(bool open)
        {
            _worldOpen = open;
            IsOpen = open;
            _dragging = false;
            _worldRoot.EnableInClassList("worldmap--hidden", !open);
            // The minimap would peek out from under the window's corner.
            _miniFrame.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            _input?.SetMenuOpen(open);
            if (open && _exploration.Player != null)
            {
                _worldFocus = _exploration.ToMap(_exploration.Player.position);
            }
        }

        /// <summary>The player's arrow: a gold triangle pointing up (north) before rotation.</summary>
        private sealed class ArrowElement : VisualElement
        {
            public ArrowElement()
            {
                style.width = 16;
                style.height = 16;
                generateVisualContent += Draw;
            }

            private static void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.95f, 0.8f, 0.45f);
                painter.strokeColor = new Color(0.2f, 0.12f, 0.05f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(8f, 0f));
                painter.LineTo(new Vector2(15f, 15f));
                painter.LineTo(new Vector2(8f, 11f));
                painter.LineTo(new Vector2(1f, 15f));
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }

        /// <summary>Another player: a light dot with a dark rim.</summary>
        private sealed class DotElement : VisualElement
        {
            public DotElement()
            {
                style.width = 16;
                style.height = 16;
                generateVisualContent += Draw;
            }

            private static void Draw(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.55f, 0.85f, 1f);
                painter.strokeColor = new Color(0.08f, 0.1f, 0.14f);
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                for (int i = 0; i < 14; i++)
                {
                    float angle = i / 14f * Mathf.PI * 2f;
                    var point = new Vector2(8f + Mathf.Cos(angle) * 5f, 8f + Mathf.Sin(angle) * 5f);
                    if (i == 0)
                    {
                        painter.MoveTo(point);
                    }
                    else
                    {
                        painter.LineTo(point);
                    }
                }

                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
            }
        }
    }
}
