using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Observes PlayerCrafting/PlayerInventory and renders known recipes as a two-column,
    /// Valheim-style UI Toolkit panel: a scrollable list of known recipes on the left, and the
    /// currently-selected recipe's icon/name/description/stats/ingredients/Craft button on the
    /// right. Owns no gameplay state - purely a view over PlayerCrafting/Inventory
    /// (ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule), same pattern as
    /// InventoryUIController. The "stats" block only ever shows Weight - it's the only
    /// ItemDefinition stat that exists yet; Damage/Durability/etc belong to the future
    /// Equipment/Combat systems ("Final weapons and armor balance" is explicitly Not Yet
    /// Decided in PROJECT_STATE.md), so no placeholder numbers are shown for them.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CraftingUIController : MonoBehaviour
    {
        [SerializeField] private PlayerCrafting _playerCrafting;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [Tooltip("E on a workbench/forge (CraftingStationAccess) opens the panel with that station's recipes.")]
        [SerializeField] private PlayerInteractor _interactor;

        private UIDocument _document;
        private VisualElement _root;
        private Label _title;

        // The station the panel was opened at with E (null = opened with the crafting key:
        // only recipes that need no station are listed).
        private Workbench _station;
        private ScrollView _recipeListView;
        private VisualElement _detailIcon;
        private Label _detailName;
        private Label _detailDescription;
        private VisualElement _detailStats;
        private Label _detailRequirement;
        private VisualElement _detailIngredients;
        private Button _detailCraftButton;

        private readonly List<RecipeListEntry> _entries = new();
        private RecipeDefinition _selectedRecipe;
        private bool _visible;

        private struct RecipeListEntry
        {
            public RecipeDefinition Recipe;
            public VisualElement Root;
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_playerCrafting == null || _playerInventory == null || _inputHandler == null)
            {
                Debug.LogError($"{nameof(CraftingUIController)} on '{name}' is missing a required reference (PlayerCrafting/PlayerInventory/InputHandler).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            // See InventoryUIController's Start for why this runs here rather than
            // Awake/OnEnable: it depends on other objects' Awake-time state (UIDocument's
            // rootVisualElement, PlayerInventory's Inventory instance).
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(CraftingUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("crafting-root");
            _title = documentRoot.Q<Label>("crafting-title");
            _recipeListView = documentRoot.Q<ScrollView>("recipe-list");
            _detailIcon = documentRoot.Q<VisualElement>("detail-icon");
            _detailName = documentRoot.Q<Label>("detail-name");
            _detailDescription = documentRoot.Q<Label>("detail-description");
            _detailStats = documentRoot.Q<VisualElement>("detail-stats");
            _detailRequirement = documentRoot.Q<Label>("detail-requirement");
            _detailIngredients = documentRoot.Q<VisualElement>("detail-ingredients");
            _detailCraftButton = documentRoot.Q<Button>("detail-craft-button");

            if (_root == null || _recipeListView == null || _detailIcon == null || _detailName == null
                || _detailDescription == null || _detailStats == null || _detailRequirement == null
                || _detailIngredients == null || _detailCraftButton == null)
            {
                Debug.LogError($"{nameof(CraftingUIController)} on '{name}' could not find one or more required elements in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _detailCraftButton.clicked += HandleCraftClicked;

            _playerInventory.Inventory.Changed += RefreshAffordability;
            _playerCrafting.NearbyWorkbenchChanged += RefreshAffordability;
            _playerCrafting.RecipesChanged += HandleRecipesChanged;

            BuildRecipeList();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleCraftingPerformed += HandleToggleCraftingPerformed;
            }

            if (_interactor != null)
            {
                _interactor.Interacted += HandleInteracted;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleCraftingPerformed -= HandleToggleCraftingPerformed;
            }

            if (_interactor != null)
            {
                _interactor.Interacted -= HandleInteracted;
            }
        }

        private void Update()
        {
            // Walked away from the station it was opened at - close, like Valheim.
            if (_visible && _station != null && !_playerCrafting.IsNear(_station))
            {
                SetVisible(false);
            }
        }

        private void HandleInteracted(IInteractable interactable)
        {
            if (interactable is not CraftingStationAccess access)
            {
                return;
            }

            if (_visible && _station == access.Station)
            {
                SetVisible(false);
                return;
            }

            Open(access.Station);
        }

        private void HandleRecipesChanged()
        {
            if (_visible)
            {
                BuildRecipeList();
                RefreshAffordability();
            }
        }

        /// <summary>Opens the panel at a station (its recipes listed) or, with null, with only
        /// the recipes that need no station.</summary>
        public void Open(Workbench station)
        {
            _station = station;
            if (_title != null)
            {
                _title.text = station != null ? station.DisplayName : "Crafting";
            }

            BuildRecipeList();
            SetVisible(true);
        }

        private void OnDestroy()
        {
            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= RefreshAffordability;
            }

            if (_playerCrafting != null)
            {
                _playerCrafting.NearbyWorkbenchChanged -= RefreshAffordability;
                _playerCrafting.RecipesChanged -= HandleRecipesChanged;
            }
        }

        private void HandleToggleCraftingPerformed()
        {
            if (_visible)
            {
                SetVisible(false);
            }
            else
            {
                Open(null);
            }
        }

        /// <summary>Closes this panel if it's open. Safe to call when already closed.</summary>
        public void Hide()
        {
            if (_visible)
            {
                SetVisible(false);
            }
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _inputHandler.SetMenuOpen(visible);

            if (visible)
            {
                RefreshAffordability();
            }
        }

        private void BuildRecipeList()
        {
            _recipeListView.Clear();
            _entries.Clear();

            RecipeDefinition previouslySelected = _selectedRecipe;
            foreach (RecipeDefinition recipe in _playerCrafting.GetVisibleRecipes(_station))
            {

                var item = new VisualElement();
                item.AddToClassList("recipe-list-item");

                var icon = new VisualElement();
                icon.AddToClassList("recipe-list-item__icon");
                if (recipe.OutputItem != null)
                {
                    icon.style.backgroundImage = new StyleBackground(recipe.OutputItem.Icon);
                }
                item.Add(icon);

                var nameLabel = new Label(recipe.DisplayName);
                nameLabel.AddToClassList("recipe-list-item__name");
                item.Add(nameLabel);

                RecipeDefinition capturedRecipe = recipe;
                item.RegisterCallback<ClickEvent>(_ => SelectRecipe(capturedRecipe));

                _recipeListView.Add(item);
                _entries.Add(new RecipeListEntry { Recipe = recipe, Root = item });
            }

            if (_entries.Count == 0)
            {
                var empty = new Label(_station != null
                    ? "Nothing to make here yet - find new materials."
                    : "Nothing to craft by hand. Use a workbench (E) for more.");
                empty.AddToClassList("recipe-list-empty");
                _recipeListView.Add(empty);
            }

            bool stillListed = _entries.Exists(entry => entry.Recipe == previouslySelected);
            SelectRecipe(stillListed ? previouslySelected : _entries.Count > 0 ? _entries[0].Recipe : null);
        }

        private void SelectRecipe(RecipeDefinition recipe)
        {
            _selectedRecipe = recipe;

            foreach (RecipeListEntry entry in _entries)
            {
                entry.Root.EnableInClassList("recipe-list-item--selected", entry.Recipe == recipe);
            }

            RefreshDetail();
        }

        private void HandleCraftClicked()
        {
            if (_selectedRecipe == null)
            {
                return;
            }

            _playerCrafting.TryCraft(_selectedRecipe);
            // TryCraft mutates the inventory via Inventory.AddItem/RemoveItem, whose Changed
            // event already triggers RefreshAffordability - no explicit refresh needed here.
        }

        private void RefreshAffordability()
        {
            foreach (RecipeListEntry entry in _entries)
            {
                bool canCraft = _playerCrafting.CanCraft(entry.Recipe);
                entry.Root.EnableInClassList("recipe-list-item--unaffordable", !canCraft);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (_selectedRecipe == null)
            {
                _detailIcon.style.backgroundImage = new StyleBackground();
                _detailName.text = string.Empty;
                _detailDescription.text = "No known recipes yet.";
                _detailDescription.AddToClassList("recipe-detail__placeholder");
                _detailStats.Clear();
                _detailRequirement.style.display = DisplayStyle.None;
                _detailIngredients.Clear();
                _detailCraftButton.SetEnabled(false);
                return;
            }

            _detailDescription.RemoveFromClassList("recipe-detail__placeholder");

            ItemDefinition output = _selectedRecipe.OutputItem;
            _detailIcon.style.backgroundImage = output != null ? new StyleBackground(output.Icon) : new StyleBackground();
            _detailName.text = _selectedRecipe.DisplayName;
            _detailDescription.text = output != null ? output.Description : string.Empty;

            BuildStats(output);
            BuildRequirement();
            BuildIngredients();

            bool canCraft = _playerCrafting.CanCraft(_selectedRecipe);
            _detailCraftButton.SetEnabled(canCraft);
        }

        private void BuildStats(ItemDefinition output)
        {
            _detailStats.Clear();

            if (output == null)
            {
                return;
            }

            if (output.IsArmor)
            {
                AddStatRow("Armour", $"{output.Armor.Armor:0.#}");
                AddStatRow("Slot", output.Armor.Slot.ToString());
            }

            AddStatRow("Weight", $"{output.Weight:F1}");
        }

        private void AddStatRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("recipe-detail__stat-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("recipe-detail__stat-label");
            row.Add(labelElement);

            var valueElement = new Label(value);
            valueElement.AddToClassList("recipe-detail__stat-value");
            row.Add(valueElement);

            _detailStats.Add(row);
        }

        private void BuildRequirement()
        {
            if (_selectedRecipe.RequiredWorkbenchTier <= 0)
            {
                _detailRequirement.style.display = DisplayStyle.None;
                return;
            }

            WorkbenchDefinition station = _selectedRecipe.Station;
            string stationName = station != null ? station.DisplayName : "Workbench";
            int level = _selectedRecipe.RequiredWorkbenchTier;
            bool hasWorkbench = _playerCrafting.GetStationLevel(station) >= level;
            // A station of the right level is near but has no roof - say so (Valheim-style).
            bool needsRoof = !hasWorkbench && _playerCrafting.GetUnshelteredStationLevel(station) >= level;
            string levelText = level > 1 ? $" level {level}" : "";
            _detailRequirement.text = needsRoof
                ? $"Requires {stationName}{levelText} - it needs a roof"
                : $"Requires {stationName}{levelText}";
            _detailRequirement.EnableInClassList("recipe-detail__requirement--missing", !hasWorkbench);
            _detailRequirement.style.display = DisplayStyle.Flex;
        }

        private void BuildIngredients()
        {
            _detailIngredients.Clear();

            foreach (RecipeIngredient ingredient in _selectedRecipe.Ingredients)
            {
                if (ingredient.Item == null)
                {
                    continue;
                }

                var slot = new VisualElement();
                slot.AddToClassList("ingredient-slot");
                slot.AddToClassList("nav-slot");
                slot.tooltip = ingredient.Item.DisplayName;

                var icon = new VisualElement();
                icon.AddToClassList("ingredient-slot__icon");
                icon.AddToClassList("nav-slot__icon");
                icon.style.backgroundImage = new StyleBackground(ingredient.Item.Icon);
                slot.Add(icon);

                int have = _playerInventory.Inventory.GetTotalQuantity(ingredient.Item);
                bool sufficient = have >= ingredient.Amount;

                var amount = new Label($"{have}/{ingredient.Amount}");
                amount.AddToClassList("ingredient-slot__amount");
                amount.EnableInClassList("ingredient-slot__amount--insufficient", !sufficient);
                slot.Add(amount);

                _detailIngredients.Add(slot);
            }
        }
    }
}
