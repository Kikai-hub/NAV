using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Equipment
{
    /// <summary>
    /// What the player is holding and wearing: the main hand (a weapon or a tool) and one armour
    /// piece per ArmorSlot (head, chest, legs). Valheim-style: an equipped item stays in the inventory
    /// (it is only marked as held), so equipping never moves items around, and the moment the
    /// last copy leaves the inventory (dropped, moved into a gravestone on death) it is
    /// unequipped automatically via Inventory.Changed.
    ///
    /// Equip/unequip comes from the hotbar keys (1-6 = the first six inventory slots,
    /// PlayerInputHandler.HotbarSlotPerformed) or RMB in the Inventory panel
    /// (InventoryUIController). PlayerCombat reads MainHandWeapon; ResourceNode reads HasTool.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour, IEquipmentView
    {
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;

        [Tooltip("How many leading inventory slots the hotbar keys map to (matches the HUD hotbar).")]
        [SerializeField, Min(1)] private int _hotbarSlotCount = 6;

        /// <summary>The held item, or null for empty hands.</summary>
        public ItemDefinition MainHand { get; private set; }

        /// <summary>Combat stats of the held item, or null (fists - PlayerCombat's fallback).</summary>
        public WeaponDefinition MainHandWeapon => MainHand != null ? MainHand.Weapon : null;

        private readonly Dictionary<ArmorSlot, ItemDefinition> _worn = new();
        private readonly List<ArmorSlot> _slotScratch = new();

        /// <summary>Every worn armour piece, by slot.</summary>
        public IReadOnlyDictionary<ArmorSlot, ItemDefinition> Worn => _worn;

        /// <summary>Sum of the worn pieces' armour values (ArmorMath).</summary>
        public float TotalArmor
        {
            get
            {
                float total = 0f;
                foreach (ItemDefinition item in _worn.Values)
                {
                    total += item.Armor.Armor;
                }

                return total;
            }
        }

        /// <summary>Sum of the worn pieces' warmth (PlayerCold).</summary>
        public float TotalWarmth
        {
            get
            {
                float total = 0f;
                foreach (ItemDefinition item in _worn.Values)
                {
                    total += item.Armor.Warmth;
                }

                return total;
            }
        }

        /// <summary>Raised when the held item or any worn armour changes.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (_playerInventory == null || _inputHandler == null)
            {
                Debug.LogError($"{nameof(PlayerEquipment)} on '{name}' is missing a required reference (PlayerInventory/InputHandler).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            // Inventory is created in PlayerInventory.Awake - subscribe once it exists.
            if (enabled)
            {
                _playerInventory.Inventory.Changed += HandleInventoryChanged;
            }
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.HotbarSlotPerformed += HandleHotbarSlot;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.HotbarSlotPerformed -= HandleHotbarSlot;
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= HandleInventoryChanged;
            }
        }

        /// <summary>True if the held item is a tool of this type at or above minTier.</summary>
        public bool HasTool(ToolType type, int minTier = 1)
        {
            return type == ToolType.None
                || (MainHand != null && MainHand.ToolType == type && MainHand.ToolTier >= minTier);
        }

        /// <summary>Equips the item in an inventory slot, or unequips it if it's already held.
        /// Non-equippable items and empty slots are ignored. Returns true if anything changed.</summary>
        public bool ToggleFromSlot(int slotIndex)
        {
            if (!enabled)
            {
                return false;
            }

            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex].IsEmpty)
            {
                return false;
            }

            ItemDefinition item = slots[slotIndex].Definition;
            if (item.IsArmor)
            {
                ToggleArmor(item);
                return true;
            }

            if (!item.IsEquippable)
            {
                return false;
            }

            if (MainHand == item)
            {
                Unequip();
            }
            else
            {
                SetMainHand(item);
            }

            return true;
        }

        public void Unequip()
        {
            SetMainHand(null);
        }

        /// <summary>Re-equips a saved item on load - only if it's equippable and actually in the
        /// (already restored) inventory.</summary>
        public void Restore(ItemDefinition item)
        {
            if (item != null && item.IsEquippable && _playerInventory.Inventory.GetTotalQuantity(item) > 0)
            {
                SetMainHand(item);
            }
        }

        /// <summary>Puts saved armour back on at load - pieces that aren't armour or aren't in
        /// the (already restored) inventory are skipped.</summary>
        public void RestoreArmor(IEnumerable<ItemDefinition> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (ItemDefinition item in items)
            {
                if (item != null && item.IsArmor && _playerInventory.Inventory.GetTotalQuantity(item) > 0)
                {
                    _worn[item.Armor.Slot] = item;
                }
            }

            Changed?.Invoke();
        }

        /// <summary>The armour worn in a slot, or null.</summary>
        public ItemDefinition GetWorn(ArmorSlot slot) => _worn.TryGetValue(slot, out ItemDefinition item) ? item : null;

        /// <summary>Whether this inventory slot shows as equipped (UI highlight): the first slot
        /// holding the held item or a worn armour piece.</summary>
        public bool IsSlotEquipped(int slotIndex)
        {
            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex].IsEmpty)
            {
                return false;
            }

            ItemDefinition item = slots[slotIndex].Definition;
            bool equipped = item == MainHand || (item.IsArmor && GetWorn(item.Armor.Slot) == item);
            return equipped && FindFirstSlot(item) == slotIndex;
        }

        /// <summary>Index of the first inventory slot holding the equipped item (for UI
        /// highlighting), or -1.</summary>
        public int FindEquippedSlotIndex()
        {
            return MainHand != null ? FindFirstSlot(MainHand) : -1;
        }

        private int FindFirstSlot(ItemDefinition item)
        {
            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Definition == item)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ToggleArmor(ItemDefinition item)
        {
            ArmorSlot slot = item.Armor.Slot;
            if (GetWorn(slot) == item)
            {
                _worn.Remove(slot);
                Debug.Log($"{name} took off {item.DisplayName}.", this);
            }
            else
            {
                _worn[slot] = item;
                Debug.Log($"{name} put on {item.DisplayName} (armour {TotalArmor:F0}).", this);
            }

            Changed?.Invoke();
        }

        private void HandleHotbarSlot(int index)
        {
            if (index >= 0 && index < _hotbarSlotCount)
            {
                ToggleFromSlot(index);
            }
        }

        private void HandleInventoryChanged()
        {
            // Armour that left the inventory (dropped, into a gravestone) comes off.
            _slotScratch.Clear();
            foreach (KeyValuePair<ArmorSlot, ItemDefinition> pair in _worn)
            {
                if (_playerInventory.Inventory.GetTotalQuantity(pair.Value) <= 0)
                {
                    _slotScratch.Add(pair.Key);
                }
            }

            foreach (ArmorSlot slot in _slotScratch)
            {
                _worn.Remove(slot);
            }

            if (MainHand != null && _playerInventory.Inventory.GetTotalQuantity(MainHand) <= 0)
            {
                Unequip();
            }
            else
            {
                // Slot moves change which slot shows as equipped even if the item didn't change.
                Changed?.Invoke();
            }
        }

        private void SetMainHand(ItemDefinition item)
        {
            if (MainHand == item)
            {
                return;
            }

            MainHand = item;
            Debug.Log(item != null ? $"{name} equipped {item.DisplayName}." : $"{name} put away their held item.", this);
            Changed?.Invoke();
        }
    }
}
