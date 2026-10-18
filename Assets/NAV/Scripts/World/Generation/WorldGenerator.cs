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

        // Attempts per requested creature before giving up on finding a valid point (right
        // biome, on the NavMesh, outside the safe zone) - bounds the loop for biomes that are
        // tiny or missing in an unlucky seed.
        private const int CreaturePlacementAttemptsPerSpawn = 30;
        private const float CreatureNavMeshSampleDistance = 2f;
        private const float SpawnFlatnessProbeDistance = 2f;
        private readonly Collider[] _spawnOverlapBuffer = new Collider[16];
        private ICollection<int> _depletedOnLoad;

        /// <summary>A dry, flat, unobstructed ground point picked deterministically from the
        /// seed during Generate (nearest such point to the terrain centre). SaveManager places a
        /// new-game player here and uses it as the respawn point; creatures keep their distance
        /// from it. Only valid when HasSpawnPoint is true.</summary>
        public Vector3 SpawnPoint { get; private set; }
        public bool HasSpawnPoint { get; private set; }

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
            SpawnResources(biomeMap, seed, depletedResourceIds);
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

                        // The list index is the node's stable id. An already-gathered node keeps
                        // its slot as null (after the RNG draws above, so every later node still
                        // gets the same position/rotation/index as when the world was created).
                        if (_depletedOnLoad != null && _depletedOnLoad.Contains(_spawnedResources.Count))
                        {
                            _spawnedResources.Add(null);
                            continue;
                        }

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

            var creatureRng = new System.Random(seed + 2);
            int heightRes = biomeMap.GetLength(0);

            SpawnBiomeCreatures(_settings.OceanBiome, biomeMap, heightRes, creatureRng);
            SpawnBiomeCreatures(_settings.PlainsBiome, biomeMap, heightRes, creatureRng);
            SpawnBiomeCreatures(_settings.ForestBiome, biomeMap, heightRes, creatureRng);
        }

        private void SpawnBiomeCreatures(BiomeDefinition biome, BiomeDefinition[,] biomeMap, int heightRes, System.Random creatureRng)
        {
            if (biome == null)
            {
                return;
            }

            Vector3 origin = _terrain.transform.position;

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
                    float localX = (float)creatureRng.NextDouble() * _settings.Width;
                    float localZ = (float)creatureRng.NextDouble() * _settings.Length;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);
                    float yaw = (float)creatureRng.NextDouble() * 360f;

                    if (!ReferenceEquals(biomeMap[hz, hx], biome))
                    {
                        continue;
                    }

                    Vector3 candidate = new Vector3(origin.x + localX, 0f, origin.z + localZ);
                    candidate.y = origin.y + _terrain.SampleHeight(candidate);

                    if (HasSpawnPoint && Vector3.Distance(candidate, SpawnPoint) < _creatureSafeRadius)
                    {
                        continue;
                    }

                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, CreatureNavMeshSampleDistance, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    _spawnedCreatures.Add(Instantiate(spawn.Prefab, navHit.position, Quaternion.Euler(0f, yaw, 0f)));
                    placed++;
                }

                if (placed < spawn.Count)
                {
                    Debug.LogWarning($"{nameof(WorldGenerator)}: placed only {placed}/{spawn.Count} '{spawn.Prefab.name}' in {biome.DisplayName} (not enough valid NavMesh points).", this);
                }
            }
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
