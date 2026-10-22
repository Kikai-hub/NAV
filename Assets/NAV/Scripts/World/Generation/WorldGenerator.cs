using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using NAV.Gameplay.AI;
using NAV.World.Biomes;

namespace NAV.World.Generation
{
    /// <summary>
    /// First slice of the World Generation pipeline from ARCHITECTURE_v0.1.md:
    /// Seed -> Noise -> Heightmap -> Land/Water -> Biome Assignment (-> a first pass at
    /// Resources and Creatures, see SpawnResources/SpawnCreatures below - Vegetation/POI are
    /// still later pipeline stages, out of scope here).
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
    /// Known Risk #1. Chunk/streaming support for a truly large open world is future work. A dry,
    /// flat player spawn point is picked per seed (SpawnPoint - see FindSpawnPoint), and
    /// resource nodes get stable spawn-index ids so gathered ones can be saved and skipped on
    /// load (GetDepletedResourceIds / Generate(seed, depletedResourceIds)).
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
        [Tooltip("Optional. A NavMeshModifierVolume (not a child of the water plane) PlaceWater stretches over everything under the water, so the NavMesh doesn't cover lake beds now that the water isn't solid.")]
        [SerializeField] private NavMeshModifierVolume _underwaterNavBlock;
        [Tooltip("Optional parent for spawned resource-node prefabs (see SpawnResources), purely for Hierarchy organization - spawning still works with this left empty.")]
        [SerializeField] private Transform _resourceContainer;
        [Tooltip("No creature is spawned within this distance of the player spawn point (see SpawnPoint).")]
        [SerializeField, Min(0f)] private float _creatureSafeRadius = 30f;

        [Header("Player Spawn Point")]
        [Tooltip("Ring spacing (m) of the outward spiral search from the terrain centre.")]
        [SerializeField, Min(1f)] private float _spawnSearchStep = 4f;
        [Tooltip("Minimum ground height above sea level for a spawn point.")]
        [SerializeField, Min(0f)] private float _spawnMinHeightAboveSea = 1.5f;
        [Tooltip("Max height difference (m) between the spawn point and ground 2 m around it - keeps spawns off slopes.")]
        [SerializeField, Min(0.05f)] private float _spawnMaxHeightDifference = 0.5f;
        [Tooltip("Radius (m) that must be free of colliders (trees, rocks) around the spawn point.")]
        [SerializeField, Min(0.1f)] private float _spawnClearRadius = 1.5f;

        [Header("Starter Resources")]
        [Tooltip("Pickups scattered close to the spawn point so a new player can gather the wood/stone for their first tools (trees need an Axe, rock needs a Pickaxe). Tracked like resources - picked up ones stay gone after load.")]
        [SerializeField] private List<StarterSpawn> _starterSpawns = new();
        [SerializeField, Min(0f)] private float _starterMinRadius = 3f;
        [SerializeField, Min(0.5f)] private float _starterMaxRadius = 15f;

        [System.Serializable]
        private class StarterSpawn
        {
            public GameObject Prefab;
            [Min(0)] public int Count = 5;
        }
        [Tooltip("Off when SaveManager drives generation instead (New Game/Load pick the seed) - see UNITY_SETUP_NEXT_STEPS.md's Save step. On for standalone testing of this scene without a SaveManager present (e.g. WorldGenTest before Save existed).")]
        [SerializeField] private bool _generateOnStart = true;

        private readonly List<GameObject> _spawnedResources = new();
        private readonly List<GameObject> _spawnedCreatures = new();
        private readonly List<GameObject> _spawnedDecor = new();

        // Attempts per requested creature before giving up on finding a valid point (right
        // biome, on the NavMesh, outside the safe zone) - bounds the loop for biomes that are
        // tiny or missing in an unlucky seed.
        private const int CreaturePlacementAttemptsPerSpawn = 30;
        private const float CreatureNavMeshSampleDistance = 2f;
        // Metres between members of one group (a pack) at spawn.
        private const float CreatureGroupSpread = 3f;
        // Metres under the water surface where the NavMesh stops - animals may stand at the edge.
        private const float UnderwaterNavMargin = 0.3f;
        private BiomeDefinition[,] _biomeMap;

        /// <summary>Raised after Generate placed the world's creatures (Play Mode only) -
        /// CreaturePopulation restarts its respawn timers.</summary>
        public event System.Action CreaturesSpawned;
        private const float SpawnFlatnessProbeDistance = 2f;
        private readonly Collider[] _spawnOverlapBuffer = new Collider[16];
        private ICollection<int> _depletedOnLoad;

        /// <summary>A dry, flat, unobstructed ground point picked deterministically from the
        /// seed during Generate (nearest such point to the terrain centre). SaveManager places a
        /// new-game player here and uses it as the respawn point; creatures keep their distance
        /// from it. Only valid when HasSpawnPoint is true.</summary>
        public Vector3 SpawnPoint { get; private set; }
        public bool HasSpawnPoint { get; private set; }

        /// <summary>Returns the position raised onto the terrain surface if it's below it, else
        /// unchanged. Used when a save from an older world generation (see
        /// SaveSystem.WorldGenerationVersion) is loaded and the ground may have moved up.</summary>
        public Vector3 LiftAboveGround(Vector3 position)
        {
            if (_terrain == null)
            {
                return position;
            }

            float ground = _terrain.transform.position.y + _terrain.SampleHeight(position) + 0.1f;
            if (position.y < ground)
            {
                position.y = ground;
            }

            return position;
        }

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

        public void Generate(int seed) => Generate(seed, null);

        /// <summary>Generates the world for a seed, skipping resource nodes whose spawn index is in
        /// depletedResourceIds (gathered in a previous session - see GetDepletedResourceIds).
        /// Skipped before the NavMesh bake, so a gathered tree leaves no hole in the mesh.</summary>
        public void Generate(int seed, ICollection<int> depletedResourceIds)
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
            // Drawn after the others so the offsets above stay what they were for every seed.
            Vector2 groveOffset = new Vector2((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);

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
                    float groveNoise = Mathf.PerlinNoise(worldX / _settings.GroveNoiseScale + groveOffset.x, worldZ / _settings.GroveNoiseScale + groveOffset.y);
                    BiomeDefinition biome = GetBiome(baseNoise, moistureNoise, groveNoise);
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
            SpawnResources(biomeMap, seed, depletedResourceIds);
            SpawnDecor(biomeMap, seed);
            PaintDetails(terrainData, biomeMap, seed);
            PlaceWater();
            FindSpawnPoint(biomeMap);
            SpawnStarterResources(seed);
            BakeNavMesh();
            SpawnCreatures(biomeMap, seed);
        }

        /// <summary>Spawn indices of resource nodes that no longer exist (gathered until
        /// depleted - ResourceNode destroys itself) or were skipped as already depleted on load.
        /// Indices are stable for a given seed + generation settings, so SaveManager can store
        /// just these numbers instead of every node.</summary>
        public List<int> GetDepletedResourceIds()
        {
            var ids = new List<int>();
            for (int i = 0; i < _spawnedResources.Count; i++)
            {
                if (_spawnedResources[i] == null)
                {
                    ids.Add(i);
                }
            }

            return ids;
        }

        /// <summary>Spiral search outward from the terrain centre for the first point that is
        /// land (non-water biome), high enough above sea level, flat, and free of colliders
        /// (trees/rocks spawned just before). Deterministic for a seed, so a loaded save gets the
        /// same respawn point its world had when it was created.</summary>
        private void FindSpawnPoint(BiomeDefinition[,] biomeMap)
        {
            HasSpawnPoint = false;
            Physics.SyncTransforms();

            Vector3 origin = _terrain.transform.position;
            float seaHeight = origin.y + _settings.SeaLevel * _settings.MaxHeight;
            int heightRes = biomeMap.GetLength(0);
            Vector3 centre = origin + new Vector3(_settings.Width * 0.5f, 0f, _settings.Length * 0.5f);
            float maxRadius = Mathf.Min(_settings.Width, _settings.Length) * 0.5f - SpawnFlatnessProbeDistance;

            for (float radius = 0f; radius <= maxRadius; radius += _spawnSearchStep)
            {
                int samples = radius <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / _spawnSearchStep));
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 candidate = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsValidSpawnPoint(candidate, biomeMap, heightRes, origin, seaHeight, out Vector3 ground))
                    {
                        SpawnPoint = ground;
                        HasSpawnPoint = true;
                        return;
                    }
                }
            }

            Debug.LogWarning($"{nameof(WorldGenerator)}: no valid player spawn point found - the player keeps its scene position.", this);
        }

        private bool IsValidSpawnPoint(Vector3 candidate, BiomeDefinition[,] biomeMap, int heightRes, Vector3 origin, float seaHeight, out Vector3 ground)
        {
            ground = candidate;
            float localX = candidate.x - origin.x;
            float localZ = candidate.z - origin.z;
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);
            BiomeDefinition biome = biomeMap[hz, hx];
            if (biome == null || biome.IsWater || ReferenceEquals(biome, _settings.OceanBiome))
            {
                return false;
            }

            float height = origin.y + _terrain.SampleHeight(candidate);
            if (height < seaHeight + _spawnMinHeightAboveSea)
            {
                return false;
            }

            Vector3[] probes =
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            };
            foreach (Vector3 direction in probes)
            {
                Vector3 probe = candidate + direction * SpawnFlatnessProbeDistance;
                if (Mathf.Abs(origin.y + _terrain.SampleHeight(probe) - height) > _spawnMaxHeightDifference)
                {
                    return false;
                }
            }

            ground = new Vector3(candidate.x, height, candidate.z);
            Vector3 bottom = ground + Vector3.up * (_spawnClearRadius + 0.1f);
            Vector3 top = ground + Vector3.up * Mathf.Max(1.8f, _spawnClearRadius + 0.2f);
            int hits = Physics.OverlapCapsuleNonAlloc(bottom, top, _spawnClearRadius, _spawnOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                // The ground itself may poke into the capsule on a gentle slope - only
                // trees/rocks/buildings count as obstructions.
                if (_spawnOverlapBuffer[i] is not TerrainCollider)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Land/Water first (base noise vs SeaLevel), then Plains/Forest by moisture -
        /// the single source of truth for "which biome is this cell", shared by height shaping,
        /// texture painting, and resource spawning so all three always agree.</summary>
        private BiomeDefinition GetBiome(float baseNoise, float moistureNoise, float groveNoise)
        {
            if (baseNoise <= _settings.SeaLevel)
            {
                return _settings.OceanBiome;
            }

            if (moistureNoise < _settings.ForestMoistureThreshold)
            {
                return _settings.PlainsBiome;
            }

            // Groves are clearings of the forest - never at its very edge, so a grove doesn't
            // read as just more plains.
            bool deepInForest = moistureNoise >= _settings.ForestMoistureThreshold + 0.03f;
            return _settings.GroveBiome != null && deepInForest && groveNoise >= _settings.GroveThreshold
                ? _settings.GroveBiome
                : _settings.ForestBiome;
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
            int groveIndex = _settings.GroveBiome != null ? AddLayer(layers, _settings.GroveBiome) : forestIndex;
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
                        : ReferenceEquals(biome, _settings.GroveBiome) ? groveIndex
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
        private void SpawnResources(BiomeDefinition[,] biomeMap, int seed, ICollection<int> depletedResourceIds)
        {
            _depletedOnLoad = depletedResourceIds;

            ClearSpawned(_spawnedResources);

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
            SpawnBiomeResources(_settings.GroveBiome, biomeMap, heightRes, origin, resourceRng, resourceLayer);
        }

        /// <summary>Scatters each biome's DecorSpawns (bushes, mushrooms, stumps, logs, pebbles)
        /// the same way resources are placed, from its own RNG (seed + 5) so resource ids never
        /// shift when decor changes. Runs before the NavMesh bake: decor that keeps its colliders
        /// is walked around. Not saved.</summary>
        private void SpawnDecor(BiomeDefinition[,] biomeMap, int seed)
        {
            ClearSpawned(_spawnedDecor);

            var decorRng = new System.Random(seed + 5);
            int heightRes = biomeMap.GetLength(0);
            Vector3 origin = _terrain.transform.position;
            int layer = LayerMask.NameToLayer(_settings.ResourceLayerName);
            Transform parent = _resourceContainer;

            foreach (BiomeDefinition biome in Biomes)
            {
                if (biome == null)
                {
                    continue;
                }

                foreach (BiomeDecorSpawn spawn in biome.DecorSpawns)
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
                            float localX = x + (float)decorRng.NextDouble() * cell;
                            float localZ = z + (float)decorRng.NextDouble() * cell;
                            float roll = (float)decorRng.NextDouble();
                            float yaw = (float)decorRng.NextDouble() * 360f;
                            float scale = Mathf.Lerp(spawn.MinScale, spawn.MaxScale, (float)decorRng.NextDouble());
                            if (localX >= _settings.Width || localZ >= _settings.Length || roll > spawn.Density)
                            {
                                continue;
                            }

                            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
                            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);
                            if (!ReferenceEquals(biomeMap[hz, hx], biome))
                            {
                                continue;
                            }

                            Vector3 position = new Vector3(origin.x + localX, 0f, origin.z + localZ);
                            position.y = origin.y + _terrain.SampleHeight(position);

                            GameObject instance = Instantiate(spawn.Prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
                            instance.transform.localScale *= scale;
                            if (!spawn.KeepColliders)
                            {
                                foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
                                {
                                    // Disabled, not destroyed: Destroy is deferred and the
                                    // NavMesh bake right after this would still see it.
                                    collider.enabled = false;
                                }
                            }

                            if (layer >= 0)
                            {
                                SetLayerRecursively(instance.transform, layer);
                            }

                            _spawnedDecor.Add(instance);
                        }
                    }
                }
            }
        }

        // Grass tufts per detail cell (~2 m) at GrassDensity 1; patch noise thins/thickens it.
        private const int MaxGrassPerCell = 6;
        private const float GrassPatchNoiseScale = 0.06f;

        /// <summary>Paints grass (waving texture grass) and flowers (instanced meshes) as terrain
        /// details. Per detail cell (~2 m) each biome's GrassDensity gives up to MaxGrassPerCell
        /// tufts (varied by a patch noise so meadows aren't uniform) and FlowerDensity the chance
        /// of one flower clump. Cheap to draw in large numbers and only rendered within
        /// Settings.DetailDistance.</summary>
        private void PaintDetails(TerrainData terrainData, BiomeDefinition[,] biomeMap, int seed)
        {
            var prototypes = new List<DetailPrototype>();
            var grassIndices = new List<int>();
            foreach (Texture2D grassTexture in _settings.GrassTextures)
            {
                if (grassTexture == null)
                {
                    continue;
                }

                prototypes.Add(new DetailPrototype
                {
                    prototypeTexture = grassTexture,
                    usePrototypeMesh = false,
                    renderMode = DetailRenderMode.Grass,
                    healthyColor = _settings.GrassHealthyColor,
                    dryColor = _settings.GrassDryColor,
                    minWidth = 0.7f,
                    maxWidth = 1.3f,
                    minHeight = 0.45f,
                    maxHeight = 1f,
                    noiseSpread = 0.4f,
                    density = 1f,
                });

                // An invalid prototype stops the terrain drawing any details at all.
                if (!prototypes[prototypes.Count - 1].Validate(out string error))
                {
                    Debug.LogError($"{nameof(WorldGenerator)}: grass texture '{grassTexture.name}' can't be used - {error}", grassTexture);
                    prototypes.RemoveAt(prototypes.Count - 1);
                    continue;
                }

                grassIndices.Add(prototypes.Count - 1);
            }

            var flowerIndices = new List<int>();
            foreach (GameObject flowerPrefab in _settings.FlowerDetailPrefabs)
            {
                int index = AddDetailPrototype(prototypes, flowerPrefab, 0.8f, 1.2f, 0.8f, 1.25f);
                if (index >= 0)
                {
                    flowerIndices.Add(index);
                }
            }

            terrainData.detailPrototypes = prototypes.ToArray();

            if (prototypes.Count == 0)
            {
                return;
            }

            const int DetailResolution = 512;
            terrainData.SetDetailResolution(DetailResolution, 32);
            // The layers below hold instance counts per cell; the default (coverage) mode reads
            // them as 0-255 coverage, so 1-6 would draw next to nothing.
            terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            // The default waving tint is brownish and multiplies the grass colours into straw.
            terrainData.wavingGrassTint = Color.white;
            terrainData.wavingGrassStrength = 0.35f;
            terrainData.wavingGrassAmount = 0.4f;
            terrainData.wavingGrassSpeed = 0.4f;
            _terrain.detailObjectDistance = _settings.DetailDistance;
            _terrain.detailObjectDensity = 1f;

            var grass = new int[grassIndices.Count][,];
            for (int i = 0; i < grass.Length; i++)
            {
                grass[i] = new int[DetailResolution, DetailResolution];
            }

            var flowers = new int[flowerIndices.Count][,];
            for (int i = 0; i < flowers.Length; i++)
            {
                flowers[i] = new int[DetailResolution, DetailResolution];
            }

            var detailRng = new System.Random(seed + 6);
            Vector2 patchOffset = new Vector2((float)detailRng.NextDouble() * 1000f, (float)detailRng.NextDouble() * 1000f);
            int heightRes = biomeMap.GetLength(0);

            for (int z = 0; z < DetailResolution; z++)
            {
                for (int x = 0; x < DetailResolution; x++)
                {
                    BiomeDefinition biome = SampleBiomeMap(biomeMap, x, z, DetailResolution, heightRes);
                    double grassRoll = detailRng.NextDouble();
                    double flowerRoll = detailRng.NextDouble();
                    int grassKind = detailRng.Next(Mathf.Max(1, grass.Length));
                    int flowerKind = detailRng.Next(Mathf.Max(1, flowers.Length));
                    if (biome == null)
                    {
                        continue;
                    }

                    if (grass.Length > 0 && biome.GrassDensity > 0f)
                    {
                        float patch = Mathf.Lerp(0.35f, 1.35f, Mathf.PerlinNoise(x * GrassPatchNoiseScale + patchOffset.x, z * GrassPatchNoiseScale + patchOffset.y));
                        float tufts = biome.GrassDensity * patch * MaxGrassPerCell;
                        int count = Mathf.FloorToInt(tufts) + (grassRoll < tufts - Mathf.Floor(tufts) ? 1 : 0);
                        grass[grassKind][z, x] = Mathf.Clamp(count, 0, 16);
                    }

                    if (flowers.Length > 0 && flowerRoll < biome.FlowerDensity)
                    {
                        flowers[flowerKind][z, x] = 1;
                    }
                }
            }

            for (int i = 0; i < grassIndices.Count; i++)
            {
                terrainData.SetDetailLayer(0, 0, grassIndices[i], grass[i]);
            }

            for (int i = 0; i < flowerIndices.Count; i++)
            {
                terrainData.SetDetailLayer(0, 0, flowerIndices[i], flowers[i]);
            }
        }

        private static int AddDetailPrototype(List<DetailPrototype> prototypes, GameObject prefab, float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            if (prefab == null)
            {
                return -1;
            }

            var prototype = new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                noiseSpread = 0.3f,
                density = 1f,
                alignToGround = 0.6f,
            };

            // One invalid prototype (e.g. a prefab with several materials) stops the terrain
            // drawing any details at all - skip it loudly instead.
            if (!prototype.Validate(out string error))
            {
                Debug.LogError($"{nameof(WorldGenerator)}: detail prefab '{prefab.name}' can't be used - {error}", prefab);
                return -1;
            }

            prototypes.Add(prototype);
            return prototypes.Count - 1;
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
                        // Drawn for every placed node (not only varied ones) so ids/positions don't
                        // depend on which entries have a size range.
                        float scale = Mathf.Lerp(spawn.MinScale, spawn.MaxScale, (float)resourceRng.NextDouble());

                        // The list index is the node's stable id. An already-gathered node keeps
                        // its slot as null (after the RNG draws above, so every later node still
                        // gets the same position/rotation/index as when the world was created).
                        if (_depletedOnLoad != null && _depletedOnLoad.Contains(_spawnedResources.Count))
                        {
                            _spawnedResources.Add(null);
                            SpawnLeftover(spawn.Prefab, worldPos, rotation, scale, resourceLayer);
                            continue;
                        }

                        GameObject instance = Instantiate(spawn.Prefab, worldPos, rotation, _resourceContainer);
                        instance.transform.localScale *= scale;
                        if (resourceLayer >= 0)
                        {
                            SetLayerRecursively(instance.transform, resourceLayer);
                        }

                        _spawnedResources.Add(instance);
                    }
                }
            }
        }

        /// <summary>Scatters _starterSpawns around SpawnPoint (deterministic, seed + 3). Appended
        /// after the biome resources in _spawnedResources, so they get ids the same way and
        /// picked-up ones are skipped on load.</summary>
        private void SpawnStarterResources(int seed)
        {
            if (!HasSpawnPoint || _starterSpawns.Count == 0)
            {
                return;
            }

            const int attemptsPerItem = 10;
            var rng = new System.Random(seed + 3);
            Vector3 origin = _terrain.transform.position;
            float seaHeight = origin.y + _settings.SeaLevel * _settings.MaxHeight;
            int resourceLayer = LayerMask.NameToLayer(_settings.ResourceLayerName);

            foreach (StarterSpawn spawn in _starterSpawns)
            {
                if (spawn == null || spawn.Prefab == null)
                {
                    continue;
                }

                for (int n = 0; n < spawn.Count; n++)
                {
                    for (int attempt = 0; attempt < attemptsPerItem; attempt++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float distance = Mathf.Lerp(_starterMinRadius, _starterMaxRadius, (float)rng.NextDouble());
                        float yaw = (float)rng.NextDouble() * 360f;
                        Vector3 position = SpawnPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                        position.y = origin.y + _terrain.SampleHeight(position);
                        if (position.y < seaHeight + 0.3f)
                        {
                            continue;
                        }

                        if (_depletedOnLoad != null && _depletedOnLoad.Contains(_spawnedResources.Count))
                        {
                            _spawnedResources.Add(null);
                            break;
                        }

                        GameObject instance = Instantiate(spawn.Prefab, position, Quaternion.Euler(0f, yaw, 0f), _resourceContainer);
                        if (resourceLayer >= 0)
                        {
                            SetLayerRecursively(instance.transform, resourceLayer);
                        }

                        _spawnedResources.Add(instance);
                        break;
                    }
                }
            }
        }

        /// <summary>What a gathered node left where it stood (a felled tree's stump - its
        /// definition's LeavesBehindPrefab), recreated on load so the spot doesn't go bare. The
        /// node's later stages (fallen trunk, logs) aren't saved.</summary>
        private void SpawnLeftover(GameObject nodePrefab, Vector3 position, Quaternion rotation, float scale, int resourceLayer)
        {
            var node = nodePrefab.GetComponent<NAV.Gameplay.Items.ResourceNode>();
            GameObject leftoverPrefab = node != null && node.Definition != null ? node.Definition.LeavesBehindPrefab : null;
            if (leftoverPrefab == null)
            {
                return;
            }

            GameObject leftover = Instantiate(leftoverPrefab, position, rotation, _resourceContainer);
            leftover.transform.localScale *= scale;
            if (resourceLayer >= 0)
            {
                SetLayerRecursively(leftover.transform, resourceLayer);
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

        private static void ClearSpawned(List<GameObject> spawned)
        {
            foreach (GameObject instance in spawned)
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

            spawned.Clear();
        }

        /// <summary>Destroys creatures spawned last time, then places each biome's CreatureSpawns
        /// at random (seeded) points that fall inside that biome and onto the freshly baked
        /// NavMesh - which is why this runs after BakeNavMesh, unlike SpawnResources (whose
        /// colliders must exist before the bake). Play Mode only: an Editor-time Regenerate would
        /// otherwise save live creatures into the scene file. Creatures are not persisted by the
        /// Save system - every load re-spawns them from the seed, same as resources.</summary>
        private void SpawnCreatures(BiomeDefinition[,] biomeMap, int seed)
        {
            ClearSpawned(_spawnedCreatures);

            if (!Application.isPlaying)
            {
                return;
            }

            if (_navMeshSurface == null)
            {
                Debug.LogWarning($"{nameof(WorldGenerator)} on '{name}' has no NavMeshSurface - creatures need a baked NavMesh, so none were spawned.", this);
                return;
            }

            _biomeMap = biomeMap;
            var creatureRng = new System.Random(seed + 2);

            foreach (BiomeDefinition biome in Biomes)
            {
                SpawnBiomeCreatures(biome, creatureRng);
            }

            CreaturesSpawned?.Invoke();
        }

        private void SpawnBiomeCreatures(BiomeDefinition biome, System.Random creatureRng)
        {
            if (biome == null)
            {
                return;
            }

            foreach (BiomeCreatureSpawn spawn in biome.CreatureSpawns)
            {
                if (spawn.Prefab == null || spawn.Count <= 0)
                {
                    continue;
                }

                int placed = 0;
                int attempts = spawn.Count * CreaturePlacementAttemptsPerSpawn;

                for (int i = 0; i < attempts && placed < spawn.Count; i++)
                {
                    Vector3 local = new Vector3((float)creatureRng.NextDouble() * _settings.Width, 0f, (float)creatureRng.NextDouble() * _settings.Length);
                    Vector3 candidate = _terrain.transform.position + local;

                    if (HasSpawnPoint && Vector3.Distance(candidate, SpawnPoint) < _creatureSafeRadius)
                    {
                        continue;
                    }

                    if (!TryGetCreaturePoint(biome, candidate, out Vector3 point))
                    {
                        continue;
                    }

                    int size = creatureRng.Next(spawn.MinGroupSize, spawn.MaxGroupSize + 1);
                    placed += SpawnCreatureGroup(biome, spawn, point, Mathf.Min(size, spawn.Count - placed), creatureRng);
                }

                if (placed < spawn.Count)
                {
                    Debug.LogWarning($"{nameof(WorldGenerator)}: placed only {placed}/{spawn.Count} '{spawn.Prefab.name}' in {biome.DisplayName} (not enough valid NavMesh points).", this);
                }
            }
        }

        /// <summary>The biomes whose CreatureSpawns this world uses (null entries skipped by
        /// callers).</summary>
        public IEnumerable<BiomeDefinition> Biomes
        {
            get
            {
                if (_settings == null)
                {
                    yield break;
                }

                yield return _settings.OceanBiome;
                yield return _settings.PlainsBiome;
                yield return _settings.ForestBiome;
                yield return _settings.GroveBiome;
            }
        }

        /// <summary>True when the last generation spawned creatures (Play Mode, NavMesh baked) -
        /// CreaturePopulation only tops up a world that has a biome map.</summary>
        public bool HasCreatureWorld => _biomeMap != null && _navMeshSurface != null && Application.isPlaying;

        /// <summary>Picks a point on the NavMesh inside the given biome, between minDistance and
        /// maxDistance from around (a ring - CreaturePopulation uses it to add animals out of the
        /// player's sight). Uses UnityEngine.Random: respawns are not part of the seeded world.</summary>
        public bool TryFindCreaturePointNear(BiomeDefinition biome, Vector3 around, float minDistance, float maxDistance, int attempts, out Vector3 point)
        {
            for (int i = 0; i < attempts; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                float distance = Random.Range(minDistance, maxDistance);
                Vector3 candidate = around + new Vector3(direction.x, 0f, direction.y) * distance;

                if (TryGetCreaturePoint(biome, candidate, out point))
                {
                    return true;
                }
            }

            point = default;
            return false;
        }

        /// <summary>Spawns up to count copies of spawn's prefab spread around centre (each on
        /// the NavMesh), tagged with spawn as their Creature.SpawnSource so CreaturePopulation can
        /// count them per entry. Returns how many were placed.</summary>
        public int SpawnCreatureGroup(BiomeDefinition biome, BiomeCreatureSpawn spawn, Vector3 centre, int count, System.Random rng = null)
        {
            int placed = 0;

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(rng?.NextDouble() ?? Random.value) * Mathf.PI * 2f;
                float yaw = (float)(rng?.NextDouble() ?? Random.value) * 360f;
                Vector3 offset = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CreatureGroupSpread;

                if (!NavMesh.SamplePosition(centre + offset, out NavMeshHit navHit, CreatureGroupSpread, NavMesh.AllAreas))
                {
                    continue;
                }

                GameObject instance = Instantiate(spawn.Prefab, navHit.position, Quaternion.Euler(0f, yaw, 0f));
                _spawnedCreatures.Add(instance);

                if (instance.TryGetComponent(out Creature creature))
                {
                    creature.SpawnSource = spawn;
                }

                placed++;
            }

            if (placed > 0)
            {
                _spawnedCreatures.RemoveAll(spawned => spawned == null);
            }

            return placed;
        }

        private bool TryGetCreaturePoint(BiomeDefinition biome, Vector3 candidate, out Vector3 point)
        {
            point = default;
            Vector3 origin = _terrain.transform.position;
            float localX = candidate.x - origin.x;
            float localZ = candidate.z - origin.z;

            if (_biomeMap == null || localX < 0f || localZ < 0f || localX > _settings.Width || localZ > _settings.Length)
            {
                return false;
            }

            int heightRes = _biomeMap.GetLength(0);
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);

            if (!ReferenceEquals(_biomeMap[hz, hx], biome))
            {
                return false;
            }

            candidate.y = origin.y + _terrain.SampleHeight(candidate);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, CreatureNavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            point = navHit.position;
            return true;
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

            // The water isn't solid (the player wades and swims), so without this the NavMesh
            // would cover lake beds and creatures would walk along the bottom. Everything lower
            // than a little under the surface is made not walkable for the bake.
            if (_underwaterNavBlock != null)
            {
                float bottom = _terrain.transform.position.y - 1f;
                float top = waterHeight - UnderwaterNavMargin;
                Transform block = _underwaterNavBlock.transform;
                block.SetPositionAndRotation(new Vector3(_waterPlane.position.x, 0f, _waterPlane.position.z), Quaternion.identity);
                block.localScale = Vector3.one;
                _underwaterNavBlock.center = new Vector3(0f, (bottom + top) * 0.5f, 0f);
                _underwaterNavBlock.size = new Vector3(_settings.Width + 10f, Mathf.Max(0.1f, top - bottom), _settings.Length + 10f);
                _underwaterNavBlock.area = NavMesh.GetAreaFromName("Not Walkable");
            }
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
