using System.Collections;
using UnityEngine;
using NAV.Core.Settings;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;
using NAV.Presentation.Animation;
using NAV.Presentation.Camera;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// The Leshy's sounds (LeshySoundSet), all in 3D at where they happen:
    ///  - a presence loop round him while he lives (creak, rustle, hum) - heard before he is seen;
    ///  - a heavy footstep each time LegGrounding sets a foot down;
    ///  - his roar timed to the cry of the roar clip; the slam's straining wood at the wind-up and
    ///    its impact on the hit (with a camera shake that fades with distance); the sweep's
    ///    whoosh just before the arm passes; a wooden groan when a blow makes him flinch;
    ///  - death: the long creak of a falling tree, then the body hitting the ground (and a shake).
    /// Volumes follow the Effects volume setting. Presentation only.
    /// </summary>
    public class LeshyAudio : MonoBehaviour
    {
        [SerializeField] private LeshySoundSet _sounds;
        [SerializeField] private Creature _creature;
        [SerializeField] private LeshyBoss _boss;
        [Tooltip("Optional - footsteps on each planted foot.")]
        [SerializeField] private LegGrounding _legs;

        [Header("Timing")]
        [Tooltip("Seconds before the blow lands that the sweep's whoosh starts.")]
        [SerializeField, Min(0f)] private float _whooshLead = 0.4f;
        [SerializeField, Min(0f)] private float _hurtCooldown = 2.5f;
        [Tooltip("Seconds from death to the body hitting the ground.")]
        [SerializeField, Min(0f)] private float _fallSeconds = 1.3f;

        [Header("Camera Shake")]
        [SerializeField, Min(0f)] private float _slamShakeDegrees = 2.2f;
        [SerializeField, Min(0f)] private float _fallShakeDegrees = 1.6f;
        [Tooltip("Beyond this distance (m) the ground doesn't shake for the player.")]
        [SerializeField, Min(1f)] private float _shakeDistance = 25f;

        private const int OneShotSources = 6;
        private const float MinDistance = 3f;

        private AudioSource _presence;
        private AudioSource[] _oneShots;
        private int _next;
        private float _lastHealth;
        private float _nextHurt;
        private ThirdPersonCameraController _camera;

        private void Awake()
        {
            if (_sounds == null || _creature == null || _boss == null)
            {
                Debug.LogError($"{nameof(LeshyAudio)} on '{name}' is missing its Sound Set, Creature or LeshyBoss.", this);
                enabled = false;
                return;
            }

            _oneShots = new AudioSource[OneShotSources];
            for (int i = 0; i < OneShotSources; i++)
            {
                var go = new GameObject("LeshySound" + i);
                go.transform.SetParent(transform, false);
                _oneShots[i] = CreateSource(go);
            }

            _presence = CreateSource(gameObject);
            _lastHealth = _creature.MaxHealth;
        }

        private void OnEnable()
        {
            if (_boss == null || _creature == null)
            {
                return;
            }

            _boss.ActionStarted += HandleAction;
            _boss.Struck += HandleStruck;
            _creature.Died += HandleDied;
            if (_legs != null)
            {
                _legs.FootPlanted += HandleFootPlanted;
            }
        }

        private void OnDisable()
        {
            if (_boss == null || _creature == null)
            {
                return;
            }

            _boss.ActionStarted -= HandleAction;
            _boss.Struck -= HandleStruck;
            _creature.Died -= HandleDied;
            if (_legs != null)
            {
                _legs.FootPlanted -= HandleFootPlanted;
            }
        }

        private void Start()
        {
            AudioClip loop = _sounds.Presence.Pick();
            if (loop == null)
            {
                return;
            }

            _presence.clip = loop;
            _presence.loop = true;
            _presence.maxDistance = _sounds.Presence.MaxDistance;
            _presence.volume = 0f;
            _presence.time = Random.Range(0f, loop.length);
            _presence.Play();
        }

        private void Update()
        {
            float effects = GameSettings.GetVolume(AudioCategory.Effects);
            float presenceTarget = _creature.IsAlive ? _sounds.Presence.Volume * effects : 0f;
            _presence.volume = Mathf.MoveTowards(_presence.volume, presenceTarget, Time.deltaTime * (_creature.IsAlive ? 0.4f : 0.6f));

            float health = _creature.CurrentHealth;
            if (_creature.IsAlive && health < _lastHealth - 0.01f && Time.time >= _nextHurt && !_boss.IsBusy)
            {
                _nextHurt = Time.time + _hurtCooldown;
                Play(_sounds.Hurt, transform.position + Vector3.up * 3f);
            }

            _lastHealth = health;
        }

        private void HandleAction(LeshyAction action)
        {
            switch (action)
            {
                case LeshyAction.Roar:
                    StartCoroutine(PlayAfter(_sounds.Roar, _boss.MomentOf(LeshyAction.Roar), 3.8f));
                    break;
                case LeshyAction.Slam:
                    StartCoroutine(PlayAfter(_sounds.SlamWindup, 0.25f, 3f));
                    break;
                case LeshyAction.Sweep:
                    StartCoroutine(PlayAfter(_sounds.SweepWhoosh, Mathf.Max(0f, _boss.MomentOf(LeshyAction.Sweep) - _whooshLead), 2.5f));
                    break;
            }
        }

        private void HandleStruck(LeshyAction action, Vector3 centre, float radius)
        {
            if (action != LeshyAction.Slam)
            {
                return;
            }

            Play(_sounds.SlamImpact, centre);
            Shake(centre, _slamShakeDegrees, 0.6f);
        }

        private void HandleFootPlanted(Vector3 point)
        {
            if (_creature.IsAlive)
            {
                Play(_sounds.Footstep, point);
            }
        }

        private void HandleDied()
        {
            StopAllCoroutines();
            Play(_sounds.DeathCreak, transform.position + Vector3.up * 3f);
            StartCoroutine(FallImpact());
        }

        private IEnumerator FallImpact()
        {
            yield return new WaitForSeconds(_fallSeconds);
            Play(_sounds.DeathImpact, transform.position + transform.forward * 1.5f);
            Shake(transform.position, _fallShakeDegrees, 0.8f);
        }

        private IEnumerator PlayAfter(LeshySoundSet.Slot slot, float delay, float height)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_creature.IsAlive)
            {
                Play(slot, transform.position + Vector3.up * height);
            }
        }

        private void Play(LeshySoundSet.Slot slot, Vector3 position)
        {
            AudioClip clip = slot.Pick();
            if (clip == null)
            {
                return;
            }

            AudioSource source = _oneShots[_next];
            _next = (_next + 1) % _oneShots.Length;
            source.transform.position = position;
            source.maxDistance = slot.MaxDistance;
            source.pitch = Random.Range(slot.Pitch.x, slot.Pitch.y);
            source.volume = slot.Volume * GameSettings.GetVolume(AudioCategory.Effects);
            source.clip = clip;
            source.Play();
        }

        private void Shake(Vector3 origin, float degrees, float seconds)
        {
            if (_camera == null)
            {
                _camera = FindAnyObjectByType<ThirdPersonCameraController>();
                if (_camera == null)
                {
                    return;
                }
            }

            float distance = Vector3.Distance(_camera.transform.position, origin);
            float strength = degrees * Mathf.Clamp01(1f - distance / _shakeDistance);
            if (strength > 0.05f)
            {
                _camera.AddShake(strength, seconds);
            }
        }

        private static AudioSource CreateSource(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
