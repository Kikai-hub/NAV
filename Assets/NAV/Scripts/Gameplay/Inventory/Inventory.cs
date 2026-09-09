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

        public bool IsEmpty
        {
            get
            {
                foreach (ItemStack slot in _slots)
                {
                    if (!slot.IsEmpty)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

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
        /// Moves/merges/swaps the contents of one slot into another within this same
        /// inventory - used by InventoryUIController's drag-and-drop. Thin wrapper over
        /// MoveSlotTo(this, ...); see that method for the actual merge/swap rules.
        /// </summary>
        public bool MoveSlot(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
            {
                return false;
            }

            return MoveSlotTo(this, fromIndex, toIndex);
        }

        /// <summary>
        /// Moves/merges/swaps the stack at fromIndex in this inventory into toIndex of
        /// target (which may be this same inventory, or a different one - e.g. dragging an
        /// item out of a Gravestone's contents into the player's own Inventory). If the
        /// target slot is empty or holds the same item, the source stacks onto it (as much
        /// as fits); otherwise the two slots swap contents outright. Returns whether
        /// anything actually changed.
        /// </summary>
        public bool MoveSlotTo(Inventory target, int fromIndex, int toIndex)
        {
            if (target == null || fromIndex < 0 || fromIndex >= _slots.Length || toIndex < 0 || toIndex >= target._slots.Length)
            {
                return false;
            }

            if (target == this && fromIndex == toIndex)
            {
                return false;
            }

            ItemStack from = _slots[fromIndex];
            ItemStack to = target._slots[toIndex];

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
                if (target != this)
                {
                    target.Changed?.Invoke();
                }
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

        /// <summary>
        /// Directly sets one slot's contents, bypassing AddItem's stacking/weight-budget policy -
        /// used by SaveManager to restore an exact saved layout (already-owned items must never be
        /// rejected by a live weight recalculation on load).
        /// </summary>
        public void SetSlot(int index, ItemDefinition definition, int quantity)
        {
            if (index < 0 || index >= _slots.Length || definition == null || quantity <= 0)
            {
                return;
            }

            _slots[index] = new ItemStack(definition, quantity);
            Changed?.Invoke();
        }

        /// <summary>
        /// Creates a new, unlimited-weight Inventory with the same Capacity as this one,
        /// copies every slot's contents into the matching slot index there, and clears this
        /// inventory completely - used by PlayerDeath to hand the player's whole Inventory
        /// over to a Gravestone as one unit, same slot layout intact, so its UI grid lines up
        /// exactly with the player's own Inventory panel.
        /// </summary>
        public Inventory ExtractAll()
        {
            var extracted = new Inventory(Capacity);
            bool changed = false;

            for (int i = 0; i < _slots.Length; i++)
            {
                ItemStack slot = _slots[i];
                if (!slot.IsEmpty)
                {
                    extracted.SetSlot(i, slot.Definition, slot.Quantity);
                    slot.Clear();
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }

            return extracted;
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
