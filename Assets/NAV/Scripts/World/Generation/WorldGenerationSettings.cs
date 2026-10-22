using UnityEngine;
using NAV.World.Biomes;

namespace NAV.World.Generation
{
    /// <summary>
    /// Tunable knobs for WorldGenerator's pipeline (Seed -> Noise -> Heightmap -> Land/Water ->
    /// Biome Assignment, per ARCHITECTURE_v0.1.md's World Generation pipeline - Terrain
    /// Features/Resources/Vegetation/Creatures/POI are later pipeline stages, out of scope for
    /// this increment). One asset per "world type"; a real per-playthrough Seed picker (world
    /// creation UI) doesn't exist yet - the serialized value here is today's placeholder/test
    /// value, same "no unlock/creation system yet" deferral used throughout this project.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldGenerationSettings", menuName = "NAV/World/Generation Settings")]
    public class WorldGenerationSettings : ScriptableObject
    {
        [Header("Seed")]
        [SerializeField] private int _seed;

        [Header("Terrain Size")]
        [SerializeField] private float _width = 500f;
        [SerializeField] private float _length = 500f;
        [SerializeField] private float _maxHeight = 40f;
        [SerializeField] private int _heightmapResolution = 513;

        [Header("Height Noise")]
        [Tooltip("Scale of the broad, low-frequency noise that shapes land/water and the overall landmass silhouette - this alone decides biome placement, before any per-biome detail is added.")]
        [SerializeField] private float _heightNoiseScale = 260f;
        [Tooltip("How strongly the high-frequency detail layer can push height up/down around the base shape, before each biome's own HeightVariation scales it down further. Effective per-cell wobble = this x the cell's biome HeightVariation.")]
        [SerializeField, Range(0f, 1f)] private float _detailNoiseWeight = 0.35f;
        [SerializeField] private float _detailNoiseScale = 60f;

        [Header("Water")]
        [Tooltip("Normalized height (0-1 of Max Height) below which terrain counts as ocean.")]
        [SerializeField, Range(0f, 1f)] private float _seaLevel = 0.32f;

        [Header("Biome Noise")]
        [SerializeField] private float _moistureNoiseScale = 220f;
        [Tooltip("Moisture (0-1) above which land counts as Forest instead of Plains.")]
        [SerializeField, Range(0f, 1f)] private float _forestMoistureThreshold = 0.55f;

        [Header("Biomes")]
        [SerializeField] private BiomeDefinition _oceanBiome;
        [SerializeField] private BiomeDefinition _plainsBiome;
        [SerializeField] private BiomeDefinition _forestBiome;
        [Tooltip("Optional. Patches of the Forest where a separate low-frequency noise is above Grove Threshold become this biome instead (birch groves).")]
        [SerializeField] private BiomeDefinition _groveBiome;
        [SerializeField] private float _groveNoiseScale = 110f;
        [SerializeField, Range(0f, 1f)] private float _groveThreshold = 0.62f;

        [Header("Grass and Flowers (terrain details)")]
        [Tooltip("Grass blade textures (white-ish masks with alpha), painted as waving terrain grass by each biome's Grass Density - each grass cell picks one at random, tinted between Healthy and Dry colour.")]
        [SerializeField] private Texture2D[] _grassTextures = new Texture2D[0];
        [SerializeField] private Color _grassHealthyColor = new(0.32f, 0.52f, 0.16f);
        [SerializeField] private Color _grassDryColor = new(0.5f, 0.56f, 0.24f);
        [Tooltip("Same, for each biome's Flower Density - each flower cell picks one of these at random. Terrain details allow exactly one material per prefab.")]
        [SerializeField] private GameObject[] _flowerDetailPrefabs = new GameObject[0];
        [Tooltip("Metres from the camera within which grass and flowers are drawn.")]
        [SerializeField, Min(10f)] private float _detailDistance = 70f;

        [Header("Resource Spawning")]
        [Tooltip("Unity Layer WorldGenerator assigns to every spawned resource prefab (trees/rocks/ore). Must match RenderDistanceSettings.ResourceLayerName so RenderDistanceController can cull them at a shorter distance than the terrain - see PROJECT_STATE.md's world-generation performance note.")]
        [SerializeField] private string _resourceLayerName = "WorldResource";

        public int Seed => _seed;
        public float Width => _width;
        public float Length => _length;
        public float MaxHeight => _maxHeight;
        public int HeightmapResolution => _heightmapResolution;
        public float HeightNoiseScale => _heightNoiseScale;
        public float DetailNoiseWeight => _detailNoiseWeight;
        public float DetailNoiseScale => _detailNoiseScale;
        public float SeaLevel => _seaLevel;
        public float MoistureNoiseScale => _moistureNoiseScale;
        public float ForestMoistureThreshold => _forestMoistureThreshold;
        public BiomeDefinition OceanBiome => _oceanBiome;
        public BiomeDefinition PlainsBiome => _plainsBiome;
        public BiomeDefinition ForestBiome => _forestBiome;
        public string ResourceLayerName => _resourceLayerName;
        public BiomeDefinition GroveBiome => _groveBiome;
        public float GroveNoiseScale => _groveNoiseScale;
        public float GroveThreshold => _groveThreshold;
        public Texture2D[] GrassTextures => _grassTextures;
        public Color GrassHealthyColor => _grassHealthyColor;
        public Color GrassDryColor => _grassDryColor;
        public GameObject[] FlowerDetailPrefabs => _flowerDetailPrefabs;
        public float DetailDistance => _detailDistance;

        private void OnValidate()
        {
            _width = Mathf.Max(10f, _width);
            _length = Mathf.Max(10f, _length);
            _maxHeight = Mathf.Max(1f, _maxHeight);
            _heightmapResolution = Mathf.ClosestPowerOfTwo(Mathf.Max(33, _heightmapResolution)) + 1;
            _heightNoiseScale = Mathf.Max(1f, _heightNoiseScale);
            _detailNoiseScale = Mathf.Max(1f, _detailNoiseScale);
            _moistureNoiseScale = Mathf.Max(1f, _moistureNoiseScale);
        }
    }
}
