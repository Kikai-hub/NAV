using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Static data for a world resource node (tree, rock, bush, ...): what it drops per
    /// gather action, and how many gather actions it takes to deplete it. See
    /// ItemDefinition for the item being dropped - this only describes the node itself.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceNodeDefinition", menuName = "NAV/Items/Resource Node Definition")]
    public class ResourceNodeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;

        [Header("Yield")]
        [SerializeField] private ItemDefinition _dropItem;
        [SerializeField] private int _amountPerHit = 1;
        [SerializeField] private int _hitsToDeplete = 3;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) && _dropItem != null
            ? _dropItem.DisplayName
            : _displayName;

        public ItemDefinition DropItem => _dropItem;
        public int AmountPerHit => _amountPerHit;
        public int HitsToDeplete => _hitsToDeplete;

        private void OnValidate()
        {
            _amountPerHit = Mathf.Max(1, _amountPerHit);
            _hitsToDeplete = Mathf.Max(1, _hitsToDeplete);
        }
    }
}
