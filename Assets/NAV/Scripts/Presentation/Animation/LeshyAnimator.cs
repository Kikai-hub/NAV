using UnityEngine;
using UnityEngine.AI;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Plays the Leshy's own clips (Leshy.controller, LeshyAnimationSetup) from what LeshyBoss
    /// and his Creature do:
    ///  - "Speed" follows the agent's ground speed (blend Idle -> Walk), and the walk is played
    ///    at the speed that keeps his feet on the ground;
    ///  - his blows and roars (LeshyBoss.ActionStarted) fire their triggers and play at
    ///    LeshyBoss.ActionSpeed (faster once enraged);
    ///  - a hit that lands while he is free makes him flinch (not more often than HitCooldown -
    ///    a tree doesn't stagger under every swing);
    ///  - death hands the body to its Ragdoll (CLAUDE.md "Death Rule"), a frame later so the
    ///    killing blow's direction is known.
    /// Presentation only.
    /// </summary>
    public class LeshyAnimator : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int SlamHash = Animator.StringToHash("Slam");
        private static readonly int SweepHash = Animator.StringToHash("Sweep");
        private static readonly int RoarHash = Animator.StringToHash("Roar");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        [SerializeField] private Creature _creature;
        [SerializeField] private LeshyBoss _boss;
        [SerializeField] private Animator _animator;
        [Tooltip("Ground speed (m/s) the walk clip shows (LeshyAnimationSetup.WalkSpeed).")]
        [SerializeField, Min(0.05f)] private float _walkClipSpeed = 1.05f;
        [SerializeField] private Vector2 _walkPlaybackRange = new(0.6f, 1.8f);
        [Tooltip("How fast Speed follows the agent (m/s per second).")]
        [SerializeField, Min(0.1f)] private float _speedBlendRate = 3f;
        [SerializeField, Min(0f)] private float _hitCooldown = 4f;

        [Header("Death")]
        [Tooltip("The body's ragdoll (NAV > Animation > Build Ragdolls).")]
        [SerializeField] private Ragdoll _ragdoll;
        [SerializeField, Min(0f)] private float _deathPush = 1.5f;

        private NavMeshAgent _agent;
        private float _speed;
        private float _lastHealth;
        private float _nextHitTime;
        private bool _ragdollPending;
        private Vector3 _lastVelocity;

        private void Awake()
        {
            if (_creature == null || _boss == null || _animator == null)
            {
                Debug.LogError($"{nameof(LeshyAnimator)} on '{name}' is missing its Creature, LeshyBoss or Animator.", this);
                enabled = false;
                return;
            }

            _agent = _creature.GetComponent<NavMeshAgent>();
            _lastHealth = _creature.MaxHealth;
        }

        private void OnEnable()
        {
            if (_boss == null || _creature == null)
            {
                return;
            }

            _boss.ActionStarted += HandleAction;
            _creature.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (_boss == null || _creature == null)
            {
                return;
            }

            _boss.ActionStarted -= HandleAction;
            _creature.Died -= HandleDied;
        }

        private void HandleAction(LeshyAction action)
        {
            _animator.ResetTrigger(HitHash);
            _animator.SetTrigger(action == LeshyAction.Slam ? SlamHash : action == LeshyAction.Sweep ? SweepHash : RoarHash);
        }

        private void HandleDied()
        {
            _ragdollPending = _ragdoll != null;
            if (_ragdoll == null)
            {
                Debug.LogWarning($"{name}: no Ragdoll - run NAV > Animation > Build Ragdolls (Death Rule).", this);
                _animator.speed = 0f;
            }
        }

        private void Update()
        {
            if (!_creature.IsAlive)
            {
                if (_ragdollPending)
                {
                    _ragdollPending = false;
                    _ragdoll.Activate(_lastVelocity, _creature.LastHitDirection * _deathPush);
                }

                return;
            }

            Vector3 velocity = _creature.IsRemoteCopy ? _creature.RemoteVelocity
                : _agent != null && _agent.enabled ? _agent.velocity : Vector3.zero;
            velocity.y = 0f;
            _lastVelocity = velocity;
            _speed = Mathf.MoveTowards(_speed, velocity.magnitude, _speedBlendRate * Time.deltaTime);
            _animator.SetFloat(SpeedHash, _speed);

            if (_boss.IsBusy)
            {
                _animator.speed = _boss.ActionSpeed;
            }
            else
            {
                _animator.speed = _speed > 0.1f
                    ? Mathf.Clamp(_speed / _walkClipSpeed, _walkPlaybackRange.x, _walkPlaybackRange.y)
                    : 1f;
            }

            float health = _creature.CurrentHealth;
            if (health < _lastHealth - 0.01f && !_boss.IsBusy && Time.time >= _nextHitTime)
            {
                _nextHitTime = Time.time + _hitCooldown;
                _animator.SetTrigger(HitHash);
            }

            _lastHealth = health;
        }
    }
}
