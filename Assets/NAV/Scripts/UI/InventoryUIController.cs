using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Observes PlayerInventory and renders its slots as a UI Toolkit panel.
    /// Owns no gameplay state - purely a view over Inventory (see ARCHITECTURE_v0.1.md's
    /// "UI observes gameplay state" rule).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUIController : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _slotContainer;
        private readonly List<VisualElement> _slotElements = new();
        private bool _visible;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_playerInventory == null || _inputHandler == null)
            {
                Debug.LogError($"{nameof(InventoryUIController)} on '{name}' is missing a required reference (PlayerInventory/InputHandler).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            // Everything here (not Awake/OnEnable) because it depends on state other
            // objects create in their own Awake/OnEnable - UIDocument builds its
            // rootVisualElement in OnEnable, and PlayerInventory creates Inventory in
            // Awake. Neither is guaranteed to have run yet at this object's Awake/OnEnable,
            // but Start is guaranteed to run only after every object's Awake+OnEnable has.
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(InventoryUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("inventory-root");
            _slotContainer = documentRoot.Q<VisualElement>("slot-container");

            if (_root == null || _slotContainer == null)
            {
                Debug.LogError($"{nameof(InventoryUIController)} on '{name}' could not find 'inventory-root'/'slot-container' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _playerInventory.Inventory.Changed += RefreshSlots;

            BuildSlots();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleInventoryPerformed += HandleToggleInventoryPerformed;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.ToggleInventoryPerformed -= HandleToggleInventoryPerformed;
            }
        }

        private void OnDestroy()
        {
            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= RefreshSlots;
            }
        }

        private void HandleToggleInventoryPerformed()
        {
            SetVisible(!_visible);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;

            if (visible)
            {
                RefreshSlots();
            }
        }

        private void BuildSlots()
        {
            _slotContainer.Clear();
            _slotElements.Clear();

            int capacity = _playerInventory.Inventory.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("inventory-slot");

                var icon = new VisualElement();
                icon.AddToClassList("inventory-slot__icon");
                slot.Add(icon);

                var quantity = new Label();
                quantity.AddToClassList("inventory-slot__quantity");
                slot.Add(quantity);

                _slotContainer.Add(slot);
                _slotElements.Add(slot);
            }
        }

        private void RefreshSlots()
        {
            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;

            for (int i = 0; i < _slotElements.Count && i < slots.Count; i++)
            {
                VisualElement slotElement = _slotElements[i];
                VisualElement icon = slotElement.Q<VisualElement>(className: "inventory-slot__icon");
                Label quantity = slotElement.Q<Label>(className: "inventory-slot__quantity");

                ItemStack stack = slots[i];
                if (stack.IsEmpty)
                {
                    icon.style.backgroundImage = new StyleBackground();
                    quantity.text = string.Empty;
                }
                else
                {
                    icon.style.backgroundImage = new StyleBackground(stack.Definition.Icon);
                    quantity.text = stack.Quantity > 1 ? stack.Quantity.ToString() : string.Empty;
                }
            }
        }
    }
}
