using UnityEngine;
using NAV.Gameplay.Environment;
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
        [Tooltip("How quickly the body turns to the camera's direction for an attack/block (seconds) - much snappier than the movement turn, so the swing goes where the player looks.")]
        [SerializeField, Min(0.01f)] private float _aimTurnSmoothTime = 0.05f;

        [Header("Water")]
        [Tooltip("Feet this deep under the surface (m) = swimming: the body floats with the head above water.")]
        [SerializeField, Min(0.3f)] private float _swimDepth = 1.1f;
        [SerializeField, Range(0.1f, 1f)] private float _swimSpeedMultiplier = 0.55f;
        [Tooltip("Speed lost wading, at the deepest point before swimming (0.4 = 40% slower).")]
        [SerializeField, Range(0f, 0.9f)] private float _wadeSlowdown = 0.4f;
        [Tooltip("How hard the water pulls a swimmer back to floating height (1/s).")]
        [SerializeField, Min(0.5f)] private float _buoyancy = 4f;
        [Tooltip("Jump out of the water (m/s up) - to climb onto a bank.")]
        [SerializeField, Min(0f)] private float _swimJumpSpeed = 3.5f;

        public bool IsGrounded { get; private set; }

        /// <summary>Floating in deep water (see Swim Depth).</summary>
        public bool IsSwimming { get; private set; }

        /// <summary>How deep the feet are under the water surface (0 out of water).</summary>
        public float WaterDepth { get; private set; }

        // Leaving the swim takes a little shallower water than entering it, so standing right at
        // the threshold doesn't flicker between wading and swimming.
        private const float SwimExitHysteresis = 0.2f;
        private const float FloatBobHeight = 0.04f;
        // m/s² the water changes a swimmer's vertical speed by.
        private const float WaterDrag = 15f;
        private const float WaterEntryDrag = 40f;
        public float CurrentSpeed { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsOverloaded { get; private set; }

        /// <summary>World-space move direction this frame (camera-relative input, length 0..1) -
        /// what the locomotion animation reads in the body's own space.</summary>
        public Vector3 MoveDirection { get; private set; }

        /// <summary>True while the body is held facing the camera's direction (FaceAim - an
        /// attack or a raised block).</summary>
        public bool IsFacingAim => Time.time < _faceAimUntil;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _rotationVelocity;
        private bool _jumpQueued;
        private float _faceAimUntil;

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

        /// <summary>Turns the body to the camera's direction and keeps it there for seconds (an
        /// attack, a raised block). Calls extend, never shorten, the current hold.</summary>
        public void FaceAim(float seconds)
        {
            _faceAimUntil = Mathf.Max(_faceAimUntil, Time.time + seconds);
        }

        /// <summary>Moves the player directly to a position/rotation (e.g. PlayerDeath's
        /// respawn) - CharacterController fights a plain transform.position set while enabled,
        /// so this briefly disables it around the move. Also zeroes vertical velocity so the
        /// player doesn't immediately fall through/launch off the respawn point using
        /// whatever gravity speed they had when they died.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _verticalVelocity = 0f;
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

            bool hasWater = WaterBody.TryGetSurface(transform.position, out float waterSurface);
            WaterDepth = hasWater ? Mathf.Max(0f, waterSurface - transform.position.y) : 0f;
            IsSwimming = WaterDepth >= (IsSwimming ? _swimDepth - SwimExitHysteresis : _swimDepth);

            if (IsSwimming)
            {
                speedMultiplier *= _swimSpeedMultiplier;
            }
            else if (WaterDepth > 0f)
            {
                speedMultiplier *= 1f - _wadeSlowdown * Mathf.Clamp01(WaterDepth / _swimDepth);
            }

            float targetSpeed = _stats.WalkSpeed * speedMultiplier;

            MoveDirection = moveDirection;

            // Valheim-style facing: the camera orbits freely (mouse, follows position only) and
            // the body doesn't follow it. Moving turns the body toward where it's going; standing
            // still leaves it as it is (the head looks toward the camera - PlayerAnimator); an
            // attack or a raised block (FaceAim) snaps it to the camera's direction so the swing
            // goes where the player looks.
            bool hasTarget = true;
            float targetYaw = 0f;
            float smoothTime = _stats.RotationSmoothTime;
            if (IsFacingAim)
            {
                targetYaw = _cameraTransform.eulerAngles.y;
                smoothTime = _aimTurnSmoothTime;
            }
            else if (hasMoveInput)
            {
                targetYaw = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            }
            else
            {
                hasTarget = false;
                _rotationVelocity = 0f;
            }

            if (hasTarget)
            {
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref _rotationVelocity, smoothTime);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (IsSwimming)
            {
                // Float: spring back to the height where the head is above water, with a slight
                // bob; a jump pushes up (onto a bank) and the water slows it again.
                float floatHeight = waterSurface - _swimDepth + Mathf.Sin(Time.time * 1.6f) * FloatBobHeight;
                float pull = Mathf.Clamp((floatHeight - transform.position.y) * _buoyancy, -3f, 2f);
                // Eased, not snapped: a jump into deep water dips in (braked hard - about a metre,
                // not to the bottom) and bobs back up.
                float drag = _verticalVelocity < pull ? WaterEntryDrag : WaterDrag;
                _verticalVelocity = Mathf.MoveTowards(_verticalVelocity, pull, drag * Time.deltaTime);
                if (_jumpQueued)
                {
                    _verticalVelocity = _swimJumpSpeed;
                    _jumpQueued = false;
                }
            }
            else
            {
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
            }

            Vector3 velocity = moveDirection * targetSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            CurrentSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        }
    }
}
