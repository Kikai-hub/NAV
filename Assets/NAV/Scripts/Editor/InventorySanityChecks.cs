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

            Object.DestroyImmediate(itemA);
            Object.DestroyImmediate(itemB);

            if (passed)
            {
                Debug.Log("[InventorySanityChecks] All checks passed.");
            }
        }

        private static ItemDefinition CreateTestDefinition(string id, int maxStackSize)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_maxStackSize").intValue = maxStackSize;
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
