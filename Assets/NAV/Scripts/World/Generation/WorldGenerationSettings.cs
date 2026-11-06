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
        [Tooltip("Optional. The wettest heart of the forest - moisture at or above Dense Forest Moisture Threshold - becomes this biome (the dense forest, tier 2: ancient trees, old oaks, copper).")]
        [SerializeField] private BiomeDefinition _denseForestBiome;
        [SerializeField, Range(0f, 1f)] private float _denseForestMoistureThreshold = 0.68f;

        [Header("Ocean and Islands (tier 3)")]
        [Tooltip("Width (m) of the coastal band along the map edge in which the land slopes down into the open sea. 0 = no sea around the map.")]
        [SerializeField, Min(0f)] private float _oceanBorder = 420f;
        [Tooltip("The outermost metres of the map are always deep sea.")]
        [SerializeField, Min(0f)] private float _deepSeaEdge = 110f;
        [Tooltip("How far below sea level (0-1 of Max Height) the open sea floor lies.")]
        [SerializeField, Range(0f, 0.5f)] private float _deepSeaDepth = 0.12f;
        [Tooltip("Optional. Islands rising out of the open sea become this biome (sea islands, tier 3).")]
        [SerializeField] private BiomeDefinition _islandBiome;
        [SerializeField] private float _islandNoiseScale = 85f;
        [Tooltip("Island noise above this raises an island; higher = fewer, smaller islands.")]
        [SerializeField, Range(0f, 1f)] private float _islandThreshold = 0.6f;
        [Tooltip("How high (0-1 of Max Height) above the sea the island tops rise.")]
        [SerializeField, Range(0f, 0.5f)] private float _islandHeight = 0.14f;

        [Header("Grass and Flowers (terrain details)")]
        [Tooltip("Small editor-made TerrainData every terrain tile is cloned from (NAV > World > Hybrid World > Create Tile Terrain Base). new TerrainData() draws grass as white quads in player builds.")]
        [SerializeField] private TerrainData _tileBaseData;
        [Tooltip("Grass blade textures (white-ish masks with alpha), painted as waving terrain grass by each biome's Grass Density - each grass cell picks one at random, tinted between Healthy and Dry colour.")]
        [SerializeField] private Texture2D[] _grassTextures = new Texture2D[0];
        [SerializeField] private Color _grassHealthyColor = new(0.32f, 0.52f, 0.16f);
        [SerializeField] private Color _grassDryColor = new(0.5f, 0.56f, 0.24f);
        [Tooltip("Same, for each biome's Flower Density - each flower cell picks one of these at random. Terrain details allow exactly one material per prefab.")]
        [SerializeField] private GameObject[] _flowerDetailPrefabs = new GameObject[0];
        [Tooltip("Metres from the camera within which grass and flowers are drawn.")]
        [SerializeField, Min(10f)] private float _detailDistance = 70f;

        [Header("Resource Spawning")]
        [Tooltip("Unity Layer WorldGenerator assigns to every spawned resource prefab (trees/rocks/ore). The camera (ThirdPersonCameraController) culls this layer at the Object Draw Distance setting, shorter than the view distance.")]
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
        public BiomeDefinition DenseForestBiome => _denseForestBiome;
        public float OceanBorder => _oceanBorder;
        public float DeepSeaEdge => _deepSeaEdge;
        public float DeepSeaDepth => _deepSeaDepth;
        public BiomeDefinition IslandBiome => _islandBiome;
        public float IslandNoiseScale => _islandNoiseScale;
        public float IslandThreshold => _islandThreshold;
        public float IslandHeight => _islandHeight;
        public float DenseForestMoistureThreshold => _denseForestMoistureThreshold;
        public TerrainData TileBaseData => _tileBaseData;
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
