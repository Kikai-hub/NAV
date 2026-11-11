using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Player;
using NAV.World.Biomes;
using NAV.World.Generation;

namespace NAV.Presentation.Map
{
    /// <summary>
    /// What the map knows (developer's request 2026-11-10, after Valheim): the world is covered by
    /// a fog, and it lifts within RevealRadius round the player as they go. Online each player
    /// has their own: only this machine's player reveals (NAV.Core.MapExplorers are only drawn as
    /// dots by MapUI). Cells of CellSize metres are grouped in tiles of
    /// TileCells x TileCells, each with its own small texture, made only where something was
    /// explored. An explored cell is coloured from the world itself - the biome's map colour on
    /// parchment, shaded by the slope, water by its depth; an unexplored one is dark parchment.
    /// Saved as one bit per cell (SaveManager), the colours are made again on load.
    /// </summary>
    public class MapExploration : MonoBehaviour
    {
        public const float CellSize = 4f;
        public const int TileCells = 128;
        public const float TileSize = CellSize * TileCells;

        [SerializeField] private WorldGenerator _world;
        [Tooltip("Found by its PlayerMotor when empty.")]
        [SerializeField] private Transform _player;
        [SerializeField, Min(8f)] private float _revealRadius = 70f;
        [Tooltip("Move at least this far (m) before revealing again.")]
        [SerializeField, Min(0.5f)] private float _revealStep = 3f;
        [SerializeField, Min(0.05f)] private float _applySeconds = 0.3f;

        public sealed class Tile
        {
            public Vector2Int Key;
            public Texture2D Texture;
            public Color32[] Pixels;
            public byte[] Bits;
            public bool Dirty;
        }

        private static readonly Color32 FogColor = new(46, 38, 28, 255);
        private static readonly Color Parchment = new(0.86f, 0.78f, 0.6f);
        private static readonly Color ShallowWater = new(0.42f, 0.6f, 0.62f);
        private static readonly Color DeepWater = new(0.16f, 0.3f, 0.42f);

        private readonly Dictionary<Vector2Int, Tile> _tiles = new();
        private readonly Dictionary<Transform, Vector3> _lastReveal = new();
        private float _nextApply;
        private Vector3 _origin;
        private bool _ready;
        private Vector2Int _exploredMinCell = new(int.MaxValue, int.MaxValue);
        private Vector2Int _exploredMaxCell = new(int.MinValue, int.MinValue);

        /// <summary>A tile was made or got new explored cells (its texture changed).</summary>
        public event Action<Tile> TileChanged;

        public IEnumerable<Tile> Tiles => _tiles.Values;
        public Transform Player => _player;

        /// <summary>World position of the corner where tile (0, 0) starts.</summary>
        public Vector3 Origin => _origin;

        /// <summary>Something has been explored (ExploredBounds is valid).</summary>
        public bool HasExplored => _exploredMaxCell.x >= _exploredMinCell.x;

        /// <summary>The box round every explored cell, in map metres (as ToMap).</summary>
        public Rect ExploredBounds => HasExplored
            ? Rect.MinMaxRect(_exploredMinCell.x * CellSize, _exploredMinCell.y * CellSize, (_exploredMaxCell.x + 1) * CellSize, (_exploredMaxCell.y + 1) * CellSize)
            : default;

        private void Grow(int cx, int cz)
        {
            _exploredMinCell = Vector2Int.Min(_exploredMinCell, new Vector2Int(cx, cz));
            _exploredMaxCell = Vector2Int.Max(_exploredMaxCell, new Vector2Int(cx, cz));
        }

        private void Update()
        {
            if (!EnsureReady())
            {
                return;
            }

            // Only this machine's own player: online, everyone has their own explored map
            // (developer, 2026-11-10) - the others show on it as dots (MapUI), not as revealed land.
            RevealAround(_player);

            if (Time.unscaledTime >= _nextApply)
            {
                _nextApply = Time.unscaledTime + _applySeconds;
                foreach (Tile tile in _tiles.Values)
                {
                    if (tile.Dirty)
                    {
                        tile.Dirty = false;
                        tile.Texture.SetPixels32(tile.Pixels);
                        tile.Texture.Apply(false);
                        TileChanged?.Invoke(tile);
                    }
                }
            }
        }

        private bool EnsureReady()
        {
            if (_world == null || !_world.HasSpawnPoint)
            {
                return false;
            }

            if (_player == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor == null)
                {
                    return false;
                }

                _player = motor.transform;
            }

            if (!_ready)
            {
                _ready = true;
                float radius = _world.WorldRadius * 1.2f;
                _origin = _world.WorldCentre - new Vector3(radius, 0f, radius);
                // Explored before the world existed (a loaded save) - colour it now.
                foreach (Tile tile in _tiles.Values)
                {
                    PaintAll(tile);
                }
            }

            return true;
        }

        private void RevealAround(Transform explorer)
        {
            Vector3 position = explorer.position;
            if (_lastReveal.TryGetValue(explorer, out Vector3 last) && (position - last).sqrMagnitude < _revealStep * _revealStep)
            {
                return;
            }

            _lastReveal[explorer] = position;
            Reveal(position, _revealRadius);
        }

        /// <summary>Lifts the fog in a circle (the developer console's "reveal" too).</summary>
        public void Reveal(Vector3 centre, float radius)
        {
            if (!EnsureReady())
            {
                return;
            }

            int cells = Mathf.CeilToInt(radius / CellSize);
            int cx = Mathf.FloorToInt((centre.x - _origin.x) / CellSize);
            int cz = Mathf.FloorToInt((centre.z - _origin.z) / CellSize);
            float r2 = (radius / CellSize) * (radius / CellSize);
            for (int dz = -cells; dz <= cells; dz++)
            {
                for (int dx = -cells; dx <= cells; dx++)
                {
                    if (dx * dx + dz * dz <= r2)
                    {
                        RevealCell(cx + dx, cz + dz);
                    }
                }
            }
        }

        private void RevealCell(int cx, int cz)
        {
            if (cx < 0 || cz < 0)
            {
                return;
            }

            var key = new Vector2Int(cx / TileCells, cz / TileCells);
            Tile tile = GetTile(key);
            int lx = cx - key.x * TileCells;
            int lz = cz - key.y * TileCells;
            int index = lz * TileCells + lx;
            if ((tile.Bits[index >> 3] & (1 << (index & 7))) != 0)
            {
                return;
            }

            tile.Bits[index >> 3] |= (byte)(1 << (index & 7));
            Grow(cx, cz);
            tile.Pixels[index] = CellColour(cx, cz);
            tile.Dirty = true;
        }

        private Tile GetTile(Vector2Int key)
        {
            if (_tiles.TryGetValue(key, out Tile tile))
            {
                return tile;
            }

            tile = new Tile
            {
                Key = key,
                Texture = new Texture2D(TileCells, TileCells, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = $"MapTile {key}" },
                Pixels = new Color32[TileCells * TileCells],
                Bits = new byte[TileCells * TileCells / 8],
                Dirty = true,
            };
            for (int i = 0; i < tile.Pixels.Length; i++)
            {
                tile.Pixels[i] = Fog(key.x * TileCells + i % TileCells, key.y * TileCells + i / TileCells);
            }

            _tiles[key] = tile;
            return tile;
        }

        private void PaintAll(Tile tile)
        {
            for (int i = 0; i < tile.Pixels.Length; i++)
            {
                int cx = tile.Key.x * TileCells + i % TileCells;
                int cz = tile.Key.y * TileCells + i / TileCells;
                tile.Pixels[i] = (tile.Bits[i >> 3] & (1 << (i & 7))) != 0 ? CellColour(cx, cz) : Fog(cx, cz);
            }

            tile.Dirty = true;
        }

        /// <summary>Plain dark parchment - the same colour as the map field behind the tiles
        /// (MapPanel.uss), so a tile's unexplored part doesn't show as a square.</summary>
        private static Color32 Fog(int cx, int cz)
        {
            return FogColor;
        }

        private Color32 CellColour(int cx, int cz)
        {
            float x = _origin.x + (cx + 0.5f) * CellSize;
            float z = _origin.z + (cz + 0.5f) * CellSize;
            float height = _world.GroundHeight(x, z);
            float sea = _world.SeaHeight;
            Color colour;
            if (height < sea)
            {
                colour = Color.Lerp(ShallowWater, DeepWater, Mathf.Clamp01((sea - height) / 14f));
            }
            else
            {
                BiomeDefinition biome = _world.GetBiomeAt(new Vector3(x, height, z));
                Color baseColour = biome != null ? biome.MapColor : new Color(0.6f, 0.65f, 0.45f);
                colour = Color.Lerp(baseColour, Parchment, 0.25f);
                // Hill shading, light from the north-west.
                float east = _world.GroundHeight(x + CellSize, z) - height;
                float north = _world.GroundHeight(x, z + CellSize) - height;
                float shade = Mathf.Clamp(1f + (north - east) * 0.09f, 0.7f, 1.2f);
                colour *= shade;
                // A dark rim where land meets water.
                if (height < sea + 0.6f)
                {
                    colour = Color.Lerp(colour, new Color(0.78f, 0.7f, 0.5f), 0.6f);
                }
            }

            colour.a = 1f;
            return colour;
        }

        // ---------------------------------------------------------------- save

        public List<NAV.Core.Save.ExploredTileSaveData> Save()
        {
            var saved = new List<NAV.Core.Save.ExploredTileSaveData>();
            foreach (Tile tile in _tiles.Values)
            {
                saved.Add(new NAV.Core.Save.ExploredTileSaveData { X = tile.Key.x, Y = tile.Key.y, Bits = Convert.ToBase64String(tile.Bits) });
            }

            return saved;
        }

        public void Restore(List<NAV.Core.Save.ExploredTileSaveData> saved)
        {
            if (saved == null)
            {
                return;
            }

            foreach (NAV.Core.Save.ExploredTileSaveData entry in saved)
            {
                if (string.IsNullOrEmpty(entry.Bits))
                {
                    continue;
                }

                Tile tile = GetTile(new Vector2Int(entry.X, entry.Y));
                byte[] bits = Convert.FromBase64String(entry.Bits);
                Array.Copy(bits, tile.Bits, Mathf.Min(bits.Length, tile.Bits.Length));
                for (int i = 0; i < TileCells * TileCells; i++)
                {
                    if ((tile.Bits[i >> 3] & (1 << (i & 7))) != 0)
                    {
                        Grow(tile.Key.x * TileCells + i % TileCells, tile.Key.y * TileCells + i / TileCells);
                    }
                }

                if (_ready)
                {
                    PaintAll(tile);
                }
            }
        }

        /// <summary>World position -> position in map metres from the origin (x right, y north).</summary>
        public Vector2 ToMap(Vector3 world)
        {
            return new Vector2(world.x - _origin.x, world.z - _origin.z);
        }
    }
}
