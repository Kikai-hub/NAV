using UnityEngine;
using UnityEngine.AI;
using NAV.Gameplay.AI;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Drives an animal model's Animator from what the Creature actually does. Made for the
    /// ithappy Animals pack controllers (one blend tree: "Vert" 0 = idle .. 1 = moving, "State"
    /// 0 = walk .. 1 = run) - the movement itself stays the NavMeshAgent's, the model only shows it:
    ///  - Vert/State follow the agent's real speed, and the playback speed is scaled so the feet
    ///    match the ground (Walk/Run Clip Speed = how fast each clip moves at this model's scale);
    ///  - the pack has no attack/death clips, so those are procedural: an attack is a short lunge
    ///    of the model toward the target (Creature.AttackPerformed), death rolls the model onto
    ///    its side and freezes the Animator (Creature.Died).
    /// Presentation only - HitFeedback leaves the death fall of such a creature to this.
    /// </summary>
    public class CreatureAnimator : MonoBehaviour
    {
        private static readonly int VertHash = Animator.StringToHash("Vert");
        private static readonly int StateHash = Animator.StringToHash("State");

        [SerializeField] private Creature _creature;
        [SerializeField] private Animator _animator;
        [Tooltip("The model's root (child of the creature) - moved by the lunge and the death roll.")]
        [SerializeField] private Transform _model;

        [Header("Locomotion")]
        [Tooltip("Ground speed (m/s) the walk clip shows at this model's scale.")]
        [SerializeField, Min(0.05f)] private float _walkClipSpeed = 1f;
        [Tooltip("Ground speed (m/s) the run clip shows at this model's scale.")]
        [SerializeField, Min(0.05f)] private float _runClipSpeed = 4f;
        [Tooltip("How fast Vert/State follow the speed (units per second) - same as the pack's own mover.")]
        [SerializeField, Min(0.1f)] private float _blendRate = 4.5f;
        [SerializeField] private Vector2 _playbackSpeedRange = new(0.6f, 1.8f);

        [Header("Attack Lunge")]
        [SerializeField, Min(0f)] private float _lungeDistance = 0.4f;
        [SerializeField, Min(0.05f)] private float _lungeSeconds = 0.35f;
        [Tooltip("Head-down pitch at the middle of the lunge (degrees).")]
        [SerializeField] private float _lungePitch = 8f;

        [Header("Death")]
        [SerializeField, Min(0.05f)] private float _deathRollSeconds = 0.6f;

        private NavMeshAgent _agent;
        private float _vert;
        private float _state;
        private Vector3 _modelBasePosition;
        private Quaternion _modelBaseRotation;
        private float _lungeStart = -1f;
        private bool _dead;
        private float _deathStart;
        private float _deathRolled;
        private float _halfWidth;
        private float _rollSide = 1f;

        private void Awake()
        {
            if (_creature == null || _animator == null || _model == null)
            {
                Debug.LogError($"{nameof(CreatureAnimator)} on '{name}' is missing its Creature, Animator or Model.", this);
                enabled = false;
                return;
            }

            _agent = _creature.GetComponent<NavMeshAgent>();
            _modelBasePosition = _model.localPosition;
            _modelBaseRotation = _model.localRotation;

            var renderer = _model.GetComponentInChildren<Renderer>();
            _halfWidth = renderer != null ? renderer.bounds.extents.x : 0.3f;
        }

        private void OnEnable()
        {
            if (_creature == null)
            {
                return;
            }

            _creature.AttackPerformed += HandleAttack;
            _creature.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (_creature == null)
            {
                return;
            }

            _creature.AttackPerformed -= HandleAttack;
            _creature.Died -= HandleDied;
        }

        private void HandleAttack()
        {
            _lungeStart = Time.time;
        }

        private void HandleDied()
        {
            _dead = true;
            _deathStart = Time.time;
            _lungeStart = -1f;
            _model.localPosition = _modelBasePosition;
            _model.localRotation = _modelBaseRotation;
            _rollSide = Random.value < 0.5f ? -1f : 1f;
            // Standing pose for the fall; the Animator is frozen once it lies down.
            _animator.SetFloat(VertHash, 0f);
            _animator.speed = 1f;
        }

        private void Update()
        {
            if (_dead)
            {
                TickDeath();
                return;
            }

            TickLocomotion();
            TickLunge();
        }

        private void TickLocomotion()
        {
            Vector3 velocity = _agent != null && _agent.enabled ? _agent.velocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool moving = speed > 0.1f;

            float targetVert = moving ? 1f : 0f;
            float targetState = Mathf.InverseLerp(_walkClipSpeed, _runClipSpeed, speed);
            float step = _blendRate * Time.deltaTime;
            _vert = Mathf.MoveTowards(_vert, targetVert, step);
            _state = Mathf.MoveTowards(_state, targetState, step);
            _animator.SetFloat(VertHash, _vert);
            _animator.SetFloat(StateHash, _state);

            // Feet match the ground: play the blended clip at the agent's speed over its own.
            float clipSpeed = Mathf.Lerp(_walkClipSpeed, _runClipSpeed, _state);
            _animator.speed = moving ? Mathf.Clamp(speed / clipSpeed, _playbackSpeedRange.x, _playbackSpeedRange.y) : 1f;
        }

        private void TickLunge()
        {
            if (_lungeStart < 0f)
            {
                return;
            }

            float progress = (Time.time - _lungeStart) / _lungeSeconds;
            if (progress >= 1f)
            {
                _lungeStart = -1f;
                _model.localPosition = _modelBasePosition;
                _model.localRotation = _modelBaseRotation;
                return;
            }

            // Fast out, slower back.
            float amount = progress < 0.35f ? progress / 0.35f : 1f - (progress - 0.35f) / 0.65f;
            amount = Mathf.SmoothStep(0f, 1f, amount);
            _model.localPosition = _modelBasePosition + Vector3.forward * (_lungeDistance * amount);
            _model.localRotation = _modelBaseRotation * Quaternion.Euler(_lungePitch * amount, 0f, 0f);
        }

        private void TickDeath()
        {
            if (_deathRolled >= 90f)
            {
                return;
            }

            float progress = Mathf.Clamp01((Time.time - _deathStart) / _deathRollSeconds);
            // Accelerating, like toppling under gravity; rolls about the edge of its feet.
            float angle = 90f * progress * progress;
            Vector3 pivot = _model.parent.position + _model.parent.right * (_halfWidth * _rollSide);
            _model.RotateAround(pivot, _model.parent.forward, -(angle - _deathRolled) * _rollSide);
            _deathRolled = angle;

            if (progress >= 1f)
            {
                _animator.speed = 0f;
            }
        }
    }
}
