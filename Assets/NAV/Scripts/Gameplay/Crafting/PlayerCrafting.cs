using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;

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
        [Tooltip("Every recipe in the game. Which ones the player sees is decided by discovery (see IsUnlocked).")]
        [SerializeField] private List<RecipeDefinition> _knownRecipes = new();

        private readonly List<Workbench> _nearbyWorkbenches = new();

        // Discovery, Valheim style - saved with the player (SaveManager).
        private readonly HashSet<string> _discoveredItems = new();
        private readonly Dictionary<string, int> _stationLevels = new();

        /// <summary>The whole recipe catalog, discovered or not.</summary>
        public IReadOnlyList<RecipeDefinition> KnownRecipes => _knownRecipes;

        /// <summary>Raised when recipes became unlocked (new item picked up, new station level
        /// reached, or a save restored).</summary>
        public event Action RecipesChanged;

        /// <summary>Raised once per newly unlocked recipe.</summary>
        public event Action<RecipeDefinition> RecipeUnlocked;

        /// <summary>Raised whenever the discovered items or station levels changed (PlayerBuilding
        /// re-checks which pieces are known).</summary>
        public event Action DiscoveryChanged;

        public IEnumerable<string> DiscoveredItems => _discoveredItems;

        /// <summary>The player has had this item at some point.</summary>
        public bool IsDiscovered(ItemDefinition item) => item != null && _discoveredItems.Contains(item.Id);
        public IReadOnlyDictionary<string, int> StationLevels => _stationLevels;

        /// <summary>
        /// Recipes that need no station are always known. A station recipe appears once the
        /// player has reached that station's level (used it with E, or stood by it while it
        /// went up a level) AND has had at least one of its ingredients - a forge recipe for a
        /// copper sword shows up only after copper has been found and a forge was used.
        /// </summary>
        public bool IsUnlocked(RecipeDefinition recipe)
        {
            if (recipe == null || recipe.OutputItem == null)
            {
                return false;
            }

            // Level 0 = craftable anywhere (the wooden hammer), whatever station it names.
            if (recipe.RequiredWorkbenchTier <= 0)
            {
                return true;
            }

            if (ReachedLevel(recipe.Station) < Mathf.Max(1, recipe.RequiredWorkbenchTier))
            {
                return false;
            }

            foreach (RecipeIngredient ingredient in recipe.Ingredients)
            {
                if (ingredient.Item != null && _discoveredItems.Contains(ingredient.Item.Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Recipes to list in the crafting panel, Valheim style - the two lists never
        /// mix: opened with the crafting key (station null) only what is made by hand (level 0);
        /// opened at a station with E only that station's unlocked recipes.</summary>
        public List<RecipeDefinition> GetVisibleRecipes(Workbench station)
        {
            var visible = new List<RecipeDefinition>();
            foreach (RecipeDefinition recipe in _knownRecipes)
            {
                if (!IsUnlocked(recipe))
                {
                    continue;
                }

                bool needsStation = recipe.RequiredWorkbenchTier > 0;
                bool show = station == null
                    ? !needsStation
                    : needsStation && (recipe.Station == null || recipe.Station == station.Definition);
                if (show)
                {
                    visible.Add(recipe);
                }
            }

            return visible;
        }

        /// <summary>The player used this station (E) - remember the level it has now.</summary>
        public void LearnStation(Workbench station)
        {
            if (station == null || station.Definition == null)
            {
                return;
            }

            string id = station.Definition.name;
            _stationLevels.TryGetValue(id, out int known);
            if (station.Tier > known)
            {
                ChangeDiscovery(() => _stationLevels[id] = station.Tier);
            }
        }

        public void RestoreDiscovery(IEnumerable<string> items, IEnumerable<KeyValuePair<string, int>> stations)
        {
            _discoveredItems.Clear();
            _stationLevels.Clear();
            if (items != null)
            {
                foreach (string id in items)
                {
                    _discoveredItems.Add(id);
                }
            }

            if (stations != null)
            {
                foreach (KeyValuePair<string, int> pair in stations)
                {
                    _stationLevels[pair.Key] = pair.Value;
                }
            }

            DiscoverInventory();
            RecipesChanged?.Invoke();
            DiscoveryChanged?.Invoke();
        }

        private int ReachedLevel(WorkbenchDefinition station)
        {
            if (station != null)
            {
                return _stationLevels.TryGetValue(station.name, out int level) ? level : 0;
            }

            int highest = 0;
            foreach (int level in _stationLevels.Values)
            {
                highest = Mathf.Max(highest, level);
            }

            return highest;
        }

        private void DiscoverInventory()
        {
            if (_playerInventory == null || _playerInventory.Inventory == null)
            {
                return;
            }

            List<string> found = null;
            foreach (ItemStack stack in _playerInventory.Inventory.Slots)
            {
                if (!stack.IsEmpty && !_discoveredItems.Contains(stack.Definition.Id))
                {
                    (found ??= new List<string>()).Add(stack.Definition.Id);
                }
            }

            // Added inside ChangeDiscovery, so it can compare the unlocked recipes before/after.
            if (found != null)
            {
                ChangeDiscovery(() => _discoveredItems.UnionWith(found));
            }
        }

        /// <summary>Applies a discovery change and announces the recipes it unlocked.</summary>
        private void ChangeDiscovery(Action change)
        {
            var before = new HashSet<RecipeDefinition>();
            foreach (RecipeDefinition recipe in _knownRecipes)
            {
                if (IsUnlocked(recipe))
                {
                    before.Add(recipe);
                }
            }

            change?.Invoke();

            bool any = false;
            foreach (RecipeDefinition recipe in _knownRecipes)
            {
                if (!before.Contains(recipe) && IsUnlocked(recipe))
                {
                    any = true;
                    Debug.Log($"{name}: new recipe - {recipe.DisplayName}.", this);
                    RecipeUnlocked?.Invoke(recipe);
                }
            }

            if (any)
            {
                RecipesChanged?.Invoke();
            }

            DiscoveryChanged?.Invoke();
        }

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

        /// <summary>Highest usable level among nearby stations of this kind (null = any kind),
        /// 0 if none - what a recipe's RequiredWorkbenchTier is checked against.</summary>
        public int GetStationLevel(WorkbenchDefinition station)
        {
            int highest = 0;
            foreach (Workbench workbench in _nearbyWorkbenches)
            {
                if (workbench != null && (station == null || workbench.Definition == station) && workbench.UsableTier > highest)
                {
                    highest = workbench.UsableTier;
                }
            }

            return highest;
        }

        /// <summary>Highest level among nearby stations of this kind that can't be used for lack
        /// of a roof - for the "needs a roof" hint.</summary>
        public int GetUnshelteredStationLevel(WorkbenchDefinition station)
        {
            int highest = 0;
            foreach (Workbench workbench in _nearbyWorkbenches)
            {
                if (workbench != null && !workbench.IsSheltered && (station == null || workbench.Definition == station) && workbench.Tier > highest)
                {
                    highest = workbench.Tier;
                }
            }

            return highest;
        }

        /// <summary>Whether the recipe can be crafted here and now (ingredients + station).</summary>
        public bool CanCraft(RecipeDefinition recipe)
        {
            return recipe != null && recipe.CanCraft(_playerInventory.Inventory, GetStationLevel(recipe.Station));
        }

        public event Action NearbyWorkbenchChanged;

        /// <summary>A nearby workbench's state changed (e.g. it gained or lost its roof, or an
        /// upgrade piece was built next to it - standing by it then counts as reaching that level).</summary>
        public void NotifyWorkbenchChanged()
        {
            foreach (Workbench workbench in _nearbyWorkbenches)
            {
                if (workbench != null)
                {
                    LearnNearbyLevel(workbench);
                }
            }

            NearbyWorkbenchChanged?.Invoke();
        }

        // Only a level the player already knew the station at can go up by standing nearby -
        // the first contact with a station kind is using it (E).
        private void LearnNearbyLevel(Workbench workbench)
        {
            if (workbench.Definition != null && _stationLevels.ContainsKey(workbench.Definition.name))
            {
                LearnStation(workbench);
            }
        }

        private void Awake()
        {
            if (_playerInventory == null)
            {
                Debug.LogError($"{nameof(PlayerCrafting)} on '{name}' is missing its PlayerInventory reference.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (enabled && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed += DiscoverInventory;
                DiscoverInventory();
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= DiscoverInventory;
            }
        }

        /// <summary>Whether the player stands in this station's range.</summary>
        public bool IsNear(Workbench workbench) => workbench != null && _nearbyWorkbenches.Contains(workbench);

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

            bool crafted = recipe.TryCraft(_playerInventory.Inventory, GetStationLevel(recipe.Station));
            if (crafted)
            {
                Debug.Log($"{name} crafted {recipe.OutputAmount}x {recipe.OutputItem.DisplayName} ({recipe.DisplayName}).", this);
            }

            return crafted;
        }
    }
}
