using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Drives the player's Animator from movement state that already exists elsewhere -
    /// PlayerInputHandler.MoveInput and PlayerMotor.IsSprinting/IsGrounded. Doesn't own or
    /// duplicate any movement logic, purely reads it and feeds the Animator (Presentation
    /// observes Gameplay state, per ARCHITECTURE_v0.1.md, generalized from UI to animation).
    ///
    /// MoveX/MoveY are PlayerMotor.MoveDirection in the body's own space. Since 2026-10-21 the
    /// body turns toward where it moves (Valheim-style; the camera orbits freely), so running
    /// plays the forward run; the 8-directional strafe blend tree is still used while the body
    /// is held facing the aim (attacking/blocking on the move). The head looks toward the
    /// camera's direction (OnAnimatorIK), and during an attack/block the spine bends toward the
    /// aim's height (ApplyAimBend) so swings reach low and high targets.
    ///
    /// MoveX/MoveY are pushed through Animator.SetFloat's damped overload (not a raw snap) so
    /// the blend tree eases between directions instead of jumping - this is what keeps a sudden
    /// direction change (e.g. releasing W and tapping S) reading as a smooth blend rather than
    /// a pop.
    ///
    /// Combat (optional refs): on PlayerCombat.AttackStarted it sets AttackStyle (the
    /// weapon's WeaponAttackStyle) and AttackSpeed and fires the Attack trigger - the "Upper
    /// Body" layer (masked to torso/arms, so the legs keep walking) plays that style's swing.
    /// Each style has a short combo (AttackCombo): consecutive attacks step through its
    /// variants via AttackVariant (e.g. one-handed: slash right, slash left, overhead) and
    /// start over after a pause. HoldingItem closes the right
    /// hand ("Right Hand" layer) whenever PlayerEquipment holds something in it;
    /// HoldingLeftItem closes the left one ("Left Hand" layer) for left-hand items (shields).
    /// HoldStyle (= the held weapon's WeaponAttackStyle, 0 = none) puts the upper body in that
    /// weapon's combat stance while standing/walking, or a carry pose while sprinting; standing
    /// still with a weapon also fades in the "Full Stance" layer (weight set here), so the legs
    /// take the stance too. Those layers and parameters are built by NAV > Animation > Setup
    /// Player Combat Layers.
    ///
    /// The attack clips swing from a stance with the hips turned, but the upper-body layer
    /// keeps the locomotion hips (facing forward), which sent every swing off to the left.
    /// Each baked attack clip carries an AttackTwist curve (its hip yaw); LateUpdate turns the
    /// spine by it after the Animator has posed the body, so the swing lands where the
    /// character faces. In the full stance the hips already come from the stance clip, so its
    /// own hip yaw (FullStanceTwist, weighted by the layer) is taken off again. Runs before
    /// PlayerCombat's LateUpdate (execution order) so the weapon hitbox is swept from the
    /// corrected pose.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(-50)]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerMotor _motor;
        [Tooltip("Optional - plays attack animations when the player attacks.")]
        [SerializeField] private PlayerCombat _combat;
        [Tooltip("Optional - closes the hand around a held item.")]
        [SerializeField] private PlayerEquipment _equipment;
        [Tooltip("Optional - plays a hammer strike whenever a building piece is placed or demolished.")]
        [SerializeField] private PlayerBuilding _building;

        [Header("Smoothing")]
        [SerializeField] private float _moveDampTime = 0.15f;

        [Header("Aim")]
        [Tooltip("The gameplay camera - where the player looks (head look, swing tilt). Empty = Camera.main.")]
        [SerializeField] private Transform _aimCamera;
        [Tooltip("How far the swing may tilt down toward a low target (degrees of spine bend).")]
        [SerializeField, Range(0f, 90f)] private float _maxAimBendDown = 80f;
        [Tooltip("Spine bend per degree of aim angle. Over 1: the spine bends above the hips, so the arms need more than the aim angle to reach a low target.")]
        [SerializeField, Range(0.5f, 2f)] private float _aimBendGain = 1.35f;
        [Tooltip("How far the swing may tilt up toward a high target (degrees).")]
        [SerializeField, Range(0f, 60f)] private float _maxAimBendUp = 30f;
        [Tooltip("Aimed targets further than this (m) only use the camera's own pitch.")]
        [SerializeField, Min(1f)] private float _aimRayDistance = 8f;
        [Tooltip("How fast the aim bend fades in/out (weight per second).")]
        [SerializeField, Min(0.1f)] private float _aimBendFadeSpeed = 8f;

        [Header("Head Look")]
        [Tooltip("Overall look-at weight (0 = off).")]
        [SerializeField, Range(0f, 1f)] private float _lookWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float _lookBodyWeight = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _lookHeadWeight = 0.85f;
        [Tooltip("Limits how far the head turns: 0 = free, 0.5 = up to ~90 degrees, 1 = none.")]
        [SerializeField, Range(0f, 1f)] private float _lookClampWeight = 0.6f;

        [Header("Combat")]
        [Tooltip("An attack within this many seconds of the previous one (same style) continues its combo; after a longer pause the combo starts over.")]
        [SerializeField, Min(0f)] private float _comboResetSeconds = 1.2f;
        [Tooltip("How fast (per second) the full-body stance fades in when standing still with a weapon, and out when moving.")]
        [SerializeField, Min(0.1f)] private float _fullStanceFadeSpeed = 4f;

        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SprintingHash = Animator.StringToHash("IsSprinting");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackStyleHash = Animator.StringToHash("AttackStyle");
        private static readonly int AttackVariantHash = Animator.StringToHash("AttackVariant");
        private static readonly int BuildStrikeHash = Animator.StringToHash("BuildStrike");
        private static readonly int FullStanceTwistHash = Animator.StringToHash("FullStanceTwist");
        private const string FullStanceLayer = "Full Stance";
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int HoldingItemHash = Animator.StringToHash("HoldingItem");
        private static readonly int HoldingLeftItemHash = Animator.StringToHash("HoldingLeftItem");
        private static readonly int HoldStyleHash = Animator.StringToHash("HoldStyle");
        private static readonly int AttackTwistHash = Animator.StringToHash("AttackTwist");

        private Animator _animator;
        private Transform _spine;
        private Transform _chest;
        private Transform _upperChest;
        private float _aimBendWeight;
        private float _aimPitch;
        private bool _hasTwistParameter;
        private bool _hasFullStanceTwist;
        private int _fullStanceLayer = -1;
        private float _fullStanceWeight;
        private int _holdStyle;
        private int _comboStyle = -1;
        private int _comboStep;
        private float _lastAttackTime = float.NegativeInfinity;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _spine = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            _chest = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Chest) : null;
            _upperChest = _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.UpperChest) : null;
            if (_aimCamera == null && UnityEngine.Camera.main != null)
            {
                _aimCamera = UnityEngine.Camera.main.transform;
            }
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                _hasTwistParameter |= parameter.nameHash == AttackTwistHash;
                _hasFullStanceTwist |= parameter.nameHash == FullStanceTwistHash;
            }

            _fullStanceLayer = _animator.GetLayerIndex(FullStanceLayer);

            if (_inputHandler == null || _motor == null)
            {
                Debug.LogError($"{nameof(PlayerAnimator)} on '{name}' is missing a required reference (InputHandler/Motor).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                _combat.AttackStarted += HandleAttackStarted;
            }

            if (_equipment != null)
            {
                _equipment.Changed += HandleEquipmentChanged;
                HandleEquipmentChanged();
            }

            if (_building != null)
            {
                _building.PiecePlaced += HandlePiecePlaced;
                _building.PieceDemolished += HandlePiecePlaced;
            }
        }

        private void OnDisable()
        {
            if (_combat != null)
            {
                _combat.AttackStarted -= HandleAttackStarted;
            }

            if (_equipment != null)
            {
                _equipment.Changed -= HandleEquipmentChanged;
            }

            if (_building != null)
            {
                _building.PiecePlaced -= HandlePiecePlaced;
                _building.PieceDemolished -= HandlePiecePlaced;
            }
        }

        // A downward hammer blow ("Build Strike" on the Upper Body layer) - purely visual,
        // PlayerCombat isn't swinging so it hits nothing.
        private void HandlePiecePlaced(BuildingPieceDefinition piece)
        {
            _animator.SetTrigger(BuildStrikeHash);
        }

        private void LateUpdate()
        {
            if (_spine == null)
            {
                return;
            }

            if (_hasTwistParameter)
            {
                float twist = _animator.GetFloat(AttackTwistHash);
                if (_hasFullStanceTwist)
                {
                    twist -= _animator.GetFloat(FullStanceTwistHash);
                }

                if (Mathf.Abs(twist) > 0.01f)
                {
                    // World up: a pure turn of the whole upper body, whatever the spine bone's axes.
                    _spine.rotation = Quaternion.AngleAxis(twist, Vector3.up) * _spine.rotation;
                }
            }

            ApplyAimBend();
        }

        /// <summary>
        /// Tilts the swing to the height of what the player aims at: while the body faces the aim
        /// (an attack or a block - PlayerMotor.IsFacingAim) the spine bends forward/back by the
        /// angle from the chest to the crosshair's target (or the camera's pitch when nothing is
        /// in reach), split over Spine/Chest/UpperChest. The attack clips swing roughly level, so
        /// this is what lets a blade reach ore at the feet or a deer's legs. Runs before
        /// PlayerCombat's LateUpdate, so the weapon hitbox is swept from the bent pose - hits stay
        /// physical contact. The swing stays in front: the body turns to the aim first.
        /// </summary>
        private void ApplyAimBend()
        {
            float targetWeight = _motor.IsFacingAim && _aimCamera != null ? 1f : 0f;
            _aimBendWeight = Mathf.MoveTowards(_aimBendWeight, targetWeight, _aimBendFadeSpeed * Time.deltaTime);
            if (_aimBendWeight <= 0f)
            {
                return;
            }

            Transform chest = _chest != null ? _chest : _spine;
            float targetPitch = AimPitchFrom(chest.position);
            // Follow the aim quickly but without popping.
            _aimPitch = Mathf.MoveTowards(_aimPitch, targetPitch, 360f * Time.deltaTime);
            float bend = _aimPitch * _aimBendWeight;
            if (Mathf.Abs(bend) < 0.01f)
            {
                return;
            }

            // Bend about the body's right axis (positive = forward/down).
            Vector3 axis = _motor.transform.right;
            if (_upperChest != null && _chest != null)
            {
                Bend(_spine, axis, bend * 0.4f);
                Bend(_chest, axis, bend * 0.35f);
                Bend(_upperChest, axis, bend * 0.25f);
            }
            else if (_chest != null)
            {
                Bend(_spine, axis, bend * 0.55f);
                Bend(_chest, axis, bend * 0.45f);
            }
            else
            {
                Bend(_spine, axis, bend);
            }
        }

        private static void Bend(Transform bone, Vector3 axis, float degrees)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        /// <summary>Degrees the swing must tilt (positive = down) to meet what the crosshair is on,
        /// measured from the chest in the body's vertical plane; nothing in reach = a softened
        /// camera pitch (the camera sits above/behind, so its own pitch overstates it).</summary>
        private float AimPitchFrom(Vector3 chestPosition)
        {
            float pitch;
            if (Physics.Raycast(_aimCamera.position, _aimCamera.forward, out RaycastHit hit, _aimRayDistance + 6f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(_motor.transform))
            {
                Vector3 offset = hit.point - chestPosition;
                float horizontal = new Vector2(offset.x, offset.z).magnitude;
                pitch = horizontal > 0.2f && offset.magnitude < _aimRayDistance
                    ? Mathf.Atan2(-offset.y, horizontal) * Mathf.Rad2Deg
                    : CameraPitch() * 0.6f;
            }
            else
            {
                pitch = CameraPitch() * 0.6f;
            }

            return Mathf.Clamp(pitch * _aimBendGain, -_maxAimBendUp, _maxAimBendDown);
        }

        private float CameraPitch()
        {
            float pitch = _aimCamera.eulerAngles.x;
            return pitch > 180f ? pitch - 360f : pitch;
        }

        /// <summary>Head (and a little of the upper body) turns toward where the camera looks,
        /// within limits (Look Clamp Weight) - the body itself keeps its facing (Valheim-style).
        /// Needs IK Pass on the Base Layer (already on for FootGrounding).</summary>
        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !_animator.isHuman || _aimCamera == null || _lookWeight <= 0f)
            {
                return;
            }

            _animator.SetLookAtPosition(_aimCamera.position + _aimCamera.forward * 20f);
            _animator.SetLookAtWeight(_lookWeight, _lookBodyWeight, _lookHeadWeight, 0f, _lookClampWeight);
        }

        private void HandleAttackStarted(WeaponDefinition weapon)
        {
            int style = (int)weapon.AttackStyle;
            if (style != _comboStyle || Time.time - _lastAttackTime > _comboResetSeconds)
            {
                _comboStyle = style;
                _comboStep = 0;
            }

            int variant = _comboStep % AttackCombo.VariantCount(weapon.AttackStyle);
            _comboStep++;

            _lastAttackTime = Time.time;
            _animator.SetInteger(AttackStyleHash, style);
            _animator.SetInteger(AttackVariantHash, variant);
            _animator.SetFloat(AttackSpeedHash, weapon.AttackAnimationSpeed);
            _animator.SetTrigger(AttackHash);
        }

        private void HandleEquipmentChanged()
        {
            if (_animator != null)
            {
                bool holding = _equipment.MainHand != null;
                bool leftHanded = holding && _equipment.MainHand.HeldInLeftHand;
                _animator.SetBool(HoldingItemHash, holding && !leftHanded);
                _animator.SetBool(HoldingLeftItemHash, leftHanded);

                // Weapon stance: only for a visible weapon (a held model and combat stats) -
                // a stance with empty hands (pickaxe has no model yet) would look broken.
                WeaponDefinition weapon = holding && _equipment.MainHand.HeldPrefab != null ? _equipment.MainHandWeapon : null;
                _holdStyle = weapon != null ? (int)weapon.AttackStyle : 0;
                _animator.SetInteger(HoldStyleHash, _holdStyle);
            }
        }

        private void Update()
        {
            // The body no longer always faces the camera (PlayerMotor turns it toward the move
            // direction, or the aim), so the blend tree gets the move direction in the body's
            // own space: forward while running where it faces, strafe/back only while it's held
            // facing the aim (attacking/blocking on the move).
            Vector3 localMove = _motor.transform.InverseTransformDirection(_motor.MoveDirection);
            var move = new Vector2(localMove.x, localMove.z);
            _animator.SetFloat(MoveXHash, move.x, _moveDampTime, Time.deltaTime);
            _animator.SetFloat(MoveYHash, move.y, _moveDampTime, Time.deltaTime);
            _animator.SetBool(SprintingHash, _motor.IsSprinting);
            // A swimmer counts as grounded - it treads water with the locomotion clips (no swim
            // animation yet) instead of being stuck in the fall pose.
            _animator.SetBool(GroundedHash, _motor.IsGrounded || _motor.IsSwimming);

            if (_fullStanceLayer >= 0)
            {
                // Full-body stance only when standing still on the ground with a weapon;
                // walking keeps the locomotion legs (the upper-body stance stays).
                bool standing = _holdStyle != 0 && move.sqrMagnitude < 0.01f && _motor.IsGrounded;
                _fullStanceWeight = Mathf.MoveTowards(_fullStanceWeight, standing ? 1f : 0f, _fullStanceFadeSpeed * Time.deltaTime);
                _animator.SetLayerWeight(_fullStanceLayer, _fullStanceWeight);
            }
        }
    }
}
