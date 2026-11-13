using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Demo
{
    /// <summary>
    /// What the Steam demo lets the player reach (developer 2026-11-12: tiers 1-2 + the Leshy,
    /// single-player; see ProjectTitle/STEAM_DEMO_PLAN.md stage 1). Lives at
    /// Resources/DemoConfig and is read through <see cref="DemoMode"/>; with Enabled off (or no
    /// asset) the game is the full game. The world itself is not cut: what is past the demo
    /// simply can't be crafted, built or smelted. NAV > Demo > Build Demo Config fills it.
    /// </summary>
    [CreateAssetMenu(fileName = "DemoConfig", menuName = "NAV/Demo/Demo Config")]
    public class DemoConfig : ScriptableObject
    {
        [Serializable]
        public class StationCap
        {
            public WorkbenchDefinition Station;
            [Min(1)] public int MaxLevel = 1;
        }

        [Tooltip("On = this build is the demo.")]
        [SerializeField] private bool _enabled;

        [Header("Progression limit")]
        [Tooltip("Highest level each station can reach (upgrade pieces past it add nothing).")]
        [SerializeField] private List<StationCap> _stationCaps = new();
        [Tooltip("Items past the demo: recipes, pieces and smelting that make or use them are hidden.")]
        [SerializeField] private List<ItemDefinition> _lockedItems = new();
        [Tooltip("Pieces past the demo that the locked items don't already catch (e.g. the boat).")]
        [SerializeField] private List<BuildingPieceDefinition> _lockedPieces = new();

        [Header("World")]
        [Tooltip("Biomes past the demo: no resource nodes and no creatures spawn there (decor stays, so the land still looks like itself). Anywhere else a node, pickup or creature that yields a locked item doesn't spawn either.")]
        [SerializeField] private List<NAV.World.Biomes.BiomeDefinition> _lockedBiomes = new();

        [Header("End of the demo")]
        [Tooltip("Crafting the last of these (all of them owned) shows the end-of-demo screen.")]
        [SerializeField] private List<ItemDefinition> _finaleItems = new();
        [Tooltip("Steam store page (wishlist button). Empty = no button.")]
        [SerializeField] private string _wishlistUrl;
        [Tooltip("Discord / feedback link. Empty = no button.")]
        [SerializeField] private string _feedbackUrl;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private StyleSheet _finaleStyle;

        [Header("Menus")]
        [SerializeField] private bool _hideMultiplayer = true;
        [Tooltip("The developer console (give, god...) only in Development Builds and the editor.")]
        [SerializeField] private bool _devConsoleOnlyInDevelopmentBuilds = true;

        public bool Enabled => _enabled;
        public IReadOnlyList<StationCap> StationCaps => _stationCaps;
        public IReadOnlyList<ItemDefinition> LockedItems => _lockedItems;
        public IReadOnlyList<BuildingPieceDefinition> LockedPieces => _lockedPieces;
        public IReadOnlyList<NAV.World.Biomes.BiomeDefinition> LockedBiomes => _lockedBiomes;
        public IReadOnlyList<ItemDefinition> FinaleItems => _finaleItems;
        public string WishlistUrl => _wishlistUrl;
        public string FeedbackUrl => _feedbackUrl;
        public PanelSettings PanelSettings => _panelSettings;
        public StyleSheet FinaleStyle => _finaleStyle;
        public bool HideMultiplayer => _hideMultiplayer;
        public bool DevConsoleOnlyInDevelopmentBuilds => _devConsoleOnlyInDevelopmentBuilds;

#if UNITY_EDITOR
        /// <summary>Editor setup only (DemoSetup).</summary>
        public void EditorFill(bool enabled, List<StationCap> caps, List<ItemDefinition> lockedItems,
            List<BuildingPieceDefinition> lockedPieces, List<NAV.World.Biomes.BiomeDefinition> lockedBiomes,
            List<ItemDefinition> finaleItems, PanelSettings panelSettings, StyleSheet finaleStyle)
        {
            _enabled = enabled;
            _stationCaps = caps;
            _lockedItems = lockedItems;
            _lockedPieces = lockedPieces;
            _lockedBiomes = lockedBiomes;
            _finaleItems = finaleItems;
            _panelSettings = panelSettings;
            _finaleStyle = finaleStyle;
        }

        public void EditorSetEnabled(bool enabled) => _enabled = enabled;
#endif
    }
}
