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
    /// IDamageable they notice: in range is only a candidate until it is inside their field of
    /// view with a clear line of sight (or close enough to be heard) - checked a few times a
    /// second (TickPerception). Noticing (or being hit) alerts creatures of the same kind within
    /// PackAlertRadius, so a pack attacks together. Neutral creatures ignore detection and only
    /// react to TakeDamage by fleeing (GDD 17: "Neutral creatures may flee when threatened").
    /// Respawning and night-time numbers are the population manager's job
    /// (World CreaturePopulation), not this class's.
    ///
    /// IDamageable.TakeDamage carries no attacker reference, so a neutral creature's flee
    /// direction is read from whatever is currently sitting in its own detection trigger (the
    /// same reference hostile creatures use as their chase target) rather than the true attacker
    /// - correct for the only damage source that exists today (melee player combat, which
    /// requires standing within a few meters, well inside any reasonable DetectionRadius).
    ///
    /// Still out of scope: ranged attacks, creatures saved with the world (they're regenerated
    /// on load).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(SphereCollider))]
    public class Creature : MonoBehaviour, IDamageable, IHitReactive, IHitSurface
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
        private float _staggerTimer;
        private Vector3 _knockbackVelocity;

        private const float SightCheckInterval = 0.2f;
        private const float TargetChestHeight = 1.2f;
        private const float LootGroundClearance = 0.3f;
        private readonly RaycastHit[] _sightHits = new RaycastHit[8];

        // Something damageable inside the detection trigger, not (yet) noticed - see TickPerception.
        private IDamageable _candidate;
        private Transform _candidateTransform;
        private float _nextSightCheck;

        /// <summary>Every living, enabled creature - pack alerts and the population manager.</summary>
        public static readonly System.Collections.Generic.List<Creature> All = new();

        /// <summary>Who spawned it (the population manager's bookkeeping - e.g. a biome spawn
        /// entry). Opaque to the creature itself.</summary>
        public object SpawnSource { get; set; }

        /// <summary>Chasing or attacking something.</summary>
        public bool IsEngaged => _state == CreatureState.Chase || _state == CreatureState.Attack;

        public float CurrentHealth { get; private set; }
        public HitSurface Surface => HitSurface.Flesh;

        /// <summary>True for a moment after a melee hit - the creature neither moves on its own
        /// nor attacks (see ReactToHit).</summary>
        public bool IsStaggered => _staggerTimer > 0f;
        public float MaxHealth => _definition != null ? _definition.MaxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0f;
        public CreatureState State => _state;
        public CreatureDefinition Definition => _definition;

        /// <summary>Fires when an attack lands its damage - for the attack animation/sound.</summary>
        public event System.Action AttackPerformed;

        /// <summary>Fires once, the moment health reaches 0 - for the death animation.</summary>
        public event System.Action Died;

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

            if (_staggerTimer > 0f)
            {
                TickStagger();
                return;
            }

            TickPerception();

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

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();

            // Other creatures are ignored entirely: with several spawned per biome, a wolf's
            // detection radius would otherwise pull it into fights with every wolf/boar nearby.
            if (damageable == null || damageable is Creature)
            {
                return;
            }

            // In range is not yet noticed: a hostile creature still has to see it (CanSee, checked
            // in Update). A neutral one only needs to know where the threat is, to flee from it.
            _candidate = damageable;
            _candidateTransform = other.transform;
            if (!_definition.IsHostile)
            {
                _target = damageable;
                _targetTransform = other.transform;
                _lastKnownTargetPosition = _targetTransform.position;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (ReferenceEquals(damageable, _candidate))
            {
                _candidate = null;
                _candidateTransform = null;
            }

            if (ReferenceEquals(damageable, _target))
            {
                _target = null;
                _targetTransform = null;
            }
        }

        // ---- Perception: line of sight + field of view, or close enough to hear. ----

        private bool CanStartChase => _state == CreatureState.Patrol || _state == CreatureState.Search || _state == CreatureState.Return;

        private void TickPerception()
        {
            if (!_definition.IsHostile || _candidate == null || _target != null || !CanStartChase || Time.time < _nextSightCheck)
            {
                return;
            }

            _nextSightCheck = Time.time + SightCheckInterval;
            if (_candidateTransform != null && _candidate.IsAlive && CanSee(_candidateTransform))
            {
                Notice(_candidate, _candidateTransform, true);
            }
        }

        /// <summary>Within AlwaysNoticeDistance: yes (heard). Otherwise the target must be inside
        /// the field of view and nothing solid between the eyes and its chest.</summary>
        private bool CanSee(Transform target)
        {
            Vector3 eye = transform.position + Vector3.up * _definition.EyeHeight;
            Vector3 aim = target.position + Vector3.up * TargetChestHeight;
            Vector3 toTarget = aim - eye;
            float distance = toTarget.magnitude;
            if (distance <= _definition.AlwaysNoticeDistance)
            {
                return true;
            }

            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > _definition.ViewAngle * 0.5f)
            {
                return false;
            }

            // Anything solid in between (terrain, trees, rocks, walls) blocks the view; its own
            // colliders and the target's don't count.
            int hits = Physics.RaycastNonAlloc(eye, toTarget / distance, _sightHits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Transform hit = _sightHits[i].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(target.root))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Starts hunting a target. alertPack: tell creatures of the same kind nearby
        /// (PackAlertRadius) - they join in without needing to see it themselves.</summary>
        private void Notice(IDamageable target, Transform targetTransform, bool alertPack)
        {
            _target = target;
            _targetTransform = targetTransform;
            _lastKnownTargetPosition = targetTransform.position;
            EnterChase();

            if (!alertPack || _definition.PackAlertRadius <= 0f)
            {
                return;
            }

            foreach (Creature other in All)
            {
                if (other != this && other.IsAlive && other._definition == _definition
                    && Vector3.Distance(other.transform.position, transform.position) <= _definition.PackAlertRadius)
                {
                    other.Alert(target, targetTransform);
                }
            }
        }

        /// <summary>A packmate spotted something: join the hunt if not busy already.</summary>
        public void Alert(IDamageable target, Transform targetTransform)
        {
            if (!IsAlive || !_definition.IsHostile || !CanStartChase || target == null || !target.IsAlive)
            {
                return;
            }

            Notice(target, targetTransform, false);
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
            // Stop on the spot: coasting from a 6 m/s chase would slide it a metre into the
            // target. The attack lunge (CreatureAnimator) supplies the forward motion instead.
            _agent.velocity = Vector3.zero;
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
                AttackPerformed?.Invoke();
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
                    // Hit by something it hadn't noticed (from behind) - whatever is in its
                    // detection range is the attacker; it turns on it and calls the pack.
                    if (_candidate != null && _candidateTransform != null && _candidate.IsAlive)
                    {
                        Notice(_candidate, _candidateTransform, true);
                    }
                    else
                    {
                        EnterChase();
                    }
                }
            }
            else if (_state != CreatureState.Flee)
            {
                Vector3 threatPosition = _targetTransform != null ? _targetTransform.position : transform.position - transform.forward;
                EnterFlee(threatPosition);
            }
        }

        /// <summary>Stagger + knockback: the agent stops (its path is kept - TakeDamage has
        /// already switched to Chase/Flee, which resumes when the stagger ends), the body slides
        /// KnockbackDistance along the swing, and the next attack waits until it's over.</summary>
        public void ReactToHit(Vector3 direction)
        {
            if (!IsAlive || _definition.StaggerSeconds <= 0f)
            {
                return;
            }

            direction.y = 0f;
            _knockbackVelocity = direction.sqrMagnitude > 0.0001f
                ? direction.normalized * (2f * _definition.KnockbackDistance / _definition.StaggerSeconds)
                : Vector3.zero;
            _staggerTimer = _definition.StaggerSeconds;
            _nextAttackTime = Mathf.Max(_nextAttackTime, Time.time + _definition.StaggerSeconds);
            _agent.isStopped = true;
        }

        private void TickStagger()
        {
            // Velocity falls linearly to 0 over the stagger, so the slide covers KnockbackDistance
            // (that's what the 2x in ReactToHit is for). Agent.Move keeps the body on the NavMesh.
            float fraction = _staggerTimer / _definition.StaggerSeconds;
            _agent.Move(_knockbackVelocity * (fraction * Time.deltaTime));

            _staggerTimer -= Time.deltaTime;
            if (_staggerTimer <= 0f)
            {
                _staggerTimer = 0f;
                _agent.isStopped = false;
            }
        }

        private void Die()
        {
            _staggerTimer = 0f;
            _state = CreatureState.Dead;
            _agent.ResetPath();
            _agent.enabled = false;
            Debug.Log($"{name} defeated.", this);
            Died?.Invoke();

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

                // On a slope the scattered point can be inside the hill - small drops (fangs)
                // would then fall through the terrain. Start them just above the ground there.
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float ground = terrain.transform.position.y + terrain.SampleHeight(spawnPosition) + LootGroundClearance;
                    spawnPosition.y = Mathf.Max(spawnPosition.y, ground);
                }

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
