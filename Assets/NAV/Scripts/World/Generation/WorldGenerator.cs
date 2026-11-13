using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using NAV.Gameplay.AI;
using NAV.World.Biomes;
using NAV.World.Generation.Hybrid;

namespace NAV.World.Generation
{
    /// <summary>
    /// Builds the hybrid world (HybridWorldShape: a round world of radius ~15 km - continents in
    /// the middle, archipelagos around them, deep ocean at the rim; biomes by climate ring,
    /// latitude and height) and streams it around the player:
    ///  - the ground: square terrain tiles computed on worker threads (TerrainTiles);
    ///  - trees, rocks, ore, bushes, decor and points of interest: 64 m chunks
    ///    (WorldGenerator.Chunks), deterministic per chunk, with stable per-node ids so gathered
    ///    ones are saved and skipped on load (GetDepletedResourceIds / Generate(seed, depleted));
    ///  - far trees: impostors (WorldGenerator.Impostors);
    ///  - the NavMesh and the creatures follow the player (LocalNavMesh, CreaturePopulation).
    /// Sea level is world Y 0 and the world's centre is the origin.
    ///
    /// Deterministic: everything derives from the seed through HybridWorldShape's noise and
    /// WorldHash - GDD 4: "The same seed must produce the same world when generation settings are
    /// identical." Generate() (no args) uses Settings' own Seed (standalone testing); SaveManager
    /// calls Generate(int) with a new-game-random or loaded-from-save seed.
    /// </summary>
    // Before the player's movement: after a teleport the ground there is built (PrimeAround) in
    // the same frame, before PlayerMotor takes its first step on it.
    [DefaultExecutionOrder(-100)]
    public partial class WorldGenerator : MonoBehaviour
    {
        /// <summary>World Y of the sea surface.</summary>
        public const float SeaLevel = 0f;

        [SerializeField] private WorldGenerationSettings _settings;
        [Tooltip("The hybrid world's shape: continents, archipelagos, climate, polar caps, height biomes, points of interest.")]
        [SerializeField] private HybridWorldSettings _worldShape;
        [Tooltip("Template only: its material, pixel error, basemap distance and collider material are copied to every terrain tile. The object itself is hidden on Generate.")]
        [SerializeField] private Terrain _terrain;
        [Tooltip("Optional. A flat quad/plane repositioned to sea level and scaled far past the world - placeholder water, not a real shader/wave system.")]
        [SerializeField] private Transform _waterPlane;
        [Tooltip("Gives the agent type and layers for the NavMesh LocalNavMesh builds around the player (it no longer bakes itself).")]
        [SerializeField] private NavMeshSurface _navMeshSurface;
        [Tooltip("Optional. A NavMeshModifierVolume (not a child of the water plane) PlaceWater stretches over everything under the water, so the NavMesh doesn't cover sea beds now that the water isn't solid.")]
        [SerializeField] private NavMeshModifierVolume _underwaterNavBlock;
        [Tooltip("Optional parent for the streamed chunks (trees, rocks, decor) and starter items, purely for Hierarchy organization.")]
        [SerializeField] private Transform _resourceContainer;
        [Tooltip("No creature is spawned within this distance of the player spawn point (see SpawnPoint).")]
        [SerializeField, Min(0f)] private float _creatureSafeRadius = 30f;

        [Header("Player Spawn Point")]
        [Tooltip("Ring spacing (m) of the outward spiral search from the world centre.")]
        [SerializeField, Min(1f)] private float _spawnSearchStep = 4f;
        [Tooltip("How far (m) from the centre the spawn search goes before giving up.")]
        [SerializeField, Min(10f)] private float _spawnSearchRadius = 3000f;
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
        [Tooltip("Off when SaveManager drives generation instead (New Game/Load pick the seed) - see UNITY_SETUP_NEXT_STEPS.md's Save step. On for standalone testing of this scene without a SaveManager present.")]
        [SerializeField] private bool _generateOnStart = true;

        private readonly List<GameObject> _spawnedCreatures = new();

        // Attempts per requested creature before giving up on finding a valid point (right
        // biome, on the NavMesh, outside the safe zone) - bounds the loop for biomes that are
        // tiny or missing near the player.
        private const int CreaturePlacementAttemptsPerSpawn = 30;
        private const float CreatureNavMeshSampleDistance = 2f;
        // Metres between members of one group (a pack) at spawn.
        private const float CreatureGroupSpread = 3f;
        // Metres under the water surface where the NavMesh stops - animals may stand at the edge.
        private const float UnderwaterNavMargin = 0.3f;
        // How many world diameters wide the sea plane is: the round world sits in an endless sea.
        private const float EndlessSeaScale = 3f;
        private const float SpawnFlatnessProbeDistance = 2f;

        private HybridWorldShape _shape;
        private TerrainTiles _tiles;
        private Transform _tileContainer;
        private readonly List<BiomeDefinition> _biomes = new();
        private readonly Collider[] _spawnOverlapBuffer = new Collider[16];

        /// <summary>Raised after Generate placed the world's creatures (Play Mode only) -
        /// CreaturePopulation restarts its respawn timers.</summary>
        public event System.Action CreaturesSpawned;

        /// <summary>A dry, flat, unobstructed ground point picked deterministically from the
        /// seed during Generate (nearest such point to the world centre). SaveManager places a
        /// new-game player here and uses it as the respawn point; creatures keep their distance
        /// from it. Only valid when HasSpawnPoint is true.</summary>
        public Vector3 SpawnPoint { get; private set; }
        public bool HasSpawnPoint { get; private set; }

        /// <summary>The current world's shape (null before the first Generate).</summary>
        public HybridWorldShape Shape => _shape;

        /// <summary>Height of the sea surface in world space.</summary>
        public float SeaHeight => SeaLevel;

        /// <summary>World radius (m) - past it there is only open ocean.</summary>
        public float WorldRadius => _worldShape != null ? _worldShape.WorldRadius : 0f;

        /// <summary>The world's centre in world space.</summary>
        public Vector3 WorldCentre => Vector3.zero;

        /// <summary>Terrain tiles standing / still being computed - for performance checks.</summary>
        public int TerrainTileCount => _tiles?.TileCount ?? 0;
        public int PendingTerrainTiles => _tiles?.PendingCount ?? 0;

        /// <summary>Ground height (world Y) at a point - the same surface the terrain tiles show.</summary>
        public float GroundHeight(float x, float z) => _tiles != null ? _tiles.HeightAt(x, z) : SeaLevel;

        /// <summary>Returns the position raised onto the ground if it's below it, else unchanged.
        /// Used when a save from an older world generation (see SaveSystem.WorldGenerationVersion)
        /// is loaded and the ground may have moved up.</summary>
        public Vector3 LiftAboveGround(Vector3 position)
        {
            if (_tiles == null)
            {
                return position;
            }

            float ground = GroundHeight(position.x, position.z) + 0.1f;
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

        private void OnDestroy()
        {
            _tiles?.Clear();
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

        /// <summary>Generates the world for a seed, skipping resource nodes whose id is in
        /// depletedResourceIds (gathered in a previous session - see GetDepletedResourceIds).</summary>
        public void Generate(int seed, ICollection<int> depletedResourceIds)
        {
            if (_settings == null || _worldShape == null || _terrain == null)
            {
                Debug.LogError($"{nameof(WorldGenerator)} on '{name}' is missing Settings, World Shape or the Terrain template.", this);
                return;
            }

            if (_settings.TileBaseData == null)
            {
                Debug.LogError($"{nameof(WorldGenerator)}: '{_settings.name}' has no Tile Base Data - run NAV > World > Hybrid World > Create Tile Terrain Base.", _settings);
                return;
            }

            _shape = new HybridWorldShape(_worldShape, seed);
            _biomes.Clear();
            _biomes.AddRange(_shape.Biomes);

            // The scene's Terrain is only the template for the tiles.
            _terrain.gameObject.SetActive(false);
            if (_tileContainer == null)
            {
                _tileContainer = new GameObject("Terrain Tiles").transform;
            }

            _tiles?.Clear();
            BuildDetailPrototypes(out DetailPrototype[] prototypes, out bool[] isFlower);
            _tiles = new TerrainTiles(_shape, seed, _terrain, _settings.TileBaseData, _tileContainer, prototypes, isFlower, _settings.DetailDistance);

            ClearSpawned(_spawnedCreatures);
            PrepareChunks(seed, depletedResourceIds);
            PlacePointsOfInterest(seed);
            PlaceWater();
            DropWholeWorldNavMesh();
            NAV.Core.WorldWater.SeaHeight = SeaLevel;
            NAV.Core.WorldWater.WorldCentre = WorldCentre;
            NAV.Core.WorldWater.WorldRadius = WorldRadius;
            NAV.Core.WorldClimate.ColdProvider = position =>
            {
                BiomeDefinition biome = GetBiomeAt(position);
                return biome != null ? biome.Cold : 0f;
            };
            _localNav.SetWater(SeaLevel - UnderwaterNavMargin, _tiles.BaseY - 1f);
            FindSpawnPoint();
            SpawnStarterResources(seed);
            // Ground, trees/rocks around the start, the NavMesh there and the first creatures.
            PrimeAround(HasSpawnPoint ? SpawnPoint : WorldCentre);
        }

        /// <summary>Spiral search outward from the world centre for the first point that is
        /// land, high enough above sea level, flat, and free of colliders (trees/rocks).
        /// Deterministic for a seed, so a loaded save gets the same respawn point its world had.</summary>
        private void FindSpawnPoint()
        {
            HasSpawnPoint = false;
            for (float radius = 0f; radius <= _spawnSearchRadius; radius += _spawnSearchStep)
            {
                int samples = radius <= 0f ? 1 : Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / _spawnSearchStep));
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 candidate = WorldCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsValidSpawnPoint(candidate, out Vector3 ground))
                    {
                        SpawnPoint = ground;
                        HasSpawnPoint = true;
                        return;
                    }
                }
            }

            Debug.LogWarning($"{nameof(WorldGenerator)}: no valid player spawn point found - the player keeps its scene position.", this);
        }

        private bool IsValidSpawnPoint(Vector3 candidate, out Vector3 ground)
        {
            ground = candidate;
            BiomeDefinition biome = _tiles.BiomeAt(candidate.x, candidate.z);
            if (biome == null || biome.IsWater || ReferenceEquals(biome, _worldShape.OceanBiome) || !IsStartingBiome(biome))
            {
                return false;
            }

            float height = GroundHeight(candidate.x, candidate.z);
            if (height < SeaLevel + _spawnMinHeightAboveSea)
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
                if (Mathf.Abs(GroundHeight(probe.x, probe.z) - height) > _spawnMaxHeightDifference)
                {
                    return false;
                }
            }

            ground = new Vector3(candidate.x, height, candidate.z);
            // Trees there exist only once their chunk is loaded.
            EnsureChunksAround(candidate);
            Physics.SyncTransforms();
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

        /// <summary>A new player starts in the safe first climate band (the plains and their
        /// copses) - never on a cold mountain or in a swamp, which the core can hold too.</summary>
        private bool IsStartingBiome(BiomeDefinition biome)
        {
            if (biome.Cold > 0f)
            {
                return false;
            }

            var bands = _worldShape.ClimateBands;
            if (bands.Count == 0)
            {
                return true;
            }

            return ReferenceEquals(biome, bands[0].Biome) || ReferenceEquals(biome, bands[0].MixBiome);
        }

        /// <summary>The grass textures and flower prefabs as terrain detail prototypes, shared by
        /// every tile; invalid ones are skipped loudly (one invalid prototype stops a terrain
        /// drawing any details at all).</summary>
        private void BuildDetailPrototypes(out DetailPrototype[] prototypes, out bool[] isFlower)
        {
            var list = new List<DetailPrototype>();
            var flowers = new List<bool>();
            foreach (Texture2D grassTexture in _settings.GrassTextures)
            {
                if (grassTexture == null)
                {
                    continue;
                }

                var prototype = new DetailPrototype
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
                };

                if (!prototype.Validate(out string error))
                {
                    Debug.LogError($"{nameof(WorldGenerator)}: grass texture '{grassTexture.name}' can't be used - {error}", grassTexture);
                    continue;
                }

                list.Add(prototype);
                flowers.Add(false);
            }

            foreach (GameObject flowerPrefab in _settings.FlowerDetailPrefabs)
            {
                if (flowerPrefab == null)
                {
                    continue;
                }

                var prototype = new DetailPrototype
                {
                    prototype = flowerPrefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f,
                    maxWidth = 1.2f,
                    minHeight = 0.8f,
                    maxHeight = 1.25f,
                    noiseSpread = 0.3f,
                    density = 1f,
                    alignToGround = 0.6f,
                };

                if (!prototype.Validate(out string error))
                {
                    Debug.LogError($"{nameof(WorldGenerator)}: detail prefab '{flowerPrefab.name}' can't be used - {error}", flowerPrefab);
                    continue;
                }

                list.Add(prototype);
                flowers.Add(true);
            }

            prototypes = list.ToArray();
            isFlower = flowers.ToArray();
        }

        /// <summary>Scatters _starterSpawns around SpawnPoint (deterministic, seed + 3). Not
        /// streamed - always there; ids StarterIdBase + n, so picked-up ones are skipped on load.</summary>
        private void SpawnStarterResources(int seed)
        {
            ClearSpawned(_starterResources);
            if (!HasSpawnPoint || _starterSpawns.Count == 0)
            {
                return;
            }

            const int attemptsPerItem = 10;
            var rng = new System.Random(seed + 3);
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
                        position.y = GroundHeight(position.x, position.z);
                        if (position.y < SeaLevel + 0.3f)
                        {
                            continue;
                        }

                        if (_depleted.Contains(StarterIdBase + _starterResources.Count))
                        {
                            _starterResources.Add(null);
                            break;
                        }

                        GameObject instance = Instantiate(spawn.Prefab, position, Quaternion.Euler(0f, yaw, 0f), _resourceContainer);
                        SetLayer(instance);
                        _starterResources.Add(instance);
                        break;
                    }
                }
            }
        }

        /// <summary>What a gathered node left where it stood (a felled tree's stump - its
        /// definition's LeavesBehindPrefab), recreated on load so the spot doesn't go bare. The
        /// node's later stages (fallen trunk, logs) aren't saved.</summary>
        private void SpawnLeftover(GameObject nodePrefab, Vector3 position, Quaternion rotation, float scale, Transform parent)
        {
            var node = nodePrefab.GetComponent<NAV.Gameplay.Items.ResourceNode>();
            GameObject leftoverPrefab = node != null && node.Definition != null ? node.Definition.LeavesBehindPrefab : null;
            if (leftoverPrefab == null)
            {
                return;
            }

            GameObject leftover = Instantiate(leftoverPrefab, position, rotation, parent);
            leftover.transform.localScale *= scale;
            SetLayer(leftover);
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

        /// <summary>Destroys the creatures there were, then places each biome's CreatureSpawns at
        /// random points within CreatureAreaRadius of centre that fall inside that biome and onto
        /// the local NavMesh (built just before - PrimeAround). Creatures live around the player
        /// (CreaturePopulation tops them up and removes far ones), so only biomes near the
        /// player get theirs. Play Mode only; not saved.</summary>
        private void SpawnCreaturesAround(Vector3 centre)
        {
            ClearSpawned(_spawnedCreatures);
            var creatureRng = new System.Random(_seed + 2);

            foreach (BiomeDefinition biome in Biomes)
            {
                SpawnBiomeCreatures(biome, creatureRng, centre);
            }

            CreaturesSpawned?.Invoke();
        }

        private void SpawnBiomeCreatures(BiomeDefinition biome, System.Random creatureRng, Vector3 centre)
        {
            if (biome == null)
            {
                return;
            }

            foreach (BiomeCreatureSpawn spawn in biome.CreatureSpawns)
            {
                if (spawn.Prefab == null || spawn.Count <= 0 || !NAV.Gameplay.Demo.DemoMode.AllowsSpawn(biome, spawn.Prefab))
                {
                    continue;
                }

                int placed = 0;
                int attempts = spawn.Count * CreaturePlacementAttemptsPerSpawn;

                for (int i = 0; i < attempts && placed < spawn.Count; i++)
                {
                    float angle = (float)creatureRng.NextDouble() * Mathf.PI * 2f;
                    float distance = Mathf.Sqrt((float)creatureRng.NextDouble()) * CreatureAreaRadius;
                    Vector3 candidate = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

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

                // Fewer (or none) is normal: only the part of the biome near the player counts.
            }
        }

        /// <summary>The biomes this world uses, each once, in a fixed order (null-free).</summary>
        public IEnumerable<BiomeDefinition> Biomes => _biomes;

        /// <summary>True when the world is generated and has a NavMesh in Play Mode -
        /// CreaturePopulation only tops up then.</summary>
        public bool HasCreatureWorld => _tiles != null && _localNav != null && _localNav.HasMesh && Application.isPlaying;

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

        /// <summary>The biome at a world position (null before the first generation) -
        /// AtmosphereFog and AmbienceController ask it where the camera is.</summary>
        public BiomeDefinition GetBiomeAt(Vector3 position)
        {
            return _tiles?.BiomeAt(position.x, position.z);
        }

        private bool TryGetCreaturePoint(BiomeDefinition biome, Vector3 candidate, out Vector3 point)
        {
            point = default;
            if (_tiles == null || !ReferenceEquals(_tiles.BiomeAt(candidate.x, candidate.z), biome))
            {
                return false;
            }

            candidate.y = GroundHeight(candidate.x, candidate.z);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, CreatureNavMeshSampleDistance, NavMesh.AllAreas))
            {
                return false;
            }

            point = navHit.position;
            return true;
        }

        private void PlaceWater()
        {
            if (_waterPlane == null)
            {
                return;
            }

            _waterPlane.position = new Vector3(WorldCentre.x, SeaLevel, WorldCentre.z);

            // Assumes a default Unity Plane primitive (10x10 units at scale 1). The water shader
            // works in world space, so one huge plane is fine.
            float seaSize = WorldRadius * 2f * EndlessSeaScale;
            _waterPlane.localScale = new Vector3(seaSize / 10f, 1f, seaSize / 10f);

            // The water isn't solid (the player wades and swims), so without this the NavMesh
            // would cover sea beds and creatures would walk along the bottom.
            if (_underwaterNavBlock != null)
            {
                float bottom = _tiles.BaseY - 1f;
                float top = SeaLevel - UnderwaterNavMargin;
                Transform block = _underwaterNavBlock.transform;
                block.SetPositionAndRotation(new Vector3(WorldCentre.x, 0f, WorldCentre.z), Quaternion.identity);
                block.localScale = Vector3.one;
                _underwaterNavBlock.center = new Vector3(0f, (bottom + top) * 0.5f, 0f);
                _underwaterNavBlock.size = new Vector3(seaSize, Mathf.Max(0.1f, top - bottom), seaSize);
                _underwaterNavBlock.area = NavMesh.GetAreaFromName("Not Walkable");
            }
        }

        /// <summary>The scene's NavMeshSurface no longer bakes the whole world (LocalNavMesh builds
        /// around the player, from physics colliders - render meshes of imported art aren't
        /// readable in builds); its old data, if any, is dropped so the two never overlap.</summary>
        private void DropWholeWorldNavMesh()
        {
            if (_navMeshSurface != null && _navMeshSurface.navMeshData != null)
            {
                _navMeshSurface.RemoveData();
                _navMeshSurface.navMeshData = null;
            }
        }
    }
}
