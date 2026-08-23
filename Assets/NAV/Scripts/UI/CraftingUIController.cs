using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Observes PlayerCrafting/PlayerInventory and renders known recipes as a UI Toolkit
    /// panel with a Craft button per recipe, greyed out when the player can't currently
    /// afford it. Owns no gameplay state - purely a view over PlayerCrafting/Inventory
    /// (ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule), same pattern as
    /// InventoryUIController.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CraftingUIController : MonoBehaviour
    {
        [SerializeField] private PlayerCrafting _playerCrafting;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private InventoryUIController _inventoryPanel;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _recipeContainer;
        private readonly List<RecipeRow> _rows = new();
        private bool _visible;

        private struct RecipeRow
        {
            public RecipeDefinition Recipe;
            public Label CostLabel;
            public Button CraftButton;
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
            _recipeContainer = documentRoot.Q<VisualElement>("recipe-container");

            if (_root == null || _recipeContainer == null)
            {
                Debug.LogError($"{nameof(CraftingUIController)} on '{name}' could not find 'crafting-root'/'recipe-container' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _playerInventory.Inventory.Changed += RefreshAffordability;

            BuildRows();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleCraftingPerformed += HandleToggleCraftingPerformed;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleCraftingPerformed -= HandleToggleCraftingPerformed;
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= RefreshAffordability;
            }
        }

        private void HandleToggleCraftingPerformed()
        {
            SetVisible(!_visible);
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

            UnityEngine.Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = visible;
            _inputHandler.SetMenuOpen(visible);

            if (visible)
            {
                // Only one modal panel makes sense on screen at once - both panels are
                // full-screen overlays centered the same way.
                _inventoryPanel?.Hide();
                RefreshAffordability();
            }
        }

        private void BuildRows()
        {
            _recipeContainer.Clear();
            _rows.Clear();

            foreach (RecipeDefinition recipe in _playerCrafting.KnownRecipes)
            {
                if (recipe == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("recipe-row");

                var info = new VisualElement();
                info.AddToClassList("recipe-row__info");

                var nameLabel = new Label(recipe.DisplayName);
                nameLabel.AddToClassList("recipe-row__name");
                info.Add(nameLabel);

                var costLabel = new Label();
                costLabel.AddToClassList("recipe-row__cost");
                info.Add(costLabel);

                row.Add(info);

                var craftButton = new Button { text = "Craft" };
                craftButton.AddToClassList("recipe-row__craft-button");
                craftButton.clicked += () => HandleCraftClicked(recipe);
                row.Add(craftButton);

                _recipeContainer.Add(row);
                _rows.Add(new RecipeRow { Recipe = recipe, CostLabel = costLabel, CraftButton = craftButton });
            }
        }

        private void HandleCraftClicked(RecipeDefinition recipe)
        {
            _playerCrafting.TryCraft(recipe);
            // TryCraft mutates the inventory via Inventory.AddItem/RemoveItem, whose Changed
            // event already triggers RefreshAffordability - no explicit refresh needed here.
        }

        private void RefreshAffordability()
        {
            foreach (RecipeRow row in _rows)
            {
                bool canCraft = row.Recipe.CanCraft(_playerInventory.Inventory);
                row.CraftButton.SetEnabled(canCraft);
                row.CostLabel.text = BuildCostText(row.Recipe);
            }
        }

        private string BuildCostText(RecipeDefinition recipe)
        {
            var parts = new List<string>();
            foreach (RecipeIngredient ingredient in recipe.Ingredients)
            {
                if (ingredient.Item == null)
                {
                    continue;
                }

                int have = _playerInventory.Inventory.GetTotalQuantity(ingredient.Item);
                parts.Add($"{ingredient.Item.DisplayName} {have}/{ingredient.Amount}");
            }

            return string.Join("   ", parts);
        }
    }
}
