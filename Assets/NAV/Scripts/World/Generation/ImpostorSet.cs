using System.Collections.Generic;
using UnityEngine;

namespace NAV.World.Generation
{
    /// <summary>
    /// The baked far-away stand-ins for the world's trees (NAV > World > Bake Tree Impostors -
    /// ImpostorBaker): per tree prefab a NAV/Impostor material holding its 8-view atlas, its
    /// size and how far below its pivot it starts. WorldGenerator draws these for trees in
    /// chunks too far away to be loaded.
    /// </summary>
    [CreateAssetMenu(fileName = "ImpostorSet", menuName = "NAV/World/Impostor Set")]
    public class ImpostorSet : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public GameObject Prefab;
            public Material Material;
        }

        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<GameObject, Material> _byPrefab;

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>The impostor material for a resource prefab, or null (rocks, bushes - not
        /// drawn far away).</summary>
        public Material Find(GameObject prefab)
        {
            if (_byPrefab == null)
            {
                _byPrefab = new Dictionary<GameObject, Material>();
                foreach (Entry entry in _entries)
                {
                    if (entry.Prefab != null && entry.Material != null)
                    {
                        _byPrefab[entry.Prefab] = entry.Material;
                    }
                }
            }

            return prefab != null && _byPrefab.TryGetValue(prefab, out Material material) ? material : null;
        }

        private void OnValidate()
        {
            _byPrefab = null;
        }
    }
}
