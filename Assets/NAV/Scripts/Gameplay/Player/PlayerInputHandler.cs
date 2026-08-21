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

        public event Action JumpRequested;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;

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

            if (_moveAction == null || _lookAction == null || _sprintAction == null || _jumpAction == null)
            {
                Debug.LogError($"{nameof(PlayerInputHandler)} on '{name}' could not find one or more required actions (Move/Look/Sprint/Jump) in the 'Player' action map.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed += HandleJumpPerformed;
            }
        }

        private void OnDisable()
        {
            if (_jumpAction != null)
            {
                _jumpAction.performed -= HandleJumpPerformed;
            }
        }

        private void Update()
        {
            MoveInput = _moveAction.ReadValue<Vector2>();
            LookInput = _lookAction.ReadValue<Vector2>();
            SprintHeld = _sprintAction.IsPressed();
        }

        private void HandleJumpPerformed(InputAction.CallbackContext context)
        {
            JumpRequested?.Invoke();
        }
    }
}
