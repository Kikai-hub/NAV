using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Placeholder combat target for verifying attack/hit-detection/damage and, via its own
    /// periodic counter-swing, blocking/parrying - the same role DebugInteractable played for
    /// PlayerInteractor before real interactables existed. This is explicitly NOT a Creature:
    /// no perception, no target selection, no patrol/pathing - it just tracks whether an
    /// IDamageable is standing in its trigger range and swings at it on a fixed timer. Real
    /// enemy AI is Roadmap Phase 8 (Creatures), out of scope here. Requires its own separate
    /// non-trigger Collider (e.g. the placeholder mesh's own Capsule/Box Collider) for the
    /// player's attack raycast to hit - the auto-added SphereCollider below is trigger-only
    /// (detection range) and is ignored by that raycast (QueryTriggerInteraction.Ignore).
    ///
    /// On death it scatters _lootDrops into the world (same independent-chance roll and
    /// physical-ItemPickup spawn path ResourceNode.SpawnDrops already uses, reusing its
    /// ResourceNodeDrop type - "item + amount + chance" means the same thing for a defeated
    /// target as it does for a depleted node). Unlike a real creature, the dummy deliberately
    /// does NOT despawn on death - it's reusable test infrastructure, not content, and staying
    /// around (just permanently unattackable/non-attacking once IsAlive is false) lets it be
    /// hit again for further manual testing without recreating it.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CombatDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _attackDamage = 5f;
        [SerializeField] private float _attackInterval = 2f;
        [SerializeField] private float _attackRange = 2.5f;

        [Header("Loot")]
        [SerializeField] private List<ResourceNodeDrop> _lootDrops = new();
        [SerializeField] private float _lootScatterRadius = 1f;
        [SerializeField] private float _lootScatterForce = 1.5f;
        [SerializeField] private float _lootUpwardForce = 2f;

        private bool _hasDroppedLoot;

        private IDamageable _target;
        private float _nextAttackTime;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _maxHealth;
        public bool IsAlive => CurrentHealth > 0f;

        private void Awake()
        {
            CurrentHealth = _maxHealth;

            var range = GetComponent<SphereCollider>();
            range.isTrigger = true;
            range.radius = _attackRange;
        }

        private void Update()
        {
            if (!IsAlive || _target == null || Time.time < _nextAttackTime)
            {
                return;
            }

            _nextAttackTime = Time.time + _attackInterval;
            _target.TakeDamage(_attackDamage);
            Debug.Log($"{name} attacked for {_attackDamage}.", this);
        }

        private void OnTriggerEnter(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                _target = damageable;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (ReferenceEquals(other.GetComponentInParent<IDamageable>(), _target))
            {
                _target = null;
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Debug.Log($"{name} took {amount} damage ({CurrentHealth}/{_maxHealth} HP left).", this);

            if (CurrentHealth <= 0f && !_hasDroppedLoot)
            {
                _hasDroppedLoot = true;
                Debug.Log($"{name} defeated.", this);
                SpawnLoot();
            }
        }

        /// <summary>Rolls each _lootDrops entry independently and spawns whichever ones hit as
        /// physical ItemPickups scattered around the dummy's position - same scatter shape as
        /// ResourceNode.SpawnDrops (small random horizontal offset + an outward/upward impulse),
        /// reusing the same ItemPickup.SpawnInWorld path. A drop whose item has no WorldPrefab
        /// fails loudly via that shared path rather than silently vanishing.</summary>
        private void SpawnLoot()
        {
            foreach (ResourceNodeDrop drop in _lootDrops)
            {
                if (drop.Item == null || Random.value > drop.Chance)
                {
                    continue;
                }

                Vector2 offset = Random.insideUnitCircle * _lootScatterRadius;
                Vector3 spawnPosition = transform.position + new Vector3(offset.x, 0.5f, offset.y);
                Vector3 outward = new Vector3(offset.x, 0f, offset.y);
                Vector3 impulse = (outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward) * _lootScatterForce + Vector3.up * _lootUpwardForce;

                ItemPickup.SpawnInWorld(drop.Item, drop.Amount, spawnPosition, Quaternion.identity, impulse);
            }
        }

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _attackDamage = Mathf.Max(0f, _attackDamage);
            _attackInterval = Mathf.Max(0.1f, _attackInterval);
            _attackRange = Mathf.Max(0.1f, _attackRange);
        }
    }
}
