using UnityEngine;
using NAV.World.Weather;

namespace NAV.Presentation.Weather
{
    /// <summary>
    /// Renders WeatherSystem's state: a rain particle volume that follows the camera, scene fog
    /// density, lightning flashes (a separate shadowless directional light pulsed for a moment,
    /// so it never fights DayNightLighting over the sun) followed by delayed thunder, and a rain
    /// ambience loop whose volume follows rain intensity. Light/sky darkening is
    /// DayNightLighting's job (it reads the same WeatherSystem).
    ///
    /// Everything is built in code at startup - no prefab/asset setup beyond assigning the
    /// WeatherSystem. Audio is a procedural placeholder (PlaceholderWeatherAudio): final
    /// audio is "Not Yet Decided" per PROJECT_STATE.md. Known limitation: rain falls through
    /// roofs (particles have no collision) - fine for open-world placeholder.
    /// </summary>
    public class WeatherEffects : MonoBehaviour
    {
        private const float RainAreaSize = 36f;
        private const float RainHeightAboveCamera = 14f;
        private const float RainFallSpeed = 22f;
        private const float MaxRainRateForAudio = 5000f;
        private const float FlashDuration = 0.18f;

        [SerializeField] private WeatherSystem _weather;
        [Tooltip("Optional - defaults to Camera.main. Rain and audio follow it.")]
        [SerializeField] private Transform _followTarget;
        [SerializeField, Min(0f)] private float _rainVolume = 0.6f;
        [SerializeField, Min(0f)] private float _thunderVolume = 0.9f;
        [SerializeField, Min(0f)] private float _lightningIntensity = 3f;

        private ParticleSystem _rain;
        private ParticleSystem.EmissionModule _rainEmission;
        private AudioSource _rainAudio;
        private AudioSource _thunderAudio;
        private AudioClip[] _thunderClips;
        private Light _lightning;
        private Material _rainMaterial;

        private float _nextLightningTime;
        private float _flashTimer;
        private float _thunderDelay = -1f;
        private bool _fogWasEnabled;

        private void Awake()
        {
            if (_weather == null)
            {
                Debug.LogError($"{nameof(WeatherEffects)} on '{name}' is missing its WeatherSystem.", this);
                enabled = false;
                return;
            }

            // Fully qualified: inside NAV.Presentation, "Camera" resolves to the
            // NAV.Presentation.Camera namespace.
            if (_followTarget == null && UnityEngine.Camera.main != null)
            {
                _followTarget = UnityEngine.Camera.main.transform;
            }

            BuildRain();
            BuildLightning();
            BuildAudio();
            _fogWasEnabled = RenderSettings.fog;
            ScheduleNextLightning();
        }

        private void OnDestroy()
        {
            RenderSettings.fog = _fogWasEnabled;
            if (_rainMaterial != null)
            {
                Destroy(_rainMaterial);
            }
        }

        private void LateUpdate()
        {
            if (_followTarget != null)
            {
                transform.position = _followTarget.position;
            }

            float rainRate = _weather.Evaluate(w => w.RainRate);
            _rainEmission.rateOverTime = rainRate;
            _rainAudio.volume = Mathf.Clamp01(rainRate / MaxRainRateForAudio) * _rainVolume;

            float fog = _weather.Evaluate(w => w.FogDensity);
            RenderSettings.fog = fog > 0.0005f;
            if (RenderSettings.fog)
            {
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = fog;
            }

            TickLightning();
        }

        private void TickLightning()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                // Double flicker: bright, dip, bright, fade.
                float t = 1f - Mathf.Clamp01(_flashTimer / FlashDuration);
                float flicker = t < 0.3f ? 1f : t < 0.45f ? 0.25f : 1f - (t - 0.45f) / 0.55f;
                _lightning.intensity = _lightningIntensity * Mathf.Max(0f, flicker);
                _lightning.enabled = _flashTimer > 0f;
            }

            if (_thunderDelay >= 0f)
            {
                _thunderDelay -= Time.deltaTime;
                if (_thunderDelay < 0f)
                {
                    _thunderAudio.PlayOneShot(_thunderClips[Random.Range(0, _thunderClips.Length)], _thunderVolume);
                }
            }

            WeatherDefinition current = _weather.Current;
            bool stormActive = current != null && current.HasLightning && _weather.Blend > 0.7f;
            if (!stormActive || Time.time < _nextLightningTime)
            {
                return;
            }

            Flash();
            ScheduleNextLightning();
        }

        /// <summary>Triggers one lightning strike now (also used for testing).</summary>
        public void Flash()
        {
            _lightning.transform.rotation = Quaternion.Euler(Random.Range(35f, 70f), Random.Range(0f, 360f), 0f);
            _flashTimer = FlashDuration;
            _lightning.enabled = true;
            // Sound travels slower than light - a distant strike rumbles a few seconds later.
            _thunderDelay = Random.Range(0.4f, 3f);
        }

        private void ScheduleNextLightning()
        {
            WeatherDefinition current = _weather.Current;
            float min = current != null ? current.MinLightningInterval : 6f;
            float max = current != null ? current.MaxLightningInterval : 16f;
            _nextLightningTime = Time.time + Random.Range(min, max);
        }

        private void BuildRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, RainHeightAboveCamera, 0f);

            _rain = go.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _rain.main;
            main.loop = true;
            main.startLifetime = (RainHeightAboveCamera + 6f) / RainFallSpeed;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.75f, 0.8f, 0.9f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 12000;

            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(RainAreaSize, 0.5f, RainAreaSize);

            ParticleSystem.VelocityOverLifetimeModule velocity = _rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, -0.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-RainFallSpeed, -RainFallSpeed * 0.85f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            _rainEmission = _rain.emission;
            _rainEmission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            // Streak length comes from fall speed only (lengthScale 0), so the screen-size cap
            // below thins drops passing right next to the camera without shortening them -
            // otherwise those read as thick white poles (tuned in Play Mode).
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 0f;
            renderer.maxParticleSize = 0.006f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                _rainMaterial = new Material(shader) { name = "Rain (runtime)" };
                // Transparent, alpha-blended (URP particle shader surface settings).
                _rainMaterial.SetFloat("_Surface", 1f);
                _rainMaterial.SetFloat("_Blend", 0f);
                _rainMaterial.SetOverrideTag("RenderType", "Transparent");
                _rainMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _rainMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _rainMaterial.SetInt("_ZWrite", 0);
                _rainMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _rainMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                renderer.sharedMaterial = _rainMaterial;
            }
            else
            {
                Debug.LogError($"{nameof(WeatherEffects)}: URP particle shader not found - rain will render pink.", this);
            }

            _rain.Play();
        }

        private void BuildLightning()
        {
            var go = new GameObject("Lightning");
            go.transform.SetParent(transform, false);
            _lightning = go.AddComponent<Light>();
            _lightning.type = LightType.Directional;
            _lightning.color = new Color(0.85f, 0.9f, 1f);
            _lightning.shadows = LightShadows.None;
            _lightning.intensity = 0f;
            _lightning.enabled = false;
        }

        private void BuildAudio()
        {
            _rainAudio = gameObject.AddComponent<AudioSource>();
            _rainAudio.clip = PlaceholderWeatherAudio.CreateRainLoop();
            _rainAudio.loop = true;
            _rainAudio.spatialBlend = 0f;
            _rainAudio.volume = 0f;
            _rainAudio.Play();

            _thunderAudio = gameObject.AddComponent<AudioSource>();
            _thunderAudio.spatialBlend = 0f;
            _thunderClips = new[]
            {
                PlaceholderWeatherAudio.CreateThunder(1),
                PlaceholderWeatherAudio.CreateThunder(2),
                PlaceholderWeatherAudio.CreateThunder(3),
            };
        }
    }
}
