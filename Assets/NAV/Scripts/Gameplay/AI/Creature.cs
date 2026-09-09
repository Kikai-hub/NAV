using UnityEngine;
using UnityEngine.AI;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.AI
{
    public enum CreatureState
    {
        Patrol,
        Chase,
        Attack,
        Search,
        Return,
        Flee,
        Dead
    }

    /// <summary>
    /// Drives one creature's state machine on top of a baked NavMesh (first use of the
    /// com.unity.ai.navigation package in this project - the scene needs a baked NavMeshSurface,
    /// see UNITY_SETUP_NEXT_STEPS.md). Perception is trigger-based (a SphereCollider sized to
    /// DetectionRadius), same "physics event, not a per-frame scan" idiom Workbench/CombatDummy
    /// already use. Hostile creatures (CreatureDefinition.IsHostile) chase/attack anything
    /// IDamageable that enters range; neutral creatures ignore detection entirely and only react
    /// to TakeDamage by fleeing (GDD 17: "Neutral creatures may flee when threatened").
    ///
    /// IDamageable.TakeDamage carries no attacker reference, so a neutral creature's flee
    /// direction is read from whatever is currently sitting in its own detection trigger (the
    /// same reference hostile creatures use as their chase target) rather than the true attacker
    /// - correct for the only damage source that exists today (melee player combat, which
    /// requires standing within a few meters, well inside any reasonable DetectionRadius).
    ///
    /// Explicitly out of scope for this increment (Roadmap Phase 8 marks these as later
    /// refinements, same style as every other stage's first pass): no animations (no
    /// Animator/rig for creatures yet), no ranged/pack behavior, no line-of-sight raycast (only
    /// trigger-radius perception), no persistence/respawn (needs Save, Phase 11).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(SphereCollider))]
    public class Creature : MonoBehaviour, IDamageable
    {
        [SerializeField] private CreatureDefinition _definition;

        [Header("Loot")]
        [SerializeField] private float _lootScatterRadius = 1f;
        [SerializeField] private float _lootScatterForce = 1.5f;
        [SerializeField] private float _lootUpwardForce = 2f;

        [Header("Death")]
        [SerializeField] private float _despawnDelaySeconds = 5f;

        private NavMeshAgent _agent;
        private Vector3 _spawnPosition;
        private CreatureState _state;
        private IDamageable _target;
        private Transform _targetTransform;
        private Vector3 _lastKnownTargetPosition;
        private float _nextAttackTime;
        private float _stateTimer;
        private bool _hasDroppedLoot;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _definition != null ? _definition.MaxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0f;
        public CreatureState State => _state;

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogError($"{nameof(Creature)} on '{name}' has no Definition assigned.", this);
                enabled = false;
                return;
            }

            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = _definition.MoveSpeed;

            SphereCollider detector = GetComponent<SphereCollider>();
            detector.isTrigger = true;
            detector.radius = _definition.DetectionRadius;

            CurrentHealth = _definition.MaxHealth;
            _spawnPosition = transform.position;
            EnterPatrol();
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            switch (_state)
            {
                case CreatureState.Patrol: TickPatrol(); break;
                case CreatureState.Chase: TickChase(); break;
                case CreatureState.Attack: TickAttack(); break;
                case CreatureState.Search: TickSearch(); break;
                case CreatureState.Return: TickReturn(); break;
                case CreatureState.Flee: TickFlee(); break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null || ReferenceEquals(damageable, this))
            {
                return;
            }

            _target = damageable;
            _targetTransform = other.transform;
            _lastKnownTargetPosition = _targetTransform.position;

            bool canStartChase = _state == CreatureState.Patrol || _state == CreatureState.Search || _state == CreatureState.Return;
            if (_definition.IsHostile && canStartChase)
            {
                EnterChase();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (ReferenceEquals(other.GetComponentInParent<IDamageable>(), _target))
            {
                _target = null;
                _targetTransform = null;
            }
        }

        // ---- Patrol: wander to a random point within PatrolRadius of spawn, wait, repeat. ----

        private void EnterPatrol()
        {
            _state = CreatureState.Patrol;
            PickPatrolDestination();
        }

        private void PickPatrolDestination()
        {
            Vector2 offset = Random.insideUnitCircle * _definition.PatrolRadius;
            Vector3 destination = _spawnPosition + new Vector3(offset.x, 0f, offset.y);
            _agent.speed = _definition.MoveSpeed;
            _agent.SetDestination(destination);
            _stateTimer = 0f;
        }

        private void TickPatrol()
        {
            if (_definition.IsHostile && _target != null)
            {
                EnterChase();
                return;
            }

            if (HasArrived())
            {
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= _definition.PatrolWaitSeconds)
                {
                    PickPatrolDestination();
                }
            }
        }

        // ---- Chase: hostile only, follows the target until within AttackRange. ----

        private void EnterChase()
        {
            _state = CreatureState.Chase;
            _agent.speed = _definition.MoveSpeed * _definition.ChaseSpeedMultiplier;
        }

        private void TickChase()
        {
            if (_target == null || !_target.IsAlive)
            {
                EnterSearch();
                return;
            }

            _lastKnownTargetPosition = _targetTransform.position;
            _agent.SetDestination(_lastKnownTargetPosition);

            if (Vector3.Distance(transform.position, _lastKnownTargetPosition) <= _definition.AttackRange)
            {
                EnterAttack();
            }
        }

        // ---- Attack: stand and swing on cooldown while the target stays in range. ----

        private void EnterAttack()
        {
            _state = CreatureState.Attack;
            _agent.ResetPath();
        }

        private void TickAttack()
        {
            if (_target == null || !_target.IsAlive)
            {
                EnterSearch();
                return;
            }

            _lastKnownTargetPosition = _targetTransform.position;
            if (Vector3.Distance(transform.position, _lastKnownTargetPosition) > _definition.AttackRange)
            {
                EnterChase();
                return;
            }

            FaceTowards(_lastKnownTargetPosition);

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _definition.AttackCooldown;
                _target.TakeDamage(_definition.AttackDamage);
                Debug.Log($"{name} attacked for {_definition.AttackDamage}.", this);
            }
        }

        // ---- Search: hostile only, move to the target's last known position and wait. ----

        private void EnterSearch()
        {
            _state = CreatureState.Search;
            _agent.speed = _definition.MoveSpeed;
            _agent.SetDestination(_lastKnownTargetPosition);
            _stateTimer = 0f;
        }

        private void TickSearch()
        {
            if (_definition.IsHostile && _target != null)
            {
                EnterChase();
                return;
            }

            if (HasArrived())
            {
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= _definition.SearchWaitSeconds)
                {
                    EnterReturn();
                }
            }
        }

        // ---- Return: walk back to spawn, then resume patrolling. ----

        private void EnterReturn()
        {
            _state = CreatureState.Return;
            _agent.speed = _definition.MoveSpeed;
            _agent.SetDestination(_spawnPosition);
        }

        private void TickReturn()
        {
            if (_definition.IsHostile && _target != null)
            {
                EnterChase();
                return;
            }

            if (HasArrived())
            {
                EnterPatrol();
            }
        }

        // ---- Flee: neutral only, run away from the threat, then Return. ----

        private void EnterFlee(Vector3 threatPosition)
        {
            _state = CreatureState.Flee;
            _agent.speed = _definition.MoveSpeed * _definition.ChaseSpeedMultiplier;

            Vector3 away = transform.position - threatPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                Vector2 random = Random.insideUnitCircle;
                away = new Vector3(random.x, 0f, random.y);
            }
            away.Normalize();

            Vector3 fleeTarget = transform.position + away * _definition.FleeDistance;
            if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit hit, _definition.FleeDistance, NavMesh.AllAreas))
            {
                fleeTarget = hit.position;
            }

            _agent.SetDestination(fleeTarget);
        }

        private void TickFlee()
        {
            if (HasArrived())
            {
                EnterReturn();
            }
        }

        private bool HasArrived()
        {
            return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        private void FaceTowards(Vector3 worldPosition)
        {
            Vector3 facing = worldPosition - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(facing);
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Debug.Log($"{name} took {amount} damage ({CurrentHealth}/{MaxHealth} HP left).", this);

            if (CurrentHealth <= 0f)
            {
                Die();
                return;
            }

            if (_definition.IsHostile)
            {
                if (_state != CreatureState.Chase && _state != CreatureState.Attack)
                {
                    EnterChase();
                }
            }
            else if (_state != CreatureState.Flee)
            {
                Vector3 threatPosition = _targetTransform != null ? _targetTransform.position : transform.position - transform.forward;
                EnterFlee(threatPosition);
            }
        }

        private void Die()
        {
            _state = CreatureState.Dead;
            _agent.ResetPath();
            _agent.enabled = false;
            Debug.Log($"{name} defeated.", this);

            if (!_hasDroppedLoot)
            {
                _hasDroppedLoot = true;
                SpawnLoot();
            }

            Destroy(gameObject, _despawnDelaySeconds);
        }

        /// <summary>Same independent-chance roll + physical-ItemPickup scatter path
        /// ResourceNode.SpawnDrops/CombatDummy.SpawnLoot already use, reusing ResourceNodeDrop
        /// as-is - "item + amount + chance" means the same thing for a defeated creature.</summary>
        private void SpawnLoot()
        {
            foreach (ResourceNodeDrop drop in _definition.LootDrops)
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
            _despawnDelaySeconds = Mathf.Max(0f, _despawnDelaySeconds);
            _lootScatterRadius = Mathf.Max(0f, _lootScatterRadius);
            _lootScatterForce = Mathf.Max(0f, _lootScatterForce);
            _lootUpwardForce = Mathf.Max(0f, _lootUpwardForce);
        }
    }
}
