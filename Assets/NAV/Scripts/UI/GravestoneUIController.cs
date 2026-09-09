using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Opens whenever the player interacts with a Gravestone (PlayerInteractor.Interacted),
    /// rendering that gravestone's Contents with the same slot-grid look as
    /// InventoryUIController - a distinct "gravestone-slot" class, though, since both panels
    /// share one PanelSettings/runtime panel and therefore one hit-test space (see the drag
    /// code below). Opening always force-shows the player's Inventory panel alongside it (the
    /// two are meant to be used together, side by side, per the developer's request).
    /// This panel has no close button/key of its own - a second E press can never reach it
    /// (PlayerInputHandler suppresses Interact entirely while MenuOpen, and opening this panel
    /// always makes MenuOpen true via Inventory.Show()) - it closes itself only when Inventory
    /// closes (the ToggleInventory key, I, is never suppressed) or once its gravestone is fully
    /// looted, whichever happens first.
    ///
    /// LMB drag moves one stack out into the player's Inventory grid, via the same
    /// Inventory.MoveSlotTo used for in-panel drag - just called across two different
    /// Inventory instances instead of one. Move All empties every remaining stack in one
    /// click via the same AddItem/RemoveFromSlot API any other item transfer already uses.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GravestoneUIController : MonoBehaviour
    {
        private const float DragGhostHalfSize = 20f;
        private const string PlayerSlotClass = "inventory-slot";
        private const string GravestoneSlotClass = "gravestone-slot";

        [SerializeField] private PlayerInteractor _playerInteractor;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private InventoryUIController _inventoryUIController;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _slotContainer;
        private Button _moveAllButton;
        private readonly List<VisualElement> _slotElements = new();
        private bool _visible;

        private Gravestone _current;

        private VisualElement _dragGhost;
        private int _dragSourceIndex = -1;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_playerInteractor == null || _playerInventory == null || _inventoryUIController == null)
            {
                Debug.LogError($"{nameof(GravestoneUIController)} on '{name}' is missing a required reference (PlayerInteractor/PlayerInventory/InventoryUIController).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(GravestoneUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("gravestone-root");
            _slotContainer = documentRoot.Q<VisualElement>("slot-container");
            _moveAllButton = documentRoot.Q<Button>("move-all-button");

            if (_root == null || _slotContainer == null || _moveAllButton == null)
            {
                Debug.LogError($"{nameof(GravestoneUIController)} on '{name}' could not find 'gravestone-root'/'slot-container'/'move-all-button' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _moveAllButton.clicked += HandleMoveAllClicked;
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_playerInteractor != null)
            {
                _playerInteractor.Interacted += HandleInteracted;
            }

            if (_inventoryUIController != null)
            {
                _inventoryUIController.VisibilityChanged += HandleInventoryVisibilityChanged;
            }
        }

        private void OnDisable()
        {
            if (_playerInteractor != null)
            {
                _playerInteractor.Interacted -= HandleInteracted;
            }

            if (_inventoryUIController != null)
            {
                _inventoryUIController.VisibilityChanged -= HandleInventoryVisibilityChanged;
            }

            UnsubscribeCurrent();
        }

        private void HandleInteracted(IInteractable interactable)
        {
            // Only ever opens, never toggles closed by a second E press: PlayerInputHandler
            // suppresses InteractPerformed entirely while MenuOpen is true, and opening this
            // panel always forces Inventory (hence MenuOpen) open - so once open, E can no
            // longer reach here at all. Closing goes through the Inventory toggle key (I)
            // instead, propagated by HandleInventoryVisibilityChanged below.
            if (interactable is Gravestone gravestone)
            {
                OpenGravestone(gravestone);
            }
        }

        private void HandleInventoryVisibilityChanged(bool visible)
        {
            if (!visible && _visible)
            {
                SetVisible(false);
            }
        }

        private void OpenGravestone(Gravestone gravestone)
        {
            UnsubscribeCurrent();
            _current = gravestone;
            _current.Contents.Changed += RefreshSlots;

            BuildSlots();
            _inventoryUIController.Show();
            SetVisible(true);
        }

        private void UnsubscribeCurrent()
        {
            if (_current != null)
            {
                _current.Contents.Changed -= RefreshSlots;
            }

            _current = null;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (!visible)
            {
                CancelDrag();
                UnsubscribeCurrent();
            }
        }

        private void BuildSlots()
        {
            _slotContainer.Clear();
            _slotElements.Clear();

            int capacity = _current.Contents.Capacity;
            for (int i = 0; i < capacity; i++)
            {
                var slot = new VisualElement { userData = i };
                slot.AddToClassList(GravestoneSlotClass);

                var icon = new VisualElement();
                icon.AddToClassList("gravestone-slot__icon");
                slot.Add(icon);

                var quantity = new Label();
                quantity.AddToClassList("gravestone-slot__quantity");
                slot.Add(quantity);

                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);

                _slotContainer.Add(slot);
                _slotElements.Add(slot);
            }

            RefreshSlots();
        }

        private void RefreshSlots()
        {
            if (_current == null)
            {
                return;
            }

            IReadOnlyList<ItemStack> slots = _current.Contents.Slots;
            for (int i = 0; i < _slotElements.Count && i < slots.Count; i++)
            {
                VisualElement slotElement = _slotElements[i];
                VisualElement icon = slotElement.Q<VisualElement>(className: "gravestone-slot__icon");
                Label quantity = slotElement.Q<Label>(className: "gravestone-slot__quantity");

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

            if (_current.Contents.IsEmpty)
            {
                SetVisible(false);
            }
        }

        private void HandleMoveAllClicked()
        {
            if (_current == null)
            {
                return;
            }

            Inventory contents = _current.Contents;
            IReadOnlyList<ItemStack> slots = contents.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                if (stack.IsEmpty)
                {
                    continue;
                }

                int leftover = _playerInventory.Inventory.AddItem(stack.Definition, stack.Quantity);
                int moved = stack.Quantity - leftover;
                if (moved > 0)
                {
                    contents.RemoveFromSlot(i, moved, out _);
                }
            }
        }

        // --- Drag and drop (LMB): move one stack out into the player's Inventory grid. Unlike
        // InventoryUIController, there's no drop-to-world case - a gravestone's items are only
        // ever recovered into the player's own Inventory, never thrown back into the world. ---

        private void OnSlotPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement slot || _current == null)
            {
                return;
            }

            int index = (int)slot.userData;
            IReadOnlyList<ItemStack> slots = _current.Contents.Slots;
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

            if (sourceIndex < 0 || _current == null)
            {
                return;
            }

            // Shared PanelSettings means every open UIDocument's slots are hit-testable from
            // any one of them - this is what lets a drag that started here find a slot that
            // belongs to InventoryUIController's completely separate UIDocument/GameObject.
            VisualElement picked = _document.rootVisualElement.panel.Pick(evt.position);
            VisualElement targetSlot = FindSlotAncestor(picked, PlayerSlotClass);

            if (targetSlot != null)
            {
                int targetIndex = (int)targetSlot.userData;
                _current.Contents.MoveSlotTo(_playerInventory.Inventory, sourceIndex, targetIndex);
            }
            // Else: dropped on the gravestone's own grid, empty panel space, or the game world -
            // all treated as a cancel.
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

        private static VisualElement FindSlotAncestor(VisualElement element, string slotClassName)
        {
            while (element != null)
            {
                if (element.ClassListContains(slotClassName))
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
