using UnityEngine;
using UnityEngine.UIElements;
using NAV.Core;

namespace NAV.UI
{
    /// <summary>
    /// The game's entry point screen: Play starts loading the gameplay scene (handing off to
    /// LoadingScreenUIController/SceneLoader), Quit exits. No Continue/Settings buttons - there
    /// is no save system or settings menu yet, and a button that does nothing would be
    /// dishonest UI (see CLAUDE.md's "Important Scope Rule").
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIController : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "SampleScene";
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private LoadingScreenUIController _loadingScreen;

        private UIDocument _document;
        private VisualElement _root;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_sceneLoader == null || _loadingScreen == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' is missing a required reference (Scene Loader/Loading Screen).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("main-menu-root");
            var playButton = documentRoot.Q<Button>("play-button");
            var quitButton = documentRoot.Q<Button>("quit-button");

            if (_root == null || playButton == null || quitButton == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' could not find 'main-menu-root'/'play-button'/'quit-button' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            playButton.clicked += HandlePlayClicked;
            quitButton.clicked += HandleQuitClicked;
        }

        private void HandlePlayClicked()
        {
            _root.style.display = DisplayStyle.None;
            _loadingScreen.Show();
            _sceneLoader.LoadScene(_gameSceneName);
        }

        private static void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
