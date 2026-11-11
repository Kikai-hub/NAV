using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Demo;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// The Steam demo's limits (developer 2026-11-12: tiers 1-2 + the Leshy, single-player;
    /// ProjectTitle/STEAM_DEMO_PLAN.md stage 1).
    /// NAV > Demo > Build Demo Config creates/refreshes Resources/DemoConfig from the lists below
    /// (keeps its On/Off and links) and logs what the demo hides. NAV > Demo > Demo Mode switches
    /// it on and off - turn it on before building the demo, off for the full game. Safe to re-run.
    /// Per PROGRESSION_TIERS_v0.1.md: tier 1 = workbench 1-2 (tanning rack), tier 2 = copper, oak,
    /// forge 1 and the joiner's bench (workbench 3). Everything from tier 3 on is locked.
    /// </summary>
    internal static class DemoSetup
    {
        private const string ConfigPath = "Assets/NAV/Resources/DemoConfig.asset";
        private const string PanelSettingsPath = "Assets/NAV/Scripts/UI/NAV_PanelSettings.asset";
        private const string FinaleStylePath = "Assets/NAV/Scripts/UI/DemoFinalePanel.uss";
        private const string WorkbenchPath = "Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset";
        private const string ForgePath = "Assets/NAV/ScriptableObjects/Workbenches/Forge.asset";
        private const int WorkbenchCap = 3; // base + tanning rack + joiner's bench
        private const int ForgeCap = 1;     // no anvil / casting mould / quench tub / furnace

        // Materials and gear of tiers 3-6 (tin/bronze, iron, silver, black steel, bog oak, furs,
        // swamp herbs). Recipes, pieces and smelting that make or use any of them are hidden.
        private static readonly string[] LockedItemNames =
        {
            "TinOre", "TinBar", "BronzeBar", "BronzeAxe", "BronzePickaxe", "BronzeHelm", "BronzeCuirass", "BronzeGreaves",
            "BogIronOre", "IronBar", "IronAxe", "IronPickaxe", "IronShelom", "IronChainmail", "IronGreaves", "IronHinge", "IronArrow",
            "SilverOre", "SilverBar", "SilverAxe", "SilverPickaxe", "SilverHelm", "SilverCuirass", "SilverGreaves",
            "BlackOre", "BlackIron", "BlackSteelBar", "BlackSteelAxe", "BlackSteelPickaxe", "BlackSteelHelm", "BlackSteelCuirass", "BlackSteelGreaves",
            "BogWood", "BogOakWood", "BogOakKnots", "BogOakCharcoal",
            "Fur", "FurCloak", "FurHood", "VolkolakPelt", "VolkolakCloak",
            "WildRosemary", "Antidote",
            // The Hunting Bow is made of Bog Wood (tier 4), so its arrows would be useless here.
            "HuntingBow", "WoodArrow", "FlintArrow",
        };

        // Past the demo but made only of allowed materials.
        private static readonly string[] LockedPieceNames =
        {
            "LadyaPiece",        // the boat - tier 3 (sea islands)
            "CastingMouldPiece", // forge upgrade for bronze casting - tier 3
        };

        // Full copper armour - needs ore, so the Stone Pickaxe, so the Leshy's heartwood first.
        private static readonly string[] FinaleItemNames = { "CopperHelm", "CopperScaleShirt", "CopperGreaves" };

        private const string ToggleMenu = "NAV/Demo/Demo Mode (Steam demo build)";

        [MenuItem("NAV/Demo/Build Demo Config")]
        private static void BuildConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
            bool created = config == null;
            if (created)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NAV/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/NAV", "Resources");
                }

                config = ScriptableObject.CreateInstance<DemoConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(WorkbenchPath);
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(ForgePath);
            if (workbench == null || forge == null)
            {
                Debug.LogError($"DemoSetup: workbench or forge definition not found ({WorkbenchPath}, {ForgePath}).");
                return;
            }

            var caps = new List<DemoConfig.StationCap>
            {
                new() { Station = workbench, MaxLevel = WorkbenchCap },
                new() { Station = forge, MaxLevel = ForgeCap },
            };

            config.EditorFill(
                created ? false : config.Enabled,
                caps,
                Find<ItemDefinition>(LockedItemNames),
                Find<BuildingPieceDefinition>(LockedPieceNames),
                Find<ItemDefinition>(FinaleItemNames),
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath),
                AssetDatabase.LoadAssetAtPath<StyleSheet>(FinaleStylePath));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"DemoSetup: {(created ? "created" : "refreshed")} {ConfigPath} (demo mode {(config.Enabled ? "ON" : "off")}).\n{Report(config)}", config);
        }

        [MenuItem(ToggleMenu)]
        private static void ToggleDemoMode()
        {
            var config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
            if (config == null)
            {
                BuildConfig();
                config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
                if (config == null)
                {
                    return;
                }
            }

            config.EditorSetEnabled(!config.Enabled);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"DemoSetup: demo mode {(config.Enabled ? "ON - builds are the Steam demo" : "off - full game")}.", config);
        }

        [MenuItem(ToggleMenu, true)]
        private static bool ToggleDemoModeValidate()
        {
            var config = AssetDatabase.LoadAssetAtPath<DemoConfig>(ConfigPath);
            Menu.SetChecked(ToggleMenu, config != null && config.Enabled);
            return true;
        }

        private static List<T> Find<T>(string[] names) where T : Object
        {
            var found = new List<T>();
            foreach (string assetName in names)
            {
                T asset = null;
                foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {assetName}"))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null && candidate.name == assetName)
                    {
                        asset = candidate;
                        break;
                    }
                }

                if (asset == null)
                {
                    Debug.LogWarning($"DemoSetup: {typeof(T).Name} '{assetName}' not found - skipped.");
                    continue;
                }

                found.Add(asset);
            }

            return found;
        }

        /// <summary>What the demo hides and keeps, judged with the config's own rules (as if on).</summary>
        private static string Report(DemoConfig config)
        {
            var locked = new HashSet<ItemDefinition>(config.LockedItems);
            var lockedPieces = new HashSet<BuildingPieceDefinition>(config.LockedPieces);
            int Cap(WorkbenchDefinition station)
            {
                foreach (DemoConfig.StationCap cap in config.StationCaps)
                {
                    if (cap.Station == station)
                    {
                        return cap.MaxLevel;
                    }
                }

                return 0;
            }

            var keptRecipes = new List<string>();
            var hiddenRecipes = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition"))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (recipe == null)
                {
                    continue;
                }

                int cap = Cap(recipe.Station);
                bool hidden = (cap > 0 && recipe.RequiredWorkbenchTier > cap) || locked.Contains(recipe.OutputItem);
                foreach (RecipeIngredient ingredient in recipe.Ingredients)
                {
                    hidden |= locked.Contains(ingredient.Item);
                }

                (hidden ? hiddenRecipes : keptRecipes).Add(recipe.DisplayName);
            }

            var keptPieces = new List<string>();
            var hiddenPieces = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:BuildingPieceDefinition"))
            {
                var piece = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (piece == null)
                {
                    continue;
                }

                int cap = Cap(piece.RequiredStation);
                bool hidden = lockedPieces.Contains(piece) || (cap > 0 && piece.RequiredStationLevel > cap);
                foreach (RecipeIngredient ingredient in piece.Cost)
                {
                    hidden |= locked.Contains(ingredient.Item);
                }

                (hidden ? hiddenPieces : keptPieces).Add(piece.DisplayName);
            }

            var text = new StringBuilder();
            text.AppendLine($"Recipes kept ({keptRecipes.Count}): {string.Join(", ", keptRecipes)}");
            text.AppendLine($"Recipes hidden ({hiddenRecipes.Count}): {string.Join(", ", hiddenRecipes)}");
            text.AppendLine($"Pieces kept ({keptPieces.Count}): {string.Join(", ", keptPieces)}");
            text.Append($"Pieces hidden ({hiddenPieces.Count}): {string.Join(", ", hiddenPieces)}");
            return text.ToString();
        }
    }
}
