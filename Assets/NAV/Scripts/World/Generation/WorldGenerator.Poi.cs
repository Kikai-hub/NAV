using System.Collections.Generic;
using NAV.World.Generation.Hybrid;
using UnityEngine;

namespace NAV.World.Generation
{
    /// <summary>
    /// Points of interest: placed over the whole world once per Generate (PoiPlacer - biome, land,
    /// height and exclusion checks), then spawned with the chunk they stand in and destroyed with
    /// it. Trees, rocks and decor keep the POI's Clear Radius free. Not saved - the seed
    /// recreates them.
    /// </summary>
    public partial class WorldGenerator
    {
        private const int PoiRotationSalt = 211;

        private readonly List<PoiPlacement> _pointsOfInterest = new();
        private readonly Dictionary<Vector2Int, List<PoiPlacement>> _poisByChunk = new();

        /// <summary>Every point of interest of the current world (positions: Y = ground height).</summary>
        public IReadOnlyList<PoiPlacement> PointsOfInterest => _pointsOfInterest;

        private void PlacePointsOfInterest(int seed)
        {
            _pointsOfInterest.Clear();
            _poisByChunk.Clear();
            var placer = new PoiPlacer(_shape, seed);
            foreach (PoiPlacement placed in placer.PlaceAll(_worldShape.PointsOfInterest, _worldShape.PoiAttemptsPerPoint))
            {
                Vector3 position = placed.Position;
                position.y = GroundHeight(position.x, position.z);
                var poi = new PoiPlacement(placed.Definition, placed.Index, position, placed.Biome);
                _pointsOfInterest.Add(poi);

                Vector2Int chunk = ChunkAt(position);
                if (!_poisByChunk.TryGetValue(chunk, out List<PoiPlacement> list))
                {
                    list = new List<PoiPlacement>();
                    _poisByChunk[chunk] = list;
                }

                list.Add(poi);
            }

            foreach (KeyValuePair<PoiDefinition, int> shortfall in placer.Shortfalls)
            {
                Debug.LogWarning($"{nameof(WorldGenerator)}: {shortfall.Value} of {shortfall.Key.Count} '{shortfall.Key.DisplayName}' points of interest found no valid spot.", shortfall.Key);
            }
        }

        private void SpawnPointsOfInterest(Vector2Int c, Transform parent)
        {
            if (!_poisByChunk.TryGetValue(c, out List<PoiPlacement> pois))
            {
                return;
            }

            foreach (PoiPlacement poi in pois)
            {
                if (poi.Definition.Prefab == null)
                {
                    continue;
                }

                float yaw = WorldHash.Value(_seed, PoiRotationSalt, PoiPlacer.StableHash(poi.Definition.Id), poi.Index, 0) * 360f;
                GameObject instance = Instantiate(poi.Definition.Prefab, poi.Position, Quaternion.Euler(0f, yaw, 0f), parent);
                instance.name = $"{poi.Definition.Prefab.name} ({poi.Definition.Id} {poi.Index})";
            }
        }

        /// <summary>True when (x, z) lies within a POI's Clear Radius (POIs of chunk c and its
        /// neighbours - a clear radius is far less than a chunk).</summary>
        private bool IsClearedForPoi(Vector2Int c, float x, float z)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!_poisByChunk.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out List<PoiPlacement> pois))
                    {
                        continue;
                    }

                    foreach (PoiPlacement poi in pois)
                    {
                        float ox = poi.Position.x - x;
                        float oz = poi.Position.z - z;
                        float radius = poi.Definition.ClearRadius;
                        if (ox * ox + oz * oz < radius * radius)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
