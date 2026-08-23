using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// Static data for a craftable recipe: ingredient cost + output item/amount, plus the
    /// logic to check/apply it against an Inventory. No workbench requirement, tier gating,
    /// or unlock condition yet - those are separate "Workbench levels" / "Recipe unlock
    /// structure" items in DEVELOPMENT_ROADMAP_v0.1.md Phase 4, deferred the same way
    /// ResourceNode defers its tool requirement.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDefinition", menuName = "NAV/Crafting/Recipe Definition")]
    public class RecipeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;

        [Header("Ingredients")]
        [SerializeField] private List<RecipeIngredient> _ingredients = new();

        [Header("Output")]
        [SerializeField] private ItemDefinition _outputItem;
        [SerializeField] private int _outputAmount = 1;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) && _outputItem != null
            ? _outputItem.DisplayName
            : _displayName;

        public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients;
        public ItemDefinition OutputItem => _outputItem;
        public int OutputAmount => _outputAmount;

        // Fully qualified, not "using NAV.Gameplay.Inventory" + bare "Inventory": this file
        // lives in the sibling namespace NAV.Gameplay.Crafting, and C# resolves an unqualified
        // "Inventory" there to the sibling *namespace* NAV.Gameplay.Inventory before it
        // considers the imported class of the same name (CS0118). ResourceNode/ItemPickup
        // never hit this because they only ever access ".Inventory" as a member, never declare
        // a parameter of bare type Inventory.
        public bool CanCraft(NAV.Gameplay.Inventory.Inventory inventory)
        {
            if (inventory == null || _outputItem == null)
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in _ingredients)
            {
                if (ingredient.Item == null || inventory.GetTotalQuantity(ingredient.Item) < ingredient.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Consumes ingredients and adds the output if (and only if) CanCraft is true.
        /// Returns whether crafting happened.
        /// </summary>
        public bool TryCraft(NAV.Gameplay.Inventory.Inventory inventory)
        {
            if (!CanCraft(inventory))
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in _ingredients)
            {
                inventory.RemoveItem(ingredient.Item, ingredient.Amount);
            }

            int leftover = inventory.AddItem(_outputItem, _outputAmount);
            if (leftover > 0)
            {
                Debug.LogWarning($"Crafted {_outputItem.DisplayName}, but only {_outputAmount - leftover}/{_outputAmount} fit in the inventory ({leftover} lost) - inventory was nearly full.");
            }

            return true;
        }

        private void OnValidate()
        {
            _outputAmount = Mathf.Max(1, _outputAmount);
        }
    }
}
