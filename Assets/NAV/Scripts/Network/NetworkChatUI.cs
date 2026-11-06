using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.Gameplay.Player;

namespace NAV.Network
{
    /// <summary>
    /// Chat between the players of an online session (hidden solo): the last lines in the
    /// bottom-left corner, fading after a while; Enter opens a line to type in (the player's
    /// controls pause meanwhile - PlayerInputHandler.SetMenuOpen), Enter sends it, Escape closes
    /// it. Lines travel through NetworkWorldSync. Builds its own small UIDocument on the HUD's
    /// panel settings.
    /// </summary>
    public class NetworkChatUI : MonoBehaviour
    {
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField, Min(1)] private int _visibleLines = 7;
        [Tooltip("Seconds a line stays on screen when the chat isn't open.")]
        [SerializeField, Min(1f)] private float _lineSeconds = 12f;

        private readonly List<(Label label, float time)> _lines = new();
        private VisualElement _log;
        private TextField _field;
        private bool _typing;
        private NetworkWorldSync _sync;

        private void Start()
        {
            if (!PhotonNetwork.InRoom || _panelSettings == null)
            {
                enabled = false;
                return;
            }

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            document.sortingOrder = 50;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 24;
            box.style.bottom = 150;
            box.style.width = 520;
            root.Add(box);

            _log = new VisualElement { pickingMode = PickingMode.Ignore };
            box.Add(_log);
            _field = new TextField { maxLength = 200 };
            _field.style.display = DisplayStyle.None;
            _field.style.marginTop = 4;
            _field.AddToClassList("nav-input");
            box.Add(_field);

            AddLine(null, $"Online: {PhotonNetwork.CurrentRoom.Name} ({(NetworkSession.IsPvp ? "PvP" : "PvE")}). Enter - chat.");
        }

        private void OnEnable()
        {
            _sync = NetworkWorldSync.Instance != null ? NetworkWorldSync.Instance : FindAnyObjectByType<NetworkWorldSync>();
            if (_sync != null)
            {
                _sync.ChatReceived += HandleChat;
            }
        }

        private void OnDisable()
        {
            if (_sync != null)
            {
                _sync.ChatReceived -= HandleChat;
            }

            if (_typing && _input != null)
            {
                _input.SetMenuOpen(false);
            }
        }

        private void HandleChat(string who, string text)
        {
            AddLine(who, text);
        }

        private void AddLine(string who, string text)
        {
            if (_log == null)
            {
                return;
            }

            var label = new Label(who == null ? text : $"{who}: {text}") { pickingMode = PickingMode.Ignore };
            label.style.color = who == null ? new Color(1f, 0.85f, 0.55f) : new Color(0.98f, 0.95f, 0.88f);
            label.style.fontSize = 16;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextOutlineColor = Color.black;
            label.style.unityTextOutlineWidth = 0.6f;
            _log.Add(label);
            _lines.Add((label, Time.unscaledTime));
            while (_lines.Count > _visibleLines)
            {
                _log.Remove(_lines[0].label);
                _lines.RemoveAt(0);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!_typing && keyboard.enterKey.wasPressedThisFrame && (_input == null || !_input.MenuOpen))
                {
                    OpenField();
                }
                else if (_typing && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                {
                    if (_sync != null)
                    {
                        _sync.SendChat(_field.value);
                    }

                    CloseField();
                }
                else if (_typing && keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseField();
                }
            }

            foreach ((Label label, float time) in _lines)
            {
                float age = Time.unscaledTime - time;
                label.style.opacity = _typing ? 1f : Mathf.Clamp01((_lineSeconds - age) / 2f);
            }
        }

        private void OpenField()
        {
            _typing = true;
            _field.value = string.Empty;
            _field.style.display = DisplayStyle.Flex;
            _field.schedule.Execute(() => _field.Focus());
            _input?.SetMenuOpen(true);
        }

        private void CloseField()
        {
            _typing = false;
            _field.style.display = DisplayStyle.None;
            _field.Blur();
            _input?.SetMenuOpen(false);
        }
    }
}
