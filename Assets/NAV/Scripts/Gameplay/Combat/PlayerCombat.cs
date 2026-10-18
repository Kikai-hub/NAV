using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Player;
using NAV.Gameplay.Building;
using NAV.Gameplay.Equipment;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Drives the player's side of melee combat: attacking (contact hit detection + damage +
    /// stamina cost + cooldown) and blocking/parrying incoming damage.
    /// Implements IDamageable itself (rather than PlayerHealth doing so) so block/parry
    /// mitigation has exactly one place to live - PlayerHealth stays the dumb HP store it
    /// already is, this decides how much of an incoming hit actually reaches it.
    /// The weapon used is whatever PlayerEquipment holds in the main hand (its item's
    /// WeaponDefinition), falling back to UnarmedWeapon (fists) when nothing - or a tool
    /// without combat stats, like the hammer - is held.
    ///
    /// An attack starts on input (cooldown + stamina spent, AttackStarted fires so
    /// Presentation plays the weapon's swing). Damage is dealt by contact: while the swing is
    /// striking, each striking capsule (Hitbox Source - the held model's MeleeHitbox and/or the
    /// free fists, following the animated hands) is swept every frame between its previous and
    /// current pose, and whatever it touches is hit - IDamageable targets (creatures, dummies)
    /// take damage, IMeleeHitReceivers (trees, rocks) take a gather-hit - each at most once per
    /// swing. "Striking" comes from the attack animation itself (Hitbox Source's IsStriking -
    /// each combo variant has its own timing); if the source has no strike timing, the
    /// weapon's HitWindowStart..End is used instead. Without a Hitbox Source it falls back to
    /// one camera-ray hit at HitWindowStart.
    /// </summary>
    public class PlayerCombat : MonoBehaviour, IDamageable
    {
        private const int MaxSweepSteps = 12;
        private const int MaxCapsules = 4;
        // A swing whose animation never reports striking (missing state, interrupted) ends here.
        private const float MaxSwingSeconds = 2f;

        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private Transform _cameraTransform;
        [Tooltip("Optional - source of the held weapon. Without it the player always fights with Unarmed Weapon.")]
        [SerializeField] private PlayerEquipment _equipment;
        [Tooltip("Fists: used whenever the main hand holds nothing with combat stats.")]
        [SerializeField] private WeaponDefinition _unarmedWeapon;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask _hitLayers = ~0;
        [Tooltip("Something implementing IMeleeHitboxSource (HeldItemView on the character model) - the weapon/fist capsule swept for contact hits. Empty = camera-ray fallback.")]
        [SerializeField] private MonoBehaviour _hitboxSource;
        [Tooltip("Upper bound on the aim raycast (crosshair target query and the no-hitbox fallback), separate from the weapon's own Range - the ray starts at the camera, but a hit only counts as in range if it's also within Range of the player's own position. Same split PlayerInteractor uses: in third person the camera sits well behind/above the player.")]
        [SerializeField] private float _maxAimDistance = 15f;

        [Header("Build Mode")]
        [Tooltip("Optional. When assigned, Attack does nothing here while build mode is active - PlayerBuilding owns Attack (placing a piece) in that state instead, matching the existing 'Attack does whatever the current tool/mode says' convention.")]
        [SerializeField] private PlayerBuilding _building;

        private float _nextAttackTime;
        private float _blockStartTime = -1f;

        private IMeleeHitboxSource _hitbox;

        // The swing in flight. Its weapon is captured at the start, so switching weapons
        // mid-swing can't change the hit. Null = no swing.
        private WeaponDefinition _swingWeapon;
        private float _swingStartTime;
        private bool _swingHasStruck;
        private bool _hasPreviousCapsules;
        private int _previousCount;
        private readonly MeleeCapsule[] _capsules = new MeleeCapsule[MaxCapsules];
        private readonly MeleeCapsule[] _previousCapsules = new MeleeCapsule[MaxCapsules];
        private readonly HashSet<object> _struckThisSwing = new();
        private readonly Collider[] _overlapBuffer = new Collider[16];

        /// <summary>Fires when an attack starts (input accepted, before anything is hit) with
        /// the weapon swung - for animation/audio.</summary>
        public event System.Action<WeaponDefinition> AttackStarted;

        /// <summary>The weapon attacks/blocks currently use: the held item's weapon, else fists.</summary>
        public WeaponDefinition EquippedWeapon => _equipment != null && _equipment.MainHandWeapon != null
            ? _equipment.MainHandWeapon
            : _unarmedWeapon;
        public bool IsBlocking { get; private set; }

        /// <summary>True while the camera-forward aim ray is currently on a live IDamageable
        /// within the equipped weapon's Range - a read-only query (no cooldown/stamina/damage
        /// side effects) for the crosshair to reflect "you're looking at something attackable",
        /// separate from a swing actually connecting.</summary>
        public bool HasTargetInSight { get; private set; }

        public bool IsAlive => _health != null && _health.IsAlive;
        public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;
        public float MaxHealth => _health != null ? _health.MaxHealth : 0f;

        private void Awake()
        {
            if (_inputHandler == null || _health == null || _stamina == null || _cameraTransform == null || _unarmedWeapon == null)
            {
                Debug.LogError($"{nameof(PlayerCombat)} on '{name}' is missing a required reference (InputHandler/Health/Stamina/CameraTransform/UnarmedWeapon).", this);
                enabled = false;
                return;
            }

            _hitbox = _hitboxSource as IMeleeHitboxSource;
            if (_hitboxSource != null && _hitbox == null)
            {
                Debug.LogError($"{nameof(PlayerCombat)} on '{name}': Hitbox Source '{_hitboxSource.name}' does not implement {nameof(IMeleeHitboxSource)} - using the camera-ray fallback.", this);
            }
        }

        private void OnEnable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.AttackPerformed += HandleAttack;
        }

        private void OnDisable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.AttackPerformed -= HandleAttack;
        }

        private void Update()
        {
            bool buildModeActive = _building != null && _building.IsBuildModeActive;
            // With the hammer in hand RMB toggles build mode instead of blocking.
            bool rmbIsBuilding = _building != null && _building.UsesSecondaryAction;
            bool wantsToBlock = !buildModeActive && !rmbIsBuilding && _inputHandler.BlockHeld;

            if (wantsToBlock && !IsBlocking)
            {
                _blockStartTime = Time.time;
            }

            IsBlocking = wantsToBlock;

            HasTargetInSight = !buildModeActive && FindTargetInSight() != null;
        }

        // After the Animator has posed the hand this frame, so the capsule is where the
        // weapon is actually drawn.
        private void LateUpdate()
        {
            if (_swingWeapon == null)
            {
                return;
            }

            float elapsed = Time.time - _swingStartTime;
            if (!_health.IsAlive)
            {
                _swingWeapon = null;
                return;
            }

            bool striking;
            if (_hitbox != null && _hitbox.HasStrikeTiming)
            {
                // Animation-timed: strike while the attack clip says so; the swing is over once
                // its striking part has passed (or it never came).
                striking = _hitbox.IsStriking;
                if (!striking && (_swingHasStruck || elapsed > MaxSwingSeconds))
                {
                    _swingWeapon = null;
                    return;
                }
            }
            else
            {
                if (elapsed > _swingWeapon.HitWindowEnd)
                {
                    _swingWeapon = null;
                    return;
                }

                striking = elapsed >= _swingWeapon.HitWindowStart;
            }

            if (_hitbox == null)
            {
                if (striking)
                {
                    _swingHasStruck = true;
                    StrikeAlongAimRay(_swingWeapon);
                    _swingWeapon = null;
                }

                return;
            }

            // Track the capsules every frame of the swing, so the first striking frame sweeps the
            // whole arc travelled since the frame before (a strike can be only a frame or two).
            int count = _hitbox.GetHitCapsules(_capsules);
            if (count != _previousCount)
            {
                _hasPreviousCapsules = false;
            }

            for (int i = 0; i < count; i++)
            {
                if (striking)
                {
                    MeleeCapsule from = _hasPreviousCapsules ? _previousCapsules[i] : _capsules[i];
                    SweepCapsule(_swingWeapon, from, _capsules[i]);
                }

                _previousCapsules[i] = _capsules[i];
            }

            _previousCount = count;
            _hasPreviousCapsules = true;
            _swingHasStruck |= striking;
        }

        /// <summary>Same aim/range split as the raycast fallback (camera-forward ray, range
        /// measured from the player's position) but non-mutating - the crosshair query.</summary>
        private IDamageable FindTargetInSight()
        {
            WeaponDefinition weapon = EquippedWeapon;
            if (weapon == null)
            {
                return null;
            }

            if (!Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxAimDistance, _hitLayers, QueryTriggerInteraction.Ignore)
                || Vector3.Distance(transform.position, hit.point) > weapon.Range)
            {
                return null;
            }

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            return target != null && !ReferenceEquals(target, this) && target.IsAlive ? target : null;
        }

        private void HandleAttack()
        {
            if (_building != null && _building.IsBuildModeActive)
            {
                return;
            }

            WeaponDefinition weapon = EquippedWeapon;
            if (weapon == null || Time.time < _nextAttackTime)
            {
                return;
            }

            if (_stamina.CurrentStamina < weapon.AttackStaminaCost)
            {
                return;
            }

            _nextAttackTime = Time.time + weapon.AttackCooldown;
            _stamina.Spend(weapon.AttackStaminaCost);

            // A new swing replaces one still in flight; anything the old one already struck
            // stays struck, the new one can hit it again.
            _swingWeapon = weapon;
            _swingStartTime = Time.time;
            _swingHasStruck = false;
            _hasPreviousCapsules = false;
            _struckThisSwing.Clear();

            AttackStarted?.Invoke(weapon);
        }

        /// <summary>
        /// Tests the capsule's path since last frame: interpolates between the previous and
        /// current pose in steps no longer than the radius (a fast swing moves the tip ~1 m
        /// per frame and would otherwise pass straight through a target).
        /// </summary>
        private void SweepCapsule(WeaponDefinition weapon, MeleeCapsule previous, MeleeCapsule current)
        {
            Vector3 fromStart = previous.Start;
            Vector3 fromEnd = previous.End;
            Vector3 start = current.Start;
            Vector3 end = current.End;
            float radius = current.Radius;

            float travel = Mathf.Max(Vector3.Distance(fromStart, start), Vector3.Distance(fromEnd, end));
            int steps = Mathf.Clamp(Mathf.CeilToInt(travel / Mathf.Max(radius, 0.01f)), 1, MaxSweepSteps);

            for (int step = 1; step <= steps; step++)
            {
                float t = (float)step / steps;
                Vector3 a = Vector3.Lerp(fromStart, start, t);
                Vector3 b = Vector3.Lerp(fromEnd, end, t);
                int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, _overlapBuffer, _hitLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider collider = _overlapBuffer[i];
                    if (collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    Strike(weapon, collider);
                }
            }
        }

        private void StrikeAlongAimRay(WeaponDefinition weapon)
        {
            if (Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxAimDistance, _hitLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Distance(transform.position, hit.point) <= weapon.Range
                && !hit.collider.transform.IsChildOf(transform))
            {
                Strike(weapon, hit.collider);
            }
        }

        /// <summary>Hits whatever the collider belongs to, once per swing: a live IDamageable
        /// takes the weapon's damage, otherwise an IMeleeHitReceiver (tree, rock) takes a
        /// gather-hit. Anything else (ground, walls) is ignored.</summary>
        private void Strike(WeaponDefinition weapon, Collider collider)
        {
            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable != null && !ReferenceEquals(damageable, this))
            {
                if (damageable.IsAlive && _struckThisSwing.Add(damageable))
                {
                    damageable.TakeDamage(weapon.Damage);
                    Debug.Log($"{name} hit {collider.name} for {weapon.Damage} with {weapon.DisplayName}.", this);
                }

                return;
            }

            IMeleeHitReceiver receiver = collider.GetComponentInParent<IMeleeHitReceiver>();
            if (receiver != null && _struckThisSwing.Add(receiver))
            {
                receiver.ReceiveMeleeHit(gameObject, weapon);
                Debug.Log($"{name} struck {collider.name} with {weapon.DisplayName}.", this);
            }
        }

        /// <summary>
        /// Applies block/parry mitigation to an incoming hit, then forwards whatever remains
        /// to PlayerHealth. A hit landing within the equipped weapon's ParryWindowSeconds of
        /// Block starting to be held is a perfect parry (fully negated, no stamina cost);
        /// otherwise a held block reduces damage by BlockDamageReduction and costs
        /// BlockStaminaCostPerHit. Not blocking at all passes the hit through unchanged.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f || _health == null || !_health.IsAlive)
            {
                return;
            }

            WeaponDefinition weapon = EquippedWeapon;
            if (IsBlocking && weapon != null)
            {
                bool isParry = Time.time - _blockStartTime <= weapon.ParryWindowSeconds;
                if (isParry)
                {
                    Debug.Log($"{name} parried an attack.", this);
                    return;
                }

                amount *= 1f - weapon.BlockDamageReduction;
                _stamina.Spend(weapon.BlockStaminaCostPerHit);
                Debug.Log($"{name} blocked, taking {amount:F1} reduced damage.", this);
            }

            _health.TakeDamage(amount);
        }
    }
}
