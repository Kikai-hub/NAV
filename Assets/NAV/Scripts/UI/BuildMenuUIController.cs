using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// The hammer's build menu (Valheim-style): while build mode is on, the inventory key opens
    /// this grid instead of the inventory (InventoryUIController stays shut). Slot 1 is the
    /// Demolish tool (hammer icon), then every known building piece. Clicking a slot selects it
    /// in PlayerBuilding (SelectDemolish / SelectPiece) and closes the menu; hovering shows the
    /// piece's name and cost (red when the inventory can't pay it); unaffordable pieces are
    /// dimmed and the current selection is framed. Closes by itself when build mode ends (the
    /// hammer is put away). A view only - all state lives in PlayerBuilding.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BuildMenuUIController : MonoBehaviour
    {
        private const string SlotClass = "build-slot";
        private const string SelectedClass = "build-slot--selected";
        private const string UnaffordableClass = "build-slot--unaffordable";
        private const string DemolishClass = "build-slot--demolish";
        private const string ShortClass = "build-info--short";
        private const int DemolishSlot = -1;

        [SerializeField] private PlayerBuilding _building;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerInventory _playerInventory;
        [Tooltip("Icon of the Demolish tool slot - the hammer's.")]
        [SerializeField] private Sprite _demolishIcon;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _slotContainer;
        private Label _infoLabel;
        private readonly List<VisualElement> _slots = new();
        private bool _visible;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_building == null || _inputHandler == null || _playerInventory == null)
            {
                Debug.LogError($"{nameof(BuildMenuUIController)} on '{name}' is missing a required reference (Building/InputHandler/PlayerInventory).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            _root = documentRoot?.Q<VisualElement>("build-menu-root");
            _slotContainer = documentRoot?.Q<VisualElement>("slot-container");
            _infoLabel = documentRoot?.Q<Label>("info-label");
            if (_root == null || _slotContainer == null || _infoLabel == null)
            {
                Debug.LogError($"{nameof(BuildMenuUIController)} on '{name}' could not find 'build-menu-root'/'slot-container'/'info-label' - check the UIDocument's Panel Settings and Source Asset (BuildMenuPanel.uxml).", this);
                enabled = false;
                return;
            }

            _building.StateChanged += HandleBuildingStateChanged;
            _playerInventory.Inventory.Changed += Refresh;
            BuildSlots();
            _root.style.display = DisplayStyle.None;
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleInventoryPerformed += HandleToggle;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleInventoryPerformed -= HandleToggle;
            }
        }

        private void OnDestroy()
        {
            if (_building != null)
            {
                _building.StateChanged -= HandleBuildingStateChanged;
            }

            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= Refresh;
            }
        }

        private void HandleToggle()
        {
            if (_visible)
            {
                SetVisible(false);
            }
            else if (_building.IsBuildModeActive)
            {
                SetVisible(true);
            }
        }

        private void HandleBuildingStateChanged()
        {
            if (_visible && !_building.IsBuildModeActive)
            {
                SetVisible(false);
            }
            else
            {
                Refresh();
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _inputHandler.SetMenuOpen(visible);
            if (visible)
            {
                Refresh();
                ShowInfo(_building.IsDemolishSelected ? DemolishSlot : _building.SelectedIndex);
            }
        }

        private void BuildSlots()
        {
            _slotContainer.Clear();
            _slots.Clear();

            AddSlot(DemolishSlot, _demolishIcon, "Demolish");
            IReadOnlyList<BuildingPieceDefinition> pieces = _building.KnownPieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                AddSlot(i, pieces[i].Icon, pieces[i].DisplayName);
            }
        }

        private void AddSlot(int index, Sprite icon, string fallbackName)
        {
            var slot = new VisualElement { userData = index };
            slot.AddToClassList(SlotClass);
            if (index == DemolishSlot)
            {
                slot.AddToClassList(DemolishClass);
            }

            if (icon != null)
            {
                var iconElement = new VisualElement();
                iconElement.AddToClassList("build-slot__icon");
                iconElement.style.backgroundImage = new StyleBackground(icon);
                slot.Add(iconElement);
            }
            else
            {
                var label = new Label(fallbackName);
                label.AddToClassList("build-slot__name");
                slot.Add(label);
            }

            slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
            slot.RegisterCallback<PointerEnterEvent>(evt => ShowInfo(index));
            _slotContainer.Add(slot);
            _slots.Add(slot);
        }

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement slot)
            {
                return;
            }

            int index = (int)slot.userData;
            if (index == DemolishSlot)
            {
                _building.SelectDemolish();
            }
            else
            {
                _building.SelectPiece(index);
            }

            SetVisible(false);
        }

        private void Refresh()
        {
            if (!_visible)
            {
                return;
            }

            IReadOnlyList<BuildingPieceDefinition> pieces = _building.KnownPieces;
            foreach (VisualElement slot in _slots)
            {
                int index = (int)slot.userData;
                bool selected = index == DemolishSlot ? _building.IsDemolishSelected
                    : !_building.IsDemolishSelected && index == _building.SelectedIndex;
                slot.EnableInClassList(SelectedClass, selected);
                bool affordable = index == DemolishSlot || pieces[index].CanAfford(_playerInventory.Inventory);
                slot.EnableInClassList(UnaffordableClass, !affordable);
            }
        }

        private void ShowInfo(int index)
        {
            if (index == DemolishSlot)
            {
                _infoLabel.text = "Demolish - LMB removes the piece you aim at; its materials drop on the ground.";
                _infoLabel.RemoveFromClassList(ShortClass);
                return;
            }

            IReadOnlyList<BuildingPieceDefinition> pieces = _building.KnownPieces;
            if (index < 0 || index >= pieces.Count)
            {
                _infoLabel.text = string.Empty;
                return;
            }

            BuildingPieceDefinition piece = pieces[index];
            var text = new StringBuilder(piece.DisplayName).Append(" - ");
            for (int i = 0; i < piece.Cost.Count; i++)
            {
                RecipeIngredient ingredient = piece.Cost[i];
                if (ingredient.Item == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    text.Append(", ");
                }

                int have = _playerInventory.Inventory.GetTotalQuantity(ingredient.Item);
                text.Append(ingredient.Amount).Append(' ').Append(ingredient.Item.DisplayName)
                    .Append(" (").Append(have).Append(')');
            }

            if (piece.RequiresWorkbench)
            {
                text.Append("\nNeeds a workbench nearby");
            }

            _infoLabel.text = text.ToString();
            _infoLabel.EnableInClassList(ShortClass, !piece.CanAfford(_playerInventory.Inventory));
        }
    }
}
