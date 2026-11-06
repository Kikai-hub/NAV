using System.Collections.Generic;
using System.Threading.Tasks;
using NAV.World.Biomes;
using NAV.World.Generation.Hybrid;
using UnityEngine;

namespace NAV.World.Generation
{
    /// <summary>
    /// The ground of the hybrid world as square Unity Terrains (tiles) around the player - one
    /// Terrain can't hold a 30 km world. A tile's heights, biomes, ground textures and grass are
    /// computed from HybridWorldShape on a worker thread; the main thread then only builds the
    /// TerrainData (one tile per frame). Neighbouring tiles share their edge samples, so there are
    /// no seams. Tiles far behind the player are destroyed.
    ///
    /// The computed samples are kept while a tile exists: HeightAt/BiomeAt answer from them
    /// (grid-exact, like the terrain itself) and fall back to sampling the shape where no tile is
    /// ready yet - so everything placed on the ground agrees with the ground.
    /// Sea level is world Y 0.
    /// </summary>
    internal sealed class TerrainTiles
    {
        public const float TileSize = 512f;
        // A sample every 2 m (the old single terrain had one every 2.5 m).
        private const int HeightResolution = 257;
        private const int GroundResolution = 256;
        private const int DetailPatchSize = 32;
        private const float CellSize = TileSize / (HeightResolution - 1);
        private const float GroundCellSize = TileSize / GroundResolution;
        // Grass as on the old terrain: up to 6 tufts per ~2 m cell at Grass Density 1, thinned
        // and thickened by a patch noise about 30 m across.
        private const int MaxGrassPerCell = 6;
        private const float GrassReferenceCell = 1000f / 512f;
        private const float GrassPatchScale = 32f;
        private const int DetailSalt = 301;
        private const int MaxAppliesPerFrame = 1;
        private const int MaxRemovalsPerFrame = 1;

        private sealed class Tile
        {
            public Task<TileData> Job;
            public TileData Data;
            public Terrain Terrain;
            public bool Failed;
        }

        private sealed class TileData
        {
            public float[,] Heights01;
            public float[] Heights;
            public BiomeDefinition[] Biomes;
            public TerrainLayer[] Layers;
            public float[,,] Alphamap;
            public int[][,] Details;
        }

        private readonly HybridWorldShape _shape;
        private readonly int _seed;
        private readonly Terrain _template;
        private readonly TerrainData _baseData;
        private readonly Transform _parent;
        private readonly DetailPrototype[] _detailPrototypes;
        private readonly bool[] _isFlower;
        private readonly int _grassKinds;
        private readonly int _flowerKinds;
        private readonly float _detailDistance;
        private readonly NoiseField _grassPatch;
        private readonly float _worldReach;
        private readonly Dictionary<Vector2Int, Tile> _tiles = new();
        private readonly List<Vector2Int> _remove = new();

        public TerrainTiles(HybridWorldShape shape, int seed, Terrain template, TerrainData baseData, Transform parent,
            DetailPrototype[] detailPrototypes, bool[] isFlower, float detailDistance)
        {
            _shape = shape;
            _seed = seed;
            _template = template;
            _baseData = baseData;
            _parent = parent;
            _detailPrototypes = detailPrototypes;
            _isFlower = isFlower;
            foreach (bool flower in isFlower)
            {
                if (flower)
                {
                    _flowerKinds++;
                }
                else
                {
                    _grassKinds++;
                }
            }

            _detailDistance = detailDistance;
            _grassPatch = new NoiseField(new NoiseLayer(GrassPatchScale, 1), seed, DetailSalt);
            BaseY = shape.MinHeight;
            HeightRange = shape.MaxHeight - shape.MinHeight;
            // Nothing but deep sea floor past the rim - no tiles there.
            _worldReach = shape.Radius + TileSize;
        }

        /// <summary>World Y of the tiles' lowest possible ground.</summary>
        public float BaseY { get; }
        public float HeightRange { get; }
        public int TileCount => _tiles.Count;
        public int PendingCount
        {
            get
            {
                int pending = 0;
                foreach (Tile tile in _tiles.Values)
                {
                    if (tile.Terrain == null && !tile.Failed)
                    {
                        pending++;
                    }
                }

                return pending;
            }
        }

        /// <summary>Starts the tiles within radius of centre that don't exist yet and marks those
        /// beyond radius + a tile for removal (Update removes them a few per frame).</summary>
        public void Request(Vector3 centre, float radius)
        {
            Vector2Int here = KeyAt(centre.x, centre.z);
            int reach = Mathf.CeilToInt(radius / TileSize) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var key = new Vector2Int(here.x + dx, here.y + dz);
                    if (!_tiles.ContainsKey(key) && IsInWorld(key) && DistanceTo(key, centre) <= radius)
                    {
                        Start(key);
                    }
                }
            }

            _remove.Clear();
            foreach (Vector2Int key in _tiles.Keys)
            {
                if (DistanceTo(key, centre) > radius + TileSize)
                {
                    _remove.Add(key);
                }
            }
        }

        /// <summary>Requests the tiles around centre and waits until all of them stand - for the
        /// start of a world and teleports.</summary>
        public void CompleteAround(Vector3 centre, float radius)
        {
            Request(centre, radius);
            foreach (KeyValuePair<Vector2Int, Tile> pair in _tiles)
            {
                if (DistanceTo(pair.Key, centre) <= radius)
                {
                    Apply(pair.Key, pair.Value, true);
                }
            }

            RemoveAll();
        }

        /// <summary>Builds finished tiles and removes far ones, a few per frame.</summary>
        public void Update()
        {
            int applied = 0;
            foreach (KeyValuePair<Vector2Int, Tile> pair in _tiles)
            {
                if (applied >= MaxAppliesPerFrame)
                {
                    break;
                }

                if (pair.Value.Terrain == null && !pair.Value.Failed && pair.Value.Job.IsCompleted)
                {
                    Apply(pair.Key, pair.Value, false);
                    applied++;
                }
            }

            for (int i = 0; i < MaxRemovalsPerFrame && _remove.Count > 0; i++)
            {
                Remove(_remove[_remove.Count - 1]);
                _remove.RemoveAt(_remove.Count - 1);
            }
        }

        public void Clear()
        {
            foreach (Tile tile in _tiles.Values)
            {
                DestroyTile(tile);
            }

            _tiles.Clear();
            _remove.Clear();
        }

        /// <summary>Ground height (world Y) at a point - from the tile's samples when computed,
        /// else straight from the shape (the same grid, interpolated the same way).</summary>
        public float HeightAt(float x, float z)
        {
            Vector2Int key = KeyAt(x, z);
            float gx = (x - key.x * TileSize) / CellSize;
            float gz = (z - key.y * TileSize) / CellSize;
            int ix = Mathf.Clamp(Mathf.FloorToInt(gx), 0, HeightResolution - 2);
            int iz = Mathf.Clamp(Mathf.FloorToInt(gz), 0, HeightResolution - 2);
            float tx = gx - ix;
            float tz = gz - iz;

            float h00, h10, h01, h11;
            TileData data = DataAt(key);
            if (data != null)
            {
                h00 = data.Heights[iz * HeightResolution + ix];
                h10 = data.Heights[iz * HeightResolution + ix + 1];
                h01 = data.Heights[(iz + 1) * HeightResolution + ix];
                h11 = data.Heights[(iz + 1) * HeightResolution + ix + 1];
            }
            else
            {
                float x0 = key.x * TileSize + ix * CellSize;
                float z0 = key.y * TileSize + iz * CellSize;
                h00 = _shape.Sample(x0, z0).Height;
                h10 = _shape.Sample(x0 + CellSize, z0).Height;
                h01 = _shape.Sample(x0, z0 + CellSize).Height;
                h11 = _shape.Sample(x0 + CellSize, z0 + CellSize).Height;
            }

            // Same split of the cell into two triangles as Unity's terrain mesh.
            float height = tx + tz <= 1f
                ? h00 + (h10 - h00) * tx + (h01 - h00) * tz
                : h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
            return Mathf.Max(height, BaseY);
        }

        /// <summary>The biome at a point: the nearest sample of its tile, else the shape's.</summary>
        public BiomeDefinition BiomeAt(float x, float z)
        {
            Vector2Int key = KeyAt(x, z);
            TileData data = DataAt(key);
            if (data == null)
            {
                return _shape.Sample(x, z).Biome;
            }

            int ix = Mathf.Clamp(Mathf.RoundToInt((x - key.x * TileSize) / CellSize), 0, HeightResolution - 1);
            int iz = Mathf.Clamp(Mathf.RoundToInt((z - key.y * TileSize) / CellSize), 0, HeightResolution - 1);
            return data.Biomes[iz * HeightResolution + ix];
        }

        private static Vector2Int KeyAt(float x, float z) => new(Mathf.FloorToInt(x / TileSize), Mathf.FloorToInt(z / TileSize));

        private static float DistanceTo(Vector2Int key, Vector3 point)
        {
            float dx = (key.x + 0.5f) * TileSize - point.x;
            float dz = (key.y + 0.5f) * TileSize - point.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private bool IsInWorld(Vector2Int key) => DistanceTo(key, Vector3.zero) <= _worldReach;

        private TileData DataAt(Vector2Int key)
        {
            if (!_tiles.TryGetValue(key, out Tile tile) || tile.Failed)
            {
                return null;
            }

            if (tile.Data == null && tile.Job.IsCompleted)
            {
                Collect(key, tile);
            }

            return tile.Data;
        }

        private void Start(Vector2Int key)
        {
            _tiles[key] = new Tile { Job = Task.Run(() => Compute(key)) };
        }

        // Takes a finished job's result (or reports its failure once).
        private void Collect(Vector2Int key, Tile tile)
        {
            if (tile.Job.IsFaulted || tile.Job.IsCanceled)
            {
                tile.Failed = true;
                Debug.LogError($"{nameof(TerrainTiles)}: tile {key} failed to generate - the ground there falls back to sampling.");
                if (tile.Job.Exception != null)
                {
                    Debug.LogException(tile.Job.Exception);
                }

                return;
            }

            tile.Data = tile.Job.Result;
        }

        private void Apply(Vector2Int key, Tile tile, bool wait)
        {
            if (tile.Terrain != null || tile.Failed)
            {
                return;
            }

            if (wait)
            {
                // Faults are reported by Collect.
                ((System.IAsyncResult)tile.Job).AsyncWaitHandle.WaitOne();
            }

            if (tile.Data == null)
            {
                Collect(key, tile);
                if (tile.Data == null)
                {
                    return;
                }
            }

            tile.Terrain = BuildTerrain(key, tile.Data);
        }

        private void Remove(Vector2Int key)
        {
            if (_tiles.TryGetValue(key, out Tile tile))
            {
                DestroyTile(tile);
                _tiles.Remove(key);
            }
        }

        private void RemoveAll()
        {
            foreach (Vector2Int key in _remove)
            {
                Remove(key);
            }

            _remove.Clear();
        }

        private static void DestroyTile(Tile tile)
        {
            // A job still running just finishes into nothing.
            if (tile.Terrain == null)
            {
                return;
            }

            TerrainData data = tile.Terrain.terrainData;
            if (Application.isPlaying)
            {
                Object.Destroy(tile.Terrain.gameObject);
                Object.Destroy(data);
            }
            else
            {
                Object.DestroyImmediate(tile.Terrain.gameObject);
                Object.DestroyImmediate(data);
            }
        }

        // Worker thread: no engine calls in here.
        private TileData Compute(Vector2Int key)
        {
            float originX = key.x * TileSize;
            float originZ = key.y * TileSize;
            const int n = HeightResolution;
            var data = new TileData
            {
                Heights01 = new float[n, n],
                Heights = new float[n * n],
                Biomes = new BiomeDefinition[n * n],
            };

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    WorldPoint point = _shape.Sample(originX + x * CellSize, originZ + z * CellSize);
                    int i = z * n + x;
                    data.Heights[i] = point.Height;
                    data.Heights01[z, x] = Mathf.Clamp01((point.Height - BaseY) / HeightRange);
                    data.Biomes[i] = point.Biome;
                }
            }

            ComputeGround(data);
            ComputeDetails(data, key, originX, originZ);
            return data;
        }

        // One ground layer per cell (the biome's), only the layers this tile uses.
        private static void ComputeGround(TileData data)
        {
            const int g = GroundResolution;
            var layers = new List<TerrainLayer>();
            var cellLayer = new int[g * g];
            for (int z = 0; z < g; z++)
            {
                for (int x = 0; x < g; x++)
                {
                    BiomeDefinition biome = data.Biomes[z * HeightResolution + x];
                    TerrainLayer layer = biome is null ? null : biome.TerrainLayer;
                    if (layer is null)
                    {
                        cellLayer[z * g + x] = -1;
                        continue;
                    }

                    int index = -1;
                    for (int l = 0; l < layers.Count; l++)
                    {
                        if (ReferenceEquals(layers[l], layer))
                        {
                            index = l;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        layers.Add(layer);
                        index = layers.Count - 1;
                    }

                    cellLayer[z * g + x] = index;
                }
            }

            data.Layers = layers.ToArray();
            if (layers.Count == 0)
            {
                return;
            }

            data.Alphamap = new float[g, g, layers.Count];
            for (int z = 0; z < g; z++)
            {
                for (int x = 0; x < g; x++)
                {
                    data.Alphamap[z, x, Mathf.Max(0, cellLayer[z * g + x])] = 1f;
                }
            }
        }

        private void ComputeDetails(TileData data, Vector2Int key, float originX, float originZ)
        {
            int kinds = _detailPrototypes.Length;
            data.Details = new int[kinds][,];
            if (kinds == 0)
            {
                return;
            }

            const int g = GroundResolution;
            float cellArea = GroundCellSize * GroundCellSize / (GrassReferenceCell * GrassReferenceCell);
            for (int z = 0; z < g; z++)
            {
                for (int x = 0; x < g; x++)
                {
                    int i = z * HeightResolution + x;
                    BiomeDefinition biome = data.Biomes[i];
                    if (biome is null || data.Heights[i] <= 0.1f)
                    {
                        continue;
                    }

                    // Global cell numbers: the same cell gets the same grass from any tile build.
                    int cellX = key.x * g + x;
                    int cellZ = key.y * g + z;
                    if (_grassKinds > 0 && biome.GrassDensity > 0f)
                    {
                        float patch = Mathf.Lerp(0.35f, 1.35f, _grassPatch.Sample(originX + x * GroundCellSize, originZ + z * GroundCellSize));
                        float tufts = biome.GrassDensity * patch * MaxGrassPerCell * cellArea;
                        int count = Mathf.FloorToInt(tufts) + (WorldHash.Value(_seed, DetailSalt, cellX, cellZ, 0) < tufts - Mathf.Floor(tufts) ? 1 : 0);
                        if (count > 0)
                        {
                            int kind = PrototypeOfKind(false, (int)(WorldHash.Value(_seed, DetailSalt, cellX, cellZ, 1) * _grassKinds));
                            Layer(data, kind)[z, x] = Mathf.Min(count, 16);
                        }
                    }

                    if (_flowerKinds > 0 && WorldHash.Value(_seed, DetailSalt, cellX, cellZ, 2) < biome.FlowerDensity * cellArea)
                    {
                        int kind = PrototypeOfKind(true, (int)(WorldHash.Value(_seed, DetailSalt, cellX, cellZ, 3) * _flowerKinds));
                        Layer(data, kind)[z, x] = 1;
                    }
                }
            }
        }

        private static int[,] Layer(TileData data, int kind)
        {
            return data.Details[kind] ??= new int[GroundResolution, GroundResolution];
        }

        // The n-th grass (or flower) prototype's index in the shared prototype array.
        private int PrototypeOfKind(bool flower, int n)
        {
            for (int i = 0; i < _isFlower.Length; i++)
            {
                if (_isFlower[i] == flower && n-- == 0)
                {
                    return i;
                }
            }

            return 0;
        }

        // Main thread: the TerrainData and its GameObject, settings copied from the template.
        private Terrain BuildTerrain(Vector2Int key, TileData tile)
        {
            // Cloned from an editor-made asset: new TerrainData() draws grass as white quads in
            // player builds (fine in the editor).
            TerrainData data = UnityEngine.Object.Instantiate(_baseData);
            data.heightmapResolution = HeightResolution;
            data.size = new Vector3(TileSize, HeightRange, TileSize);
            data.SetHeights(0, 0, tile.Heights01);

            if (tile.Layers.Length > 0)
            {
                data.alphamapResolution = GroundResolution;
                data.terrainLayers = tile.Layers;
                data.SetAlphamaps(0, 0, tile.Alphamap);
            }

            if (_detailPrototypes.Length > 0)
            {
                data.SetDetailResolution(GroundResolution, DetailPatchSize);
                // The layers hold instance counts per cell, not 0-255 coverage.
                data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                data.detailPrototypes = _detailPrototypes;
                data.wavingGrassTint = Color.white;
                data.wavingGrassStrength = 0.35f;
                data.wavingGrassAmount = 0.4f;
                data.wavingGrassSpeed = 0.4f;
                for (int i = 0; i < tile.Details.Length; i++)
                {
                    if (tile.Details[i] != null)
                    {
                        data.SetDetailLayer(0, 0, i, tile.Details[i]);
                    }
                }
            }

            GameObject tileObject = Terrain.CreateTerrainGameObject(data);
            tileObject.name = $"Terrain Tile {key.x},{key.y}";
            tileObject.layer = _template.gameObject.layer;
            tileObject.transform.SetParent(_parent, false);
            tileObject.transform.position = new Vector3(key.x * TileSize, BaseY, key.y * TileSize);

            var terrain = tileObject.GetComponent<Terrain>();
            terrain.materialTemplate = _template.materialTemplate;
            terrain.heightmapPixelError = _template.heightmapPixelError;
            terrain.basemapDistance = _template.basemapDistance;
            terrain.drawInstanced = _template.drawInstanced;
            terrain.shadowCastingMode = _template.shadowCastingMode;
            terrain.reflectionProbeUsage = _template.reflectionProbeUsage;
            terrain.groupingID = _template.groupingID;
            terrain.allowAutoConnect = true;
            terrain.detailObjectDistance = _detailDistance;
            terrain.detailObjectDensity = 1f;

            var templateCollider = _template.GetComponent<TerrainCollider>();
            if (templateCollider != null)
            {
                tileObject.GetComponent<TerrainCollider>().sharedMaterial = templateCollider.sharedMaterial;
            }

            return terrain;
        }
    }
}
