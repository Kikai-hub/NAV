using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Drives the player's Animator from movement state that already exists elsewhere -
    /// PlayerInputHandler.MoveInput and PlayerMotor.IsSprinting/IsGrounded. Doesn't own or
    /// duplicate any movement logic, purely reads it and feeds the Animator (Presentation
    /// observes Gameplay state, per ARCHITECTURE_v0.1.md, generalized from UI to animation).
    ///
    /// MoveInput can be fed into the Animator's MoveX/MoveY directly with no extra transform:
    /// PlayerMotor locks the body's yaw to the camera's yaw every single frame (see
    /// PlayerMotor.Update), so the character's local forward/right axes always match the
    /// camera's - raw WASD input (camera-relative by construction) IS already
    /// character-local-space input. This is what makes an 8-directional strafe locomotion
    /// blend tree correct here: Left/Right/Backward input plays a strafe/backward animation
    /// while the body keeps facing the camera direction, instead of the character turning to
    /// face movement direction.
    ///
    /// MoveX/MoveY are pushed through Animator.SetFloat's damped overload (not a raw snap) so
    /// the blend tree eases between directions instead of jumping - this is what keeps a sudden
    /// direction change (e.g. releasing W and tapping S) reading as a smooth blend rather than
    /// a pop.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerMotor _motor;

        [Header("Smoothing")]
        [SerializeField] private float _moveDampTime = 0.15f;

        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SprintingHash = Animator.StringToHash("IsSprinting");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");

        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_inputHandler == null || _motor == null)
            {
                Debug.LogError($"{nameof(PlayerAnimator)} on '{name}' is missing a required reference (InputHandler/Motor).", this);
                enabled = false;
            }
        }

        private void Update()
        {
            Vector2 move = _inputHandler.MoveInput;
            _animator.SetFloat(MoveXHash, move.x, _moveDampTime, Time.deltaTime);
            _animator.SetFloat(MoveYHash, move.y, _moveDampTime, Time.deltaTime);
            _animator.SetBool(SprintingHash, _motor.IsSprinting);
            _animator.SetBool(GroundedHash, _motor.IsGrounded);
        }
    }
}
