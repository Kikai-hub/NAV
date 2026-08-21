using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Runs a handful of assertions against ItemStack's stacking math every time scripts
    /// recompile, and logs the result to the Console. This is a stand-in for automated tests
    /// until the project has an assembly definition layout that supports the Test Runner
    /// (introducing one is an architecture change, not a test-only addition - see
    /// PROJECT_STATE.md before doing that).
    /// </summary>
    [InitializeOnLoad]
    internal static class ItemStackSanityChecks
    {
        static ItemStackSanityChecks()
        {
            RunChecks();
        }

        private static void RunChecks()
        {
            bool passed = true;

            var definitionA = CreateTestDefinition("test_item_a", maxStackSize: 10);
            var definitionB = CreateTestDefinition("test_item_b", maxStackSize: 10);

            var stack = new ItemStack();
            passed &= Check(stack.IsEmpty, "a new ItemStack should start empty");

            int leftover = stack.Add(definitionA, 6);
            passed &= Check(leftover == 0, "adding 6 into an empty max-10 stack should not overflow");
            passed &= Check(stack.Quantity == 6, "stack quantity should be 6 after adding 6");

            leftover = stack.Add(definitionA, 6);
            passed &= Check(leftover == 2, "adding 6 more (total 12) into a max-10 stack should overflow by 2");
            passed &= Check(stack.Quantity == 10, "stack should cap at MaxStackSize (10)");

            passed &= Check(!stack.CanAccept(definitionB), "a non-empty stack should reject a different ItemDefinition");
            passed &= Check(stack.CanAccept(definitionA), "a non-empty stack should accept more of its own ItemDefinition");

            int removed = stack.Remove(4);
            passed &= Check(removed == 4, "removing 4 from a stack of 10 should remove 4");
            passed &= Check(stack.Quantity == 6, "stack quantity should be 6 after removing 4");

            removed = stack.Remove(100);
            passed &= Check(removed == 6, "removing more than available should remove only what's there");
            passed &= Check(stack.IsEmpty, "stack should be empty (and cleared) after removing everything");

            Object.DestroyImmediate(definitionA);
            Object.DestroyImmediate(definitionB);

            if (passed)
            {
                Debug.Log("[ItemStackSanityChecks] All checks passed.");
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
                Debug.LogError($"[ItemStackSanityChecks] FAILED: {message}");
            }

            return condition;
        }
    }
}
