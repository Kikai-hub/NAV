using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.Core.Settings;

namespace NAV.UI
{
    /// <summary>
    /// The Settings window, shared by the main menu and the pause menu (Open(onClosed) from
    /// MainMenuUIController / PauseUIController). Three tabs:
    ///  - Audio: master, effects, ambience, music volume;
    ///  - Controls: mouse sensitivity, invert Y, rebinding of the keyboard/mouse keys (Input
    ///    System interactive rebinding; Esc cancels; a key already used by another action is
    ///    swapped onto it);
    ///  - Graphics: preset, display mode, resolution, VSync, frame limit, field of view, render
    ///    draw distance of trees/rocks, render scale, shadows, anti-aliasing, texture quality.
    /// Every change is written to GameSettings.Current and applied at once (GameSettings.Apply);
    /// the file is saved when the window closes. Rows are built in code from SettingsPanel.uxml's
    /// empty pages. Esc (or Back) closes it; PauseUIController ignores its own Esc while this is
    /// open.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SettingsUIController : MonoBehaviour
    {
        [Tooltip("The project's Input Actions asset - its keyboard/mouse bindings are listed and rebound here.")]
        [SerializeField] private InputActionAsset _actions;
        private const string KeyboardGroup = "Keyboard&Mouse";
        private const float MinObjectDistance = 80f;
        private const float MaxObjectDistance = 400f;

        private static readonly (string Label, string Action, string Part)[] RebindRows =
        {
            ("Move Forward", "Move", "up"),
            ("Move Back", "Move", "down"),
            ("Move Left", "Move", "left"),
            ("Move Right", "Move", "right"),
            ("Jump", "Jump", null),
            ("Sprint", "Sprint", null),
            ("Attack", "Attack", null),
            ("Block / Build Mode", "Block", null),
            ("Use / Pick Up", "Interact", null),
            ("Inventory", "ToggleInventory", null),
            ("Crafting", "ToggleCrafting", null),
            ("Rotate Building Piece", "RotatePiece", null),
        };

        private static readonly string[] DisplayModeNames = { "Fullscreen (Borderless)", "Exclusive Fullscreen", "Windowed" };
        private static readonly FullScreenMode[] DisplayModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        private static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, 240 };
        private static readonly int[] MsaaValues = { 1, 2, 4, 8 };

        private UIDocument _document;
        private VisualElement _root;
        private readonly Dictionary<string, Button> _tabs = new();
        private readonly Dictionary<string, ScrollView> _pages = new();
        private string _currentTab = "audio";
        private Action _onClosed;

        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private int _rebindEndedFrame = -1;
        private readonly List<(InputAction Action, int Index, Button Button)> _keyButtons = new();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            _root = documentRoot?.Q<VisualElement>("settings-root");
            if (_root == null)
            {
                Debug.LogError($"{nameof(SettingsUIController)} on '{name}' could not find 'settings-root' - check its UIDocument (Panel Settings + SettingsPanel.uxml).", this);
                enabled = false;
                return;
            }

            foreach (string tab in new[] { "audio", "controls", "graphics" })
            {
                string captured = tab;
                Button button = documentRoot.Q<Button>("tab-" + tab);
                _tabs[tab] = button;
                _pages[tab] = documentRoot.Q<ScrollView>("page-" + tab);
                button.clicked += () => ShowTab(captured);
            }

            documentRoot.Q<Button>("settings-back-button").clicked += Close;
            documentRoot.Q<Button>("settings-reset-button").clicked += HandleResetClicked;

            if (_actions != null)
            {
                GameSettings.ApplyBindingOverrides(_actions);
            }

            _root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            CancelRebind();
        }

        /// <summary>Shows the window; onClosed runs when it's closed (Back / Esc).</summary>
        public void Open(Action onClosed)
        {
            if (_root == null)
            {
                onClosed?.Invoke();
                return;
            }

            _onClosed = onClosed;
            IsOpen = true;
            _root.style.display = DisplayStyle.Flex;
            BuildAllPages();
            ShowTab(_currentTab);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CancelRebind();
            IsOpen = false;
            _root.style.display = DisplayStyle.None;
            GameSettings.Save();
            Action callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }

        private void Update()
        {
            // Esc closes the window - unless it just cancelled (or is waiting for) a key rebind.
            if (IsOpen && _rebind == null && Time.frameCount != _rebindEndedFrame
                && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void ShowTab(string tab)
        {
            CancelRebind();
            _currentTab = tab;
            foreach (KeyValuePair<string, ScrollView> page in _pages)
            {
                page.Value.style.display = page.Key == tab ? DisplayStyle.Flex : DisplayStyle.None;
                _tabs[page.Key].EnableInClassList("settings-tab--active", page.Key == tab);
            }
        }

        private void HandleResetClicked()
        {
            CancelRebind();
            switch (_currentTab)
            {
                case "audio":
                    GameSettings.ResetAudio();
                    break;
                case "controls":
                    GameSettings.ResetControls();
                    if (_actions != null)
                    {
                        _actions.RemoveAllBindingOverrides();
                    }
                    break;
                default:
                    GameSettings.ResetGraphics();
                    break;
            }

            BuildAllPages();
        }

        private void BuildAllPages()
        {
            BuildAudioPage(_pages["audio"]);
            BuildControlsPage(_pages["controls"]);
            BuildGraphicsPage(_pages["graphics"]);
        }

        // ---- Audio ----

        private void BuildAudioPage(ScrollView page)
        {
            page.Clear();
            GameSettingsData data = GameSettings.Current;
            AddPercentSlider(page, "Master Volume", data.MasterVolume, v => data.MasterVolume = v);
            AddPercentSlider(page, "Effects", data.EffectsVolume, v => data.EffectsVolume = v);
            AddPercentSlider(page, "Ambience (weather)", data.AmbienceVolume, v => data.AmbienceVolume = v);
            AddPercentSlider(page, "Music", data.MusicVolume, v => data.MusicVolume = v);
            AddHint(page, "There is no music in the game yet - the Music volume is kept for when there is.");
        }

        // ---- Controls ----

        private void BuildControlsPage(ScrollView page)
        {
            page.Clear();
            _keyButtons.Clear();
            GameSettingsData data = GameSettings.Current;

            AddSection(page, "Mouse");
            AddSlider(page, "Mouse Sensitivity", 0.2f, 3f, data.MouseSensitivity, v => v.ToString("0.00") + "x", v => data.MouseSensitivity = v);
            AddToggle(page, "Invert Vertical Look", data.InvertY, v => data.InvertY = v);

            AddSection(page, "Keys");
            if (_actions == null)
            {
                AddHint(page, "Key rebinding is unavailable here (no Input Actions asset assigned).");
                return;
            }

            foreach (var row in RebindRows)
            {
                InputAction action = _actions.FindAction("Player/" + row.Action);
                int index = action != null ? FindKeyboardBinding(action, row.Part) : -1;
                if (index >= 0)
                {
                    AddKeyRow(page, row.Label, action, index);
                }
            }

            InputAction hotbar = _actions.FindAction("Player/HotbarSlot");
            if (hotbar != null)
            {
                for (int i = 0; i < hotbar.bindings.Count; i++)
                {
                    AddKeyRow(page, "Hotbar Slot " + (i + 1), hotbar, i);
                }
            }

            AddHint(page, "Click a key, then press the new key or mouse button. Esc cancels. A key already used by another action is swapped onto it. Esc itself (pause) can't be rebound.");
        }

        private static int FindKeyboardBinding(InputAction action, string part)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.groups == null || !binding.groups.Contains(KeyboardGroup))
                {
                    continue;
                }

                if (part == null ? !binding.isPartOfComposite : binding.isPartOfComposite && binding.name == part)
                {
                    return i;
                }
            }

            return -1;
        }

        private void AddKeyRow(VisualElement page, string label, InputAction action, int index)
        {
            VisualElement row = AddRow(page, label);
            var button = new Button { text = KeyText(action, index) };
            button.AddToClassList("settings-key-button");
            button.clicked += () => StartRebind(action, index, button);
            row.Add(button);
            _keyButtons.Add((action, index, button));
        }

        /// <summary>The key's physical name from the binding path ("W", "Space", "Left Button") -
        /// not GetBindingDisplayString, which follows the OS keyboard layout (a Russian layout
        /// showed WASD as "Ц Ы Ф В").</summary>
        private static string KeyText(InputAction action, int index)
        {
            string path = action.bindings[index].effectivePath;
            string text = string.IsNullOrEmpty(path) ? null
                : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return string.IsNullOrEmpty(text) ? "-" : text;
        }

        private void StartRebind(InputAction action, int index, Button button)
        {
            if (_rebind != null)
            {
                return;
            }

            string oldPath = action.bindings[index].effectivePath;
            bool wasEnabled = action.enabled;
            action.Disable();
            button.text = "Press a key...";
            button.AddToClassList("settings-key-button--listening");

            _rebind = action.PerformInteractiveRebinding(index)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    string newPath = action.bindings[index].effectivePath;
                    SwapConflicts(action, index, newPath, oldPath);
                    FinishRebind(action, wasEnabled);
                })
                .OnCancel(operation => FinishRebind(action, wasEnabled))
                .Start();
        }

        /// <summary>Any other listed key using newPath gets the rebound key's old one.</summary>
        private void SwapConflicts(InputAction action, int index, string newPath, string oldPath)
        {
            foreach (var key in _keyButtons)
            {
                if (key.Action == action && key.Index == index)
                {
                    continue;
                }

                if (key.Action.bindings[key.Index].effectivePath == newPath)
                {
                    key.Action.ApplyBindingOverride(key.Index, oldPath);
                }
            }
        }

        private void FinishRebind(InputAction action, bool wasEnabled)
        {
            _rebind?.Dispose();
            _rebind = null;
            _rebindEndedFrame = Time.frameCount;
            if (wasEnabled)
            {
                action.Enable();
            }

            GameSettings.Current.BindingOverrides = _actions.SaveBindingOverridesAsJson();
            GameSettings.Apply();
            RefreshKeyButtons();
        }

        private void CancelRebind()
        {
            if (_rebind != null)
            {
                _rebind.Cancel();
            }
        }

        private void RefreshKeyButtons()
        {
            foreach (var key in _keyButtons)
            {
                key.Button.text = KeyText(key.Action, key.Index);
                key.Button.RemoveFromClassList("settings-key-button--listening");
            }
        }

        // ---- Graphics ----

        private void BuildGraphicsPage(ScrollView page)
        {
            page.Clear();
            GameSettingsData data = GameSettings.Current;

            AddSection(page, "Display");
            int modeIndex = Mathf.Max(0, Array.IndexOf(DisplayModes, data.DisplayMode));
            AddDropdown(page, "Display Mode", DisplayModeNames, modeIndex, i => data.DisplayMode = DisplayModes[i], false);

            List<Vector2Int> resolutions = AvailableResolutions();
            var resolutionNames = new List<string>();
            int resolutionIndex = resolutions.Count - 1;
            int wantWidth = data.ResolutionWidth > 0 ? data.ResolutionWidth : Screen.currentResolution.width;
            int wantHeight = data.ResolutionHeight > 0 ? data.ResolutionHeight : Screen.currentResolution.height;
            for (int i = 0; i < resolutions.Count; i++)
            {
                resolutionNames.Add(resolutions[i].x + " x " + resolutions[i].y);
                if (resolutions[i].x == wantWidth && resolutions[i].y == wantHeight)
                {
                    resolutionIndex = i;
                }
            }

            AddDropdown(page, "Resolution", resolutionNames.ToArray(), resolutionIndex, i =>
            {
                data.ResolutionWidth = resolutions[i].x;
                data.ResolutionHeight = resolutions[i].y;
            }, false);
            AddToggle(page, "VSync", data.VSync, v => data.VSync = v);
            string[] limitNames = { "Unlimited", "30", "60", "120", "144", "240" };
            AddDropdown(page, "Frame Rate Limit (VSync off)", limitNames, Mathf.Max(0, Array.IndexOf(FrameLimits, data.FrameRateLimit)), i => data.FrameRateLimit = FrameLimits[i], false);
            AddSlider(page, "Field of View", 50f, 90f, data.FieldOfView, v => Mathf.RoundToInt(v) + "°", v => data.FieldOfView = Mathf.Round(v));

            AddSection(page, "Quality");
            string[] presetNames = { "Low", "Medium", "High", "Ultra", "Custom" };
            AddDropdown(page, "Preset", presetNames, (int)data.Preset, i =>
            {
                if ((GraphicsPreset)i != GraphicsPreset.Custom)
                {
                    GameSettings.ApplyPreset(data, (GraphicsPreset)i);
                    // The other graphics rows show the preset's values.
                    ScheduleNextFrame(() => BuildGraphicsPage(page));
                }
                else
                {
                    data.Preset = GraphicsPreset.Custom;
                }
            }, false);

            // Right end of the slider = no limit (stored as 0).
            float distance = data.ObjectDrawDistance > 0f ? data.ObjectDrawDistance : MaxObjectDistance;
            AddSlider(page, "Object Draw Distance", MinObjectDistance, MaxObjectDistance, distance,
                v => v >= MaxObjectDistance - 1f ? "Max" : Mathf.RoundToInt(v) + " m",
                v => data.ObjectDrawDistance = v >= MaxObjectDistance - 1f ? 0f : Mathf.Round(v / 10f) * 10f, true);
            AddSlider(page, "Render Scale", 0.5f, 1f, data.RenderScale, v => Mathf.RoundToInt(v * 100f) + "%", v => data.RenderScale = Mathf.Round(v * 20f) / 20f, true);
            AddDropdown(page, "Shadows", new[] { "Off", "Low", "Medium", "High" }, (int)data.Shadows, i => data.Shadows = (ShadowLevel)i, true);
            AddDropdown(page, "Anti-Aliasing", new[] { "Off", "MSAA 2x", "MSAA 4x", "MSAA 8x" }, Mathf.Max(0, Array.IndexOf(MsaaValues, data.Msaa)), i => data.Msaa = MsaaValues[i], true);
            AddDropdown(page, "Texture Quality", new[] { "Full", "Half", "Quarter" }, Mathf.Clamp(data.TextureQuality, 0, 2), i => data.TextureQuality = i, true);
            if (Application.isEditor)
            {
                AddHint(page, "Display mode and resolution only take effect in a built game, not in the editor.");
            }
        }

        /// <summary>Runs after the current UI event (rebuilding a page from inside one of its own
        /// callbacks would tear down the control that is still dispatching).</summary>
        private void ScheduleNextFrame(Action action)
        {
            _root.schedule.Execute(action);
        }

        private static List<Vector2Int> AvailableResolutions()
        {
            var result = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (!result.Contains(size))
                {
                    result.Add(size);
                }
            }

            if (result.Count == 0)
            {
                result.Add(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
            }

            return result;
        }

        // ---- Row builders ----

        private static void AddSection(VisualElement page, string title)
        {
            var label = new Label(title);
            label.AddToClassList("settings-section");
            page.Add(label);
        }

        private static void AddHint(VisualElement page, string text)
        {
            var label = new Label(text);
            label.AddToClassList("settings-hint");
            page.Add(label);
        }

        private static VisualElement AddRow(VisualElement page, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-row");
            var text = new Label(label);
            text.AddToClassList("settings-row__label");
            row.Add(text);
            page.Add(row);
            return row;
        }

        private void AddPercentSlider(VisualElement page, string label, float value, Action<float> set)
        {
            AddSlider(page, label, 0f, 1f, value, v => Mathf.RoundToInt(v * 100f) + "%", set);
        }

        /// <summary>A slider row; graphics = changing it turns the preset into Custom.</summary>
        private void AddSlider(VisualElement page, string label, float min, float max, float value, Func<float, string> format, Action<float> set, bool graphics = false)
        {
            VisualElement row = AddRow(page, label);
            var slider = new Slider(min, max) { value = Mathf.Clamp(value, min, max) };
            slider.AddToClassList("settings-row__control");
            var valueLabel = new Label(format(slider.value));
            valueLabel.AddToClassList("settings-row__value");
            slider.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                valueLabel.text = format(evt.newValue);
                MarkCustom(graphics);
                GameSettings.Apply();
            });
            row.Add(slider);
            row.Add(valueLabel);
        }

        private void AddToggle(VisualElement page, string label, bool value, Action<bool> set)
        {
            VisualElement row = AddRow(page, label);
            var toggle = new Toggle { value = value };
            toggle.AddToClassList("settings-row__control");
            toggle.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                GameSettings.Apply();
            });
            row.Add(toggle);
        }

        private void AddDropdown(VisualElement page, string label, string[] choices, int index, Action<int> set, bool graphics)
        {
            VisualElement row = AddRow(page, label);
            var dropdown = new DropdownField(new List<string>(choices), Mathf.Clamp(index, 0, choices.Length - 1));
            dropdown.AddToClassList("settings-row__control");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                set(dropdown.index);
                MarkCustom(graphics);
                GameSettings.Apply();
            });
            row.Add(dropdown);
        }

        private static void MarkCustom(bool graphics)
        {
            if (graphics)
            {
                GameSettings.Current.Preset = GraphicsPreset.Custom;
            }
        }
    }
}
