using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// Applies one "render distance" value to fog, the camera's far clip plane, and the cull
    /// distance for the WorldResource layer (see RenderDistanceSettings.ResourceLayerName) -
    /// the scattered trees/rocks/ore WorldGenerator spawns by the thousand, which is what
    /// actually drives draw calls up, not the terrain itself. Fog alone only hides the pop-in
    /// visually; combining it with a shorter cull distance for that one layer is what actually
    /// stops those objects from being rendered at all past a shorter range than the terrain/sky.
    ///
    /// Persists the chosen distance via PlayerPrefs so it survives a restart, and exposes
    /// SetDistance(float) as the single entry point a future Settings UI slider will call - see
    /// PROJECT_STATE.md/CLAUDE.md's Scope Rule, no such UI exists yet, this is the API it will
    /// bind to. Until then, drag the Distance field in the Inspector during Play Mode to test
    /// live (OnValidate re-applies it through the exact same code path).
    /// </summary>
    public class RenderDistanceController : MonoBehaviour
    {
        private const string PlayerPrefsKey = "NAV_RenderDistance";

        [SerializeField] private RenderDistanceSettings _settings;
        [SerializeField] private Camera _camera;
        [Tooltip("Current render distance in meters. Drag this in the Inspector during Play Mode to live-test - same code path SetDistance(float) exposes for a future Settings UI.")]
        [SerializeField] private float _distance;

        private int _resourceLayer = -1;

        public float Distance => _distance;

        private void Awake()
        {
            if (_settings == null || _camera == null)
            {
                Debug.LogError($"{nameof(RenderDistanceController)} on '{name}' is missing a required reference (Settings/Camera).", this);
                enabled = false;
                return;
            }

            _resourceLayer = LayerMask.NameToLayer(_settings.ResourceLayerName);
            if (_resourceLayer < 0)
            {
                Debug.LogError($"{nameof(RenderDistanceController)}: Layer '{_settings.ResourceLayerName}' does not exist - create it in Project Settings > Tags and Layers. World-resource culling is skipped until then; fog/far clip still apply.", this);
            }

            float saved = PlayerPrefs.GetFloat(PlayerPrefsKey, _settings.DefaultDistance);
            SetDistance(saved);
        }

        public void SetDistance(float meters)
        {
            _distance = Mathf.Clamp(meters, _settings.MinDistance, _settings.MaxDistance);

            // A small buffer beyond the fog's own end distance avoids a hard silhouette right at
            // the clip edge before fog has fully resolved to the fog color there.
            _camera.farClipPlane = _distance * 1.05f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = _distance * _settings.FogStartRatio;
            RenderSettings.fogEndDistance = _distance;

            if (_resourceLayer >= 0)
            {
                float[] cullDistances = _camera.layerCullDistances ?? new float[32];
                cullDistances[_resourceLayer] = _distance * _settings.ResourceCullRatio;
                _camera.layerCullDistances = cullDistances;
            }

            PlayerPrefs.SetFloat(PlayerPrefsKey, _distance);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _settings != null && _camera != null)
            {
                SetDistance(_distance);
            }
        }
    }
}
