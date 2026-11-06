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
    public class Creature : MonoBehaviour, IDamageable, IHitReactive, IHitSurface, IUncleanTarget
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

        /// <summary>What a hit on it sounds like - flesh for animals, wood for the Leshy.</summary>
        public HitSurface Surface => _definition != null ? _definition.Surface : HitSurface.Flesh;

        /// <summary>Multiplies every movement speed (a boss's rage). 1 = the definition's speeds.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>A hostile creature that, for now, leaves everyone alone (the Leshy while the
        /// forest isn't angry): it wanders and neither notices nor hunts anything until it is
        /// provoked - hit, or Provoke called (felling trees before its eyes).</summary>
        public bool IsPeaceful { get; private set; }

        /// <summary>A peaceful creature was provoked and turns hostile.</summary>
        public event System.Action Provoked;

        // Hostile right now: by its nature and not (still) peaceful.
        private bool Hostile => _definition.IsHostile && !IsPeaceful;

        /// <summary>Makes a hostile creature peaceful until provoked (it drops any hunt).</summary>
        public void MakePeaceful()
        {
            if (!_definition.IsHostile || !IsAlive)
            {
                return;
            }

            IsPeaceful = true;
            _target = null;
            _targetTransform = null;
            if (_state == CreatureState.Chase || _state == CreatureState.Attack)
            {
                EnterReturn();
            }
        }

        /// <summary>Ends the peace: it turns on the given target (or whoever is near it).</summary>
        public void Provoke(IDamageable target, Transform targetTransform)
        {
            if (!IsPeaceful || !IsAlive)
            {
                return;
            }

            IsPeaceful = false;
            Provoked?.Invoke();
            if (target != null && targetTransform != null && target.IsAlive)
            {
                Notice(target, targetTransform, false);
            }
            else if (_candidate != null && _candidateTransform != null && _candidate.IsAlive)
            {
                Notice(_candidate, _candidateTransform, false);
            }
        }

        // Optional ICreatureAttack on the same object (bosses) - replaces the instant bite.
        private ICreatureAttack _customAttack;

        /// <summary>True for a moment after a melee hit - the creature neither moves on its own
        /// nor attacks (see ReactToHit).</summary>
        public bool IsStaggered => _staggerTimer > 0f;
        public float MaxHealth => _definition != null ? _definition.MaxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0f;
        public CreatureState State => _state;
        public CreatureDefinition Definition => _definition;
        public bool IsUnclean => _definition != null && _definition.IsUnclean;

        /// <summary>Horizontal direction of the last hit that reached ReactToHit (zero if none) -
        /// the death ragdoll falls away from the killing blow.</summary>
        public Vector3 LastHitDirection { get; private set; }

        // ---- Multiplayer (NAV.Network.NetworkCreatureSync) ----

        /// <summary>Which creature this is across the network (owner's actor * 100000 + count); 0 offline.</summary>
        public int NetworkId { get; set; }

        /// <summary>True for a copy of a creature another machine runs: no AI, no NavMesh agent;
        /// moved by the network; hits on it go to its owner (RemoteDamageRequested).</summary>
        public bool IsRemoteCopy { get; private set; }

        /// <summary>A copy's ground velocity as its owner reports it (CreatureAnimator's walk/run).</summary>
        public Vector3 RemoteVelocity { get; set; }

        /// <summary>Who dealt the killing blow when it wasn't this machine's player (the network
        /// sets it with TakeDamageFrom) - then no loot drops here; the killer's machine drops it.</summary>
        public object KilledBy { get; private set; }

        /// <summary>A hit landed on a copy - the network sends it to the owner.</summary>
        public event System.Action<Creature, float> RemoteDamageRequested;

        private object _damageSource;

        /// <summary>Turns this instance into a copy of another machine's creature.</summary>
        public void MakeRemoteCopy(int networkId)
        {
            NetworkId = networkId;
            IsRemoteCopy = true;
            _agent.enabled = false;
            GetComponent<SphereCollider>().enabled = false;
            All.Remove(this);
        }

        /// <summary>Damage from another machine's player (the owner side of a copy's hit).</summary>
        public void TakeDamageFrom(float amount, object source)
        {
            _damageSource = source;
            TakeDamage(amount);
            _damageSource = null;
        }

        /// <summary>The owner says this copy died: the same death here (ragdoll), loot only if
        /// this machine's player killed it.</summary>
        public void DieAsCopy(bool dropLoot)
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHealth = 0f;
            _state = CreatureState.Dead;
            Died?.Invoke();
            if (dropLoot && !_hasDroppedLoot)
            {
                _hasDroppedLoot = true;
                SpawnLoot();
            }

            Destroy(gameObject, _despawnDelaySeconds);
        }

        /// <summary>The owner's creature attacked - the copy plays the same lunge.</summary>
        public void PlayAttackAsCopy()
        {
            AttackPerformed?.Invoke();
        }

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
            _customAttack = GetComponent<ICreatureAttack>();

            SphereCollider detector = GetComponent<SphereCollider>();
            detector.isTrigger = true;
            detector.radius = _definition.DetectionRadius;

            CurrentHealth = _definition.MaxHealth;
            _spawnPosition = transform.position;
            if (CreatingRemoteCopy)
            {
                // A network copy: no AI, and its agent never touches this machine's NavMesh.
                IsRemoteCopy = true;
                _agent.enabled = false;
                detector.enabled = false;
                return;
            }

            EnterPatrol();
        }

        /// <summary>Set by the network while it instantiates a copy of another machine's
        /// creature, so Awake starts it as a copy (no patrol, no agent).</summary>
        public static bool CreatingRemoteCopy { get; set; }

        private void Update()
        {
            // A copy of another machine's creature is moved by the network, not by its own AI.
            if (!IsAlive || IsRemoteCopy)
            {
                return;
            }

            if (_staggerTimer > 0f)
            {
                TickStagger();
                return;
            }

            // Committed to its own action (a boss's blow or roar): it stands where it is.
            if (_customAttack != null && _customAttack.IsBusy)
            {
                if (_agent.hasPath)
                {
                    _agent.ResetPath();
                }

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
            // Copies aren't this machine's creatures - population counts and pack alerts skip them.
            if (!IsRemoteCopy)
            {
                All.Add(this);
            }
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
            // Remote players' avatars are fair game (their bites go to their owner); copies of
            // creatures run on another machine (IsRemoteCopy) don't hunt at all.
            if (damageable == null || damageable is Creature || IsRemoteCopy)
            {
                return;
            }

            // In range is not yet noticed: a hostile creature still has to see it (CanSee, checked
            // in Update). A neutral one only needs to know where the threat is, to flee from it.
            _candidate = damageable;
            _candidateTransform = other.transform;
            if (!Hostile)
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
            if (!Hostile || _candidate == null || _target != null || !CanStartChase || Time.time < _nextSightCheck)
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
            if (!IsAlive || !Hostile || !CanStartChase || target == null || !target.IsAlive)
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
            _agent.speed = _definition.MoveSpeed * SpeedMultiplier;
            _agent.SetDestination(destination);
            _stateTimer = 0f;
        }

        private void TickPatrol()
        {
            if (Hostile && _target != null)
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
            _agent.speed = _definition.MoveSpeed * _definition.ChaseSpeedMultiplier * SpeedMultiplier;
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

            if (_customAttack != null)
            {
                // Its own telegraphed blow - once it has turned to face the target.
                if (Time.time >= _nextAttackTime && IsFacing(_lastKnownTargetPosition, CustomAttackFacingDegrees)
                    && _customAttack.TryBeginAttack(_target, _targetTransform))
                {
                    _nextAttackTime = Time.time + _definition.AttackCooldown;
                    AttackPerformed?.Invoke();
                }

                return;
            }

            if (Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _definition.AttackCooldown;
                if (_target is IRemoteProxy remotePlayer)
                {
                    // Another machine's player: its owner takes the bite (and the poison).
                    remotePlayer.TakeCreatureDamage(_definition.AttackDamage, _definition.PoisonDamage, _definition.PoisonSeconds);
                }
                else
                {
                    _target.TakeDamage(_definition.AttackDamage);
                }

                // A poisonous bite: the poison works whether or not the hit was blocked - a
                // shield stops teeth, not venom on them (keeps it simple; revisit with real fights).
                if (!(_target is IRemoteProxy) && _definition.PoisonDamage > 0f && _targetTransform != null && _targetTransform.GetComponentInParent<IPoisonable>() is IPoisonable poisonable)
                {
                    poisonable.ApplyPoison(_definition.PoisonDamage, _definition.PoisonSeconds);
                }

                AttackPerformed?.Invoke();
                Debug.Log($"{name} attacked for {_definition.AttackDamage}.", this);
            }
        }

        // ---- Search: hostile only, move to the target's last known position and wait. ----

        private void EnterSearch()
        {
            _state = CreatureState.Search;
            _agent.speed = _definition.MoveSpeed * SpeedMultiplier;
            _agent.SetDestination(_lastKnownTargetPosition);
            _stateTimer = 0f;
        }

        private void TickSearch()
        {
            if (Hostile && _target != null)
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
            _agent.speed = _definition.MoveSpeed * SpeedMultiplier;
            _agent.SetDestination(_spawnPosition);
        }

        private void TickReturn()
        {
            if (Hostile && _target != null)
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
            _agent.speed = _definition.MoveSpeed * _definition.ChaseSpeedMultiplier * SpeedMultiplier;

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

        private const float CustomAttackFacingDegrees = 25f;

        /// <summary>Turns to face a point - at once, or at the definition's TurnSpeed (a heavy
        /// boss swings round slowly, so a target can get behind it).</summary>
        private void FaceTowards(Vector3 worldPosition)
        {
            Vector3 facing = worldPosition - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion look = Quaternion.LookRotation(facing);
            transform.rotation = _definition.TurnSpeed > 0f
                ? Quaternion.RotateTowards(transform.rotation, look, _definition.TurnSpeed * SpeedMultiplier * Time.deltaTime)
                : look;
        }

        private bool IsFacing(Vector3 worldPosition, float degrees)
        {
            Vector3 facing = worldPosition - transform.position;
            facing.y = 0f;
            return facing.sqrMagnitude <= 0.001f || Vector3.Angle(transform.forward, facing) <= degrees;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            if (IsRemoteCopy)
            {
                // Its owner's machine decides (and answers with DieAsCopy).
                RemoteDamageRequested?.Invoke(this, amount);
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Debug.Log($"{name} took {amount} damage ({CurrentHealth}/{MaxHealth} HP left).", this);

            if (CurrentHealth <= 0f)
            {
                Die();
                return;
            }

            // A peaceful creature that is hit is provoked - it turns on whoever is near it.
            if (IsPeaceful)
            {
                IsPeaceful = false;
                Provoked?.Invoke();
            }

            if (Hostile)
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
            direction.y = 0f;
            LastHitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            if (!IsAlive || _definition.StaggerSeconds <= 0f)
            {
                return;
            }

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
            KilledBy = _damageSource;
            Died?.Invoke();

            // Killed by another machine's player: the loot drops on their machine instead.
            if (!_hasDroppedLoot && KilledBy == null)
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
                Terrain terrain = TerrainUnder(spawnPosition);
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

        // The ground is several terrain tiles - the one whose square holds the point.
        private static Terrain TerrainUnder(Vector3 point)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= origin.x && point.z >= origin.z && point.x < origin.x + size.x && point.z < origin.z + size.z)
                {
                    return terrain;
                }
            }

            return null;
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
