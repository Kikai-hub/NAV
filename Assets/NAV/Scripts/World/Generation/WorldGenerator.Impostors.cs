using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NAV.Core.Settings;

namespace NAV.World.Generation
{
    /// <summary>
    /// The far plane: in chunks within the view distance that aren't loaded, trees are drawn as
    /// impostors (ImpostorSet - NAV/Impostor quads) with GPU instancing - no GameObjects, two
    /// triangles a tree, no shadows. Placed by the same PlaceNodes as the real trees, so a forest
    /// on the horizon is the forest you'll walk into, and gathered trees stay gone. A far chunk's
    /// instances are computed once (a little per frame) and kept; the draw lists are rebuilt when
    /// the set of far chunks changes.
    /// </summary>
    public partial class WorldGenerator
    {
        private const float FarComputeBudgetMs = 1f;
        private const float DrawListRebuildSeconds = 0.25f;
        private const int MaxInstancesPerDraw = 1023;

        [Header("Far Plane")]
        [Tooltip("Baked tree impostors (NAV > World > Bake Tree Impostors). Empty = nothing drawn past the loaded chunks.")]
        [SerializeField] private ImpostorSet _impostors;

        private class FarChunk
        {
            public readonly Dictionary<Material, List<Matrix4x4>> Instances = new();
        }

        private readonly Dictionary<Vector2Int, FarChunk> _farChunks = new();
        private readonly List<Vector2Int> _farQueue = new();
        private readonly Dictionary<Material, List<Matrix4x4>> _drawLists = new();
        private Mesh _impostorQuad;
        private bool _drawListsDirty = true;
        private float _nextDrawListRebuild;

        /// <summary>How far the camera must draw WorldResource objects so every loaded chunk
        /// shows (the impostors take over only where chunks aren't loaded) - for
        /// ThirdPersonCameraController's layer culling.</summary>
        public static float ResourceDrawDistance(GameSettingsData settings)
        {
            float objects = settings.ObjectDrawDistance > 0f
                ? Mathf.Min(settings.ObjectDrawDistance, settings.EffectiveViewDistance)
                : settings.EffectiveViewDistance;
            return Mathf.Clamp(objects, MinStreamRadius, MaxStreamRadius) + ChunkSize * 0.5f + UnloadMargin + ChunkSize;
        }

        public int FarChunkCount => _farChunks.Count;

        private void ResetImpostors()
        {
            _farChunks.Clear();
            _farQueue.Clear();
            _drawLists.Clear();
            _drawListsDirty = true;
        }

        /// <summary>Queues the far chunks around position that have no instances yet, nearest
        /// first, and forgets those now beyond the view distance.</summary>
        private void RefreshFarChunks(Vector3 position)
        {
            if (_impostors == null)
            {
                return;
            }

            float view = GameSettings.Current.EffectiveViewDistance + ChunkSize;
            _farQueue.Clear();
            Vector2Int centre = ChunkAt(position);
            int reach = Mathf.CeilToInt(view / ChunkSize) + 1;
            for (int dz = -reach; dz <= reach; dz++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(centre.x + dx, centre.y + dz);
                    if (IsInWorld(c) && !_farChunks.ContainsKey(c) && FlatDistance(ChunkCentre(c), position) <= view)
                    {
                        _farQueue.Add(c);
                    }
                }
            }

            _farQueue.Sort((a, b) => FlatDistance(ChunkCentre(a), position).CompareTo(FlatDistance(ChunkCentre(b), position)));

            _unloadList.Clear();
            foreach (Vector2Int c in _farChunks.Keys)
            {
                if (FlatDistance(ChunkCentre(c), position) > view + UnloadMargin)
                {
                    _unloadList.Add(c);
                }
            }

            foreach (Vector2Int c in _unloadList)
            {
                _farChunks.Remove(c);
            }

            _drawListsDirty = true;
        }

        private void ComputeFarChunks()
        {
            if (_impostors == null || _farQueue.Count == 0)
            {
                return;
            }

            float start = Time.realtimeSinceStartup;
            while (_farQueue.Count > 0 && (Time.realtimeSinceStartup - start) * 1000f < FarComputeBudgetMs)
            {
                Vector2Int c = _farQueue[0];
                _farQueue.RemoveAt(0);
                var far = new FarChunk();
                PlaceNodes(c, _placements);
                foreach (NodePlacement placement in _placements)
                {
                    Material material = _impostors.Find(placement.Spawn.Prefab);
                    if (material == null || _depleted.Contains(placement.Id))
                    {
                        continue;
                    }

                    if (!far.Instances.TryGetValue(material, out List<Matrix4x4> list))
                    {
                        list = new List<Matrix4x4>();
                        far.Instances[material] = list;
                    }

                    list.Add(Matrix4x4.TRS(placement.Position, placement.Rotation, Vector3.one * placement.Scale));
                }

                _farChunks[c] = far;
                _drawListsDirty = true;
            }
        }

        private void LateUpdate()
        {
            float started = Time.realtimeSinceStartup;
            DrawFarPlane();
            LastFarPlaneMs = (Time.realtimeSinceStartup - started) * 1000f;
        }

        private void DrawFarPlane()
        {
            if (!Application.isPlaying || _impostors == null || _biomeMap == null)
            {
                return;
            }

            if (_drawListsDirty && Time.unscaledTime >= _nextDrawListRebuild)
            {
                RebuildDrawLists();
            }

            if (_impostorQuad == null)
            {
                _impostorQuad = BuildImpostorQuad();
            }

            var bounds = new Bounds(_terrain.transform.position + new Vector3(_settings.Width, _settings.MaxHeight, _settings.Length) * 0.5f,
                new Vector3(_settings.Width, _settings.MaxHeight * 4f, _settings.Length));
            foreach (KeyValuePair<Material, List<Matrix4x4>> pair in _drawLists)
            {
                var parameters = new RenderParams(pair.Key)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = bounds,
                };
                List<Matrix4x4> list = pair.Value;
                for (int start = 0; start < list.Count; start += MaxInstancesPerDraw)
                {
                    Graphics.RenderMeshInstanced(parameters, _impostorQuad, 0, list, Mathf.Min(MaxInstancesPerDraw, list.Count - start), start);
                }
            }
        }

        /// <summary>Joins the far chunks that aren't loaded into one instance list per impostor.</summary>
        private void RebuildDrawLists()
        {
            _drawListsDirty = false;
            _nextDrawListRebuild = Time.unscaledTime + DrawListRebuildSeconds;
            foreach (List<Matrix4x4> list in _drawLists.Values)
            {
                list.Clear();
            }

            foreach (KeyValuePair<Vector2Int, FarChunk> pair in _farChunks)
            {
                if (_chunks.ContainsKey(pair.Key))
                {
                    continue;
                }

                foreach (KeyValuePair<Material, List<Matrix4x4>> instances in pair.Value.Instances)
                {
                    if (!_drawLists.TryGetValue(instances.Key, out List<Matrix4x4> list))
                    {
                        list = new List<Matrix4x4>();
                        _drawLists[instances.Key] = list;
                    }

                    list.AddRange(instances.Value);
                }
            }
        }

        /// <summary>A unit quad standing on its bottom edge: x -0.5..0.5, y 0..1 (the shader
        /// scales it to the tree and turns it to the camera).</summary>
        private static Mesh BuildImpostorQuad()
        {
            var mesh = new Mesh { name = "Impostor Quad" };
            mesh.SetVertices(new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(0.5f, 1f, 0f), new(-0.5f, 1f, 0f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            // Vertices move in the shader - the bounds only need to keep it from being culled.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            return mesh;
        }
    }
}
