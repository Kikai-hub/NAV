using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Foot IK: puts the animated feet on the actual ground instead of the flat plane the
    /// clips were authored on. Without it the character walked "on air" on slopes - one foot
    /// hovering, soles level whatever the terrain - and hovered by the CharacterController's
    /// skin width everywhere.
    ///
    /// In OnAnimatorIK (needs IK Pass on the Animator's Base Layer - set by NAV > Animation >
    /// Setup Player Combat Layers) each foot casts down from above its animated position; the
    /// foot goal is moved to the hit point plus the sole height, keeping the animation's own
    /// lift during a step, and the sole is turned to the surface normal (less while the foot is
    /// raised). The body (pelvis) is lowered by the lower foot's offset so that leg can reach
    /// down a slope or into a dip without stretching. Everything is smoothed and fades out while
    /// airborne. Presentation only - movement and collision are untouched.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class FootGrounding : MonoBehaviour
    {
        [Tooltip("Optional - fades the IK out while the player is airborne (jumping/falling).")]
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Raycast")]
        [Tooltip("How far above the animated foot the ground ray starts (steps up / uphill).")]
        [SerializeField, Min(0.05f)] private float _rayStartHeight = 0.5f;
        [Tooltip("How far below the animated foot the ray reaches (steps down / downhill).")]
        [SerializeField, Min(0.05f)] private float _rayDepth = 0.6f;

        [Header("Feel")]
        [Tooltip("Largest distance the pelvis may drop to let a leg reach lower ground.")]
        [SerializeField, Min(0f)] private float _maxBodyDrop = 0.45f;
        [Tooltip("Steepest surface (degrees) a sole will align to.")]
        [SerializeField, Range(0f, 60f)] private float _maxFootAngle = 40f;
        [Tooltip("Animated foot lift (m) above which the sole stops aligning to the ground.")]
        [SerializeField, Min(0.01f)] private float _liftForFreeFoot = 0.15f;
        [SerializeField, Min(0f)] private float _footSmoothing = 18f;
        [SerializeField, Min(0f)] private float _bodySmoothing = 10f;
        [SerializeField, Min(0f)] private float _weightSmoothing = 8f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Animator _animator;
        private Transform _ownerRoot;
        private float _bodyOffset;
        private float _weight;
        private float _leftOffset;
        private float _rightOffset;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            // Ignore our own colliders (the CharacterController on the player root).
            _ownerRoot = _motor != null ? _motor.transform : transform.root;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !_animator.isHuman)
            {
                return;
            }

            float dt = Time.deltaTime;
            bool grounded = _motor == null || _motor.IsGrounded;
            _weight = Mathf.MoveTowards(_weight, grounded ? 1f : 0f, _weightSmoothing * dt);

            Vector3 up = transform.up;
            float rootHeight = Vector3.Dot(transform.position, up);

            bool leftHit = SolveFoot(AvatarIKGoal.LeftFoot, _animator.leftFeetBottomHeight, rootHeight, up,
                out Vector3 leftTarget, out Quaternion leftRotation, out float leftRotationWeight);
            bool rightHit = SolveFoot(AvatarIKGoal.RightFoot, _animator.rightFeetBottomHeight, rootHeight, up,
                out Vector3 rightTarget, out Quaternion rightRotation, out float rightRotationWeight);

            // How far each foot has to move along up (negative = down) to reach the ground.
            float leftNeed = leftHit ? Vector3.Dot(leftTarget - _animator.GetIKPosition(AvatarIKGoal.LeftFoot), up) : 0f;
            float rightNeed = rightHit ? Vector3.Dot(rightTarget - _animator.GetIKPosition(AvatarIKGoal.RightFoot), up) : 0f;
            float smoothing = 1f - Mathf.Exp(-_footSmoothing * dt);
            _leftOffset = Mathf.Lerp(_leftOffset, leftNeed, smoothing);
            _rightOffset = Mathf.Lerp(_rightOffset, rightNeed, smoothing);

            // Drop the pelvis by the lower foot so that leg can reach without over-stretching.
            float bodyTarget = Mathf.Clamp(Mathf.Min(_leftOffset, _rightOffset, 0f), -_maxBodyDrop, 0f) * _weight;
            _bodyOffset = Mathf.Lerp(_bodyOffset, bodyTarget, 1f - Mathf.Exp(-_bodySmoothing * dt));
            _animator.bodyPosition += up * _bodyOffset;

            ApplyFoot(AvatarIKGoal.LeftFoot, leftHit, _leftOffset, leftRotation, leftRotationWeight, up);
            ApplyFoot(AvatarIKGoal.RightFoot, rightHit, _rightOffset, rightRotation, rightRotationWeight, up);
        }

        private bool SolveFoot(AvatarIKGoal goal, float soleHeight, float rootHeight, Vector3 up,
            out Vector3 target, out Quaternion rotation, out float rotationWeight)
        {
            Vector3 animated = _animator.GetIKPosition(goal);
            rotation = _animator.GetIKRotation(goal);
            target = animated;
            rotationWeight = 0f;

            // How high the animation itself lifts this foot above the flat plane it was made on.
            float lift = Mathf.Max(0f, Vector3.Dot(animated, up) - rootHeight - soleHeight);

            if (!TryGround(animated, up, out RaycastHit hit))
            {
                return false;
            }

            target = hit.point + up * (soleHeight + lift);

            float slope = Vector3.Angle(hit.normal, up);
            Vector3 normal = slope <= _maxFootAngle ? hit.normal : Vector3.Slerp(up, hit.normal, _maxFootAngle / slope);
            rotation = Quaternion.FromToRotation(up, normal) * rotation;
            rotationWeight = 1f - Mathf.Clamp01(lift / _liftForFreeFoot);
            return true;
        }

        private void ApplyFoot(AvatarIKGoal goal, bool hit, float offset, Quaternion rotation, float rotationWeight, Vector3 up)
        {
            if (!hit)
            {
                _animator.SetIKPositionWeight(goal, 0f);
                _animator.SetIKRotationWeight(goal, 0f);
                return;
            }

            _animator.SetIKPosition(goal, _animator.GetIKPosition(goal) + up * offset);
            _animator.SetIKPositionWeight(goal, _weight);
            _animator.SetIKRotation(goal, rotation);
            _animator.SetIKRotationWeight(goal, _weight * rotationWeight);
        }

        /// <summary>Nearest non-trigger hit below the foot that isn't part of the player.</summary>
        private bool TryGround(Vector3 foot, Vector3 up, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(foot + up * _rayStartHeight, -up, _hits, _rayStartHeight + _rayDepth,
                _groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(_ownerRoot) || _hits[i].distance >= best)
                {
                    continue;
                }

                best = _hits[i].distance;
                ground = _hits[i];
            }

            return best < float.MaxValue;
        }
    }
}
