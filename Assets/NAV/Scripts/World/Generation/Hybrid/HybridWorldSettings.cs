using System.Collections.Generic;
using NAV.World.Biomes;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>
    /// Every number of the hybrid world (continents in the middle, archipelagos around them, deep
    /// ocean at the rim) - read by HybridWorldShape. Distances in metres, zone edges as fractions
    /// of the world radius.
    /// </summary>
    [CreateAssetMenu(fileName = "HybridWorldSettings", menuName = "NAV/World/Hybrid World Settings")]
    public class HybridWorldSettings : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Radius of the round world, metres. Past it there is only open ocean.")]
        [SerializeField, Min(500f)] private float _worldRadius = 15000f;

        [Header("Land Noise (two layers)")]
        [Tooltip("Low frequency: the outline of the continents.")]
        [SerializeField] private NoiseLayer _continentNoise = new(4200f, 4);
        [Tooltip("High frequency: ragged coasts and the scatter of small islands.")]
        [SerializeField] private NoiseLayer _detailNoise = new(520f, 3);
        [Tooltip("Share of the detail layer in the land value: in the continent zone and in the archipelago zone.")]
        [SerializeField] private Vector2 _detailWeight = new(0.18f, 0.5f);

        [Header("Continent / Ocean Mask")]
        [Tooltip("Continent zone ends here (fraction of radius): inside, land is guaranteed.")]
        [SerializeField, Range(0f, 1f)] private float _continentZoneEnd = 0.03f;
        [Tooltip("Archipelago zone ends here; from here to the rim everything sinks into deep ocean.")]
        [SerializeField, Range(0f, 1f)] private float _archipelagoZoneEnd = 0.8f;
        [Tooltip("Half-width of the soft transition between zones (fraction of radius).")]
        [SerializeField, Range(0.005f, 0.2f)] private float _zoneBlend = 0.01f;
        [Tooltip("How far (fraction of radius) the continent noise pushes the continent's coast in or out - without it the core would be a plain circle.")]
        [SerializeField, Range(0f, 0.5f)] private float _continentCoastWarp = 0.02f;
        [Tooltip("Land value at which continent relief starts above its lowest elevation.")]
        [SerializeField, Range(0f, 1f)] private float _continentReliefBase = 0.35f;
        [Tooltip("Lowest land elevation in the continent zone - the core never dips under the sea.")]
        [SerializeField, Range(0f, 0.2f)] private float _continentMinElevation = 0.02f;
        [Tooltip("Land value needed for an island: at the inner and at the outer edge of the archipelago zone. Higher = fewer, smaller islands.")]
        [SerializeField] private Vector2 _archipelagoThreshold = new(0.46f, 0.66f);
        [Tooltip("Sea floor elevation of the deep ocean at the rim (negative).")]
        [SerializeField, Range(-1f, 0f)] private float _deepOceanElevation = -0.5f;

        [Header("Height")]
        [Tooltip("Metres per unit of elevation above the sea.")]
        [SerializeField, Min(1f)] private float _landHeightScale = 220f;
        [Tooltip("Metres per unit of elevation under the sea.")]
        [SerializeField, Min(1f)] private float _seaDepthScale = 120f;
        [SerializeField] private NoiseLayer _mountainNoise = new(1100f, 4, 2.1f, 0.5f);
        [Tooltip("Continent noise above which ridges start to rise - the hearts of the continent and the big islands.")]
        [SerializeField, Range(0f, 1f)] private float _mountainStart = 0.52f;
        [Tooltip("Extra metres the ridges add at the highest continent noise.")]
        [SerializeField, Min(0f)] private float _mountainHeight = 260f;
        [Tooltip("Walking-scale bumps on top (hillocks, hollows), scaled by each biome's Height Variation and faded out at the shoreline.")]
        [SerializeField] private NoiseLayer _localReliefNoise = new(60f, 3);
        [Tooltip("Metres the local relief moves the ground up or down at Height Variation 1.")]
        [SerializeField, Min(0f)] private float _localReliefHeight = 6f;

        [Header("Climate (radial zones)")]
        [Tooltip("From the safe centre outwards. A land point takes the first band whose Up To is beyond its (noisy) distance.")]
        [SerializeField] private List<ClimateBand> _climateBands = new();
        [Tooltip("Border noise: large wobble of the zone edges.")]
        [SerializeField] private NoiseLayer _climateBorderNoise = new(1600f, 3);
        [Tooltip("How far (fraction of radius) the border noise can shift a zone edge.")]
        [SerializeField, Range(0f, 0.4f)] private float _climateBorderBlur = 0.12f;
        [Tooltip("Patches of a band's Mix Biome (copses in the plains): size of a patch.")]
        [SerializeField] private NoiseLayer _bandMixNoise = new(300f, 3);

        [Header("Polar caps (by latitude, world Z)")]
        [SerializeField] private BiomeDefinition _northPolarBiome;
        [SerializeField] private BiomeDefinition _southPolarBiome;
        [Tooltip("Polar cap starts at this |Z| / radius.")]
        [SerializeField, Range(0f, 1f)] private float _polarStart = 0.6f;
        [SerializeField, Range(0f, 0.3f)] private float _polarBorderBlur = 0.07f;

        [Header("Height biomes")]
        [Tooltip("Checked in order before climate; the first match wins (mountains, swamps).")]
        [SerializeField] private List<HeightBiomeRule> _heightRules = new();
        [SerializeField] private NoiseLayer _patchNoise = new(380f, 2);

        [Header("Water")]
        [SerializeField] private BiomeDefinition _oceanBiome;

        [Header("Points of Interest")]
        [SerializeField] private List<PoiDefinition> _pointsOfInterest = new();
        [Tooltip("Random tries per wanted point before the placer gives up on it.")]
        [SerializeField, Range(1, 200)] private int _poiAttemptsPerPoint = 40;

        public float WorldRadius => _worldRadius;
        public NoiseLayer ContinentNoise => _continentNoise;
        public NoiseLayer DetailNoise => _detailNoise;
        public Vector2 DetailWeight => _detailWeight;
        public float ContinentZoneEnd => _continentZoneEnd;
        public float ArchipelagoZoneEnd => Mathf.Max(_archipelagoZoneEnd, _continentZoneEnd + 0.01f);
        public float ZoneBlend => _zoneBlend;
        public float ContinentCoastWarp => _continentCoastWarp;
        public float ContinentReliefBase => _continentReliefBase;
        public float ContinentMinElevation => _continentMinElevation;
        public Vector2 ArchipelagoThreshold => _archipelagoThreshold;
        public float DeepOceanElevation => _deepOceanElevation;
        public float LandHeightScale => _landHeightScale;
        public float SeaDepthScale => _seaDepthScale;
        public NoiseLayer MountainNoise => _mountainNoise;
        public float MountainStart => _mountainStart;
        public float MountainHeight => _mountainHeight;
        public NoiseLayer LocalReliefNoise => _localReliefNoise;
        public float LocalReliefHeight => _localReliefHeight;
        public IReadOnlyList<ClimateBand> ClimateBands => _climateBands;
        public NoiseLayer ClimateBorderNoise => _climateBorderNoise;
        public float ClimateBorderBlur => _climateBorderBlur;
        public NoiseLayer BandMixNoise => _bandMixNoise;
        public BiomeDefinition NorthPolarBiome => _northPolarBiome;
        public BiomeDefinition SouthPolarBiome => _southPolarBiome;
        public float PolarStart => _polarStart;
        public float PolarBorderBlur => _polarBorderBlur;
        public IReadOnlyList<HeightBiomeRule> HeightRules => _heightRules;
        public NoiseLayer PatchNoise => _patchNoise;
        public BiomeDefinition OceanBiome => _oceanBiome;
        public IReadOnlyList<PoiDefinition> PointsOfInterest => _pointsOfInterest;
        public int PoiAttemptsPerPoint => _poiAttemptsPerPoint;

#if UNITY_EDITOR
        /// <summary>Editor setup only: fills the biome slots and lists in one go.</summary>
        public void EditorAssign(BiomeDefinition ocean, BiomeDefinition northPolar, BiomeDefinition southPolar,
            List<ClimateBand> climateBands, List<HeightBiomeRule> heightRules, List<PoiDefinition> pointsOfInterest)
        {
            _oceanBiome = ocean;
            _northPolarBiome = northPolar;
            _southPolarBiome = southPolar;
            _climateBands = climateBands;
            _heightRules = heightRules;
            _pointsOfInterest = pointsOfInterest;
        }
#endif
    }
}
