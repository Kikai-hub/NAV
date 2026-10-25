using System.Collections.Generic;
using UnityEngine;
using NAV.Core.Settings;
using NAV.World.Biomes;

namespace NAV.World.Generation
{
    /// <summary>
    /// Chunk streaming: trees, rocks, ore, bushes and decor exist only in the 64 m chunks around
    /// the player, loaded nearest-first a few milliseconds per frame and destroyed again once out
    /// of range. A chunk's content comes from WorldHash(seed, kind, cell) - not from one random
    /// sequence walked across the map - so it's the same whenever and in whatever order it loads.
    /// Each node's id is chunk index * MaxNodesPerChunk + its order within the chunk; a node that
    /// was gathered is remembered when its chunk unloads (and saved), and on reload only its
    /// leftover (a stump) comes back. Felled trunks and logs are children of the chunk (ResourceNode
    /// spawns its next stage under its own parent), so they go with it - they were never saved.
    /// The NavMesh follows the player too (LocalNavMesh), and creatures live around the player.
    /// </summary>
    public partial class WorldGenerator
    {
        private const float ChunkSize = 64f;
        private const int MaxNodesPerChunk = 4096;
        private const int StarterIdBase = int.MaxValue - 100000;
        private const int DecorKindOffset = 1 << 20;
        // Load radius limits (m) - beyond them stage 3's impostors draw the far trees.
        private const float MinStreamRadius = 120f;
        private const float MaxStreamRadius = 400f;
        private const float UnloadMargin = ChunkSize;
        private const float LoadBudgetMs = 3f;
        private const int MaxUnloadsPerFrame = 1;
        // Creatures stay within ~115 m of the player (CreaturePopulation's leash) and the player
        // within NavMeshRebuildDistance of the mesh centre, so the half-size must cover both.
        private const float NavMeshSize = 400f;
        private const float NavMeshRebuildDistance = 60f;
        /// <summary>Creatures are placed within this distance of the player.</summary>
        public const float CreatureAreaRadius = 100f;

        [Header("Streaming")]
        [Tooltip("Who the world streams around - the player. Camera.main when empty.")]
        [SerializeField] private Transform _streamTarget;

        private class Chunk
        {
            public GameObject Root;
            public readonly List<KeyValuePair<int, GameObject>> Nodes = new();
        }

        private readonly Dictionary<Vector2Int, Chunk> _chunks = new();
        private readonly HashSet<int> _depleted = new();
        private readonly List<Vector2Int> _loadQueue = new();
        private readonly List<Vector2Int> _unloadList = new();
        private readonly List<Vector2Int> _unloadQueue = new();
        // The chunk being filled across frames (one spawn entry per step) - a dense-forest chunk
        // is ~16 ms of instantiating, too much for one frame.
        private IEnumerator<bool> _loading;
        private Vector2Int _loadingChunk;
        private readonly List<GameObject> _starterResources = new();
        private int _seed;
        private int _resourceLayer = -1;
        private Vector3 _streamCentre;
        private bool _primed;
        private Vector2Int _queuedFrom = new(int.MinValue, int.MinValue);
        private LocalNavMesh _localNav;

        /// <summary>How far around the player chunks are kept loaded: the object draw distance
        /// (0 = no limit -> the view distance), within MinStreamRadius..MaxStreamRadius.</summary>
        public float StreamRadius
        {
            get
            {
                GameSettingsData settings = GameSettings.Current;
                float objects = settings.ObjectDrawDistance > 0f
                    ? Mathf.Min(settings.ObjectDrawDistance, settings.EffectiveViewDistance)
                    : settings.EffectiveViewDistance;
                return Mathf.Clamp(objects, MinStreamRadius, MaxStreamRadius) + ChunkSize * 0.5f;
            }
        }

        public int LoadedChunkCount => _chunks.Count;

        /// <summary>Drops every loaded chunk and starts a new world's bookkeeping.</summary>
        private void PrepareChunks(int seed, ICollection<int> depletedIds)
        {
            foreach (Chunk chunk in _chunks.Values)
            {
                DestroyObject(chunk.Root);
            }

            _chunks.Clear();
            _loadQueue.Clear();
            _unloadQueue.Clear();
            _loading = null;
            _queuedFrom = new Vector2Int(int.MinValue, int.MinValue);
            _depleted.Clear();
            if (depletedIds != null)
            {
                _depleted.UnionWith(depletedIds);
            }

            _seed = seed;
            _primed = false;
            if (_resourceContainer == null)
            {
                _resourceContainer = new GameObject("World Content (streamed)").transform;
            }

            _resourceLayer = LayerMask.NameToLayer(_settings.ResourceLayerName);
            if (_resourceLayer < 0)
            {
                Debug.LogError($"{nameof(WorldGenerator)}: Layer '{_settings.ResourceLayerName}' does not exist - create it in Project Settings > Tags and Layers. Spawned resources will stay on their prefab's default layer and won't be distance-culled.", this);
            }

            _localNav?.Clear();
            _localNav = new LocalNavMesh(_navMeshSurface, NavMeshSize);
            ResetImpostors();
        }

        private int ChunksX => Mathf.CeilToInt(_settings.Width / ChunkSize);
        private int ChunksZ => Mathf.CeilToInt(_settings.Length / ChunkSize);

        private Vector2Int ChunkAt(Vector3 position)
        {
            Vector3 origin = _terrain.transform.position;
            return new Vector2Int(Mathf.FloorToInt((position.x - origin.x) / ChunkSize), Mathf.FloorToInt((position.z - origin.z) / ChunkSize));
        }

        private bool IsInWorld(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < ChunksX && c.y < ChunksZ;

        private Vector3 ChunkCentre(Vector2Int c)
        {
            Vector3 origin = _terrain.transform.position;
            return new Vector3(origin.x + (c.x + 0.5f) * ChunkSize, 0f, origin.z + (c.y + 0.5f) * ChunkSize);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Loads the chunk containing position and its 8 neighbours now - FindSpawnPoint
        /// checks for trees there with physics.</summary>
        private void EnsureChunksAround(Vector3 position)
        {
            Vector2Int centre = ChunkAt(position);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    EnsureChunk(new Vector2Int(centre.x + dx, centre.y + dz));
                }
            }
        }

        private void EnsureChunk(Vector2Int c)
        {
            if (IsInWorld(c) && !_chunks.ContainsKey(c))
            {
                LoadChunk(c);
            }
        }

        /// <summary>Loads everything in range of position at once, drops what's out of range,
        /// builds the NavMesh there and (in Play Mode) places creatures around it - for the start
        /// of a world and for teleports (load, respawn), so the player never arrives in an empty
        /// world.</summary>
        public void PrimeAround(Vector3 position)
        {
            if (_biomeMap == null || _terrain == null)
            {
                return;
            }

            _streamCentre = position;
            _primed = true;
            float radius = StreamRadius;
            Vector2Int centre = ChunkAt(position);
            int reach = Mathf.CeilToInt(radius / ChunkSize) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(centre.x + dx, centre.y + dz);
                    if (FlatDistance(ChunkCentre(c), position) <= radius)
                    {
                        EnsureChunk(c);
                    }
                }
            }

            UnloadFarChunks(position, radius);
            foreach (Vector2Int c in _unloadQueue)
            {
                UnloadChunk(c);
            }

            _unloadQueue.Clear();
            _queuedFrom = new Vector2Int(int.MinValue, int.MinValue);
            Physics.SyncTransforms();
            _localNav.Build(position, true);

            // The far plane at once too (one-off, like the rest of a teleport).
            RefreshFarChunks(position);
            while (_farQueue.Count > 0)
            {
                ComputeFarChunks();
            }

            _drawListsDirty = true;
            _nextDrawListRebuild = 0f;

            if (Application.isPlaying)
            {
                SpawnCreaturesAround(position);
            }
        }

        /// <summary>Milliseconds the last frame spent streaming (Update) and drawing the far
        /// plane (LateUpdate) - for performance checks.</summary>
        public float LastStreamMs { get; private set; }
        public float LastFarPlaneMs { get; private set; }

        private void Update()
        {
            float started = Time.realtimeSinceStartup;
            StreamUpdate();
            LastStreamMs = (Time.realtimeSinceStartup - started) * 1000f;
        }

        private void StreamUpdate()
        {
            if (!Application.isPlaying || _biomeMap == null || _localNav == null)
            {
                return;
            }

            if (_streamTarget == null)
            {
                _streamTarget = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : null;
                if (_streamTarget == null)
                {
                    return;
                }
            }

            Vector3 position = _streamTarget.position;
            float radius = StreamRadius;

            // A teleport (load, respawn) - everything at once rather than a slow fill-in.
            if (!_primed || FlatDistance(position, _streamCentre) > radius)
            {
                PrimeAround(position);
                return;
            }

            _streamCentre = position;
            Vector2Int here = ChunkAt(position);
            if (here != _queuedFrom)
            {
                _queuedFrom = here;
                RefreshFarChunks(position);
                RebuildLoadQueue(position, radius);
                UnloadFarChunks(position, radius);
            }

            // Loading and unloading share a few milliseconds per frame: a chunk fills in over
            // several frames, and a row of chunks left behind is destroyed a couple at a time.
            float start = Time.realtimeSinceStartup;
            // Destroy() returns at once and does the work at the end of the frame (~3 ms a
            // chunk), invisible to the time budget - so unloads are counted instead.
            int unloads = 0;
            while ((Time.realtimeSinceStartup - start) * 1000f < LoadBudgetMs)
            {
                if (_loading != null)
                {
                    if (!_loading.MoveNext())
                    {
                        _loading = null;
                    }

                    continue;
                }

                if (_loadQueue.Count > 0)
                {
                    Vector2Int next = _loadQueue[0];
                    _loadQueue.RemoveAt(0);
                    if (IsInWorld(next) && !_chunks.ContainsKey(next))
                    {
                        _loadingChunk = next;
                        _loading = LoadChunkSteps(next);
                    }

                    continue;
                }

                if (_unloadQueue.Count > 0 && unloads++ < MaxUnloadsPerFrame)
                {
                    UnloadChunk(_unloadQueue[_unloadQueue.Count - 1]);
                    _unloadQueue.RemoveAt(_unloadQueue.Count - 1);
                    continue;
                }

                break;
            }

            ComputeFarChunks();

            if (!_localNav.IsBuilding && FlatDistance(position, _localNav.Centre) > NavMeshRebuildDistance)
            {
                _localNav.Build(position, false);
            }
        }

        private void RebuildLoadQueue(Vector3 position, float radius)
        {
            _loadQueue.Clear();
            Vector2Int centre = ChunkAt(position);
            int reach = Mathf.CeilToInt(radius / ChunkSize) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(centre.x + dx, centre.y + dz);
                    if (IsInWorld(c) && !_chunks.ContainsKey(c) && FlatDistance(ChunkCentre(c), position) <= radius)
                    {
                        _loadQueue.Add(c);
                    }
                }
            }

            _loadQueue.Sort((a, b) => FlatDistance(ChunkCentre(a), position).CompareTo(FlatDistance(ChunkCentre(b), position)));
        }

        /// <summary>Queues the chunks now out of range for unloading (Update destroys them within
        /// its frame budget; PrimeAround all at once).</summary>
        private void UnloadFarChunks(Vector3 position, float radius)
        {
            _unloadQueue.Clear();
            foreach (KeyValuePair<Vector2Int, Chunk> pair in _chunks)
            {
                if (FlatDistance(ChunkCentre(pair.Key), position) > radius + UnloadMargin)
                {
                    _unloadQueue.Add(pair.Key);
                }
            }
        }

        private void UnloadChunk(Vector2Int c)
        {
            if (!_chunks.TryGetValue(c, out Chunk chunk))
            {
                return;
            }

            if (_loading != null && c == _loadingChunk)
            {
                _loading = null;
            }

            foreach (KeyValuePair<int, GameObject> node in chunk.Nodes)
            {
                if (node.Value == null)
                {
                    _depleted.Add(node.Key);
                }
            }

            DestroyObject(chunk.Root);
            _chunks.Remove(c);
            _drawListsDirty = true;
        }

        private void LoadChunk(Vector2Int c)
        {
            if (_loading != null && c == _loadingChunk)
            {
                // Already being filled across frames - finish it now.
                while (_loading.MoveNext())
                {
                }

                _loading = null;
                return;
            }

            IEnumerator<bool> steps = LoadChunkSteps(c);
            while (steps.MoveNext())
            {
            }
        }

        /// <summary>Where one resource node of a chunk stands - the same for the real node
        /// (LoadChunkSteps) and its far-away impostor (WorldGenerator.Impostors).</summary>
        private struct NodePlacement
        {
            public BiomeResourceSpawn Spawn;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public int Id;
        }

        private readonly List<NodePlacement> _placements = new();

        /// <summary>Every resource node of chunk c in a fixed order (ids are that order): for
        /// each biome and spawn entry, the entry's MinSpacing grid cells whose corner lies in the
        /// chunk, each jittered, density-rolled and kept only on its own biome and height band -
        /// all from WorldHash, so the same every time. Gathered ones are included (Id tells).</summary>
        private void PlaceNodes(Vector2Int c, List<NodePlacement> results)
        {
            results.Clear();
            Vector3 origin = _terrain.transform.position;
            float minX = c.x * ChunkSize;
            float minZ = c.y * ChunkSize;
            float seaHeight = origin.y + _settings.SeaLevel * _settings.MaxHeight;
            int chunkIndex = c.y * ChunksX + c.x;
            int localIndex = 0;
            int biomeIndex = 0;

            foreach (BiomeDefinition biome in Biomes)
            {
                biomeIndex++;
                if (biome == null)
                {
                    continue;
                }

                for (int e = 0; e < biome.ResourceSpawns.Count; e++)
                {
                    BiomeResourceSpawn spawn = biome.ResourceSpawns[e];
                    if (spawn.Prefab == null || spawn.Density <= 0f || spawn.MinSpacing <= 0f)
                    {
                        continue;
                    }

                    int kind = biomeIndex * 256 + e;
                    float cell = spawn.MinSpacing;
                    int ix0 = Mathf.CeilToInt(minX / cell);
                    int ix1 = Mathf.CeilToInt((minX + ChunkSize) / cell);
                    int iz0 = Mathf.CeilToInt(minZ / cell);
                    int iz1 = Mathf.CeilToInt((minZ + ChunkSize) / cell);
                    for (int iz = iz0; iz < iz1; iz++)
                    {
                        for (int ix = ix0; ix < ix1; ix++)
                        {
                            float localX = (ix + WorldHash.Value(_seed, kind, ix, iz, 0)) * cell;
                            float localZ = (iz + WorldHash.Value(_seed, kind, ix, iz, 1)) * cell;
                            if (localX >= _settings.Width || localZ >= _settings.Length
                                || !ReferenceEquals(BiomeAtLocal(localX, localZ), biome)
                                || WorldHash.Value(_seed, kind, ix, iz, 2) > spawn.Density)
                            {
                                continue;
                            }

                            var position = new Vector3(origin.x + localX, 0f, origin.z + localZ);
                            position.y = origin.y + _terrain.SampleHeight(position);
                            float aboveWater = position.y - seaHeight;
                            if (aboveWater < spawn.HeightAboveWater.x || aboveWater > spawn.HeightAboveWater.y)
                            {
                                continue;
                            }

                            results.Add(new NodePlacement
                            {
                                Spawn = spawn,
                                Position = position,
                                Rotation = Quaternion.Euler(0f, WorldHash.Value(_seed, kind, ix, iz, 3) * 360f, 0f),
                                Scale = Mathf.Lerp(spawn.MinScale, spawn.MaxScale, WorldHash.Value(_seed, kind, ix, iz, 4)),
                                Id = chunkIndex * MaxNodesPerChunk + localIndex++,
                            });
                        }
                    }
                }
            }
        }

        // Instantiations per loading step (~1-2 ms) - Update runs steps until its budget is spent.
        private const int NodesPerStep = 12;

        /// <summary>Fills a chunk a few nodes per step (Update spreads the steps over frames).
        /// The chunk counts as loaded from the first step.</summary>
        private IEnumerator<bool> LoadChunkSteps(Vector2Int c)
        {
            var chunk = new Chunk { Root = new GameObject($"Chunk {c.x},{c.y}") };
            chunk.Root.transform.SetParent(_resourceContainer, false);
            _chunks[c] = chunk;
            _drawListsDirty = true;

            var placements = new List<NodePlacement>();
            PlaceNodes(c, placements);
            yield return true;

            int sinceYield = 0;
            foreach (NodePlacement placement in placements)
            {
                if (_depleted.Contains(placement.Id))
                {
                    SpawnLeftover(placement.Spawn.Prefab, placement.Position, placement.Rotation, placement.Scale, chunk.Root.transform);
                    chunk.Nodes.Add(new KeyValuePair<int, GameObject>(placement.Id, null));
                    continue;
                }

                GameObject instance = Instantiate(placement.Spawn.Prefab, placement.Position, placement.Rotation, chunk.Root.transform);
                instance.transform.localScale *= placement.Scale;
                SetLayer(instance);
                chunk.Nodes.Add(new KeyValuePair<int, GameObject>(placement.Id, instance));
                if (++sinceYield >= NodesPerStep)
                {
                    sinceYield = 0;
                    yield return true;
                }
            }

            int biomeIndex = 0;
            float minX = c.x * ChunkSize;
            float minZ = c.y * ChunkSize;
            foreach (BiomeDefinition biome in Biomes)
            {
                biomeIndex++;
                if (biome == null)
                {
                    continue;
                }

                for (int e = 0; e < biome.DecorSpawns.Count; e++)
                {
                    LoadDecor(biome, biome.DecorSpawns[e], DecorKindOffset + biomeIndex * 256 + e, minX, minZ, chunk.Root.transform);
                    yield return true;
                }
            }
        }

        /// <summary>Decor (bushes, mushrooms, stumps, logs, pebbles) the same way, no ids - not
        /// gatherable, not saved. Walk-through unless Keep Colliders.</summary>
        private void LoadDecor(BiomeDefinition biome, BiomeDecorSpawn spawn, int kind, float minX, float minZ, Transform parent)
        {
            if (spawn.Prefab == null || spawn.Density <= 0f || spawn.MinSpacing <= 0f)
            {
                return;
            }

            Vector3 origin = _terrain.transform.position;
            float cell = spawn.MinSpacing;
            int ix0 = Mathf.CeilToInt(minX / cell);
            int ix1 = Mathf.CeilToInt((minX + ChunkSize) / cell);
            int iz0 = Mathf.CeilToInt(minZ / cell);
            int iz1 = Mathf.CeilToInt((minZ + ChunkSize) / cell);
            for (int iz = iz0; iz < iz1; iz++)
            {
                for (int ix = ix0; ix < ix1; ix++)
                {
                    float localX = (ix + WorldHash.Value(_seed, kind, ix, iz, 0)) * cell;
                    float localZ = (iz + WorldHash.Value(_seed, kind, ix, iz, 1)) * cell;
                    if (localX >= _settings.Width || localZ >= _settings.Length
                        || WorldHash.Value(_seed, kind, ix, iz, 2) > spawn.Density
                        || !ReferenceEquals(BiomeAtLocal(localX, localZ), biome))
                    {
                        continue;
                    }

                    var position = new Vector3(origin.x + localX, 0f, origin.z + localZ);
                    position.y = origin.y + _terrain.SampleHeight(position);
                    GameObject instance = Instantiate(spawn.Prefab, position, Quaternion.Euler(0f, WorldHash.Value(_seed, kind, ix, iz, 3) * 360f, 0f), parent);
                    instance.transform.localScale *= Mathf.Lerp(spawn.MinScale, spawn.MaxScale, WorldHash.Value(_seed, kind, ix, iz, 4));
                    if (!spawn.KeepColliders)
                    {
                        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
                        {
                            // Disabled, not destroyed: Destroy is deferred and a NavMesh build in
                            // the same frame would still see it.
                            collider.enabled = false;
                        }
                    }

                    SetLayer(instance);
                }
            }
        }

        private BiomeDefinition BiomeAtLocal(float localX, float localZ)
        {
            int heightRes = _biomeMap.GetLength(0);
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / _settings.Width * (heightRes - 1)), 0, heightRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / _settings.Length * (heightRes - 1)), 0, heightRes - 1);
            return _biomeMap[hz, hx];
        }

        private void SetLayer(GameObject instance)
        {
            if (_resourceLayer >= 0)
            {
                SetLayerRecursively(instance.transform, _resourceLayer);
            }
        }

        private static void DestroyObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        /// <summary>Ids of gathered nodes: remembered from unloaded chunks, the loaded chunks'
        /// gone ones, and picked-up starter items.</summary>
        public List<int> GetDepletedResourceIds()
        {
            var ids = new HashSet<int>(_depleted);
            foreach (Chunk chunk in _chunks.Values)
            {
                foreach (KeyValuePair<int, GameObject> node in chunk.Nodes)
                {
                    if (node.Value == null)
                    {
                        ids.Add(node.Key);
                    }
                }
            }

            for (int i = 0; i < _starterResources.Count; i++)
            {
                if (_starterResources[i] == null)
                {
                    ids.Add(StarterIdBase + i);
                }
            }

            return new List<int>(ids);
        }
    }
}
