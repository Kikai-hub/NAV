using UnityEngine;
using NAV.Gameplay.Player;
using NAV.Gameplay.Building;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Drives the player's side of melee combat: attacking (camera-forward raycast hit
    /// detection + damage + stamina cost + cooldown) and blocking/parrying incoming damage.
    /// Implements IDamageable itself (rather than PlayerHealth doing so) so block/parry
    /// mitigation has exactly one place to live - PlayerHealth stays the dumb HP store it
    /// already is, this decides how much of an incoming hit actually reaches it.
    /// EquippedWeapon is a single fixed reference, the same "no equip/unlock system yet"
    /// deferral used by PlayerCrafting/PlayerBuilding's known lists - Equipment (Roadmap
    /// Phase 2/4) is still a future system.
    /// </summary>
    public class PlayerCombat : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private WeaponDefinition _equippedWeapon;

        [Header("Hit Detection")]
        [SerializeField] private LayerMask _hitLayers = ~0;
        [Tooltip("Upper bound on the raycast itself, separate from the weapon's own Range - the ray still starts at the camera (avoids the player's own collider blocking it), but a hit only counts as in range if it's also within Range of the player's own position. Same split PlayerInteractor uses for the same reason: in third person the camera sits well behind/above the player, so checking range as raw ray length would consume most of the budget just reaching the character instead of the target.")]
        [SerializeField] private float _maxAimDistance = 15f;

        [Header("Build Mode")]
        [Tooltip("Optional. When assigned, Attack does nothing here while build mode is active - PlayerBuilding owns Attack (placing a piece) in that state instead, matching the existing 'Attack does whatever the current tool/mode says' convention.")]
        [SerializeField] private PlayerBuilding _building;

        private float _nextAttackTime;
        private float _blockStartTime = -1f;

        public WeaponDefinition EquippedWeapon => _equippedWeapon;
        public bool IsBlocking { get; private set; }

        /// <summary>True while the camera-forward aim ray is currently on a live IDamageable
        /// within the equipped weapon's Range - a read-only query (no cooldown/stamina/damage
        /// side effects) for the crosshair to reflect "you're looking at something attackable",
        /// separate from HandleAttack actually firing a hit.</summary>
        public bool HasTargetInSight { get; private set; }

        public bool IsAlive => _health != null && _health.IsAlive;
        public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;
        public float MaxHealth => _health != null ? _health.MaxHealth : 0f;

        private void Awake()
        {
            if (_inputHandler == null || _health == null || _stamina == null || _cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerCombat)} on '{name}' is missing a required reference (InputHandler/Health/Stamina/CameraTransform).", this);
                enabled = false;
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
            bool wantsToBlock = !buildModeActive && _inputHandler.BlockHeld;

            if (wantsToBlock && !IsBlocking)
            {
                _blockStartTime = Time.time;
            }

            IsBlocking = wantsToBlock;

            HasTargetInSight = !buildModeActive && FindTargetInSight() != null;
        }

        /// <summary>Same aim/range split as HandleAttack (camera-forward ray, range measured
        /// from the player's position) but non-mutating - used by both HandleAttack and the
        /// HasTargetInSight query so the two can never disagree about what counts as "in
        /// range".</summary>
        private IDamageable FindTargetInSight()
        {
            if (_equippedWeapon == null)
            {
                return null;
            }

            if (!Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxAimDistance, _hitLayers, QueryTriggerInteraction.Ignore)
                || Vector3.Distance(transform.position, hit.point) > _equippedWeapon.Range)
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

            if (_equippedWeapon == null || Time.time < _nextAttackTime)
            {
                return;
            }

            if (_stamina.CurrentStamina < _equippedWeapon.AttackStaminaCost)
            {
                return;
            }

            _nextAttackTime = Time.time + _equippedWeapon.AttackCooldown;
            _stamina.Spend(_equippedWeapon.AttackStaminaCost);

            // Aim from the camera (avoids the player's own collider blocking the ray), but
            // measure range from the player's position, not the camera's - same split
            // PlayerInteractor uses and for the same reason (see _maxAimDistance's tooltip).
            if (Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxAimDistance, _hitLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Distance(transform.position, hit.point) <= _equippedWeapon.Range)
            {
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && !ReferenceEquals(target, this))
                {
                    target.TakeDamage(_equippedWeapon.Damage);
                    Debug.Log($"{name} hit {hit.collider.name} for {_equippedWeapon.Damage} with {_equippedWeapon.DisplayName}.", this);
                }
                else
                {
                    Debug.Log($"{name} swung {_equippedWeapon.DisplayName} and hit {hit.collider.name}, but it has no IDamageable.", this);
                }
            }
            else
            {
                Debug.Log($"{name} swung {_equippedWeapon.DisplayName} and hit nothing within {_equippedWeapon.Range}m.", this);
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

            if (IsBlocking && _equippedWeapon != null)
            {
                bool isParry = Time.time - _blockStartTime <= _equippedWeapon.ParryWindowSeconds;
                if (isParry)
                {
                    Debug.Log($"{name} parried an attack.", this);
                    return;
                }

                amount *= 1f - _equippedWeapon.BlockDamageReduction;
                _stamina.Spend(_equippedWeapon.BlockStaminaCostPerHit);
                Debug.Log($"{name} blocked, taking {amount:F1} reduced damage.", this);
            }

            _health.TakeDamage(amount);
        }
    }
}
