using UnityEngine;

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
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CombatDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _attackDamage = 5f;
        [SerializeField] private float _attackInterval = 2f;
        [SerializeField] private float _attackRange = 2.5f;

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

            if (CurrentHealth <= 0f)
            {
                Debug.Log($"{name} defeated.", this);
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
