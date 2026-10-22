using UnityEngine;
using NAV.Core.Settings;
using NAV.Gameplay.Environment;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Environment
{
    /// <summary>
    /// Makes the water react to the player: rings spread from where they wade or swim (fed to
    /// the NAV/Water shader as global ripple sources), a splash of droplets and a splash sound
    /// when they enter the water (bigger when they fall or jump in), quieter sloshing while they
    /// move in it. Other systems can add their own rings with Emit (e.g. something falling in).
    /// </summary>
    public class WaterInteraction : MonoBehaviour
    {
        private const int RippleCount = 16;   // NAV/Water's RIPPLE_COUNT
        private static readonly int RipplesId = Shader.PropertyToID("_NAVWaterRipples");
        private static readonly Vector4[] Ripples = new Vector4[RippleCount];
        private static int _nextRipple;

        [SerializeField] private PlayerMotor _player;
        [Tooltip("HitParticle.mat (URP Particles/Unlit) - a material asset so the shader is in builds.")]
        [SerializeField] private Material _particleMaterialTemplate;
        [Tooltip("Falling or jumping in.")]
        [SerializeField] private AudioClip _bigSplash;
        [Tooltip("Stepping in, wading, swimming strokes - one picked at random.")]
        [SerializeField] private AudioClip[] _splashes = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

        [Header("Feel")]
        [Tooltip("Entering faster than this (m/s down) is a big splash.")]
        [SerializeField, Min(0f)] private float _bigSplashSpeed = 4f;
        [Tooltip("Seconds between rings while moving (at full run / slowly).")]
        [SerializeField] private Vector2 _moveRippleInterval = new(0.25f, 0.6f);
        [SerializeField, Min(0.1f)] private float _idleSwimRippleInterval = 1.3f;

        private const float InWaterDepth = 0.05f;
        private const float MovingSpeed = 0.4f;

        private ParticleSystem _particles;
        private AudioSource _audio;
        private float _previousY;
        private bool _wasInWater;
        private float _nextRippleTime;
        private float _nextSlosh;

        /// <summary>Adds a ring spreading from position (on the water surface) - strength ~0.3
        /// (a gentle stroke) to 1.5 (a big splash).</summary>
        public static void Emit(Vector3 position, float strength)
        {
            Ripples[_nextRipple] = new Vector4(position.x, position.z, Time.timeSinceLevelLoad, strength);
            _nextRipple = (_nextRipple + 1) % RippleCount;
            Shader.SetGlobalVectorArray(RipplesId, Ripples);
        }

        private void Awake()
        {
            if (_player == null)
            {
                Debug.LogError($"{nameof(WaterInteraction)} on '{name}' needs the Player (PlayerMotor).", this);
                enabled = false;
                return;
            }

            // Stale entries from a previous Play session would still be "alive" for the shader.
            for (int i = 0; i < RippleCount; i++)
            {
                Ripples[i] = new Vector4(0f, 0f, -1000f, 0f);
            }

            Shader.SetGlobalVectorArray(RipplesId, Ripples);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.minDistance = 3f;
            _audio.maxDistance = 30f;
            CreateParticles();
            _previousY = _player.transform.position.y;
        }

        private void Update()
        {
            Vector3 position = _player.transform.position;
            float fallSpeed = Time.deltaTime > 0f ? (_previousY - position.y) / Time.deltaTime : 0f;
            _previousY = position.y;

            bool hasWater = WaterBody.TryGetSurface(position, out float surface);
            if (!hasWater || _player.WaterDepth <= InWaterDepth)
            {
                _wasInWater = false;
                return;
            }

            Vector3 onSurface = new Vector3(position.x, surface, position.z);
            _audio.transform.position = onSurface;

            if (!_wasInWater)
            {
                _wasInWater = true;
                bool big = fallSpeed > _bigSplashSpeed;
                Splash(onSurface, big ? 1.5f : 0.7f, big ? 40 : 14);
                Play(big ? _bigSplash : RandomSplash(), big ? 1f : 0.5f);
            }

            bool moving = _player.CurrentSpeed > MovingSpeed;
            if (Time.time >= _nextRippleTime && (moving || _player.IsSwimming))
            {
                float speed01 = Mathf.Clamp01(_player.CurrentSpeed / 6f);
                Emit(onSurface, moving ? Mathf.Lerp(0.45f, 0.8f, speed01) : 0.3f);
                _nextRippleTime = Time.time + (moving ? Mathf.Lerp(_moveRippleInterval.y, _moveRippleInterval.x, speed01) : _idleSwimRippleInterval);

                if (moving && speed01 > 0.5f && !_player.IsSwimming)
                {
                    Splash(onSurface, 0.3f, 5);
                }
            }

            if (moving && Time.time >= _nextSlosh)
            {
                Play(RandomSplash(), _player.IsSwimming ? 0.22f : 0.15f);
                _nextSlosh = Time.time + Random.Range(0.55f, 0.9f);
            }
        }

        private void Splash(Vector3 position, float strength, int droplets)
        {
            Emit(position, strength);
            _particles.transform.position = position;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            ParticleSystem.MainModule main = _particles.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * strength + 1f, 3.5f * strength + 1.5f);
            _particles.Emit(emit, droplets);
        }

        private AudioClip RandomSplash()
        {
            return _splashes.Length > 0 ? _splashes[Random.Range(0, _splashes.Length)] : null;
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            _audio.pitch = Random.Range(0.9f, 1.1f);
            _audio.PlayOneShot(clip, volume * _volume * GameSettings.GetVolume(AudioCategory.Effects));
        }

        private void CreateParticles()
        {
            var particleObject = new GameObject("Water Splash Particles");
            particleObject.transform.SetParent(transform, false);
            _particles = particleObject.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.85f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            if (_particleMaterialTemplate != null)
            {
                particleRenderer.material = new Material(_particleMaterialTemplate) { name = "Water Splash (runtime)" };
            }
            else
            {
                Debug.LogWarning($"{nameof(WaterInteraction)} on '{name}' has no Particle Material Template - splashes use the default particle material.", this);
            }
        }
    }
}
