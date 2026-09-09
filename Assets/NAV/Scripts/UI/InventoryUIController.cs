using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Observes PlayerInventory and renders its slots as a UI Toolkit panel, anchored to the
    /// left side of the screen so it can stay open alongside the crafting panel (right side)
    /// without covering the player. Owns no gameplay state - purely a view over Inventory
    /// (see ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule), except for the LMB
    /// drag gesture itself, which only ever calls into Inventory/PlayerInventory's own
    /// public methods (MoveSlot, DropItem) rather than mutating state directly.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class InventoryUIController : MonoBehaviour
    {
        private const float DragGhostHalfSize = 20f;

        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _slotContainer;
        private Label _weightLabel;
        private readonly List<VisualElement> _slotElements = new();
        private bool _visible;

        private VisualElement _dragGhost;
        private int _dragSourceIndex = -1;

        /// <summary>Fires whenever this panel's shown/hidden state actually changes - lets
        /// GravestoneUIController close itself in lockstep when Inventory closes, without
        /// depending on input-event subscription order (see that class's own comment).</summary>
        public event Action<bool> VisibilityChanged;

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
            _weightLabel = documentRoot.Q<Label>("weight-label");

            if (_root == null || _slotContainer == null || _weightLabel == null)
            {
                Debug.LogError($"{nameof(InventoryUIController)} on '{name}' could not find 'inventory-root'/'slot-container'/'weight-label' in its UIDocument's source asset.", this);
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

        /// <summary>Closes this panel if it's open. Safe to call when already closed.</summary>
        public void Hide()
        {
            if (_visible)
            {
                SetVisible(false);
            }
        }

        /// <summary>Opens this panel if it's closed. Safe to call when already open - used by
        /// GravestoneUIController to force Inventory open alongside its own panel without
        /// disturbing an already-in-progress drag if the player had it open already.</summary>
        public void Show()
        {
            if (!_visible)
            {
                SetVisible(true);
            }
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _inputHandler.SetMenuOpen(visible);

            if (visible)
            {
                RefreshSlots();
            }
            else
            {
                CancelDrag();
            }

            VisibilityChanged?.Invoke(visible);
        }

        private void BuildSlots()
        {
            _slotContainer.Clear();
            _slotElements.Clear();

            int capacity = _playerInventory.Inventory.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                var slot = new VisualElement { userData = i };
                slot.AddToClassList("inventory-slot");

                var icon = new VisualElement();
                icon.AddToClassList("inventory-slot__icon");
                slot.Add(icon);

                var quantity = new Label();
                quantity.AddToClassList("inventory-slot__quantity");
                slot.Add(quantity);

                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);

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

            RefreshWeightLabel();
        }

        private void RefreshWeightLabel()
        {
            Inventory inventory = _playerInventory.Inventory;
            bool hasLimit = inventory.MaxWeight > 0f;

            _weightLabel.text = hasLimit
                ? $"Weight: {inventory.TotalWeight:F1}/{inventory.MaxWeight:F0} kg" + (inventory.IsOverloaded ? "  (OVERLOADED)" : string.Empty)
                : $"Weight: {inventory.TotalWeight:F1} kg";

            _weightLabel.EnableInClassList("inventory-weight--overloaded", inventory.IsOverloaded);
        }

        // --- Drag and drop (LMB): move/swap within the grid, or drop outside the panel to
        // throw the item into the world via PlayerInventory.DropItem. ---

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement slot)
            {
                return;
            }

            int index = (int)slot.userData;
            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            if (index < 0 || index >= slots.Count || slots[index].IsEmpty)
            {
                return;
            }

            _dragSourceIndex = index;
            slot.CapturePointer(evt.pointerId);
            slot.RegisterCallback<PointerMoveEvent>(OnSlotPointerMove);
            slot.RegisterCallback<PointerUpEvent>(OnSlotPointerUp);

            _dragGhost = new VisualElement { pickingMode = PickingMode.Ignore };
            _dragGhost.AddToClassList("inventory-drag-ghost");
            _dragGhost.style.backgroundImage = new StyleBackground(slots[index].Definition.Icon);
            _document.rootVisualElement.Add(_dragGhost);
            PositionGhost(evt.position);

            evt.StopPropagation();
        }

        private void OnSlotPointerMove(PointerMoveEvent evt)
        {
            PositionGhost(evt.position);
        }

        private void OnSlotPointerUp(PointerUpEvent evt)
        {
            if (evt.currentTarget is VisualElement slot)
            {
                slot.ReleasePointer(evt.pointerId);
                slot.UnregisterCallback<PointerMoveEvent>(OnSlotPointerMove);
                slot.UnregisterCallback<PointerUpEvent>(OnSlotPointerUp);
            }

            int sourceIndex = _dragSourceIndex;
            _dragSourceIndex = -1;

            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }

            if (sourceIndex < 0)
            {
                return;
            }

            VisualElement picked = _document.rootVisualElement.panel.Pick(evt.position);
            VisualElement targetSlot = FindSlotAncestor(picked);

            if (targetSlot != null)
            {
                int targetIndex = (int)targetSlot.userData;
                if (targetIndex != sourceIndex)
                {
                    _playerInventory.Inventory.MoveSlot(sourceIndex, targetIndex);
                }
            }
            else if (!_root.worldBound.Contains(evt.position))
            {
                // Released outside the whole panel (over the game world) - throw the stack.
                IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
                if (sourceIndex < slots.Count && !slots[sourceIndex].IsEmpty)
                {
                    _playerInventory.DropItem(sourceIndex, slots[sourceIndex].Quantity);
                }
            }
            // Else: released inside the panel but not on a slot (title/padding) - cancel.
        }

        private void CancelDrag()
        {
            _dragSourceIndex = -1;
            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }
        }

        private static VisualElement FindSlotAncestor(VisualElement element)
        {
            while (element != null)
            {
                if (element.ClassListContains("inventory-slot"))
                {
                    return element;
                }

                element = element.parent;
            }

            return null;
        }

        private void PositionGhost(Vector2 position)
        {
            if (_dragGhost == null)
            {
                return;
            }

            _dragGhost.style.left = position.x - DragGhostHalfSize;
            _dragGhost.style.top = position.y - DragGhostHalfSize;
        }
    }
}
