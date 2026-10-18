using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Combat stats for a melee weapon. No Equipment system exists yet (Phase 2's "Equipment
    /// slots" and Phase 4's "Basic tools" are both still unchecked) - PlayerCombat's equipped
    /// weapon is a single fixed serialized reference for this increment, the same "no
    /// unlock/equip system yet" deferral used by PlayerCrafting's known recipes and
    /// PlayerBuilding's known pieces. Block/Parry stats live here rather than on a separate
    /// shield type - there is no dual-wield/offhand system yet, so the one equipped weapon is
    /// also what the player blocks with.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "NAV/Combat/Weapon Definition")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;
        [SerializeField] private Sprite _icon;
        [Tooltip("Weapon family (dagger, sword, spear, ...) - see WeaponClass.")]
        [SerializeField] private WeaponClass _weaponClass = WeaponClass.Sword;

        [Header("Attack")]
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _range = 2f;
        [SerializeField] private float _attackCooldown = 0.6f;
        [SerializeField] private float _attackStaminaCost = 10f;

        [Header("Attack Animation")]
        [Tooltip("Which attack animation set this weapon uses (each style swings differently).")]
        [SerializeField] private WeaponAttackStyle _attackStyle = WeaponAttackStyle.OneHanded;
        [Tooltip("Playback speed of the attack animation - tune so the swing roughly fits Attack Cooldown.")]
        [SerializeField, Min(0.1f)] private float _attackAnimationSpeed = 1f;
        [Tooltip("FALLBACK only - normally each attack animation's own HitActive curve decides when the hitbox is live. Seconds after the attack starts when the hitbox becomes live, if the animations carry no strike timing.")]
        [SerializeField, Min(0f)] private float _hitWindowStart = 0.1f;
        [Tooltip("FALLBACK only (see Hit Window Start) - seconds after the attack starts when the hitbox stops dealing hits.")]
        [SerializeField, Min(0f)] private float _hitWindowEnd = 0.35f;

        [Header("Block / Parry")]
        [SerializeField, Range(0f, 1f)] private float _blockDamageReduction = 0.5f;
        [SerializeField] private float _blockStaminaCostPerHit = 10f;
        [SerializeField] private float _parryWindowSeconds = 0.25f;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public WeaponClass WeaponClass => _weaponClass;
        public float Damage => _damage;
        public float Range => _range;
        public float AttackCooldown => _attackCooldown;
        public float AttackStaminaCost => _attackStaminaCost;
        public WeaponAttackStyle AttackStyle => _attackStyle;
        public float AttackAnimationSpeed => _attackAnimationSpeed;

        /// <summary>Seconds after the attack starts that its hitbox (MeleeHitbox / fist)
        /// deals damage on contact - from HitWindowStart to HitWindowEnd.</summary>
        public float HitWindowStart => _hitWindowStart;
        public float HitWindowEnd => _hitWindowEnd;

        /// <summary>Fraction of incoming damage removed while held-blocking outside the parry
        /// window (0 = no reduction, 1 = fully blocked).</summary>
        public float BlockDamageReduction => _blockDamageReduction;
        public float BlockStaminaCostPerHit => _blockStaminaCostPerHit;

        /// <summary>How soon after Block starts being held a hit still counts as a perfect
        /// parry (full damage negation, no stamina cost) instead of a regular reduced-damage
        /// block.</summary>
        public float ParryWindowSeconds => _parryWindowSeconds;

        private void OnValidate()
        {
            _damage = Mathf.Max(0f, _damage);
            _range = Mathf.Max(0f, _range);
            _attackCooldown = Mathf.Max(0f, _attackCooldown);
            _attackStaminaCost = Mathf.Max(0f, _attackStaminaCost);
            _blockDamageReduction = Mathf.Clamp01(_blockDamageReduction);
            _blockStaminaCostPerHit = Mathf.Max(0f, _blockStaminaCostPerHit);
            _parryWindowSeconds = Mathf.Max(0f, _parryWindowSeconds);
            _attackAnimationSpeed = Mathf.Max(0.1f, _attackAnimationSpeed);
            _hitWindowStart = Mathf.Max(0f, _hitWindowStart);
            _hitWindowEnd = Mathf.Max(_hitWindowStart, _hitWindowEnd);

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
