using System;
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
    /// it without owning gameplay state itself (ARCHITECTURE_v0.1.md). Also tracks which
    /// Workbench(es) are currently in range (registered/unregistered by Workbench's own
    /// trigger events - see Workbench.cs) so RecipeDefinition.RequiredWorkbenchTier can be
    /// checked without a per-frame distance scan.
    /// </summary>
    public class PlayerCrafting : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private List<RecipeDefinition> _knownRecipes = new();

        private readonly List<Workbench> _nearbyWorkbenches = new();

        public IReadOnlyList<RecipeDefinition> KnownRecipes => _knownRecipes;

        /// <summary>Highest usable tier among all currently-overlapping Workbenches (a bench
        /// that needs a roof and has none counts as 0), 0 if none.</summary>
        public int NearbyWorkbenchTier
        {
            get
            {
                int highest = 0;
                foreach (Workbench workbench in _nearbyWorkbenches)
                {
                    if (workbench != null && workbench.UsableTier > highest)
                    {
                        highest = workbench.UsableTier;
                    }
                }

                return highest;
            }
        }

        /// <summary>Highest tier among nearby workbenches that can't be used because they have
        /// no roof - for the crafting panel's "needs a roof" hint. 0 if none.</summary>
        public int NearbyUnshelteredWorkbenchTier
        {
            get
            {
                int highest = 0;
                foreach (Workbench workbench in _nearbyWorkbenches)
                {
                    if (workbench != null && !workbench.IsSheltered && workbench.Tier > highest)
                    {
                        highest = workbench.Tier;
                    }
                }

                return highest;
            }
        }

        public event Action NearbyWorkbenchChanged;

        /// <summary>A nearby workbench's state changed (e.g. it gained or lost its roof).</summary>
        public void NotifyWorkbenchChanged()
        {
            NearbyWorkbenchChanged?.Invoke();
        }

        private void Awake()
        {
            if (_playerInventory == null)
            {
                Debug.LogError($"{nameof(PlayerCrafting)} on '{name}' is missing its PlayerInventory reference.", this);
                enabled = false;
            }
        }

        public void RegisterNearbyWorkbench(Workbench workbench)
        {
            if (workbench != null && !_nearbyWorkbenches.Contains(workbench))
            {
                _nearbyWorkbenches.Add(workbench);
                NearbyWorkbenchChanged?.Invoke();
            }
        }

        public void UnregisterNearbyWorkbench(Workbench workbench)
        {
            if (_nearbyWorkbenches.Remove(workbench))
            {
                NearbyWorkbenchChanged?.Invoke();
            }
        }

        public bool TryCraft(RecipeDefinition recipe)
        {
            if (!enabled || recipe == null)
            {
                return false;
            }

            bool crafted = recipe.TryCraft(_playerInventory.Inventory, NearbyWorkbenchTier);
            if (crafted)
            {
                Debug.Log($"{name} crafted {recipe.OutputAmount}x {recipe.OutputItem.DisplayName} ({recipe.DisplayName}).", this);
            }

            return crafted;
        }
    }
}
