using System.Collections.Generic;
using NAV.World.Biomes;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>
    /// A kind of point of interest (ruin, shrine, boss arena slot, village...) and the rules for
    /// where it may stand. PoiPlacer reads these; what actually spawns there is Prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "Poi", menuName = "NAV/World/Point Of Interest")]
    public class PoiDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "poi";
        [SerializeField] private string _displayName;
        [SerializeField] private GameObject _prefab;
        [Tooltip("Colour of its dot on the world map preview.")]
        [SerializeField] private Color _mapColor = Color.red;

        [Header("Count")]
        [SerializeField, Min(0)] private int _count = 10;
        [Tooltip("Bigger first: the placer handles higher priorities before lower ones, so rare large POIs get the room.")]
        [SerializeField] private int _priority;

        [Header("Where")]
        [Tooltip("Empty = any biome.")]
        [SerializeField] private List<BiomeDefinition> _allowedBiomes = new();
        [SerializeField] private bool _requiresLand = true;
        [Tooltip("Metres above sea level.")]
        [SerializeField] private Vector2 _heightRange = new(0.5f, 10000f);
        [Tooltip("Distance from the centre, fractions of the world radius.")]
        [SerializeField] private Vector2 _radialRange = new(0f, 1f);

        [Header("Exclusion")]
        [Tooltip("No other POI (of any kind) within this many metres.")]
        [SerializeField, Min(0f)] private float _exclusionRadius = 300f;
        [Tooltip("No POI of this same kind within this many metres.")]
        [SerializeField, Min(0f)] private float _sameKindRadius = 1500f;
        [Tooltip("Trees, rocks and decor are kept this many metres away from the POI.")]
        [SerializeField, Min(0f)] private float _clearRadius = 14f;

        public string Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
        public GameObject Prefab => _prefab;
        public Color MapColor => _mapColor;
        public int Count => _count;
        public int Priority => _priority;
        public IReadOnlyList<BiomeDefinition> AllowedBiomes => _allowedBiomes;
        public bool RequiresLand => _requiresLand;
        public Vector2 HeightRange => _heightRange;
        public Vector2 RadialRange => _radialRange;
        public float ExclusionRadius => _exclusionRadius;
        public float SameKindRadius => _sameKindRadius;
        public float ClearRadius => _clearRadius;

        public bool AllowsBiome(BiomeDefinition biome)
        {
            if (_allowedBiomes.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < _allowedBiomes.Count; i++)
            {
                if (_allowedBiomes[i] == biome)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, string displayName, Color mapColor, int count, int priority,
            List<BiomeDefinition> allowedBiomes, bool requiresLand, Vector2 heightRange, Vector2 radialRange,
            float exclusionRadius, float sameKindRadius)
        {
            _id = id;
            _displayName = displayName;
            _mapColor = mapColor;
            _count = count;
            _priority = priority;
            _allowedBiomes = allowedBiomes;
            _requiresLand = requiresLand;
            _heightRange = heightRange;
            _radialRange = radialRange;
            _exclusionRadius = exclusionRadius;
            _sameKindRadius = sameKindRadius;
        }
#endif
    }
}
