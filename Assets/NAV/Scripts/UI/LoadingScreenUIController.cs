using UnityEngine;
using UnityEngine.UIElements;
using NAV.Core;

namespace NAV.UI
{
    /// <summary>
    /// Full-screen progress bar shown while SceneLoader loads the gameplay scene. Hidden by
    /// default; MainMenuUIController calls Show() when Play is pressed. Purely a view over
    /// SceneLoader's progress - owns no loading logic itself.
    ///
    /// The real AsyncOperation progress from SceneLoader is coarse (jumps straight to 1 for a
    /// small scene like today's near-empty SampleScene), which read as a dead/static bar - so
    /// the fill visually eases toward that target instead of snapping to it, and a looping
    /// shimmer sweep plus an animated "Loading..." ellipsis run independently of real progress,
    /// purely to keep the screen reading as "working" regardless of how fast the actual load is.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoadingScreenUIController : MonoBehaviour
    {
        private const float DisplayedProgressPerSecond = 1.5f;
        private const float DotIntervalSeconds = 0.4f;
        private const float ShimmerTrackPercentPerSecond = 55f;
        private const float ShimmerWidthPercent = 20f;

        private static readonly string[] DotCycle = { "", ".", "..", "..." };

        [SerializeField] private SceneLoader _sceneLoader;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _progressFill;
        private VisualElement _progressShimmer;
        private Label _label;

        private float _targetProgress;
        private float _displayedProgress;
        private float _dotsTimer;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_sceneLoader == null)
            {
                Debug.LogError($"{nameof(LoadingScreenUIController)} on '{name}' is missing a required reference (Scene Loader).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(LoadingScreenUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("loading-root");
            _progressFill = documentRoot.Q<VisualElement>("loading-progress-fill");
            _progressShimmer = documentRoot.Q<VisualElement>("loading-progress-shimmer");
            _label = documentRoot.Q<Label>("loading-label");

            if (_root == null || _progressFill == null || _progressShimmer == null || _label == null)
            {
                Debug.LogError($"{nameof(LoadingScreenUIController)} on '{name}' could not find 'loading-root'/'loading-progress-fill'/'loading-progress-shimmer'/'loading-label' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _sceneLoader.ProgressChanged += RefreshProgress;
            _root.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (_sceneLoader != null)
            {
                _sceneLoader.ProgressChanged -= RefreshProgress;
            }
        }

        private void Update()
        {
            if (_root == null || _root.style.display == DisplayStyle.None)
            {
                return;
            }

            _displayedProgress = Mathf.MoveTowards(_displayedProgress, _targetProgress, Time.unscaledDeltaTime * DisplayedProgressPerSecond);
            _progressFill.style.width = Length.Percent(_displayedProgress * 100f);

            _dotsTimer += Time.unscaledDeltaTime;
            int dotIndex = (int)(_dotsTimer / DotIntervalSeconds) % DotCycle.Length;
            _label.text = "Loading" + DotCycle[dotIndex];

            float shimmerLeft = Mathf.PingPong(Time.unscaledTime * ShimmerTrackPercentPerSecond, 100f - ShimmerWidthPercent);
            _progressShimmer.style.left = Length.Percent(shimmerLeft);
        }

        public void Show()
        {
            _targetProgress = 0f;
            _displayedProgress = 0f;
            _dotsTimer = 0f;
            _root.style.display = DisplayStyle.Flex;
            _progressFill.style.width = Length.Percent(0f);
            _label.text = "Loading";
        }

        private void RefreshProgress(float fraction)
        {
            _targetProgress = Mathf.Clamp01(fraction);
        }
    }
}
