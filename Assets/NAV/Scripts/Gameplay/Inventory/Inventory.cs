using System;
using System.Collections.Generic;
using UnityEngine;
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

        /// <summary>Maximum total carried weight in kg. 0 (or less) means unlimited.</summary>
        public float MaxWeight { get; }

        public float TotalWeight
        {
            get
            {
                float total = 0f;
                foreach (ItemStack slot in _slots)
                {
                    if (!slot.IsEmpty)
                    {
                        total += slot.Definition.Weight * slot.Quantity;
                    }
                }

                return total;
            }
        }

        public bool IsOverloaded => MaxWeight > 0f && TotalWeight >= MaxWeight;

        public event Action Changed;

        public Inventory(int capacity, float maxWeight = 0f)
        {
            capacity = Math.Max(1, capacity);
            _slots = new ItemStack[capacity];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new ItemStack();
            }

            MaxWeight = Math.Max(0f, maxWeight);
        }

        /// <summary>
        /// Adds amount of definition, filling existing matching stacks before empty slots.
        /// Amount is first clamped to whatever still fits under MaxWeight (weightless items,
        /// or an unlimited inventory, are never blocked this way). Returns the leftover that
        /// did not fit (0 if it all fit) - weight-blocked and slot-blocked leftover are both
        /// folded into this single return value, same contract as before weight existed.
        /// </summary>
        public int AddItem(ItemDefinition definition, int amount)
        {
            if (definition == null || amount <= 0)
            {
                return amount;
            }

            int amountToTry = amount;
            if (MaxWeight > 0f && definition.Weight > 0f)
            {
                float weightBudget = Math.Max(0f, MaxWeight - TotalWeight);
                int maxByWeight = Mathf.FloorToInt(weightBudget / definition.Weight);
                amountToTry = Math.Min(amount, Math.Max(0, maxByWeight));
            }

            int blockedByWeight = amount - amountToTry;
            int remaining = amountToTry;

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

            int totalLeftover = remaining + blockedByWeight;

            if (totalLeftover != amount)
            {
                Changed?.Invoke();
            }

            return totalLeftover;
        }

        /// <summary>
        /// Moves/merges/swaps the contents of one slot into another - used by inventory UI
        /// drag-and-drop. If the target is empty or holds the same item, source stacks onto
        /// it (as much as fits); otherwise the two slots swap contents outright. Returns
        /// whether anything actually changed.
        /// </summary>
        public bool MoveSlot(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex || fromIndex < 0 || fromIndex >= _slots.Length || toIndex < 0 || toIndex >= _slots.Length)
            {
                return false;
            }

            ItemStack from = _slots[fromIndex];
            ItemStack to = _slots[toIndex];

            if (from.IsEmpty)
            {
                return false;
            }

            bool changed;
            if (to.IsEmpty || to.Definition == from.Definition)
            {
                int leftover = to.Add(from.Definition, from.Quantity);
                int moved = from.Quantity - leftover;
                changed = moved > 0;
                if (moved > 0)
                {
                    from.Remove(moved);
                }
            }
            else
            {
                from.Swap(to);
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return changed;
        }

        /// <summary>
        /// Removes up to amount from one specific slot (unlike RemoveItem, which searches
        /// every slot by definition) - used to drop a specific stack into the world. Returns
        /// the slot's definition (null if the slot was already empty/invalid) and, via
        /// removed, how much was actually taken.
        /// </summary>
        public ItemDefinition RemoveFromSlot(int index, int amount, out int removed)
        {
            removed = 0;

            if (index < 0 || index >= _slots.Length || amount <= 0)
            {
                return null;
            }

            ItemStack slot = _slots[index];
            if (slot.IsEmpty)
            {
                return null;
            }

            ItemDefinition definition = slot.Definition;
            removed = slot.Remove(amount);

            if (removed > 0)
            {
                Changed?.Invoke();
            }

            return definition;
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
