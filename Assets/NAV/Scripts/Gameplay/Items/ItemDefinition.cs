using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Survival;

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

        [Header("Food")]
        [Tooltip("Optional. Assign to make this item edible (right-click in the Inventory panel) - see PlayerFood.")]
        [SerializeField] private FoodDefinition _food;

        [Header("Equipment (main hand)")]
        [Tooltip("Gathering capability while held. None = not a tool.")]
        [SerializeField] private ToolType _toolType;
        [SerializeField, Min(1)] private int _toolTier = 1;
        [Tooltip("Combat stats while held. Empty = attacks with fists while this is held (e.g. a hammer).")]
        [SerializeField] private WeaponDefinition _weapon;
        [Tooltip("Optional. Model shown in the character's hand while equipped (HeldItemView). Its origin is the grip and +Y runs along the handle towards the blade/head - the Kevin Iglesias rig's B-handProp.R convention. No colliders. Empty = nothing is shown in hand.")]
        [SerializeField] private GameObject _heldPrefab;
        [Tooltip("Show Held Prefab in the left hand (B-handProp.L) instead of the right - shields.")]
        [SerializeField] private bool _heldInLeftHand;

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

        /// <summary>What eating this item does, or null if it isn't edible.</summary>
        public FoodDefinition Food => _food;
        public bool IsEdible => _food != null;

        public ToolType ToolType => _toolType;
        public int ToolTier => _toolTier;
        public WeaponDefinition Weapon => _weapon;

        /// <summary>Model for the character's hand while equipped, or null.</summary>
        public GameObject HeldPrefab => _heldPrefab;
        public bool HeldInLeftHand => _heldInLeftHand;

        /// <summary>Can be held in the main hand (PlayerEquipment) - any tool or weapon.</summary>
        public bool IsEquippable => _toolType != ToolType.None || _weapon != null;

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
