using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.Core.Save;
using NAV.Core.Settings;
using NAV.Gameplay.AI;
using NAV.Gameplay.Items;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Player;
using NAV.World.Encounters;
using NAV.World.Generation;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.UI
{
    /// <summary>
    /// The developer console (developer's request 2026-11-10): switched on in Settings >
    /// Controls > Developer, opened and closed with ` (the key under Esc - Ё on a Russian
    /// layout). A band across the top: the output, a line to type in; Enter runs it, Up/Down go
    /// through the last commands, Tab completes a command name, Esc or ` closes. The player's
    /// controls pause while it is open. Errors from the game are printed here too. Commands:
    /// DevConsole.Register (the ones below, and any system's own - the map's "reveal").
    /// </summary>
    public class DevConsoleUI : MonoBehaviour
    {
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private StyleSheet _style;
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private ItemDatabase _items;
        [Tooltip("What 'spawn' can make (Prefabs/Creatures, Prefabs/Bosses).")]
        [SerializeField] private GameObject[] _creatures = new GameObject[0];
        [SerializeField, Min(10)] private int _maxLines = 200;

        private readonly List<string> _history = new();
        private int _historyIndex;
        private VisualElement _panel;
        private ScrollView _log;
        private TextField _field;
        private PlayerMotor _player;

        /// <summary>Open now (the chat and other keys wait).</summary>
        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_panelSettings == null)
            {
                Debug.LogError($"{nameof(DevConsoleUI)} on '{name}' has no Panel Settings.", this);
                enabled = false;
                return;
            }

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            document.sortingOrder = 90;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (_style != null)
            {
                root.styleSheets.Add(_style);
            }

            _panel = new VisualElement(); _panel.AddToClassList(NAV.Core.Localization.Loc.NoLocClass);
            _panel.AddToClassList("console");
            _panel.AddToClassList("console--hidden");
            root.Add(_panel);
            var title = new Label("Developer console - help for the commands, ` or Esc to close");
            title.AddToClassList("console__title");
            _panel.Add(title);
            _log = new ScrollView(ScrollViewMode.Vertical);
            _log.AddToClassList("console__log");
            _panel.Add(_log);
            _field = new TextField();
            _field.AddToClassList("console__input");
            _panel.Add(_field);
            _field.RegisterCallback<KeyDownEvent>(HandleKey, TrickleDown.TrickleDown);

            RegisterCommands();
        }

        private void OnEnable()
        {
            DevConsole.Printed += AddLine;
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            DevConsole.Printed -= AddLine;
            Application.logMessageReceived -= HandleLog;
            if (IsOpen)
            {
                SetOpen(false);
            }
        }

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                AddLine("[error] " + message);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    SetOpen(false);
                }
                else if (GameSettings.Current.DevConsole && NAV.Gameplay.Demo.DemoMode.AllowsDevConsole && !ChatUI.IsTyping && (_input == null || !_input.MenuOpen))
                {
                    SetOpen(true);
                }
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetOpen(false);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            _panel.EnableInClassList("console--hidden", !open);
            _input?.SetMenuOpen(open);
            if (open)
            {
                _field.value = string.Empty;
                // The ` that opened it lands in the field a moment later - clear it then.
                _field.schedule.Execute(() =>
                {
                    _field.value = _field.value.Replace("`", string.Empty).Replace("ё", string.Empty).Replace("Ё", string.Empty);
                    _field.Focus();
                }).StartingIn(30);
            }
            else
            {
                _field.Blur();
            }
        }

        private void HandleKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    string line = _field.value.Replace("`", string.Empty);
                    _field.value = string.Empty;
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        _history.Add(line);
                        _historyIndex = _history.Count;
                        DevConsole.Execute(line);
                    }

                    evt.StopPropagation();
                    _field.schedule.Execute(() => _field.Focus());
                    break;
                case KeyCode.UpArrow:
                    if (_history.Count > 0)
                    {
                        _historyIndex = Mathf.Max(0, _historyIndex - 1);
                        SetField(_history[_historyIndex]);
                    }

                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    _historyIndex = Mathf.Min(_history.Count, _historyIndex + 1);
                    SetField(_historyIndex < _history.Count ? _history[_historyIndex] : string.Empty);
                    evt.StopPropagation();
                    break;
                case KeyCode.Tab:
                    string[] matches = DevConsole.Complete(_field.value.Trim()).ToArray();
                    if (matches.Length == 1)
                    {
                        SetField(matches[0] + " ");
                    }
                    else if (matches.Length > 1)
                    {
                        AddLine(string.Join("  ", matches));
                    }

                    evt.StopPropagation();
                    break;
            }
        }

        private void SetField(string text)
        {
            _field.value = text;
            _field.schedule.Execute(() => _field.SelectRange(text.Length, text.Length));
        }

        private void AddLine(string text)
        {
            if (_log == null)
            {
                return;
            }

            foreach (string part in text.Split('\n'))
            {
                var label = new Label(part);
                label.AddToClassList("console__line");
                if (part.StartsWith("> "))
                {
                    label.AddToClassList("console__line--command");
                }
                else if (part.StartsWith("[error]") || part.StartsWith("Error") || part.StartsWith("Unknown"))
                {
                    label.AddToClassList("console__line--error");
                }

                _log.Add(label);
            }

            while (_log.contentContainer.childCount > _maxLines)
            {
                _log.contentContainer.RemoveAt(0);
            }

            _log.schedule.Execute(() => _log.scrollOffset = new Vector2(0f, float.MaxValue));
        }

        // ---------------------------------------------------------------- commands

        private PlayerMotor Player => _player != null ? _player : _player = FindAnyObjectByType<PlayerMotor>();

        private void RegisterCommands()
        {
            DevConsole.Register("help", "help [command]", "Lists the commands, or tells about one.", args =>
            {
                if (args.Length > 0)
                {
                    DevConsole.Command one = DevConsole.All.FirstOrDefault(c => string.Equals(c.Name, args[0], StringComparison.OrdinalIgnoreCase));
                    return one != null ? $"{one.Usage} - {one.Help}" : $"No command '{args[0]}'.";
                }

                return string.Join("\n", DevConsole.All.Select(c => $"{c.Usage}  -  {c.Help}"));
            });
            DevConsole.Register("clear", "clear", "Clears the console.", _ =>
            {
                _log.Clear();
                return null;
            });
            DevConsole.Register("give", "give <itemId> [count]", "Puts items into the inventory (items lists the ids).", args =>
            {
                if (args.Length == 0)
                {
                    throw new ArgumentException("Which item?");
                }

                ItemDefinition item = FindItem(args[0]);
                if (item == null)
                {
                    return $"No item '{args[0]}'. Try: items {args[0]}";
                }

                int count = Mathf.Max(1, DevConsole.Int(args, 1, 1));
                int left = Player.GetComponent<PlayerInventory>().Inventory.AddItem(item, count);
                return $"Gave {count - left} {item.DisplayName} ({item.Id}){(left > 0 ? $" - {left} did not fit" : string.Empty)}.";
            });
            DevConsole.Register("items", "items [filter]", "Lists item ids (containing the filter).", args =>
            {
                string filter = args.Length > 0 ? args[0] : string.Empty;
                IEnumerable<ItemDefinition> all = _items != null ? _items.Items : Enumerable.Empty<ItemDefinition>();
                var found = all.Where(i => i != null && (i.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || i.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).Select(i => $"{i.Id} ({i.DisplayName})").ToList();
                return found.Count == 0 ? "Nothing." : string.Join(", ", found.Take(80)) + (found.Count > 80 ? $" ... and {found.Count - 80} more" : string.Empty);
            });
            DevConsole.Register("heal", "heal", "Full health.", _ =>
            {
                var health = Player.GetComponent<PlayerHealth>();
                health.SetHealth(health.MaxHealth);
                return "Healed.";
            });
            DevConsole.Register("god", "god", "No damage on/off.", _ =>
            {
                var health = Player.GetComponent<PlayerHealth>();
                health.Invulnerable = !health.Invulnerable;
                return health.Invulnerable ? "God mode ON." : "God mode off.";
            });
            DevConsole.Register("pos", "pos", "Where the player is (and the biome).", _ =>
            {
                Vector3 p = Player.transform.position;
                var world = FindAnyObjectByType<WorldGenerator>();
                var biome = world != null ? world.GetBiomeAt(p) : null;
                return $"x {p.x:F1}  y {p.y:F1}  z {p.z:F1}  {(biome != null ? biome.name : string.Empty)}";
            });
            DevConsole.Register("tp", "tp <x> <z> | tp <x> <y> <z> | tp spawn", "Teleports the player (onto the ground for x z).", args =>
            {
                var world = FindAnyObjectByType<WorldGenerator>();
                Vector3 target;
                if (args.Length == 1 && args[0].Equals("spawn", StringComparison.OrdinalIgnoreCase))
                {
                    if (world == null || !world.HasSpawnPoint)
                    {
                        return "No spawn point.";
                    }

                    target = world.SpawnPoint;
                }
                else if (args.Length >= 3)
                {
                    target = new Vector3(DevConsole.Float(args, 0, "x"), DevConsole.Float(args, 1, "y"), DevConsole.Float(args, 2, "z"));
                }
                else
                {
                    float x = DevConsole.Float(args, 0, "x");
                    float z = DevConsole.Float(args, 1, "z");
                    target = new Vector3(x, world != null ? world.GroundHeight(x, z) + 0.5f : Player.transform.position.y, z);
                }

                Player.Teleport(target, Player.transform.rotation);
                return $"Teleported to {target.x:F0} {target.y:F0} {target.z:F0}.";
            });
            DevConsole.Register("time", "time <hour> [day]", "Sets the time of day (0-24).", args =>
            {
                var clock = FindAnyObjectByType<WorldClock>();
                if (clock == null)
                {
                    return "No clock.";
                }

                float hour = Mathf.Repeat(DevConsole.Float(args, 0, "the hour"), 24f);
                int day = DevConsole.Int(args, 1, clock.Day);
                clock.SetTime(day, hour);
                return $"Day {day}, {Mathf.FloorToInt(hour):00}:{Mathf.FloorToInt(hour % 1f * 60f):00}.";
            });
            DevConsole.Register("weather", "weather [id]", "Lists the weathers, or switches to one now.", args =>
            {
                var weather = FindAnyObjectByType<WeatherSystem>();
                if (weather == null)
                {
                    return "No weather system.";
                }

                if (args.Length == 0)
                {
                    return "Now: " + (weather.Current != null ? weather.Current.Id : "-") + ". All: " + string.Join(", ", weather.Weathers.Where(w => w != null).Select(w => w.Id));
                }

                WeatherDefinition found = weather.Weathers.FirstOrDefault(w => w != null && string.Equals(w.Id, args[0], StringComparison.OrdinalIgnoreCase));
                if (found == null)
                {
                    return $"No weather '{args[0]}'.";
                }

                weather.SetWeather(found, false);
                return $"Weather: {found.Id}.";
            });
            DevConsole.Register("timescale", "timescale <x>", "Game speed (1 = normal).", args =>
            {
                Time.timeScale = Mathf.Clamp(DevConsole.Float(args, 0, "the speed"), 0f, 20f);
                return $"Time scale {Time.timeScale:0.##}.";
            });
            DevConsole.Register("spawn", "spawn <creature> [count]", "Spawns creatures in front of the player (no name = the list).", args =>
            {
                if (args.Length == 0)
                {
                    return "Creatures: " + string.Join(", ", _creatures.Where(c => c != null).Select(c => c.name));
                }

                GameObject prefab = _creatures.FirstOrDefault(c => c != null && c.name.StartsWith(args[0], StringComparison.OrdinalIgnoreCase));
                if (prefab == null)
                {
                    return $"No creature '{args[0]}'. Type spawn for the list.";
                }

                int count = Mathf.Clamp(DevConsole.Int(args, 1, 1), 1, 20);
                int made = 0;
                for (int i = 0; i < count; i++)
                {
                    Vector3 around = Player.transform.position + Player.transform.forward * 8f + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f));
                    if (NavMesh.SamplePosition(around, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    {
                        Instantiate(prefab, hit.position, Quaternion.LookRotation(-Player.transform.forward));
                        made++;
                    }
                }

                return made > 0 ? $"Spawned {made} {prefab.name}." : "No NavMesh in front of the player.";
            });
            DevConsole.Register("killall", "killall [radius]", "Kills the creatures around the player (default 40 m).", args =>
            {
                float radius = args.Length > 0 ? DevConsole.Float(args, 0, "the radius") : 40f;
                int killed = 0;
                foreach (Creature creature in Creature.All.ToList())
                {
                    if (creature != null && creature.IsAlive && Vector3.Distance(creature.transform.position, Player.transform.position) <= radius)
                    {
                        creature.TakeDamage(creature.MaxHealth * 10f);
                        killed++;
                    }
                }

                return $"Killed {killed}.";
            });
            DevConsole.Register("leshy", "leshy [angry|peaceful]", "Brings the Leshy now (angry by default).", args =>
            {
                var wrath = FindAnyObjectByType<ForestWrath>();
                if (wrath == null)
                {
                    return "No ForestWrath here.";
                }

                bool peaceful = args.Length > 0 && args[0].StartsWith("p", StringComparison.OrdinalIgnoreCase);
                wrath.AddWrath(peaceful ? -wrath.Wrath : 24f - wrath.Wrath);
                wrath.SummonNow();
                return peaceful ? "A peaceful Leshy is coming (the omen first)." : "An angry Leshy is coming (the omen first).";
            });
            DevConsole.Register("wrath", "wrath [value]", "Shows or sets the forest's wrath.", args =>
            {
                var wrath = FindAnyObjectByType<ForestWrath>();
                if (wrath == null)
                {
                    return "No ForestWrath here.";
                }

                if (args.Length > 0)
                {
                    wrath.AddWrath(DevConsole.Float(args, 0, "the wrath") - wrath.Wrath);
                }

                return $"Wrath {wrath.Wrath:F1}, Leshy killed {wrath.Defeats} times, cooldown {wrath.CooldownSeconds:F0} s.";
            });
            DevConsole.Register("seed", "seed", "The world's seed.", _ => $"Seed {SaveSystem.CurrentSeed}, world '{SaveSystem.CurrentWorldName}'.");
        }

        private ItemDefinition FindItem(string id)
        {
            if (_items == null)
            {
                return null;
            }

            return _items.Items.FirstOrDefault(i => i != null && string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? _items.Items.FirstOrDefault(i => i != null && string.Equals(i.DisplayName.Replace(" ", string.Empty), id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
