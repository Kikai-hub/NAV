using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;
using NAV.Gameplay.Inventory;

namespace NAV.Editor
{
    /// <summary>
    /// Runs a handful of assertions against Inventory's slot-filling math every time
    /// scripts recompile, and logs the result to the Console. Same rationale as
    /// ItemStackSanityChecks: no assembly definitions yet, so no Test Runner target.
    /// </summary>
    [InitializeOnLoad]
    internal static class InventorySanityChecks
    {
        static InventorySanityChecks()
        {
            RunChecks();
        }

        private static void RunChecks()
        {
            bool passed = true;

            var itemA = CreateTestDefinition("test_item_a", maxStackSize: 5);
            var itemB = CreateTestDefinition("test_item_b", maxStackSize: 5);

            var inventory = new Inventory(2);
            passed &= Check(inventory.Capacity == 2, "inventory should report the requested capacity");

            int leftover = inventory.AddItem(itemA, 7);
            passed &= Check(leftover == 0, "adding 7 of a max-5 item into a 2-slot inventory should fully fit (5 + 2)");
            passed &= Check(inventory.GetTotalQuantity(itemA) == 7, "total quantity of itemA should be 7");

            leftover = inventory.AddItem(itemA, 10);
            passed &= Check(leftover == 7, "only 3 space remains (2*5 - 7); adding 10 more should overflow by 7");
            passed &= Check(inventory.GetTotalQuantity(itemA) == 10, "inventory should be full at 10 (2 slots * max 5)");

            leftover = inventory.AddItem(itemB, 1);
            passed &= Check(leftover == 1, "inventory is full of itemA; a different item should not fit anywhere");

            // Slots are [5 itemA, 5 itemA] at this point. Removing exactly 5 empties the
            // first slot entirely (removal walks slots in order) - removing less (e.g. 4)
            // would leave that slot still occupied by a partial itemA stack, and itemB would
            // have nowhere to go, since AddItem only places a new item into a fully empty slot.
            int removed = inventory.RemoveItem(itemA, 5);
            passed &= Check(removed == 5, "removing 5 of itemA should remove 5 (empties the first slot)");
            passed &= Check(inventory.GetTotalQuantity(itemA) == 5, "itemA total should be 5 after removing 5");

            leftover = inventory.AddItem(itemB, 3);
            passed &= Check(leftover == 0, "after emptying a slot, itemB should now fit");
            passed &= Check(inventory.GetTotalQuantity(itemB) == 3, "itemB total should be 3");
            passed &= Check(inventory.GetTotalQuantity(itemA) == 5, "itemA total should be unaffected by adding itemB");

            removed = inventory.RemoveItem(itemA, 100);
            passed &= Check(removed == 5, "removing more than available should remove only what's there");
            passed &= Check(inventory.GetTotalQuantity(itemA) == 0, "itemA total should be 0 after removing everything");

            // --- MoveSlot: move into an empty slot, swap two different items, self is a no-op ---
            var moveInventory = new Inventory(2);
            moveInventory.AddItem(itemA, 2); // slot 0: 2x itemA

            bool moved = moveInventory.MoveSlot(0, 1);
            passed &= Check(moved, "MoveSlot into an empty slot should report a change");
            passed &= Check(moveInventory.Slots[0].IsEmpty, "source slot should be empty after moving into an empty target");
            passed &= Check(moveInventory.Slots[1].Definition == itemA && moveInventory.Slots[1].Quantity == 2, "target slot should receive the moved stack");

            moveInventory.AddItem(itemB, 3); // slot 0 is empty again -> slot 0: 3x itemB
            moved = moveInventory.MoveSlot(0, 1); // itemB (slot0) onto itemA (slot1) - different items -> swap
            passed &= Check(moved, "MoveSlot between two different items should swap and report a change");
            passed &= Check(moveInventory.Slots[0].Definition == itemA && moveInventory.Slots[0].Quantity == 2, "swap should leave itemA in the slot itemB moved from");
            passed &= Check(moveInventory.Slots[1].Definition == itemB && moveInventory.Slots[1].Quantity == 3, "swap should leave itemB in the slot itemA moved from");

            passed &= Check(!moveInventory.MoveSlot(1, 1), "MoveSlot onto itself should be a no-op");

            // --- RemoveFromSlot: targets one specific slot, unlike RemoveItem's by-definition search ---
            var removeInventory = new Inventory(3);
            removeInventory.AddItem(itemA, 5); // slot 0: 5x itemA (max stack, full)
            removeInventory.AddItem(itemA, 2); // overflow -> slot 1: 2x itemA

            ItemDefinition removedDefinition = removeInventory.RemoveFromSlot(0, 2, out int removedFromSlot);
            passed &= Check(removedDefinition == itemA, "RemoveFromSlot should return the slot's item definition");
            passed &= Check(removedFromSlot == 2, "RemoveFromSlot should report how much it actually removed");
            passed &= Check(removeInventory.Slots[0].Quantity == 3, "removing 2 of 5 from slot 0 should leave 3 there");
            passed &= Check(removeInventory.Slots[1].Quantity == 2, "slot 1 should be untouched by removing from slot 0");

            // Merging slot 1 (2x) onto slot 0 (now 3x, 2 space left) should fit exactly.
            moved = removeInventory.MoveSlot(1, 0);
            passed &= Check(moved, "merging a same-item stack with just enough room should report a change");
            passed &= Check(removeInventory.Slots[0].Quantity == 5, "merge should top the target back up to its max stack size");
            passed &= Check(removeInventory.Slots[1].IsEmpty, "source should be fully drained by an exact-fit merge");

            removedDefinition = removeInventory.RemoveFromSlot(2, 5, out removedFromSlot);
            passed &= Check(removedDefinition == null && removedFromSlot == 0, "RemoveFromSlot on an already-empty slot should be a no-op");

            // --- Weight-limited AddItem: amount is clamped to whatever fits under MaxWeight ---
            var heavyItem = CreateTestDefinition("test_item_heavy", maxStackSize: 100, weight: 10f);
            var lightItem = CreateTestDefinition("test_item_light", maxStackSize: 100, weight: 5f);
            var weightedInventory = new Inventory(5, maxWeight: 25f);

            passed &= Check(weightedInventory.MaxWeight == 25f, "inventory should report the requested max weight");

            int weightLeftover = weightedInventory.AddItem(heavyItem, 3);
            passed &= Check(weightLeftover == 1, "25kg budget / 10kg each fits 2; the 3rd of 3 requested should be blocked by weight");
            passed &= Check(weightedInventory.GetTotalQuantity(heavyItem) == 2, "only 2 heavy (10kg) items should have been added");
            passed &= Check(weightedInventory.TotalWeight == 20f, "total weight should be 20kg (2 * 10kg)");
            passed &= Check(!weightedInventory.IsOverloaded, "20kg of a 25kg max should not count as overloaded yet");

            weightLeftover = weightedInventory.AddItem(heavyItem, 1);
            passed &= Check(weightLeftover == 1, "remaining 5kg budget is not enough for one more 10kg item; it should be fully blocked");
            passed &= Check(weightedInventory.TotalWeight == 20f, "weight should be unchanged since nothing more fit");

            weightLeftover = weightedInventory.AddItem(lightItem, 1);
            passed &= Check(weightLeftover == 0, "a 5kg item should exactly fill the remaining 5kg budget");
            passed &= Check(weightedInventory.TotalWeight == 25f, "total weight should now be exactly at the 25kg cap");
            passed &= Check(weightedInventory.IsOverloaded, "an inventory at exactly its max weight should report overloaded");

            // --- ExtractAll: used by PlayerDeath to move the whole inventory into a Gravestone ---
            var extractInventory = new Inventory(3);
            extractInventory.AddItem(itemA, 4);
            extractInventory.SetSlot(2, itemB, 2); // slot 2 specifically, to check layout is preserved

            Inventory extracted = extractInventory.ExtractAll();
            passed &= Check(extracted.Capacity == extractInventory.Capacity, "ExtractAll's returned Inventory should have the same capacity as the source");
            passed &= Check(extractInventory.IsEmpty, "ExtractAll should empty every slot of the source inventory");
            passed &= Check(extracted.GetTotalQuantity(itemA) == 4 && extracted.GetTotalQuantity(itemB) == 2, "ExtractAll's returned Inventory should carry the exact quantities that were taken");
            passed &= Check(extracted.Slots[2].Definition == itemB && extracted.Slots[2].Quantity == 2, "ExtractAll should preserve the original slot layout, not compact it");

            passed &= Check(extractInventory.ExtractAll().IsEmpty, "ExtractAll on an already-empty inventory should return an empty Inventory");

            // --- MoveSlotTo: the cross-inventory counterpart of MoveSlot (Gravestone -> player drag) ---
            var sourceInventory = new Inventory(2);
            var targetInventory = new Inventory(2);
            sourceInventory.AddItem(itemA, 3); // slot 0: 3x itemA

            bool movedAcross = sourceInventory.MoveSlotTo(targetInventory, 0, 0);
            passed &= Check(movedAcross, "MoveSlotTo into an empty slot of a different inventory should report a change");
            passed &= Check(sourceInventory.Slots[0].IsEmpty, "source slot should be empty after moving out");
            passed &= Check(targetInventory.Slots[0].Definition == itemA && targetInventory.Slots[0].Quantity == 3, "target inventory should receive the moved stack");

            sourceInventory.AddItem(itemB, 1); // slot 0 (source) is empty again -> slot 0: 1x itemB
            movedAcross = sourceInventory.MoveSlotTo(targetInventory, 0, 0); // itemB onto itemA - different items -> swap across inventories
            passed &= Check(movedAcross, "MoveSlotTo between different items across inventories should swap and report a change");
            passed &= Check(sourceInventory.Slots[0].Definition == itemA && sourceInventory.Slots[0].Quantity == 3, "swap should leave itemA back in the source inventory");
            passed &= Check(targetInventory.Slots[0].Definition == itemB && targetInventory.Slots[0].Quantity == 1, "swap should leave itemB in the target inventory");

            Object.DestroyImmediate(itemA);
            Object.DestroyImmediate(itemB);
            Object.DestroyImmediate(heavyItem);
            Object.DestroyImmediate(lightItem);

            if (passed)
            {
                Debug.Log("[InventorySanityChecks] All checks passed.");
            }
        }

        private static ItemDefinition CreateTestDefinition(string id, int maxStackSize, float weight = 0f)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_maxStackSize").intValue = maxStackSize;
            serialized.FindProperty("_weight").floatValue = weight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private static bool Check(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[InventorySanityChecks] FAILED: {message}");
            }

            return condition;
        }
    }
}
