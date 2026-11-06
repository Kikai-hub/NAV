using System.Collections.Generic;
using NAV.World.Biomes;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>One placed point of interest. Position is in metres from the world centre, Y = height above sea.</summary>
    public readonly struct PoiPlacement
    {
        public PoiPlacement(PoiDefinition definition, int index, Vector3 position, BiomeDefinition biome)
        {
            Definition = definition;
            Index = index;
            Position = position;
            Biome = biome;
        }

        public PoiDefinition Definition { get; }
        /// <summary>Its number among the POIs of the same kind - a stable id for saves.</summary>
        public int Index { get; }
        public Vector3 Position { get; }
        public BiomeDefinition Biome { get; }
    }

    /// <summary>
    /// Scatters the points of interest over the whole world once, deterministically from the seed:
    /// for each kind (highest priority first) it tries random spots in its radial band and keeps a
    /// spot only if
    ///  - it is on land (when the kind needs land) and in the allowed height range,
    ///  - its biome is allowed,
    ///  - no POI stands within the exclusion zone: max(both kinds' Exclusion Radius) for any kind,
    ///    Same Kind Radius for its own kind.
    /// Kinds that run out of attempts get fewer points; Shortfalls reports how many were missing.
    /// The exclusion check uses a uniform grid, so it stays fast with thousands of points.
    /// </summary>
    public sealed class PoiPlacer
    {
        private const int PoiSalt = 201;
        // A kind of one or two points in a rare biome (a boss slot in the swamp) needs more random
        // tries than Count x per-point: most of the round world is sea. Tries are cheap.
        private const int MinAttemptsPerKind = 1000;

        private readonly HybridWorldShape _shape;
        private readonly int _seed;
        private readonly List<PoiPlacement> _placed = new();
        private readonly Dictionary<Vector2Int, List<int>> _grid = new();
        private readonly Dictionary<PoiDefinition, int> _shortfalls = new();
        private float _cellSize = 1f;
        private float _largestReach;

        public PoiPlacer(HybridWorldShape shape, int seed)
        {
            _shape = shape;
            _seed = seed;
        }

        public IReadOnlyDictionary<PoiDefinition, int> Shortfalls => _shortfalls;

        public List<PoiPlacement> PlaceAll(IReadOnlyList<PoiDefinition> definitions, int attemptsPerPoint)
        {
            _placed.Clear();
            _grid.Clear();
            _shortfalls.Clear();
            _largestReach = 0f;

            var ordered = new List<PoiDefinition>();
            foreach (PoiDefinition definition in definitions)
            {
                if (definition != null)
                {
                    ordered.Add(definition);
                    _largestReach = Mathf.Max(_largestReach, definition.ExclusionRadius, definition.SameKindRadius);
                }
            }

            // Stable order: priority, then id - the same seed always gives the same world.
            ordered.Sort((a, b) => a.Priority != b.Priority ? b.Priority.CompareTo(a.Priority) : string.CompareOrdinal(a.Id, b.Id));
            _cellSize = Mathf.Max(50f, _largestReach);

            foreach (PoiDefinition definition in ordered)
            {
                PlaceKind(definition, attemptsPerPoint);
            }

            return new List<PoiPlacement>(_placed);
        }

        private void PlaceKind(PoiDefinition definition, int attemptsPerPoint)
        {
            int kind = StableHash(definition.Id);
            int attempts = Mathf.Max(definition.Count * Mathf.Max(1, attemptsPerPoint), definition.Count > 0 ? MinAttemptsPerKind : 0);
            int placed = 0;
            for (int attempt = 0; attempt < attempts && placed < definition.Count; attempt++)
            {
                Vector2 spot = RandomSpot(definition, kind, attempt);
                if (TryAccept(definition, spot, placed, out PoiPlacement placement))
                {
                    Add(placement);
                    placed++;
                }
            }

            if (placed < definition.Count)
            {
                _shortfalls[definition] = definition.Count - placed;
            }
        }

        // Uniform over the ring between the radial range's edges (sqrt keeps the density even).
        private Vector2 RandomSpot(PoiDefinition definition, int kind, int attempt)
        {
            float inner = Mathf.Clamp01(definition.RadialRange.x);
            float outer = Mathf.Clamp(definition.RadialRange.y, inner, 1f);
            float u = WorldHash.Value(_seed, PoiSalt, kind, attempt, 0);
            float angle = WorldHash.Value(_seed, PoiSalt, kind, attempt, 1) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, u)) * _shape.Radius;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private bool TryAccept(PoiDefinition definition, Vector2 spot, int index, out PoiPlacement placement)
        {
            placement = default;

            // Cheapest check first: the exclusion zone needs no noise.
            if (IsCrowded(definition, spot))
            {
                return false;
            }

            WorldPoint point = _shape.Sample(spot.x, spot.y);
            if (definition.RequiresLand && !point.IsLand)
            {
                return false;
            }

            if (point.Height < definition.HeightRange.x || point.Height > definition.HeightRange.y)
            {
                return false;
            }

            if (!definition.AllowsBiome(point.Biome))
            {
                return false;
            }

            placement = new PoiPlacement(definition, index, new Vector3(spot.x, Mathf.Max(0f, point.Height), spot.y), point.Biome);
            return true;
        }

        private bool IsCrowded(PoiDefinition definition, Vector2 spot)
        {
            Vector2Int centre = CellOf(spot);
            int reach = Mathf.CeilToInt(_largestReach / _cellSize);
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    if (!_grid.TryGetValue(new Vector2Int(centre.x + dx, centre.y + dz), out List<int> cell))
                    {
                        continue;
                    }

                    foreach (int i in cell)
                    {
                        PoiPlacement other = _placed[i];
                        float minimum = Mathf.Max(definition.ExclusionRadius, other.Definition.ExclusionRadius);
                        if (other.Definition == definition)
                        {
                            minimum = Mathf.Max(minimum, definition.SameKindRadius);
                        }

                        Vector2 delta = new Vector2(other.Position.x, other.Position.z) - spot;
                        if (delta.sqrMagnitude < minimum * minimum)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void Add(PoiPlacement placement)
        {
            Vector2Int cell = CellOf(new Vector2(placement.Position.x, placement.Position.z));
            if (!_grid.TryGetValue(cell, out List<int> list))
            {
                list = new List<int>();
                _grid[cell] = list;
            }

            list.Add(_placed.Count);
            _placed.Add(placement);
        }

        private Vector2Int CellOf(Vector2 spot) => new(Mathf.FloorToInt(spot.x / _cellSize), Mathf.FloorToInt(spot.y / _cellSize));

        // string.GetHashCode is not stable between runs/platforms; FNV-1a is.
        public static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in text ?? string.Empty)
                {
                    hash = (hash ^ c) * 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
