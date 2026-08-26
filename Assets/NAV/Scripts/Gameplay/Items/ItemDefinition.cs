using UnityEngine;

namespace NAV.Gameplay.Items
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "NAV/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private ItemCategory _category;

        [Header("Stacking")]
        [SerializeField] private int _maxStackSize = 1;

        [Header("Physical")]
        [SerializeField] private float _weight;

        [Header("World")]
        [SerializeField] private GameObject _worldPrefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public ItemCategory Category => _category;
        public int MaxStackSize => _maxStackSize;
        public float Weight => _weight;

        /// <summary>
        /// Prefab instantiated when this item is dropped into the world (must carry an
        /// ItemPickup component - see PlayerInventory.DropItem). Null means dropping this
        /// item is unsupported until an asset is assigned.
        /// </summary>
        public GameObject WorldPrefab => _worldPrefab;

        private void OnValidate()
        {
            _maxStackSize = Mathf.Max(1, _maxStackSize);
            _weight = Mathf.Max(0f, _weight);

            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = name;
            }
        }
    }
}
