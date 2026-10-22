using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NAV.Core.Save;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// ESC-toggled pause overlay. Resume closes it and returns to gameplay; Leave to Main Menu
    /// unpauses and loads the MainMenu scene directly (no async loading-screen hop for this
    /// transition - SampleScene has no loading-screen infrastructure of its own, and adding one
    /// just for this would duplicate MainMenu's; revisit if that becomes worth it). Reuses
    /// PlayerInputHandler.MenuOpen for cursor lock/camera freeze like every other modal panel,
    /// and additionally drives Time.timeScale so gameplay simulation itself actually halts, not
    /// just input.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseUIController : MonoBehaviour
    {
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private PlayerInputHandler _inputHandler;
        [Tooltip("Optional. If assigned, leaving to the Main Menu autosaves first so a manual quit never loses progress the periodic autosave hasn't caught yet.")]
        [SerializeField] private SaveManager _saveManager;
        [Tooltip("Optional - the Settings window; without it the Settings button is hidden.")]
        [SerializeField] private SettingsUIController _settings;

        private UIDocument _document;
        private VisualElement _root;
        private bool _visible;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_inputHandler == null)
            {
                Debug.LogError($"{nameof(PauseUIController)} on '{name}' is missing a required reference (Input Handler).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(PauseUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("pause-root");
            var resumeButton = documentRoot.Q<Button>("resume-button");
            var mainMenuButton = documentRoot.Q<Button>("main-menu-button");

            if (_root == null || resumeButton == null || mainMenuButton == null)
            {
                Debug.LogError($"{nameof(PauseUIController)} on '{name}' could not find 'pause-root'/'resume-button'/'main-menu-button' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            resumeButton.clicked += HandleResumeClicked;
            mainMenuButton.clicked += HandleMainMenuClicked;

            var settingsButton = documentRoot.Q<Button>("settings-button");
            if (settingsButton != null && _settings != null)
            {
                settingsButton.clicked += HandleSettingsClicked;
            }
            else if (settingsButton != null)
            {
                settingsButton.style.display = DisplayStyle.None;
            }

            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.PausePerformed += HandlePausePerformed;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.PausePerformed -= HandlePausePerformed;
            }
        }

        private void OnDestroy()
        {
            // Safety net: never leave the engine's global time scale stuck at 0.
            Time.timeScale = 1f;
        }

        private void HandlePausePerformed()
        {
            // Esc while the Settings window is open belongs to it (it closes itself, or cancels
            // a key rebind) - the pause menu stays as it is underneath.
            if (_settings != null && _settings.IsOpen)
            {
                return;
            }

            SetVisible(!_visible);
        }

        /// <summary>Hides the pause panel while the Settings window is up (still paused), and
        /// brings it back when that closes.</summary>
        private void HandleSettingsClicked()
        {
            _root.style.display = DisplayStyle.None;
            _settings.Open(() =>
            {
                if (_visible)
                {
                    _root.style.display = DisplayStyle.Flex;
                }
            });
        }

        private void HandleResumeClicked()
        {
            SetVisible(false);
        }

        private void HandleMainMenuClicked()
        {
            _saveManager?.Autosave();
            Time.timeScale = 1f;
            SceneManager.LoadScene(_mainMenuSceneName);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _inputHandler.SetMenuOpen(visible);
            Time.timeScale = visible ? 0f : 1f;
        }
    }
}
