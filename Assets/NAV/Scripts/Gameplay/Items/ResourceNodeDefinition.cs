using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Static data for a world resource node (tree, rock, bush, ...): how many hits it takes
    /// to deplete it, and what it scatters into the world (one or more ResourceNodeDrop
    /// entries, each with its own independent chance) once that happens - e.g. a felled tree
    /// mostly drops Wood but occasionally also drops Resin. Interim hits before depletion carry
    /// no reward of their own, only depletion-feedback (see ResourceNode). See ItemDefinition
    /// for the items being dropped - this only describes the node itself.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceNodeDefinition", menuName = "NAV/Items/Resource Node Definition")]
    public class ResourceNodeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;

        [Header("Yield")]
        [SerializeField] private List<ResourceNodeDrop> _drops = new();
        [SerializeField] private int _hitsToDeplete = 3;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) && _drops.Count > 0 && _drops[0].Item != null
            ? _drops[0].Item.DisplayName
            : _displayName;

        public IReadOnlyList<ResourceNodeDrop> Drops => _drops;
        public int HitsToDeplete => _hitsToDeplete;

        private void OnValidate()
        {
            _hitsToDeplete = Mathf.Max(1, _hitsToDeplete);
        }
    }
}
