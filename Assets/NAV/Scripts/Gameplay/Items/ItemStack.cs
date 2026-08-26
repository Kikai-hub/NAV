using System;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Runtime instance of a stack of items: which ItemDefinition, and how many.
    /// Not a ScriptableObject - this is per-inventory-slot state, not shared content data.
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        [SerializeField] private ItemDefinition _definition;
        [SerializeField] private int _quantity;

        public ItemDefinition Definition => _definition;
        public int Quantity => _quantity;
        public bool IsEmpty => _definition == null || _quantity <= 0;

        public ItemStack()
        {
        }

        public ItemStack(ItemDefinition definition, int quantity)
        {
            _definition = definition;
            _quantity = Mathf.Max(0, quantity);
        }

        public bool CanAccept(ItemDefinition definition)
        {
            return definition != null && (IsEmpty || _definition == definition);
        }

        /// <summary>
        /// Adds as much of amount as fits, respecting MaxStackSize. Returns the leftover
        /// that did not fit (0 if it all fit).
        /// </summary>
        public int Add(ItemDefinition definition, int amount)
        {
            if (amount <= 0 || !CanAccept(definition))
            {
                return amount;
            }

            if (IsEmpty)
            {
                _definition = definition;
                _quantity = 0;
            }

            int spaceLeft = _definition.MaxStackSize - _quantity;
            int amountToAdd = Mathf.Min(spaceLeft, amount);
            _quantity += amountToAdd;

            return amount - amountToAdd;
        }

        /// <summary>
        /// Removes up to amount. Returns how much was actually removed, and clears the
        /// stack's definition once its quantity reaches zero.
        /// </summary>
        public int Remove(int amount)
        {
            if (amount <= 0 || IsEmpty)
            {
                return 0;
            }

            int amountToRemove = Mathf.Min(_quantity, amount);
            _quantity -= amountToRemove;

            if (_quantity <= 0)
            {
                Clear();
            }

            return amountToRemove;
        }

        public void Clear()
        {
            _definition = null;
            _quantity = 0;
        }

        /// <summary>Exchanges contents with another stack (used for inventory slot drag/drop).</summary>
        public void Swap(ItemStack other)
        {
            (ItemDefinition definition, int quantity) = (_definition, _quantity);
            _definition = other._definition;
            _quantity = other._quantity;
            other._definition = definition;
            other._quantity = quantity;
        }
    }
}
