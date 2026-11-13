using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NAV.Core.Localization
{
    /// <summary>
    /// Translates every UIDocument's UXML text (<see cref="Loc.Apply"/>) a frame after each scene
    /// loads, again whenever the language changes, and every second for panels that rebuild
    /// their trees. Text made in code is translated where it is written (Loc.T), not here.
    /// Lives on its own DontDestroyOnLoad object - no scene setup.
    /// </summary>
    public class LocalizationRuntime : MonoBehaviour
    {
        private const float RefreshSeconds = 1f;
        private float _nextRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<LocalizationRuntime>() != null)
            {
                return;
            }

            var host = new GameObject(nameof(LocalizationRuntime));
            DontDestroyOnLoad(host);
            host.AddComponent<LocalizationRuntime>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Loc.Changed += ApplyAll;
            _nextRefresh = 0f;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Loc.Changed -= ApplyAll;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _nextRefresh = 0f; // next Update - the documents build their trees in OnEnable/Start
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            ApplyAll();
        }

        private static void ApplyAll()
        {
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
            {
                if (document != null && document.rootVisualElement != null)
                {
                    Loc.Apply(document.rootVisualElement);
                }
            }
        }
    }
}
