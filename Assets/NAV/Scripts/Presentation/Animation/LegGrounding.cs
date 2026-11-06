using System;
using UnityEngine;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Foot IK for a Generic-rig biped (the Leshy) - what FootGrounding does for the humanoid
    /// player, done by hand because a Generic Animator has no IK goals. After the Animator
    /// (LateUpdate), each foot casts down from above its animated position:
    ///  - the whole model is moved up/down so the foot over the LOWER ground lands on it - this
    ///    also takes out the gap between the NavMesh (which the agent stands on) and the real
    ///    ground, so the body never hovers;
    ///  - the other foot is lifted onto its higher ground by a two-bone IK (thigh, shin), the
    ///    animation's own lift during a step kept;
    ///  - a planted sole is turned to the surface normal.
    /// Everything is smoothed. Off while the Animator is off (the death ragdoll has the body).
    /// Presentation only - the agent's movement is untouched.
    /// </summary>
    public class LegGrounding : MonoBehaviour
    {
        [Serializable]
        public class Leg
        {
            public Transform UpperLeg;
            public Transform LowerLeg;
            public Transform Foot;
            [HideInInspector] public float SoleHeight;
            [HideInInspector] public float Offset;
            [NonSerialized] public bool Hit;
            [NonSerialized] public bool Raised;
            [NonSerialized] public RaycastHit Ground;
        }

        [Tooltip("The model's Animator (its object is the model root that gets moved up/down).")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Leg _left = new();
        [SerializeField] private Leg _right = new();
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Raycast")]
        [SerializeField, Min(0.1f)] private float _rayStartHeight = 1.5f;
        [SerializeField, Min(0.1f)] private float _rayDepth = 2f;

        [Header("Feel")]
        [Tooltip("Largest distance (m) the body may drop below the agent to reach lower ground.")]
        [SerializeField, Min(0f)] private float _maxBodyDrop = 0.9f;
        [Tooltip("Largest distance (m) the body may rise above the agent (the NavMesh lies a little off the ground).")]
        [SerializeField, Min(0f)] private float _maxBodyRise = 0.35f;
        [SerializeField, Range(0f, 60f)] private float _maxFootAngle = 35f;
        [Tooltip("Animated foot lift (m) above which the sole stops aligning to the ground.")]
        [SerializeField, Min(0.01f)] private float _liftForFreeFoot = 0.3f;
        [SerializeField, Min(0f)] private float _footSmoothing = 14f;
        [SerializeField, Min(0f)] private float _bodySmoothing = 8f;

        // A foot counts as lifted above RaisedLift and as set down again below PlantedLift.
        private const float RaisedLift = 0.12f;
        private const float PlantedLift = 0.05f;

        /// <summary>A foot was set down on the ground (its position) - footsteps, dust.</summary>
        public event Action<Vector3> FootPlanted;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Transform _model;
        private Transform _owner;
        private float _bodyOffset;
        private bool _ready;

        /// <summary>Setup (LeshyBossSetup): the leg bones; sole heights are read from the bind pose.</summary>
        public void EditorConfigure(Animator animator, Transform[] left, Transform[] right)
        {
            _animator = animator;
            _left = new Leg { UpperLeg = left[0], LowerLeg = left[1], Foot = left[2] };
            _right = new Leg { UpperLeg = right[0], LowerLeg = right[1], Foot = right[2] };
            _left.SoleHeight = left[2].position.y - animator.transform.position.y;
            _right.SoleHeight = right[2].position.y - animator.transform.position.y;
            _groundLayers = ~(1 << LayerMask.NameToLayer("Ragdoll"));
        }

        private void Awake()
        {
            _ready = _animator != null && _left.Foot != null && _right.Foot != null && _left.UpperLeg != null && _right.UpperLeg != null;
            if (!_ready)
            {
                Debug.LogError($"{nameof(LegGrounding)} on '{name}' is missing its Animator or leg bones (re-run the setup).", this);
                enabled = false;
                return;
            }

            _model = _animator.transform;
            _owner = transform.root;
        }

        private void LateUpdate()
        {
            if (!_animator.enabled)
            {
                return;
            }

            float dt = Time.deltaTime;
            Vector3 up = Vector3.up;
            float agentY = _model.parent != null ? _model.parent.position.y : _model.position.y;
            float baseY = _model.position.y;

            // Where each foot wants to be: its ground + sole + the animation's own lift.
            float leftAnim = _left.Foot.position.y - baseY;
            float rightAnim = _right.Foot.position.y - baseY;
            float leftWant = Want(_left, leftAnim);
            float rightWant = Want(_right, rightAnim);

            // The body so the foot over the lower ground lands on it without stretching.
            float target = 0f;
            if (_left.Hit || _right.Hit)
            {
                float leftBase = _left.Hit ? leftWant - leftAnim : float.MaxValue;
                float rightBase = _right.Hit ? rightWant - rightAnim : float.MaxValue;
                target = Mathf.Clamp(Mathf.Min(leftBase, rightBase) - agentY, -_maxBodyDrop, _maxBodyRise);
            }

            _bodyOffset = Mathf.Lerp(_bodyOffset, target, 1f - Mathf.Exp(-_bodySmoothing * dt));
            Vector3 local = _model.localPosition;
            local.y = _bodyOffset;
            _model.localPosition = local;
            baseY = _model.position.y;

            Place(_left, leftWant, baseY + leftAnim, leftAnim, up, dt);
            Place(_right, rightWant, baseY + rightAnim, rightAnim, up, dt);
        }

        private float Want(Leg leg, float animatedHeight)
        {
            leg.Hit = TryGround(leg.Foot.position, out leg.Ground);
            float lift = Mathf.Max(0f, animatedHeight - leg.SoleHeight);
            return leg.Hit ? leg.Ground.point.y + leg.SoleHeight + lift : 0f;
        }

        private void Place(Leg leg, float want, float current, float animatedHeight, Vector3 up, float dt)
        {
            float need = leg.Hit ? Mathf.Max(0f, want - current) : 0f;
            leg.Offset = Mathf.Lerp(leg.Offset, need, 1f - Mathf.Exp(-_footSmoothing * dt));
            Quaternion footRotation = leg.Foot.rotation;
            if (leg.Offset > 0.005f)
            {
                SolveTwoBone(leg.UpperLeg, leg.LowerLeg, leg.Foot, leg.Foot.position + up * leg.Offset);
            }

            leg.Foot.rotation = footRotation;
            if (!leg.Hit)
            {
                return;
            }

            // A planted sole follows the slope; a raised one keeps the animation's angle.
            float lift = Mathf.Max(0f, animatedHeight - leg.SoleHeight);
            if (lift > RaisedLift)
            {
                leg.Raised = true;
            }
            else if (leg.Raised && lift < PlantedLift)
            {
                leg.Raised = false;
                FootPlanted?.Invoke(leg.Ground.point);
            }

            float weight = 1f - Mathf.Clamp01(lift / _liftForFreeFoot);
            float slope = Vector3.Angle(leg.Ground.normal, up);
            Vector3 normal = slope <= _maxFootAngle ? leg.Ground.normal : Vector3.Slerp(up, leg.Ground.normal, _maxFootAngle / slope);
            leg.Foot.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(up, normal), weight) * footRotation;
        }

        /// <summary>Bends the knee so the hip-to-foot distance matches the target's, then turns
        /// the thigh so the foot lands on it - the knee stays in the animation's bend plane.</summary>
        private static void SolveTwoBone(Transform hip, Transform knee, Transform foot, Vector3 target)
        {
            Vector3 a = hip.position;
            Vector3 b = knee.position;
            Vector3 c = foot.position;
            float upper = Vector3.Distance(a, b);
            float lower = Vector3.Distance(b, c);
            float reach = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.01f);

            Vector3 axis = Vector3.Cross(a - b, c - b);
            if (axis.sqrMagnitude > 1e-8f)
            {
                float now = Vector3.Angle(a - b, c - b);
                float cos = (upper * upper + lower * lower - reach * reach) / (2f * upper * lower);
                float wanted = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
                // Rotating the shin about cross(thigh, shin) by + opens the knee.
                knee.rotation = Quaternion.AngleAxis(wanted - now, axis.normalized) * knee.rotation;
            }

            Vector3 toFoot = foot.position - a;
            Vector3 toTarget = target - a;
            if (toFoot.sqrMagnitude > 1e-8f && toTarget.sqrMagnitude > 1e-8f)
            {
                hip.rotation = Quaternion.FromToRotation(toFoot, toTarget) * hip.rotation;
            }
        }

        /// <summary>The nearest solid ground below a point - not this creature, not a
        /// character (the player's capsule), not loose physics objects (items, logs).</summary>
        private bool TryGround(Vector3 point, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * _rayStartHeight, Vector3.down, _hits, _rayStartHeight + _rayDepth,
                _groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (_hits[i].distance >= best || collider.transform.IsChildOf(_owner) || collider is CharacterController
                    || (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic))
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
