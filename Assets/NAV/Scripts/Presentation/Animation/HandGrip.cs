using UnityEngine;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Closes the character's fingers around a held item's grip. The animator's hand layers
    /// (Kevin Iglesias "HandsClosed") were made for a five-fingered rig; on the hero's Mixamo
    /// rig - one finger chain per hand (Index1-3, which carries all four fingers; no thumb) -
    /// the same muscles over-curl that chain back into the wrist, a lump with no room for a
    /// handle, so weapons looked held in the air.
    ///
    /// Here the chain is posed from its rest (open) pose by fixed curl angles about the hand's
    /// own right axis - the axis the grip runs along (HeldItemView's B-handProp sockets sit in
    /// the middle of the ring this makes; NAV > Animation > Setup Hero Hand Grip places them).
    /// The right hand grips whenever the animator's HoldingItem is on, the left one with
    /// HoldingLeftItem or a two-handed / polearm HoldStyle (both hands on the haft). Blended in
    /// and out, applied in LateUpdate after the Animator, so it wins over the hand layers.
    /// Rest rotations are captured from the model asset by the setup (the scene pose can be
    /// any animated frame).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HandGrip : MonoBehaviour
    {
        private static readonly int HoldingItemHash = Animator.StringToHash("HoldingItem");
        private static readonly int HoldingLeftItemHash = Animator.StringToHash("HoldingLeftItem");
        private static readonly int HoldStyleHash = Animator.StringToHash("HoldStyle");

        // WeaponAttackStyle values held with both hands (TwoHanded = 2, Polearm = 3).
        private const int TwoHandedStyle = 2;
        private const int PolearmStyle = 3;

        [Tooltip("Curl (degrees) of the finger chain's first, second and third bone toward the palm. Fitted to the hero: a ring ~2.5 cm round a grip.")]
        [SerializeField] private Vector3 _curl = new(15f, 80f, 95f);
        [Tooltip("How fast the grip closes/opens (weight per second).")]
        [SerializeField, Min(0.1f)] private float _blendSpeed = 10f;

        [Header("Support hand (two-handed weapons, polearms)")]
        [Tooltip("Pull the left hand onto the weapon's haft/handle (two-bone arm IK) - the stance clips were made for another body, so the left fist missed the haft.")]
        [SerializeField] private bool _supportHandIK = true;
        [Tooltip("The right hand's grip socket (B-handProp.R): the weapon's axis runs along its up.")]
        [SerializeField] private Transform _rightSocket;
        [Tooltip("The left hand's grip socket (B-handProp.L): the middle of the left fist's ring.")]
        [SerializeField] private Transform _leftSocket;
        [Tooltip("Least distance between the two fists along the haft (a fist is ~9 cm wide).")]
        [SerializeField, Min(0.05f)] private float _minHandGap = 0.1f;

        [Header("Rest pose (open hand) - filled by the setup from the model")]
        [SerializeField] private Quaternion[] _rightRest = new Quaternion[3];
        [SerializeField] private Quaternion[] _leftRest = new Quaternion[3];

        private Animator _animator;
        private Transform _rightHand;
        private Transform _leftHand;
        private Transform[] _rightChain;
        private Transform[] _leftChain;
        private bool _hasHolding;
        private bool _hasHoldingLeft;
        private bool _hasHoldStyle;
        private float _rightWeight;
        private float _leftWeight;
        private readonly Quaternion[] _animated = new Quaternion[3];
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private bool _twoHanded;
        // The held item's reach along the right socket's up: where the left fist may go.
        private Transform _measuredItem;
        private float _hiltMin;
        private float _hiltMax;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (!_animator.isHuman)
            {
                Debug.LogError($"{nameof(HandGrip)} on '{name}' needs a humanoid Animator.", this);
                enabled = false;
                return;
            }

            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightChain = Chain(HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
            _leftChain = Chain(HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
            if (_rightChain == null || _leftChain == null || _rightRest == null || _rightRest.Length != 3 || _leftRest == null || _leftRest.Length != 3)
            {
                Debug.LogError($"{nameof(HandGrip)} on '{name}': no index finger chain or no rest pose - run NAV > Animation > Setup Hero Hand Grip.", this);
                enabled = false;
                return;
            }

            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (_supportHandIK && (_rightSocket == null || _leftSocket == null))
            {
                Debug.LogWarning($"{nameof(HandGrip)} on '{name}': no hand sockets assigned - the left hand won't be pulled onto two-handed weapons (run NAV > Animation > Setup Hero Hand Grip).", this);
                _supportHandIK = false;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                _hasHolding |= parameter.nameHash == HoldingItemHash;
                _hasHoldingLeft |= parameter.nameHash == HoldingLeftItemHash;
                _hasHoldStyle |= parameter.nameHash == HoldStyleHash;
            }
        }

        private Transform[] Chain(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
        {
            Transform first = _animator.GetBoneTransform(a);
            Transform second = _animator.GetBoneTransform(b);
            Transform third = _animator.GetBoneTransform(c);
            return first != null && second != null && third != null ? new[] { first, second, third } : null;
        }

        private void LateUpdate()
        {
            bool right = _hasHolding && _animator.GetBool(HoldingItemHash);
            int style = _hasHoldStyle ? _animator.GetInteger(HoldStyleHash) : 0;
            bool twoHanded = right && (style == TwoHandedStyle || style == PolearmStyle);
            bool left = (_hasHoldingLeft && _animator.GetBool(HoldingLeftItemHash)) || twoHanded;
            // Keeps reaching for the haft while the grip opens after letting go.
            _twoHanded = twoHanded || (_twoHanded && _leftWeight > 0f);

            float step = _blendSpeed * Time.deltaTime;
            _rightWeight = Mathf.MoveTowards(_rightWeight, right ? 1f : 0f, step);
            _leftWeight = Mathf.MoveTowards(_leftWeight, left ? 1f : 0f, step);
            if (_supportHandIK && _twoHanded && _leftWeight > 0f)
            {
                ReachForHaft(_leftWeight);
            }

            Apply(_rightHand, _rightChain, _rightRest, _rightWeight);
            Apply(_leftHand, _leftChain, _leftRest, _leftWeight);
        }

        /// <summary>Puts the left fist on the weapon's axis (the right socket's up line) where the
        /// animated left hand is nearest to it - kept a fist's width from the right hand, and off
        /// the blade/head (the held MeleeHitbox) and beyond the butt. The fist's ring turns to lie
        /// along the haft (keeping its roll); the arm reaches with two-bone IK.</summary>
        private void ReachForHaft(float weight)
        {
            if (_rightSocket.childCount == 0 || _leftUpperArm == null || _leftLowerArm == null)
            {
                return;
            }

            MeasureHeldItem(_rightSocket.GetChild(0));
            Vector3 origin = _rightSocket.position;
            Vector3 axis = _rightSocket.up;
            float along = Vector3.Dot(_leftSocket.position - origin, axis);
            float position;
            bool roomAbove = _hiltMax >= _minHandGap;
            bool roomBelow = -_hiltMin >= _minHandGap;
            if ((along >= 0f && roomAbove) || (along < 0f && !roomBelow && roomAbove))
            {
                position = Mathf.Clamp(along, _minHandGap, _hiltMax);
            }
            else if (roomBelow)
            {
                position = Mathf.Clamp(along, _hiltMin, -_minHandGap);
            }
            else
            {
                return;
            }

            Vector3 ringTarget = origin + axis * position;
            Quaternion handRotation = _leftHand.rotation;
            Vector3 ringAxis = _leftHand.right;
            Quaternion turn = Quaternion.FromToRotation(ringAxis, Vector3.Dot(ringAxis, axis) >= 0f ? axis : -axis);
            Quaternion targetRotation = turn * handRotation;
            Vector3 ringOffset = turn * (_leftSocket.position - _leftHand.position);
            Vector3 wristTarget = ringTarget - ringOffset;

            wristTarget = Vector3.Lerp(_leftHand.position, wristTarget, weight);
            SolveTwoBone(_leftUpperArm, _leftLowerArm, _leftHand, wristTarget);
            _leftHand.rotation = Quaternion.Slerp(handRotation, targetRotation, weight);
        }

        /// <summary>Once per held item: how far along the right socket's up the left fist may go -
        /// from just above the item's lowest point (butt / pommel) to just short of the striking
        /// part (its MeleeHitbox capsule start, or near the far end).</summary>
        private void MeasureHeldItem(Transform item)
        {
            if (item == _measuredItem)
            {
                return;
            }

            _measuredItem = item;
            float min = 0f;
            float max = 0f;
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds bounds = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? bounds.min.x : bounds.max.x, (i / 2) % 2 == 0 ? bounds.min.y : bounds.max.y, i / 4 == 0 ? bounds.min.z : bounds.max.z);
                    float y = _rightSocket.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
            }

            const float endMargin = 0.05f;
            _hiltMin = min + endMargin;
            _hiltMax = max - 0.1f;
            var hitbox = item.GetComponentInChildren<NAV.Gameplay.Combat.MeleeHitbox>();
            if (hitbox != null)
            {
                hitbox.GetWorldCapsule(out Vector3 start, out Vector3 end, out float radius);
                float strike = Mathf.Min(_rightSocket.InverseTransformPoint(start).y, _rightSocket.InverseTransformPoint(end).y);
                _hiltMax = Mathf.Min(_hiltMax, strike - radius - endMargin);
            }
        }

        /// <summary>Analytic two-bone IK: bends the elbow to the length the wrist needs (in the
        /// arm's current bend plane), then swings the arm so the wrist lands on the target.</summary>
        private static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            float upperLength = Vector3.Distance(a, b);
            float lowerLength = Vector3.Distance(b, c);
            float reach = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, (upperLength + lowerLength) * 0.999f);

            Vector3 bendAxis = Vector3.Cross(c - a, b - a);
            if (bendAxis.sqrMagnitude < 1e-8f)
            {
                bendAxis = Vector3.Cross(c - a, upper.forward);
            }

            bendAxis.Normalize();
            float shoulderNow = Vector3.Angle(c - a, b - a) * Mathf.Deg2Rad;
            float elbowNow = Vector3.Angle(a - b, c - b) * Mathf.Deg2Rad;
            float shoulderWanted = Mathf.Acos(Mathf.Clamp((lowerLength * lowerLength - upperLength * upperLength - reach * reach) / (-2f * upperLength * reach), -1f, 1f));
            float elbowWanted = Mathf.Acos(Mathf.Clamp((reach * reach - upperLength * upperLength - lowerLength * lowerLength) / (-2f * upperLength * lowerLength), -1f, 1f));

            upper.rotation = Quaternion.AngleAxis((shoulderWanted - shoulderNow) * Mathf.Rad2Deg, bendAxis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis((elbowWanted - elbowNow) * Mathf.Rad2Deg, bendAxis) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(end.position - a, target - a) * upper.rotation;
        }

        private void Apply(Transform hand, Transform[] chain, Quaternion[] rest, float weight)
        {
            if (weight <= 0f || hand == null)
            {
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                _animated[i] = chain[i].localRotation;
                chain[i].localRotation = rest[i];
            }

            // Curl about the hand's right axis (the grip's axis), first bone to last - each turn
            // carries the bones after it.
            Vector3 axis = hand.right;
            Turn(chain[0], axis, _curl.x);
            Turn(chain[1], axis, _curl.y);
            Turn(chain[2], axis, _curl.z);

            if (weight < 1f)
            {
                for (int i = 0; i < 3; i++)
                {
                    chain[i].localRotation = Quaternion.Slerp(_animated[i], chain[i].localRotation, weight);
                }
            }
        }

        private static void Turn(Transform bone, Vector3 axis, float degrees)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

#if UNITY_EDITOR
        /// <summary>Editor setup: the open-hand rest rotations of both finger chains.</summary>
        public void EditorSetRest(Quaternion[] right, Quaternion[] left)
        {
            _rightRest = right;
            _leftRest = left;
        }

        /// <summary>Editor setup: the grip sockets (B-handProp.R/.L).</summary>
        public void EditorSetSockets(Transform right, Transform left)
        {
            _rightSocket = right;
            _leftSocket = left;
        }
#endif
    }
}
