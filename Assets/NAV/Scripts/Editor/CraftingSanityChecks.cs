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

            passed &= Check(!recipe.CanCraft(inventory, availableWorkbenchTier: 0), "recipe should not be craftable with an empty inventory");
            passed &= Check(!recipe.TryCraft(inventory, availableWorkbenchTier: 0), "TryCraft should fail when ingredients are missing");
            passed &= Check(inventory.GetTotalQuantity(axe) == 0, "a failed TryCraft should not have produced any output");

            inventory.AddItem(wood, 3);
            passed &= Check(!recipe.CanCraft(inventory, availableWorkbenchTier: 0), "recipe should not be craftable with only one of two required ingredients");

            inventory.AddItem(stone, 1);
            passed &= Check(!recipe.CanCraft(inventory, availableWorkbenchTier: 0), "recipe should not be craftable with insufficient stone (1 of 2 required)");

            inventory.AddItem(stone, 1);
            passed &= Check(recipe.CanCraft(inventory, availableWorkbenchTier: 0), "recipe should be craftable once every ingredient meets its required amount (RequiredWorkbenchTier defaults to 0 - craftable anywhere)");

            bool crafted = recipe.TryCraft(inventory, availableWorkbenchTier: 0);
            passed &= Check(crafted, "TryCraft should succeed once ingredients are available");
            passed &= Check(inventory.GetTotalQuantity(wood) == 0, "crafting should consume all 3 required wood");
            passed &= Check(inventory.GetTotalQuantity(stone) == 0, "crafting should consume all 2 required stone");
            passed &= Check(inventory.GetTotalQuantity(axe) == 1, "crafting should add 1 output item");

            // --- Workbench gating: RequiredWorkbenchTier blocks CanCraft regardless of ingredients ---
            ItemDefinition ironOre = CreateTestItem("test_iron_ore", maxStackSize: 50);
            ItemDefinition ironIngot = CreateTestItem("test_iron_ingot", maxStackSize: 50);

            RecipeDefinition gatedRecipe = CreateTestRecipe(
                ingredients: new (ItemDefinition item, int amount)[] { (ironOre, 1) },
                outputItem: ironIngot,
                outputAmount: 1,
                requiredWorkbenchTier: 2);

            var gatedInventory = new Inventory(5);
            gatedInventory.AddItem(ironOre, 1); // ingredients fully available - only the workbench tier is missing

            passed &= Check(!gatedRecipe.CanCraft(gatedInventory, availableWorkbenchTier: 0), "a tier-2 recipe should not be craftable with no workbench nearby, even with every ingredient in hand");
            passed &= Check(!gatedRecipe.CanCraft(gatedInventory, availableWorkbenchTier: 1), "a tier-2 recipe should not be craftable near a lower-tier (1) workbench");
            passed &= Check(gatedRecipe.CanCraft(gatedInventory, availableWorkbenchTier: 2), "a tier-2 recipe should be craftable at exactly its required tier");
            passed &= Check(gatedRecipe.CanCraft(gatedInventory, availableWorkbenchTier: 3), "a tier-2 recipe should also be craftable near a higher-tier (3) workbench");

            bool gatedCrafted = gatedRecipe.TryCraft(gatedInventory, availableWorkbenchTier: 2);
            passed &= Check(gatedCrafted, "TryCraft should succeed once both ingredients and workbench tier are satisfied");
            passed &= Check(gatedInventory.GetTotalQuantity(ironIngot) == 1, "crafting the gated recipe should add its output");

            Object.DestroyImmediate(wood);
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(axe);
            Object.DestroyImmediate(recipe);
            Object.DestroyImmediate(ironOre);
            Object.DestroyImmediate(ironIngot);
            Object.DestroyImmediate(gatedRecipe);

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

        private static RecipeDefinition CreateTestRecipe((ItemDefinition item, int amount)[] ingredients, ItemDefinition outputItem, int outputAmount, int requiredWorkbenchTier = 0)
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
            serialized.FindProperty("_requiredWorkbenchTier").intValue = requiredWorkbenchTier;
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
