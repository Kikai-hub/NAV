using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Crafting;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// Static data for a placeable building piece: display info, resource cost, and the
    /// prefab spawned when it's built. Cost reuses RecipeIngredient (Crafting) rather than a
    /// new item+amount type - it's the same "pay these ingredients" concept RecipeDefinition
    /// already has, just applied to placement instead of crafting.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingPieceDefinition", menuName = "NAV/Building/Building Piece Definition")]
    public class BuildingPieceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        [Header("Cost")]
        [SerializeField] private List<RecipeIngredient> _cost = new();

        [Header("World")]
        [SerializeField] private GameObject _prefab;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public IReadOnlyList<RecipeIngredient> Cost => _cost;

        /// <summary>Prefab instantiated both for the placement ghost and the real built piece
        /// (must carry a BuildingPiece component - see PlayerBuilding).</summary>
        public GameObject Prefab => _prefab;

        // Fully qualified, not "using NAV.Gameplay.Inventory" + bare "Inventory": same
        // CS0118 namespace-vs-class collision RecipeDefinition already documents - this file
        // lives in the sibling namespace NAV.Gameplay.Building.
        public bool CanAfford(NAV.Gameplay.Inventory.Inventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in _cost)
            {
                if (ingredient.Item == null || inventory.GetTotalQuantity(ingredient.Item) < ingredient.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Removes this piece's cost from the inventory. Caller must have already
        /// confirmed CanAfford.</summary>
        public void PayCost(NAV.Gameplay.Inventory.Inventory inventory)
        {
            foreach (RecipeIngredient ingredient in _cost)
            {
                inventory.RemoveItem(ingredient.Item, ingredient.Amount);
            }
        }
    }
}
