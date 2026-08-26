using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NAV.Core
{
    /// <summary>
    /// Drives an async scene load and reports its progress for a loading screen to observe
    /// (Core layer service, no UI dependency - matches ARCHITECTURE_v0.1.md's "UI observes
    /// gameplay/Core state" rule). Single-scene load (not additive): when activation is allowed,
    /// Unity unloads whichever scene this component lives in as part of the same transition, so
    /// no DontDestroyOnLoad/persistence is needed here.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [Tooltip("Artificial floor on how long the loading screen stays up, so a fast load (e.g. today's near-empty SampleScene) doesn't just flash for a single frame.")]
        [SerializeField] private float _minimumDisplaySeconds = 0.5f;

        public event Action<float> ProgressChanged;

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            float startTime = Time.unscaledTime;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Debug.LogError($"{nameof(SceneLoader)} could not start loading scene '{sceneName}' - check it's added to Build Settings.", this);
                yield break;
            }

            // Unity caps progress at 0.9 until activation is allowed; remap to a clean 0-1 range.
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                ProgressChanged?.Invoke(operation.progress / 0.9f);
                yield return null;
            }

            ProgressChanged?.Invoke(1f);

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < _minimumDisplaySeconds)
            {
                yield return new WaitForSecondsRealtime(_minimumDisplaySeconds - elapsed);
            }

            operation.allowSceneActivation = true;
        }
    }
}
