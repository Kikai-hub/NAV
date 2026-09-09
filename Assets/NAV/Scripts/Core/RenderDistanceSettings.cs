using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// Tunable range/ratios for RenderDistanceController. Kept as its own asset (not hard-coded on
    /// the controller) so a future Settings UI slider can be range-bound to MinDistance/MaxDistance
    /// without touching code, per CLAUDE.md's Data-Driven Design rule.
    /// </summary>
    [CreateAssetMenu(fileName = "RenderDistanceSettings", menuName = "NAV/Core/Render Distance Settings")]
    public class RenderDistanceSettings : ScriptableObject
    {
        [Header("Range")]
        [SerializeField] private float _minDistance = 50f;
        [SerializeField] private float _maxDistance = 500f;
        [SerializeField] private float _defaultDistance = 250f;

        [Header("Fog")]
        [Tooltip("Fog starts at Distance x this ratio, and reaches full fog exactly at Distance (Camera.farClipPlane sits just beyond Distance so nothing pops at the exact edge).")]
        [SerializeField, Range(0f, 1f)] private float _fogStartRatio = 0.6f;

        [Header("World Resource Culling")]
        [Tooltip("Must match the Unity Layer WorldGenerator assigns to spawned resource prefabs (trees/rocks/ore) - see WorldGenerationSettings.ResourceLayerName. Small scattered world objects stop rendering at Distance x this ratio, well before terrain/fog do, since there are far more of them than anything else on screen.")]
        [SerializeField] private string _resourceLayerName = "WorldResource";
        [SerializeField, Range(0f, 1f)] private float _resourceCullRatio = 0.5f;

        public float MinDistance => _minDistance;
        public float MaxDistance => _maxDistance;
        public float DefaultDistance => _defaultDistance;
        public float FogStartRatio => _fogStartRatio;
        public string ResourceLayerName => _resourceLayerName;
        public float ResourceCullRatio => _resourceCullRatio;

        private void OnValidate()
        {
            _minDistance = Mathf.Max(1f, _minDistance);
            _maxDistance = Mathf.Max(_minDistance, _maxDistance);
            _defaultDistance = Mathf.Clamp(_defaultDistance, _minDistance, _maxDistance);
        }
    }
}
