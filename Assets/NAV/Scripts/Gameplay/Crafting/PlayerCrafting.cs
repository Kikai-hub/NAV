using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Inventory;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// Holds the recipes a player can currently craft and applies them against their
    /// PlayerInventory. "Known recipes" is just a fixed serialized list for now - there is no
    /// discovery/unlock system yet (see RecipeDefinition's deferred "Recipe unlock structure"
    /// roadmap item). Mirrors PlayerInventory: a thin MonoBehaviour wrapper so UI can observe
    /// it without owning gameplay state itself (ARCHITECTURE_v0.1.md).
    /// </summary>
    public class PlayerCrafting : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private List<RecipeDefinition> _knownRecipes = new();

        public IReadOnlyList<RecipeDefinition> KnownRecipes => _knownRecipes;

        private void Awake()
        {
            if (_playerInventory == null)
            {
                Debug.LogError($"{nameof(PlayerCrafting)} on '{name}' is missing its PlayerInventory reference.", this);
                enabled = false;
            }
        }

        public bool TryCraft(RecipeDefinition recipe)
        {
            if (!enabled || recipe == null)
            {
                return false;
            }

            bool crafted = recipe.TryCraft(_playerInventory.Inventory);
            if (crafted)
            {
                Debug.Log($"{name} crafted {recipe.OutputAmount}x {recipe.OutputItem.DisplayName} ({recipe.DisplayName}).", this);
            }

            return crafted;
        }
    }
}
