using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.Core;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// The chat - solo and online (it was online-only and white). Bottom left, above the health
    /// bar: a dark, half-transparent panel (ChatPanel.uss) with the last lines, which fades a
    /// while after the last one came; Enter opens the line to type in (the player's controls
    /// pause - PlayerInputHandler.SetMenuOpen), Enter sends it, Escape closes it. Lines go
    /// through NAV.Core.ChatChannel - online the network carries them to everyone, solo they come
    /// back to the player (and the game's own lines appear there too). Builds its own small
    /// UIDocument on the HUD's panel settings.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private StyleSheet _style;
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField, Min(1)] private int _visibleLines = 8;
        [Tooltip("Seconds the lines stay up after the last one came, when the chat isn't open.")]
        [SerializeField, Min(1f)] private float _lineSeconds = 12f;

        private readonly List<Label> _lines = new();
        private VisualElement _log;
        private TextField _field;
        private bool _typing;
        private float _lastLineTime = -100f;

        /// <summary>The player is typing a line (other keys - the console's, the map's - wait).</summary>
        public static bool IsTyping { get; private set; }

        private void Awake()
        {
            if (_panelSettings == null)
            {
                Debug.LogError($"{nameof(ChatUI)} on '{name}' has no Panel Settings.", this);
                enabled = false;
                return;
            }

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            document.sortingOrder = 50;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (_style != null)
            {
                root.styleSheets.Add(_style);
            }

            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("chat");
            root.Add(box);

            _log = new VisualElement { pickingMode = PickingMode.Ignore };
            _log.AddToClassList("chat__log");
            _log.AddToClassList("chat__log--hidden");
            box.Add(_log);

            _field = new TextField { maxLength = 200 };
            _field.AddToClassList("chat__input");
            _field.style.display = DisplayStyle.None;
            box.Add(_field);
        }

        private void OnEnable()
        {
            ChatChannel.Received += AddLine;
        }

        private void OnDisable()
        {
            ChatChannel.Received -= AddLine;
            if (_typing)
            {
                CloseField();
            }
        }

        private void AddLine(string who, string text)
        {
            if (_log == null)
            {
                return;
            }

            var label = new Label(who == null ? text : $"{who}: {text}") { pickingMode = PickingMode.Ignore }; label.AddToClassList(NAV.Core.Localization.Loc.NoLocClass);
            label.AddToClassList("chat__line");
            if (who == null)
            {
                label.AddToClassList("chat__line--system");
            }

            _log.Add(label);
            _lines.Add(label);
            while (_lines.Count > _visibleLines)
            {
                _log.Remove(_lines[0]);
                _lines.RemoveAt(0);
            }

            _lastLineTime = Time.unscaledTime;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                if (!_typing && enter && (_input == null || !_input.MenuOpen) && !DevConsoleUI.IsOpen)
                {
                    OpenField();
                }
                else if (_typing && enter)
                {
                    ChatChannel.Send(_field.value);
                    CloseField();
                }
                else if (_typing && keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseField();
                }
            }

            bool show = _typing || (_lines.Count > 0 && Time.unscaledTime - _lastLineTime < _lineSeconds);
            _log.EnableInClassList("chat__log--hidden", !show || _lines.Count == 0);
        }

        private void OpenField()
        {
            _typing = true;
            IsTyping = true;
            _field.value = string.Empty;
            _field.style.display = DisplayStyle.Flex;
            _field.schedule.Execute(() => _field.Focus());
            _input?.SetMenuOpen(true);
        }

        private void CloseField()
        {
            _typing = false;
            IsTyping = false;
            _field.style.display = DisplayStyle.None;
            _field.Blur();
            _input?.SetMenuOpen(false);
        }
    }
}
