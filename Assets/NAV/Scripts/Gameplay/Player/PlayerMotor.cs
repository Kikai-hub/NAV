using UnityEngine;
using NAV.Gameplay.Inventory;

namespace NAV.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerMovementStats _stats;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private PlayerInventory _inventory;

        public bool IsGrounded { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsOverloaded { get; private set; }

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _rotationVelocity;
        private bool _jumpQueued;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (_inputHandler == null || _stats == null || _stamina == null || _cameraTransform == null || _inventory == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} on '{name}' is missing a required reference (InputHandler/Stats/Stamina/CameraTransform/Inventory).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.JumpRequested += HandleJumpRequested;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.JumpRequested -= HandleJumpRequested;
            }
        }

        private void HandleJumpRequested()
        {
            _jumpQueued = true;
        }

        private void Update()
        {
            IsGrounded = _controller.isGrounded;

            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            Vector2 moveInput = _inputHandler.MoveInput;
            Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            bool hasMoveInput = moveDirection.sqrMagnitude > 0.0001f;
            IsOverloaded = _inventory.Inventory != null && _inventory.Inventory.IsOverloaded;
            IsSprinting = _stamina.TickSprint(_inputHandler.SprintHeld, hasMoveInput, IsOverloaded, Time.deltaTime);

            float speedMultiplier = IsSprinting ? _stats.SprintSpeedMultiplier : 1f;
            if (IsOverloaded)
            {
                speedMultiplier *= _stats.OverloadSpeedMultiplier;
            }

            float targetSpeed = _stats.WalkSpeed * speedMultiplier;

            // Body yaw always tracks the camera's yaw (never the raw move-input direction).
            // The camera is independent of the player (mouse-controlled, follows position only),
            // so this keeps the character's facing decoupled from movement: strafing moves
            // sideways instead of snapping the body to face the strafe direction.
            float targetYaw = _cameraTransform.eulerAngles.y;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _rotationVelocity, _stats.RotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (IsGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _stats.GroundedGravityValue;
            }

            if (_jumpQueued)
            {
                if (IsGrounded)
                {
                    _verticalVelocity = Mathf.Sqrt(-2f * _stats.Gravity * _stats.JumpHeight);
                }
                _jumpQueued = false;
            }

            _verticalVelocity += _stats.Gravity * Time.deltaTime;

            Vector3 velocity = moveDirection * targetSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            CurrentSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        }
    }
}
