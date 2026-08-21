using UnityEngine;

namespace NAV.Gameplay.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementStats", menuName = "NAV/Player/Movement Stats")]
    public class PlayerMovementStats : ScriptableObject
    {
        [Header("Speed")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeedMultiplier = 1.6f;

        [Header("Jump & Gravity")]
        [SerializeField] private float _jumpHeight = 1.2f;
        [SerializeField] private float _gravity = -20f;
        [SerializeField] private float _groundedGravityValue = -2f;

        [Header("Rotation")]
        [SerializeField] private float _rotationSmoothTime = 0.1f;

        public float WalkSpeed => _walkSpeed;
        public float SprintSpeedMultiplier => _sprintSpeedMultiplier;
        public float JumpHeight => _jumpHeight;
        public float Gravity => _gravity;
        public float GroundedGravityValue => _groundedGravityValue;
        public float RotationSmoothTime => _rotationSmoothTime;

        private void OnValidate()
        {
            _walkSpeed = Mathf.Max(0.1f, _walkSpeed);
            _sprintSpeedMultiplier = Mathf.Max(1f, _sprintSpeedMultiplier);
            _jumpHeight = Mathf.Max(0.1f, _jumpHeight);
            _gravity = Mathf.Min(-0.1f, _gravity);
            _rotationSmoothTime = Mathf.Max(0.01f, _rotationSmoothTime);
        }
    }
}
