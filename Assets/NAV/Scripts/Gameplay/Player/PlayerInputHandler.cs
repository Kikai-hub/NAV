using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NAV.Gameplay.Player
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }

        private int _menuOpenRequests;

        /// <summary>
        /// While true, a modal UI (e.g. the inventory or crafting panel) has focus: Look
        /// reads zero (camera stays still while the mouse drives the UI instead) and
        /// Jump/Interact stop firing. Move/Sprint keep working - the player can still
        /// walk/run around with a panel open. ToggleInventory/ToggleCrafting themselves are
        /// unaffected either way.
        ///
        /// Backed by a request count, not a single flag, so multiple panels can each
        /// request/release it independently - e.g. if two panels were ever open at once,
        /// closing one must not clear MenuOpen while the other is still open. (In practice
        /// InventoryUIController/CraftingUIController also close each other on open, so at
        /// most one request is outstanding at a time today, but the count is what makes that
        /// not load-bearing for correctness.)
        /// </summary>
        public bool MenuOpen => _menuOpenRequests > 0;

        public void SetMenuOpen(bool open)
        {
            _menuOpenRequests = open ? _menuOpenRequests + 1 : Mathf.Max(0, _menuOpenRequests - 1);
        }

        public event Action JumpRequested;
        public event Action InteractPerformed;
        public event Action ToggleInventoryPerformed;
        public event Action ToggleCraftingPerformed;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _toggleInventoryAction;
        private InputAction _toggleCraftingAction;

        private void Awake()
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput.actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputHandler)} on '{name}' requires an Input Actions asset assigned to its PlayerInput component.", this);
                enabled = false;
                return;
            }

            _moveAction = playerInput.actions["Player/Move"];
            _lookAction = playerInput.actions["Player/Look"];
            _sprintAction = playerInput.actions["Player/Sprint"];
            _jumpAction = playerInput.actions["Player/Jump"];
            _interactAction = playerInput.actions["Player/Interact"];
            _toggleInventoryAction = playerInput.actions["Player/ToggleInventory"];
            _toggleCraftingAction = playerInput.actions["Player/ToggleCrafting"];

            if (_moveAction == null || _lookAction == null || _sprintAction == null || _jumpAction == null || _interactAction == null || _toggleInventoryAction == null || _toggleCraftingAction == null)
            {
                Debug.LogError($"{nameof(PlayerInputHandler)} on '{name}' could not find one or more required actions (Move/Look/Sprint/Jump/Interact/ToggleInventory/ToggleCrafting) in the 'Player' action map.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed += HandleJumpPerformed;
            }

            if (_interactAction != null)
            {
                _interactAction.performed += HandleInteractPerformed;
            }

            if (_toggleInventoryAction != null)
            {
                _toggleInventoryAction.performed += HandleToggleInventoryPerformed;
            }

            if (_toggleCraftingAction != null)
            {
                _toggleCraftingAction.performed += HandleToggleCraftingPerformed;
            }
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= HandleJumpPerformed;
            }

            if (_interactAction != null)
            {
                _interactAction.performed -= HandleInteractPerformed;
            }

            if (_toggleInventoryAction != null)
            {
                _toggleInventoryAction.performed -= HandleToggleInventoryPerformed;
            }

            if (_toggleCraftingAction != null)
            {
                _toggleCraftingAction.performed -= HandleToggleCraftingPerformed;
            }
        }

        private void Update()
        {
            MoveInput = _moveAction.ReadValue<Vector2>();
            LookInput = MenuOpen ? Vector2.zero : _lookAction.ReadValue<Vector2>();
            SprintHeld = !MenuOpen && _sprintAction.IsPressed();
        }

        private void HandleJumpPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            JumpRequested?.Invoke();
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            InteractPerformed?.Invoke();
        }

        private void HandleToggleInventoryPerformed(InputAction.CallbackContext context)
        {
            ToggleInventoryPerformed?.Invoke();
        }

        private void HandleToggleCraftingPerformed(InputAction.CallbackContext context)
        {
            ToggleCraftingPerformed?.Invoke();
        }
    }
}
