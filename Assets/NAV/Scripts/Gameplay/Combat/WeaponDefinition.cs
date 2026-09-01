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

        [Header("Attack")]
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _range = 2f;
        [SerializeField] private float _attackCooldown = 0.6f;
        [SerializeField] private float _attackStaminaCost = 10f;

        [Header("Block / Parry")]
        [SerializeField, Range(0f, 1f)] private float _blockDamageReduction = 0.5f;
        [SerializeField] private float _blockStaminaCostPerHit = 10f;
        [SerializeField] private float _parryWindowSeconds = 0.25f;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public float Damage => _damage;
        public float Range => _range;
        public float AttackCooldown => _attackCooldown;
        public float AttackStaminaCost => _attackStaminaCost;

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

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
