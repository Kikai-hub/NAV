using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;
using NAV.Gameplay.Inventory;

namespace NAV.Editor
{
    /// <summary>
    /// Runs a handful of assertions against RecipeDefinition's CanCraft/TryCraft logic every
    /// time scripts recompile, and logs the result to the Console. Same rationale as
    /// ItemStackSanityChecks/InventorySanityChecks: no assembly definitions yet, so no Test
    /// Runner target.
    /// </summary>
    [InitializeOnLoad]
    internal static class CraftingSanityChecks
    {
        static CraftingSanityChecks()
        {
            RunChecks();
        }

        private static void RunChecks()
        {
            bool passed = true;

            ItemDefinition wood = CreateTestItem("test_wood", maxStackSize: 50);
            ItemDefinition stone = CreateTestItem("test_stone", maxStackSize: 50);
            ItemDefinition axe = CreateTestItem("test_axe", maxStackSize: 1);

            RecipeDefinition recipe = CreateTestRecipe(
                ingredients: new (ItemDefinition item, int amount)[] { (wood, 3), (stone, 2) },
                outputItem: axe,
                outputAmount: 1);

            var inventory = new Inventory(5);

            passed &= Check(!recipe.CanCraft(inventory), "recipe should not be craftable with an empty inventory");
            passed &= Check(!recipe.TryCraft(inventory), "TryCraft should fail when ingredients are missing");
            passed &= Check(inventory.GetTotalQuantity(axe) == 0, "a failed TryCraft should not have produced any output");

            inventory.AddItem(wood, 3);
            passed &= Check(!recipe.CanCraft(inventory), "recipe should not be craftable with only one of two required ingredients");

            inventory.AddItem(stone, 1);
            passed &= Check(!recipe.CanCraft(inventory), "recipe should not be craftable with insufficient stone (1 of 2 required)");

            inventory.AddItem(stone, 1);
            passed &= Check(recipe.CanCraft(inventory), "recipe should be craftable once every ingredient meets its required amount");

            bool crafted = recipe.TryCraft(inventory);
            passed &= Check(crafted, "TryCraft should succeed once ingredients are available");
            passed &= Check(inventory.GetTotalQuantity(wood) == 0, "crafting should consume all 3 required wood");
            passed &= Check(inventory.GetTotalQuantity(stone) == 0, "crafting should consume all 2 required stone");
            passed &= Check(inventory.GetTotalQuantity(axe) == 1, "crafting should add 1 output item");

            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(axe);
            Object.DestroyImmediate(recipe);

            if (passed)
            {
                Debug.Log("[CraftingSanityChecks] All checks passed.");
            }
        }

        private static ItemDefinition CreateTestItem(string id, int maxStackSize)
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_maxStackSize").intValue = maxStackSize;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return definition;
        }

        private static RecipeDefinition CreateTestRecipe((ItemDefinition item, int amount)[] ingredients, ItemDefinition outputItem, int outputAmount)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.hideFlags = HideFlags.HideAndDontSave;

            var serialized = new SerializedObject(recipe);
            SerializedProperty ingredientsProperty = serialized.FindProperty("_ingredients");
            ingredientsProperty.ClearArray();

            for (int i = 0; i < ingredients.Length; i++)
            {
                ingredientsProperty.InsertArrayElementAtIndex(i);
                SerializedProperty element = ingredientsProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_item").objectReferenceValue = ingredients[i].item;
                element.FindPropertyRelative("_amount").intValue = ingredients[i].amount;
            }

            serialized.FindProperty("_outputItem").objectReferenceValue = outputItem;
            serialized.FindProperty("_outputAmount").intValue = outputAmount;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return recipe;
        }

        private static bool Check(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[CraftingSanityChecks] FAILED: {message}");
            }

            return condition;
        }
    }
}
