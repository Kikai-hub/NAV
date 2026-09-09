using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.AI
{
    /// <summary>
    /// Static data for a creature: stats, perception/attack ranges, and loot. IsHostile decides
    /// which half of Creature's state machine is reachable from Patrol - hostile creatures chase
    /// anything IDamageable that enters DetectionRadius (GDD: "Idle/Patrol -> Detect Player ->
    /// Chase -> Attack -> Search/Return"), neutral creatures never initiate a chase and only
    /// react to actually being attacked (GDD: "Neutral creatures may flee when threatened").
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureDefinition", menuName = "NAV/AI/Creature Definition")]
    public class CreatureDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;
        [SerializeField] private bool _isHostile;

        [Header("Stats")]
        [SerializeField] private float _maxHealth = 30f;
        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _chaseSpeedMultiplier = 1.4f;

        [Header("Perception")]
        [SerializeField] private float _detectionRadius = 8f;

        [Header("Attack")]
        [SerializeField] private float _attackDamage = 10f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _attackCooldown = 1.5f;

        [Header("Patrol")]
        [SerializeField] private float _patrolRadius = 6f;
        [SerializeField] private float _patrolWaitSeconds = 3f;

        [Header("Search / Flee")]
        [SerializeField] private float _searchWaitSeconds = 4f;
        [SerializeField] private float _fleeDistance = 10f;

        [Header("Loot")]
        [SerializeField] private List<ResourceNodeDrop> _lootDrops = new();

        public string DisplayName => _displayName;

        /// <summary>Hostile creatures chase/attack on sight; neutral creatures only flee once
        /// actually damaged.</summary>
        public bool IsHostile => _isHostile;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float ChaseSpeedMultiplier => _chaseSpeedMultiplier;
        public float DetectionRadius => _detectionRadius;
        public float AttackDamage => _attackDamage;
        public float AttackRange => _attackRange;
        public float AttackCooldown => _attackCooldown;
        public float PatrolRadius => _patrolRadius;
        public float PatrolWaitSeconds => _patrolWaitSeconds;
        public float SearchWaitSeconds => _searchWaitSeconds;
        public float FleeDistance => _fleeDistance;
        public IReadOnlyList<ResourceNodeDrop> LootDrops => _lootDrops;

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _moveSpeed = Mathf.Max(0.1f, _moveSpeed);
            _chaseSpeedMultiplier = Mathf.Max(1f, _chaseSpeedMultiplier);
            _detectionRadius = Mathf.Max(0.1f, _detectionRadius);
            _attackDamage = Mathf.Max(0f, _attackDamage);
            _attackRange = Mathf.Max(0.1f, _attackRange);
            _attackCooldown = Mathf.Max(0.1f, _attackCooldown);
            _patrolRadius = Mathf.Max(0f, _patrolRadius);
            _patrolWaitSeconds = Mathf.Max(0f, _patrolWaitSeconds);
            _searchWaitSeconds = Mathf.Max(0f, _searchWaitSeconds);
            _fleeDistance = Mathf.Max(0.1f, _fleeDistance);

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
