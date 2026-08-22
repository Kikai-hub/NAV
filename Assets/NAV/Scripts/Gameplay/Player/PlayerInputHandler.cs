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

        /// <summary>
        /// While true, a modal UI (e.g. the inventory panel) has focus: Look reads zero
        /// (camera stays still while the mouse drives the UI instead) and Jump/Interact
        /// stop firing. Move/Sprint keep working - the player can still walk/run around
        /// with the panel open. ToggleInventory itself is unaffected either way. Set by
        /// whatever owns that modal state (see InventoryUIController).
        /// </summary>
        public bool MenuOpen { get; private set; }

        public void SetMenuOpen(bool open)
        {
            MenuOpen = open;
        }

        public event Action JumpRequested;
        public event Action InteractPerformed;
        public event Action ToggleInventoryPerformed;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _toggleInventoryAction;

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

            if (_moveAction == null || _lookAction == null || _sprintAction == null || _jumpAction == null || _interactAction == null || _toggleInventoryAction == null)
            {
                Debug.LogError($"{nameof(PlayerInputHandler)} on '{name}' could not find one or more required actions (Move/Look/Sprint/Jump/Interact/ToggleInventory) in the 'Player' action map.", this);
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
    }
}
