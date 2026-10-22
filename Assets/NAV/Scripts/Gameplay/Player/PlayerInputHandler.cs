using System;
using UnityEngine;
using UnityEngine.InputSystem;
using NAV.Core.Settings;

namespace NAV.Gameplay.Player
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInputHandler : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintHeld { get; private set; }

        /// <summary>Held-block state for Combat (PlayerCombat), same "read continuously in
        /// Update, zeroed while a modal panel has focus" treatment as SprintHeld - not an
        /// event, since blocking is a hold, not a one-shot action.</summary>
        public bool BlockHeld { get; private set; }

        private int _menuOpenRequests;

        /// <summary>
        /// While true, a modal UI (e.g. the inventory or crafting panel) has focus: Look
        /// reads zero (camera stays still while the mouse drives the UI instead) and
        /// Jump/Interact stop firing. Move/Sprint keep working - the player can still
        /// walk/run around with a panel open. ToggleInventory/ToggleCrafting themselves are
        /// unaffected either way.
        ///
        /// Backed by a request count, not a single flag, so multiple panels can each
        /// request/release it independently - Inventory and Crafting are no longer mutually
        /// exclusive (both can be open at once, side by side), so at any time 0, 1, or 2
        /// requests may be outstanding. This is also why cursor lock/visibility live here
        /// instead of in each panel controller: whichever panel closes last must be the one
        /// that re-locks the cursor, not whichever happens to close first.
        /// </summary>
        public bool MenuOpen => _menuOpenRequests > 0;

        public void SetMenuOpen(bool open)
        {
            int previousRequests = _menuOpenRequests;
            _menuOpenRequests = open ? _menuOpenRequests + 1 : Mathf.Max(0, _menuOpenRequests - 1);

            if (previousRequests == 0 && _menuOpenRequests > 0)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (previousRequests > 0 && _menuOpenRequests == 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public event Action JumpRequested;
        public event Action InteractPerformed;
        public event Action ToggleInventoryPerformed;
        public event Action ToggleCraftingPerformed;
        public event Action PausePerformed;

        /// <summary>Primary-click action. Consumed by both PlayerBuilding (place ghost, while
        /// build mode is active) and PlayerCombat (melee attack, otherwise) - matching how a
        /// game's LMB does whatever the currently active tool/mode says it does. PlayerCombat
        /// itself checks PlayerBuilding.IsBuildModeActive to stay out of the way while
        /// building.</summary>
        public event Action AttackPerformed;

        /// <summary>Secondary-click (RMB, the Block action) pressed - the one-shot counterpart of
        /// BlockHeld. PlayerBuilding uses it to enter/leave build mode while the hammer is held
        /// (PlayerCombat doesn't block then). Suppressed while a menu is open.</summary>
        public event Action SecondaryPerformed;

        /// <summary>The old B-key build toggle. No longer used - build mode is entered with the
        /// hammer (RMB), see PlayerBuilding. Kept only because the action still exists.</summary>
        public event Action ToggleBuildPerformed;
        public event Action RotatePiecePerformed;
        public event Action CycleNextPerformed;
        public event Action CyclePreviousPerformed;

        /// <summary>Hotbar key pressed (keys 1-6), with a 0-based slot index - PlayerEquipment
        /// equips/unequips the item in that inventory slot. Fires even with a panel open (the
        /// keys aren't used by any UI).</summary>
        public event Action<int> HotbarSlotPerformed;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _interactAction;
        private InputAction _toggleInventoryAction;
        private InputAction _toggleCraftingAction;
        private InputAction _pauseAction;
        private InputAction _attackAction;
        private InputAction _toggleBuildAction;
        private InputAction _rotatePieceAction;
        private InputAction _nextAction;
        private InputAction _previousAction;
        private InputAction _blockAction;
        private InputAction _hotbarSlotAction;
        private InputActionAsset _actions;

        private void HandleSettingsChanged()
        {
            GameSettings.ApplyBindingOverrides(_actions);
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= HandleSettingsChanged;
        }

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
            _pauseAction = playerInput.actions["Player/Pause"];
            _attackAction = playerInput.actions["Player/Attack"];
            _toggleBuildAction = playerInput.actions["Player/ToggleBuild"];
            _rotatePieceAction = playerInput.actions["Player/RotatePiece"];
            _nextAction = playerInput.actions["Player/Next"];
            _previousAction = playerInput.actions["Player/Previous"];
            _blockAction = playerInput.actions["Player/Block"];
            _hotbarSlotAction = playerInput.actions["Player/HotbarSlot"];

            // The player's rebound keys (Settings window), now and whenever they change.
            _actions = playerInput.actions;
            GameSettings.ApplyBindingOverrides(_actions);
            GameSettings.Changed += HandleSettingsChanged;

            if (_moveAction == null || _lookAction == null || _sprintAction == null || _jumpAction == null || _interactAction == null || _toggleInventoryAction == null || _toggleCraftingAction == null
                || _attackAction == null || _toggleBuildAction == null || _rotatePieceAction == null || _nextAction == null || _previousAction == null || _pauseAction == null || _blockAction == null || _hotbarSlotAction == null)
            {
                Debug.LogError($"{nameof(PlayerInputHandler)} on '{name}' could not find one or more required actions (Move/Look/Sprint/Jump/Interact/ToggleInventory/ToggleCrafting/Attack/ToggleBuild/RotatePiece/Next/Previous/Pause/Block) in the 'Player' action map.", this);
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

            if (_pauseAction != null)
            {
                _pauseAction.performed += HandlePausePerformed;
            }

            if (_attackAction != null)
            {
                _attackAction.performed += HandleAttackPerformed;
            }

            if (_blockAction != null)
            {
                _blockAction.performed += HandleSecondaryPerformed;
            }

            if (_toggleBuildAction != null)
            {
                _toggleBuildAction.performed += HandleToggleBuildPerformed;
            }

            if (_rotatePieceAction != null)
            {
                _rotatePieceAction.performed += HandleRotatePiecePerformed;
            }

            if (_nextAction != null)
            {
                _nextAction.performed += HandleCycleNextPerformed;
            }

            if (_previousAction != null)
            {
                _previousAction.performed += HandleCyclePreviousPerformed;
            }

            if (_hotbarSlotAction != null)
            {
                _hotbarSlotAction.performed += HandleHotbarSlotPerformed;
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

            if (_pauseAction != null)
            {
                _pauseAction.performed -= HandlePausePerformed;
            }

            if (_attackAction != null)
            {
                _attackAction.performed -= HandleAttackPerformed;
            }

            if (_blockAction != null)
            {
                _blockAction.performed -= HandleSecondaryPerformed;
            }

            if (_toggleBuildAction != null)
            {
                _toggleBuildAction.performed -= HandleToggleBuildPerformed;
            }

            if (_rotatePieceAction != null)
            {
                _rotatePieceAction.performed -= HandleRotatePiecePerformed;
            }

            if (_nextAction != null)
            {
                _nextAction.performed -= HandleCycleNextPerformed;
            }

            if (_previousAction != null)
            {
                _previousAction.performed -= HandleCyclePreviousPerformed;
            }

            if (_hotbarSlotAction != null)
            {
                _hotbarSlotAction.performed -= HandleHotbarSlotPerformed;
            }
        }

        private void Update()
        {
            MoveInput = _moveAction.ReadValue<Vector2>();
            LookInput = MenuOpen ? Vector2.zero : _lookAction.ReadValue<Vector2>();
            SprintHeld = !MenuOpen && _sprintAction.IsPressed();
            BlockHeld = !MenuOpen && _blockAction.IsPressed();
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

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            PausePerformed?.Invoke();
        }

        private void HandleAttackPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            AttackPerformed?.Invoke();
        }

        private void HandleSecondaryPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            SecondaryPerformed?.Invoke();
        }

        private void HandleToggleBuildPerformed(InputAction.CallbackContext context)
        {
            ToggleBuildPerformed?.Invoke();
        }

        private void HandleRotatePiecePerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            RotatePiecePerformed?.Invoke();
        }

        private void HandleCycleNextPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            CycleNextPerformed?.Invoke();
        }

        private void HandleCyclePreviousPerformed(InputAction.CallbackContext context)
        {
            if (MenuOpen)
            {
                return;
            }

            CyclePreviousPerformed?.Invoke();
        }

        private void HandleHotbarSlotPerformed(InputAction.CallbackContext context)
        {
            // The action's bindings are the slots in order (1-6 by default) - read which binding
            // fired rather than the key's name, so a rebound slot (say, Z) still maps right.
            int bindingIndex = context.action.GetBindingIndexForControl(context.control);
            if (bindingIndex >= 0)
            {
                HotbarSlotPerformed?.Invoke(bindingIndex);
            }
        }
    }
}
