using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Combat;

namespace NAV.Gameplay.Bosses
{
    public enum LeshyAction
    {
        Slam,
        Sweep,
        Roar
    }

    /// <summary>
    /// The Leshy's fighting (boss 1, the developer's design 2026-11-06): a slow living tree
    /// with two telegraphed blows instead of a creature's instant bite (ICreatureAttack):
    ///  - Slam: both arms raised for a long shaking wind-up, then brought down in front of him -
    ///    everything in a circle there is hit;
    ///  - Sweep: the trunk winds up and an arm swings across - everything in an arc round him.
    /// The timings match his clips (LeshyAnimationSetup): the hit lands HitDelay seconds after
    /// the start, so stepping out of the circle/arc during the wind-up dodges it. He roars when
    /// he comes (PlayEntrance), when a peaceful Leshy is provoked, and once more at half health, after which he is enraged - he
    /// moves and acts faster. Movement, perception, health, loot and death stay Creature's;
    /// the animator listens to the events here (Presentation LeshyAnimator).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class LeshyBoss : MonoBehaviour, ICreatureAttack
    {
        [Serializable]
        private class Blow
        {
            [Tooltip("Damage to each target in the area.")]
            public float Damage = 30f;
            [Tooltip("Seconds from the start of the attack to the hit (at normal speed) - matches the clip.")]
            public float HitDelay = 2f;
            [Tooltip("Seconds the whole attack takes (at normal speed) - he stands committed till then.")]
            public float Duration = 3f;
            [Tooltip("Where the area is centred: metres in front of him.")]
            public float Reach = 2.4f;
            [Tooltip("Radius of the area (m).")]
            public float Radius = 2.6f;
            [Tooltip("Width of the area as an arc round him (degrees, 360 = a full circle round the centre).")]
            [Range(10f, 360f)] public float Arc = 360f;
        }

        [SerializeField] private Blow _slam = new() { Damage = 40f, HitDelay = 2.15f, Duration = 3.6f, Reach = 2f, Radius = 1.7f, Arc = 360f };
        [SerializeField] private Blow _sweep = new() { Damage = 25f, HitDelay = 1.95f, Duration = 3.2f, Reach = 0f, Radius = 3.1f, Arc = 150f };
        [Tooltip("Chance a blow is a slam when the target is close enough for both.")]
        [SerializeField, Range(0f, 1f)] private float _slamChance = 0.5f;
        [Tooltip("Within this distance (m) he may sweep; farther only the slam reaches.")]
        [SerializeField, Min(0f)] private float _sweepMaxDistance = 2.8f;
        [Tooltip("How far above/below him a target can be and still be hit (m).")]
        [SerializeField, Min(0.5f)] private float _verticalReach = 3f;

        [Header("Roar")]
        [SerializeField, Min(0.5f)] private float _roarSeconds = 4.4f;

        [Header("Rage")]
        [Tooltip("At or below this share of health he roars and is enraged.")]
        [SerializeField, Range(0f, 1f)] private float _rageHealthFraction = 0.5f;
        [Tooltip("Enraged: his blows and roar play this much faster.")]
        [SerializeField, Min(1f)] private float _rageActionSpeed = 1.25f;
        [Tooltip("Enraged: he walks this much faster.")]
        [SerializeField, Min(1f)] private float _rageMoveSpeed = 1.3f;

        private const int MaxOverlaps = 32;
        private readonly Collider[] _overlaps = new Collider[MaxOverlaps];
        private readonly HashSet<IDamageable> _struck = new();

        private Creature _creature;
        private float _busyUntil;
        private bool _enraged;
        private bool _rageRoarPending;
        private Coroutine _pendingBlow;

        /// <summary>A blow or roar began - the animator plays it at ActionSpeed.</summary>
        public event Action<LeshyAction> ActionStarted;

        /// <summary>A blow landed now (centre and radius of its area) - dust, shake, sound.</summary>
        public event Action<LeshyAction, Vector3, float> Struck;

        public bool IsBusy => Time.time < _busyUntil;
        public bool IsEnraged => _enraged;

        /// <summary>How fast his actions play (1, faster when enraged).</summary>
        public float ActionSpeed => _enraged ? _rageActionSpeed : 1f;

        /// <summary>Seconds from the start of an action to its moment (a blow's hit, the roar's
        /// cry) at the current speed - sounds are timed to it.</summary>
        public float MomentOf(LeshyAction action)
        {
            float seconds = action == LeshyAction.Slam ? _slam.HitDelay
                : action == LeshyAction.Sweep ? _sweep.HitDelay
                : _roarSeconds * 0.22f; // the cry after the deep inhale (Leshy_Roar keys)
            return seconds / ActionSpeed;
        }

        private void Awake()
        {
            _creature = GetComponent<Creature>();
        }

        private void OnEnable()
        {
            _creature.Died += HandleDied;
            _creature.Provoked += HandleProvoked;
        }

        private void OnDisable()
        {
            _creature.Died -= HandleDied;
            _creature.Provoked -= HandleProvoked;
        }

        private void Update()
        {
            if (!_creature.IsAlive || _creature.IsRemoteCopy)
            {
                return;
            }

            if (!_enraged && _creature.CurrentHealth <= _creature.MaxHealth * _rageHealthFraction)
            {
                _enraged = true;
                _rageRoarPending = true;
                _creature.SpeedMultiplier = _rageMoveSpeed;
                if (TryGetComponent(out BossActor actor))
                {
                    actor.IsEnraged = true;
                }
            }

            if (_rageRoarPending && !IsBusy)
            {
                _rageRoarPending = false;
                Roar();
            }
        }

        /// <summary>His entrance: a roar before he goes for anyone.</summary>
        public void PlayEntrance()
        {
            Roar();
        }

        public bool TryBeginAttack(IDamageable target, Transform targetTransform)
        {
            if (IsBusy || !_creature.IsAlive || targetTransform == null)
            {
                return false;
            }

            Vector3 offset = targetTransform.position - transform.position;
            offset.y = 0f;
            bool slam = offset.magnitude > _sweepMaxDistance || UnityEngine.Random.value < _slamChance;
            Blow blow = slam ? _slam : _sweep;
            LeshyAction action = slam ? LeshyAction.Slam : LeshyAction.Sweep;

            float speed = ActionSpeed;
            _busyUntil = Time.time + blow.Duration / speed;
            ActionStarted?.Invoke(action);
            _pendingBlow = StartCoroutine(StrikeAfter(blow, action, blow.HitDelay / speed));
            return true;
        }

        private void Roar()
        {
            _busyUntil = Time.time + _roarSeconds / ActionSpeed;
            ActionStarted?.Invoke(LeshyAction.Roar);
        }

        private IEnumerator StrikeAfter(Blow blow, LeshyAction action, float delay)
        {
            yield return new WaitForSeconds(delay);
            _pendingBlow = null;
            if (_creature.IsAlive)
            {
                Strike(blow, action);
            }
        }

        /// <summary>Hits everything damageable in the blow's area once - players (a networked
        /// player's avatar passes it to its owner), not other creatures.</summary>
        private void Strike(Blow blow, LeshyAction action)
        {
            Vector3 centre = transform.position + transform.forward * blow.Reach;
            int count = Physics.OverlapSphereNonAlloc(centre + Vector3.up * 1f, blow.Radius + 0.5f, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            _struck.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider hit = _overlaps[i];
                if (hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target is Creature || !target.IsAlive || _struck.Contains(target))
                {
                    continue;
                }

                Vector3 point = hit.ClosestPoint(centre);
                Vector3 flat = point - centre;
                float vertical = Mathf.Abs(point.y - transform.position.y);
                flat.y = 0f;
                if (flat.magnitude > blow.Radius || vertical > _verticalReach)
                {
                    continue;
                }

                if (blow.Arc < 360f)
                {
                    Vector3 fromBody = point - transform.position;
                    fromBody.y = 0f;
                    if (fromBody.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, fromBody) > blow.Arc * 0.5f)
                    {
                        continue;
                    }
                }

                _struck.Add(target);
                if (target is IRemoteProxy remote)
                {
                    remote.TakeCreatureDamage(blow.Damage, 0f, 0f);
                }
                else
                {
                    target.TakeDamage(blow.Damage);
                }

                if (hit.GetComponentInParent<IHitReactive>() is IHitReactive reactive)
                {
                    Vector3 away = point - transform.position;
                    away.y = 0f;
                    reactive.ReactToHit(away.sqrMagnitude > 0.01f ? away.normalized : transform.forward);
                }

                Debug.Log($"{name}: {action} hit {hit.transform.root.name} for {blow.Damage}.", this);
            }

            Struck?.Invoke(action, centre, blow.Radius);
        }

        /// <summary>A peaceful Leshy was hit or angered: he roars before he goes for you.</summary>
        private void HandleProvoked()
        {
            if (IsBusy)
            {
                _rageRoarPending = true;
                return;
            }

            Roar();
        }

        private void HandleDied()
        {
            if (_pendingBlow != null)
            {
                StopCoroutine(_pendingBlow);
                _pendingBlow = null;
            }

            _busyUntil = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * _slam.Reach, _slam.Radius);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * _sweep.Reach, _sweep.Radius);
        }
    }
}
