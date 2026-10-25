using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace NAV.World.Generation
{
    /// <summary>
    /// A NavMesh only around the player instead of over the whole world (a 2.5 km world would take
    /// many seconds to bake, and streamed chunks keep adding and removing tree colliders anyway):
    /// a square of Size metres, rebuilt on a background thread (NavMeshBuilder.UpdateNavMeshDataAsync)
    /// whenever the player has moved RebuildDistance from its centre. Sources are the physics
    /// colliders in the box (terrain, loaded trees/rocks/buildings) - NavMeshModifier markups are
    /// honoured (pickups marked Ignore From Build) - plus a not-walkable box under the water.
    /// Agent type and layers come from the scene's NavMeshSurface (which no longer bakes itself).
    /// </summary>
    public class LocalNavMesh
    {
        private const float VoxelSize = 0.25f;
        private const int TileSizeVoxels = 192; // 48 m tiles
        private readonly int _agentTypeId;
        private readonly LayerMask _layers;
        private readonly float _size;
        private readonly List<NavMeshBuildSource> _sources = new();
        private readonly List<NavMeshBuildMarkup> _markups = new();
        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        private AsyncOperation _pending;
        private float _waterHeight = float.NegativeInfinity;
        private float _groundBottom;

        public LocalNavMesh(NavMeshSurface surface, float size)
        {
            _agentTypeId = surface != null ? surface.agentTypeID : 0;
            _layers = surface != null ? surface.layerMask : (LayerMask)~0;
            _size = size;
        }

        /// <summary>Where the current mesh is centred (only meaningful once Build has run).</summary>
        public Vector3 Centre { get; private set; }
        public bool HasMesh => _data != null;
        public bool IsBuilding => _pending != null && !_pending.isDone;
        public float HalfSize => _size * 0.5f;

        /// <summary>Main-thread milliseconds the last Build spent gathering sources, and how many
        /// (performance checks).</summary>
        public float LastCollectMs { get; private set; }
        public int LastSourceCount { get; private set; }

        /// <summary>Everything at or below waterHeight (down to bottom) is made not walkable -
        /// the water isn't solid, so lake beds would otherwise be walkable.</summary>
        public void SetWater(float waterHeight, float bottom)
        {
            _waterHeight = waterHeight;
            _groundBottom = bottom;
        }

        /// <summary>Rebuilds around centre - immediately (a hitch, for world start and
        /// teleports), or in the background.</summary>
        public void Build(Vector3 centre, bool immediate)
        {
            if (IsBuilding && !immediate)
            {
                return;
            }

            centre.y = 0f;
            Centre = centre;
            var bounds = new Bounds(centre, new Vector3(_size, 2000f, _size));
            float started = Time.realtimeSinceStartup;
            CollectSources(bounds);
            LastCollectMs = (Time.realtimeSinceStartup - started) * 1000f;
            LastSourceCount = _sources.Count;

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(_agentTypeId);
            // Coarser than the default (~a sixth of the agent radius): half the voxels, so a
            // rebuild's final step on the main thread is a fraction of the ~100 ms it was. Animals
            // don't need finer paths than this.
            settings.overrideVoxelSize = true;
            settings.voxelSize = VoxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = TileSizeVoxels;
            if (_data == null)
            {
                _data = new NavMeshData(_agentTypeId) { name = "Local NavMesh" };
                _instance = NavMesh.AddNavMeshData(_data);
            }

            if (immediate)
            {
                NavMeshBuilder.UpdateNavMeshData(_data, settings, _sources, bounds);
                _pending = null;
            }
            else
            {
                _pending = NavMeshBuilder.UpdateNavMeshDataAsync(_data, settings, _sources, bounds);
            }
        }

        public void Clear()
        {
            if (_instance.valid)
            {
                _instance.Remove();
            }

            _data = null;
            _pending = null;
        }

        private void CollectSources(Bounds bounds)
        {
            _markups.Clear();
            foreach (NavMeshModifier modifier in NavMeshModifier.activeModifiers)
            {
                _markups.Add(new NavMeshBuildMarkup
                {
                    root = modifier.transform,
                    overrideArea = modifier.overrideArea,
                    area = modifier.area,
                    ignoreFromBuild = modifier.ignoreFromBuild,
                    applyToChildren = modifier.applyToChildren,
                });
            }

            NavMeshBuilder.CollectSources(bounds, _layers, NavMeshCollectGeometry.PhysicsColliders, 0, _markups, _sources);

            if (!float.IsNegativeInfinity(_waterHeight))
            {
                float top = _waterHeight;
                var size = new Vector3(bounds.size.x + 4f, Mathf.Max(0.1f, top - _groundBottom), bounds.size.z + 4f);
                _sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.Translate(new Vector3(bounds.center.x, (top + _groundBottom) * 0.5f, bounds.center.z)),
                    size = size,
                    area = NavMesh.GetAreaFromName("Not Walkable"),
                });
            }
        }
    }
}
