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
            if (_spine == null || !_hasTwistParameter)
            {
                return;
            }

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
            Vector2 move = _inputHandler.MoveInput;
            _animator.SetFloat(MoveXHash, move.x, _moveDampTime, Time.deltaTime);
            _animator.SetFloat(MoveYHash, move.y, _moveDampTime, Time.deltaTime);
            _animator.SetBool(SprintingHash, _motor.IsSprinting);
            _animator.SetBool(GroundedHash, _motor.IsGrounded);

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
