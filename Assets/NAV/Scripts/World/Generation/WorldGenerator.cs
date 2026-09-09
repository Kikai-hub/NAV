using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using NAV.World.Biomes;

namespace NAV.World.Generation
{
    /// <summary>
    /// First slice of the World Generation pipeline from ARCHITECTURE_v0.1.md:
    /// Seed -> Noise -> Heightmap -> Land/Water -> Biome Assignment (-> a first pass at
    /// Resources, see SpawnResources below - Vegetation/Creatures/POI are still later pipeline
    /// stages, out of scope here).
    ///
    /// Deterministic: every noise sample is offset by values derived from a seed via a seeded
    /// System.Random (not Unity's global Random, whose state depends on call order elsewhere in
    /// the game) - see GDD 4: "The same seed must produce the same world when generation settings
    /// are identical." Generate() (no args) uses Settings' own configured Seed - manual Editor
    /// testing (the Regenerate context menu action) and standalone play of this scene without a
    /// SaveManager. Generate(int seed) takes an explicit seed instead - SaveManager calls this
    /// overload with a new-game-random or loaded-from-save seed, which is why WorldGenerationSettings.Seed
    /// itself is never mutated at runtime (see SaveManager's own doc comment).
    ///
    /// Bounded, non-streaming: generates one fixed-size Terrain, not chunks - see PROJECT_STATE.md
    /// Known Risk #1. Chunk/streaming support for a truly large open world is future work, as is
    /// picking a guaranteed-dry spawn point for the player (today's fixed scene spawn can land in
    /// ocean for an unlucky seed - see UNITY_SETUP_NEXT_STEPS.md Step 24).
    ///
    /// Height/biome classification is deliberately biome-first, not the other way around: a low-
    /// frequency "base noise" pass alone decides Land/Water and Plains/Forest (see GetBiome) -
    /// only once a cell's biome is known does a separate high-frequency detail pass get added on
    /// top, scaled by that biome's own HeightVariation. Without this split, one uniform detail
    /// layer painted the same everywhere made the whole map read as one continuous mountain range
    /// regardless of which biome's texture was underneath it (developer feedback after Step 24's
    /// first playtest) - Plains now stays close to flat, Forest gets moderate rolling hills, Ocean
    /// seabed stays smooth, while Land/Water and Plains/Forest placement itself is untouched by
    /// the fix (still comes from the same base noise/moisture used before).
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        [SerializeField] private WorldGenerationSettings _settings;
        [SerializeField] private Terrain _terrain;
        [Tooltip("Optional. A flat quad/plane repositioned to sea level and scaled to the terrain's footprint - placeholder water, not a real shader/wave system.")]
        [SerializeField] private Transform _waterPlane;
        [Tooltip("Optional. BuildNavMesh() runs once, right after generation, so creatures/the player can path on the freshly generated terrain.")]
        [SerializeField] private NavMeshSurface _navMeshSurface;
        [Tooltip("Optional parent for spawned resource-node prefabs (see SpawnResources), purely for Hierarchy organization - spawning still works with this left empty.")]
        [SerializeField] private Transform _resourceContainer;
        [Tooltip("Off when SaveManager drives generation instead (New Game/Load pick the seed) - see UNITY_SETUP_NEXT_STEPS.md's Save step. On for standalone testing of this scene without a SaveManager present (e.g. WorldGenTest before Save existed).")]
        [SerializeField] private bool _generateOnStart = true;

        private readonly List<GameObject> _spawnedResources = new();

        private void Start()
        {
            if (_generateOnStart)
            {
                Generate();
            }
        }

        /// <summary>Regenerates using Settings' own configured Seed - manual Editor testing (the
        /// Regenerate context menu action) and standalone play of this scene without a
        /// SaveManager. SaveManager itself always calls Generate(int) directly with a specific
        /// (new-game-random or loaded-from-save) seed instead.</summary>
        [ContextMenu("Regenerate")]
        public void Generate()
        {
            if (_settings == null)
            {
                Debug.LogError($"{nameof(WorldGenerator)} on '{name}' is missing Settings.", this);
                return;
            }

            Generate(_settings.Seed);
        }

        public void Generate(int seed)
        {
            if (_settings == null || _terrain == null)
            {
                Debug.LogError($"{nameof(WorldGenerator)} on '{name}' is missing Settings or Terrain.", this);
                return;
            }

            // Seeded independently of UnityEngine.Random so this never depends on (or disturbs)
            // whatever else in the game has called Random.* first.
            var rng = new System.Random(seed);
            Vector2 heightOffset = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
            Vector2 detailOffset = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);
            Vector2 moistureOffset = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);

            TerrainData terrainData = _terrain.terrainData;
            terrainData.heightmapResolution = _settings.HeightmapResolution;
            terrainData.size = new Vector3(_settings.Width, _settings.MaxHeight, _settings.Length);

            int resolution = terrainData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            var biomeMap = new BiomeDefinition[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = (float)x / (resolution - 1) * _settings.Width;
                    float worldZ = (float)z / (resolution - 1) * _settings.Length;

                    // Base noise alone decides Land/Water and Plains/Forest - see the class doc.
                    float baseNoise = Mathf.PerlinNoise(worldX / _settings.HeightNoiseScale + heightOffset.x, worldZ / _settings.HeightNoiseScale + heightOffset.y);
                    float moistureNoise = Mathf.PerlinNoise(worldX / _settings.MoistureNoiseScale + moistureOffset.x, worldZ / _settings.MoistureNoiseScale + moistureOffset.y);
                    BiomeDefinition biome = GetBiome(baseNoise, moistureNoise);
                    biomeMap[z, x] = biome;

                    // Detail wobbles the base height up/down (not just up), scaled by how hilly
                    // this specific biome is allowed to be.
                    float detailNoise = Mathf.PerlinNoise(worldX / _settings.DetailNoiseScale + detailOffset.x, worldZ / _settings.DetailNoiseScale + detailOffset.y);
                    float detailWobble = (detailNoise - 0.5f) * 2f;
                    float heightVariation = biome != null ? biome.HeightVariation : 1f;

                    heights[z, x] = Mathf.Clamp01(baseNoise + detailWobble * _settings.DetailNoiseWeight * heightVariation);
                }
            }

            terrainData.SetHeights(0, 0, heights);

            PaintBiomes(terrainData, biomeMap);
            SpawnResources(biomeMap, seed);
            PlaceWater();
            BakeNavMesh();
        }

        /// <summary>Land/Water first (base noise vs SeaLevel), then Plains/Forest by moisture -
        /// the single source of truth for "which biome is this cell", shared by height shaping,
        /// texture painting, and resource spawning so all three always agree.</summary>
        private BiomeDefinition GetBiome(float baseNoise, float moistureNoise)
        {
            if (baseNoise <= _settings.SeaLevel)
            {
                return _settings.OceanBiome;
            }

            return moistureNoise >= _settings.ForestMoistureThreshold ? _settings.ForestBiome : _settings.PlainsBiome;
        }

        /// <summary>Assigns Settings' three BiomeDefinitions as terrain layers and paints each
        /// alphamap cell fully onto whichever layer biomeMap already assigned it - a hard per-cell
        /// choice, no blending between biomes yet.</summary>
        private void PaintBiomes(TerrainData terrainData, BiomeDefinition[,] biomeMap)
        {
            var layers = new List<TerrainLayer>();
            int oceanIndex = AddLayer(layers, _settings.OceanBiome);
            int plainsIndex = AddLayer(layers, _settings.PlainsBiome);
            int forestIndex = AddLayer(layers, _settings.ForestBiome);
            terrainData.terrainLayers = layers.ToArray();

            int alphaRes = terrainData.alphamapResolution;
            int heightRes = biomeMap.GetLength(0);
            float[,,] alphamap = new float[alphaRes, alphaRes, layers.Count];

            for (int z = 0; z < alphaRes; z++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    BiomeDefinition biome = SampleBiomeMap(biomeMap, x, z, alphaRes, heightRes);
                    int layerIndex = ReferenceEquals(biome, _settings.ForestBiome) ? forestIndex
                        : ReferenceEquals(biome, _settings.PlainsBiome) ? plainsIndex
                        : oceanIndex;

                    for (int layer = 0; layer < layers.Count; layer++)
                    {
                        alphamap[z, x, layer] = layer == layerIndex ? 1f : 0f;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamap);
        }

        private int AddLayer(List<TerrainLayer> layers, BiomeDefinition biome)
        {
            if (biome == null || biome.TerrainLayer == null)
            {
                Debug.LogError($"{nameof(WorldGenerator)}: a biome in Settings has no TerrainLayer assigned - terrain painting will be wrong until one is set.", this);
            }

            layers.Add(biome != null ? biome.TerrainLayer : null);
            return layers.Count - 1;
        }

        /// <summary>Destroys any resources this generator spawned last time, then re-scatters each
        /// biome's ResourceSpawns list: for each entry, walks a MinSpacing grid across the whole
        /// terrain, jitters one candidate point per cell (deterministically, from a seeded RNG),
        /// keeps it only if that point actually falls inside this biome (via biomeMap - so a
        /// Forest-only prefab never lands on Plains just because its grid technically overlaps the
        /// terrain), and then rolls Density to decide whether it actually spawns.</summary>
        private void SpawnResources(BiomeDefinition[,] biomeMap, int seed)
        {
            ClearSpawnedResources();

            // Offset from the height/detail/moisture seeds so resource placement varies
            // independently of terrain shape while staying fully deterministic per seed.
            var resourceRng = new System.Random(seed + 1);
            int heightRes = biomeMap.GetLength(0);
            Vector3 origin = _terrain.transform.position;

            // Resolved once per generation, not per instance - see RenderDistanceController, which
            // culls this same layer at a shorter distance than the terrain to keep the thousands of
            // spawned resources here from tanking draw calls at long view distances.
            int resourceLayer = LayerMask.NameToLayer(_settings.ResourceLayerName);
            if (resourceLayer < 0)
            {
                Debug.LogError($"{nameof(WorldGenerator)}: Layer '{_settings.ResourceLayerName}' does not exist - create it in Project Settings > Tags and Layers. Spawned resources will stay on their prefab's default layer and won't be distance-culled.", this);
            }

            SpawnBiomeResources(_settings.OceanBiome, biomeMap, heightRes, origin, resourceRng, resourceLayer);
            SpawnBiomeResources(_settings.PlainsBiome, biomeMap, heightRes, origin, resourceRng, resourceLayer);
            SpawnBiomeResources(_settings.ForestBiome, biomeMap, heightRes, origin, resourceRng, resourceLayer);
        }

        private void SpawnBiomeResources(BiomeDefinition biome, BiomeDefinition[,] biomeMap, int heightRes, Vector3 origin, System.Random resourceRng, int resourceLayer)
        {
            if (biome == null)
            {
                return;
            }

            foreach (BiomeResourceSpawn spawn in biome.ResourceSpawns)
            {
                if (spawn.Prefab == null || spawn.Density <= 0f || spawn.MinSpacing <= 0f)
                {
                    continue;
                }

                float cell = spawn.MinSpacing;

                for (float z = 0f; z < _settings.Length; z += cell)
                {
                    for (float x = 0f; x < _settings.Width; x += cell)
                    {
                        float localX = x + (float)resourceRng.NextDouble() * cell;
                        float localZ = z + (float)resourceRng.NextDouble() * cell;
                        if (localX >= _settings.Width || localZ >= _settings.Length)
                        {
                            continue;
                        }

                        int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
                        int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);

                        if (!ReferenceEquals(biomeMap[hz, hx], biome) || resourceRng.NextDouble() > spawn.Density)
                        {
                            continue;
                        }

                        Vector3 worldPos = new Vector3(origin.x + localX, 0f, origin.z + localZ);
                        worldPos.y = origin.y + _terrain.SampleHeight(worldPos);

                        Quaternion rotation = Quaternion.Euler(0f, (float)resourceRng.NextDouble() * 360f, 0f);
                        GameObject instance = Instantiate(spawn.Prefab, worldPos, rotation, _resourceContainer);
                        if (resourceLayer >= 0)
                        {
                            SetLayerRecursively(instance.transform, resourceLayer);
                        }

                        _spawnedResources.Add(instance);
                    }
                }
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        private void ClearSpawnedResources()
        {
            foreach (GameObject instance in _spawnedResources)
            {
                if (instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(instance);
                }
                else
                {
                    DestroyImmediate(instance);
                }
            }

            _spawnedResources.Clear();
        }

        private BiomeDefinition SampleBiomeMap(BiomeDefinition[,] biomeMap, int x, int z, int fromRes, int toRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt((float)x / (fromRes - 1) * (toRes - 1)), 0, toRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt((float)z / (fromRes - 1) * (toRes - 1)), 0, toRes - 1);
            return biomeMap[hz, hx];
        }

        private void PlaceWater()
        {
            if (_waterPlane == null)
            {
                return;
            }

            float waterHeight = _terrain.transform.position.y + _settings.SeaLevel * _settings.MaxHeight;
            _waterPlane.position = new Vector3(
                _terrain.transform.position.x + _settings.Width * 0.5f,
                waterHeight,
                _terrain.transform.position.z + _settings.Length * 0.5f);

            // Assumes a default Unity Plane primitive (10x10 units at scale 1) - see
            // UNITY_SETUP_NEXT_STEPS.md Step 24.
            _waterPlane.localScale = new Vector3(_settings.Width / 10f, 1f, _settings.Length / 10f);
        }

        private void BakeNavMesh()
        {
            if (_navMeshSurface == null)
            {
                return;
            }

            // Render-mesh geometry (the default) needs each source mesh's "Read/Write Enabled"
            // import flag on, which imported art assets (e.g. the Spruce tree pack's LOD meshes)
            // usually don't have - that only silently works in the Editor's own Play mode, not in
            // a real build ("does not allow read access" warnings otherwise). Physics Colliders
            // don't have this restriction and every spawned resource-node prefab already has a
            // real collider (for gathering/attack raycasts), so this is free.
            _navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            _navMeshSurface.BuildNavMesh();
        }
    }
}
