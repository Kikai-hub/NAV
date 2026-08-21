using System;
using System.Collections.Generic;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// A fixed-size collection of ItemStack slots. Plain runtime state (not a
    /// ScriptableObject, not a MonoBehaviour) - see ARCHITECTURE_v0.1.md's
    /// "Data vs Runtime State" split. PlayerInventory owns/exposes an instance of this.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        private readonly ItemStack[] _slots;

        public int Capacity => _slots.Length;
        public IReadOnlyList<ItemStack> Slots => _slots;

        public event Action Changed;

        public Inventory(int capacity)
        {
            capacity = Math.Max(1, capacity);
            _slots = new ItemStack[capacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new ItemStack();
            }
        }

        /// <summary>
        /// Adds amount of definition, filling existing matching stacks before empty slots.
        /// Returns the leftover that did not fit (0 if it all fit).
        /// </summary>
        public int AddItem(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0)
            {
                return amount;
            }

            int remaining = amount;

            foreach (ItemStack slot in _slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!slot.IsEmpty && slot.Definition == definition)
                {
                    remaining = slot.Add(definition, remaining);
                }
            }

            foreach (ItemStack slot in _slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (slot.IsEmpty)
                {
                    remaining = slot.Add(definition, remaining);
                }
            }

            if (remaining != amount)
            {
                Changed?.Invoke();
            }

            return remaining;
        }

        /// <summary>
        /// Removes up to amount of definition across slots. Returns how much was actually
        /// removed.
        /// </summary>
        public int RemoveItem(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0)
            {
                return 0;
            }

            int removed = 0;

            foreach (ItemStack slot in _slots)
            {
                if (removed >= amount)
                {
                    break;
                }

                if (!slot.IsEmpty && slot.Definition == definition)
                {
                    removed += slot.Remove(amount - removed);
                }
            }

            if (removed > 0)
            {
                Changed?.Invoke();
            }

            return removed;
        }

        public int GetTotalQuantity(ItemDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            int total = 0;
            foreach (ItemStack slot in _slots)
            {
                if (!slot.IsEmpty && slot.Definition == definition)
                {
                    total += slot.Quantity;
                }
            }

            return total;
        }
    }
}
