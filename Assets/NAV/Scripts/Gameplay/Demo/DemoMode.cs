using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Demo
{
    /// <summary>
    /// The demo's limits, asked by crafting, building, stations and menus. Everything answers
    /// "allowed" when <see cref="IsActive"/> is false, so the full game is untouched.
    /// </summary>
    public static class DemoMode
    {
        /// <summary>Resources path of the <see cref="DemoConfig"/> asset.</summary>
        public const string ResourcePath = "DemoConfig";

        private static DemoConfig _config;
        private static bool _loaded;
        private static HashSet<ItemDefinition> _lockedItems;
        private static HashSet<BuildingPieceDefinition> _lockedPieces;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _config = null;
            _loaded = false;
            _lockedItems = null;
            _lockedPieces = null;
        }

        public static DemoConfig Config
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _config = Resources.Load<DemoConfig>(ResourcePath);
                    _lockedItems = new HashSet<ItemDefinition>();
                    _lockedPieces = new HashSet<BuildingPieceDefinition>();
                    if (_config != null)
                    {
                        foreach (ItemDefinition item in _config.LockedItems)
                        {
                            if (item != null)
                            {
                                _lockedItems.Add(item);
                            }
                        }

                        foreach (BuildingPieceDefinition piece in _config.LockedPieces)
                        {
                            if (piece != null)
                            {
                                _lockedPieces.Add(piece);
                            }
                        }
                    }
                }

                return _config;
            }
        }

        /// <summary>This build is the demo.</summary>
        public static bool IsActive => Config != null && Config.Enabled;

        public static bool HidesMultiplayer => IsActive && Config.HideMultiplayer;

        /// <summary>The developer console may be used (always in the full game).</summary>
        public static bool AllowsDevConsole => !IsActive || !Config.DevConsoleOnlyInDevelopmentBuilds || Debug.isDebugBuild;

        public static bool IsLocked(ItemDefinition item) => IsActive && item != null && _lockedItems.Contains(item);

        /// <summary>The station's level as the demo lets it count.</summary>
        public static int ClampStationLevel(WorkbenchDefinition station, int level)
        {
            int cap = MaxLevel(station);
            return cap > 0 ? Mathf.Min(level, cap) : level;
        }

        /// <summary>Highest level the demo allows at this station, 0 = no limit.</summary>
        public static int MaxLevel(WorkbenchDefinition station)
        {
            if (!IsActive || station == null)
            {
                return 0;
            }

            foreach (DemoConfig.StationCap cap in Config.StationCaps)
            {
                if (cap != null && cap.Station == station)
                {
                    return cap.MaxLevel;
                }
            }

            return 0;
        }

        public static bool Allows(RecipeDefinition recipe)
        {
            if (!IsActive || recipe == null)
            {
                return true;
            }

            int cap = MaxLevel(recipe.Station);
            if (cap > 0 && recipe.RequiredWorkbenchTier > cap)
            {
                return false;
            }

            if (IsLocked(recipe.OutputItem))
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in recipe.Ingredients)
            {
                if (IsLocked(ingredient.Item))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Allows(BuildingPieceDefinition piece)
        {
            if (!IsActive || piece == null)
            {
                return true;
            }

            if (_lockedPieces.Contains(piece))
            {
                return false;
            }

            int cap = MaxLevel(piece.RequiredStation);
            if (cap > 0 && piece.RequiredStationLevel > cap)
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in piece.Cost)
            {
                if (IsLocked(ingredient.Item))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A smelter/kiln conversion (locked ore in or locked bar out = not done).</summary>
        public static bool Allows(ProcessingRecipe recipe)
        {
            return !IsActive || recipe == null || (!IsLocked(recipe.Input) && !IsLocked(recipe.Output));
        }
    }
}
