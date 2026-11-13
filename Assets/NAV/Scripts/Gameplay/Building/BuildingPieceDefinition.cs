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
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;

        [Header("Cost")]
        [SerializeField] private List<RecipeIngredient> _cost = new();

        [Header("World")]
        [SerializeField] private GameObject _prefab;

        [Header("Rules")]
        [Tooltip("Can only be placed inside a placed workbench's build radius (Valheim-style). Off for the workbench itself and the campfire, which can go anywhere.")]
        [SerializeField] private bool _requiresWorkbench = true;

        [Tooltip("Optional: the piece also needs this station kind with at least Required Station Level within its build radius (oak pieces need the workbench at level 3 - the joiner's bench). Empty = any workbench, any level.")]
        [SerializeField] private WorkbenchDefinition _requiredStation;
        [SerializeField, Min(0)] private int _requiredStationLevel;

        [Tooltip("The piece has a front/back that matters (roof slope, gable, carved door): when it snaps, its front (+Z) turns towards the player instead of always taking the snap point's rotation; R turns it the other way.")]
        [SerializeField] private bool _directional;

        [Tooltip("A roof slope or a gable: when it snaps, its high side (its highest snap point - the ridge, the gable's apex) turns towards the middle of the building it joins, whichever way the player holds it.")]
        [SerializeField] private bool _risesTowardCentre;

        [Tooltip("Placed on the water surface (its pivot at the waterline), only where the water is deep enough - a boat.")]
        [SerializeField] private bool _floatsOnWater;

        [Header("Discovery")]
        [Tooltip("Items that must also have been found before this piece shows in the build menu, on top of every cost material - for pieces made only of basic materials that shouldn't be known from the start (the charcoal kiln waits for ore).")]
        [SerializeField] private List<NAV.Gameplay.Items.ItemDefinition> _unlockItems = new();

        /// <summary>Stable identifier used by SaveManager/BuildingPieceDatabase to reference this
        /// definition from a save file (JSON can't hold a direct asset reference) - same pattern
        /// as ItemDefinition.Id. Auto-fills from the asset name if left blank.</summary>
        public string Id => _id;
        public string DisplayName => NAV.Core.Localization.Loc.T(_displayName);
        public Sprite Icon => _icon;
        public IReadOnlyList<RecipeIngredient> Cost => _cost;

        /// <summary>Prefab instantiated both for the placement ghost and the real built piece
        /// (must carry a BuildingPiece component - see PlayerBuilding).</summary>
        public GameObject Prefab => _prefab;

        /// <summary>Must be placed within a placed workbench's build radius.</summary>
        public bool RequiresWorkbench => _requiresWorkbench;

        /// <summary>Station kind whose build area (at RequiredStationLevel or higher) the piece
        /// must stand in, or null.</summary>
        public WorkbenchDefinition RequiredStation => _requiredStation;
        public int RequiredStationLevel => _requiredStationLevel;

        /// <summary>Snapping may turn it 180 degrees to face the way the player rotated it.</summary>
        public bool Directional => _directional;

        /// <summary>Snapping turns its high side towards the middle of the building (roof, gable).</summary>
        public bool RisesTowardCentre => _risesTowardCentre;

        /// <summary>Goes on the water (a boat) - see PlayerBuilding.</summary>
        public bool FloatsOnWater => _floatsOnWater;

        /// <summary>Valheim's rule for pieces: known once the player has had every cost material
        /// (and every Unlock Item) - discovered is PlayerCrafting.IsDiscovered.</summary>
        public bool IsUnlocked(System.Func<NAV.Gameplay.Items.ItemDefinition, bool> discovered)
        {
            foreach (RecipeIngredient ingredient in _cost)
            {
                if (ingredient.Item != null && !discovered(ingredient.Item))
                {
                    return false;
                }
            }

            foreach (NAV.Gameplay.Items.ItemDefinition item in _unlockItems)
            {
                if (item != null && !discovered(item))
                {
                    return false;
                }
            }

            return true;
        }

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

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = name;
            }
        }
    }
}
