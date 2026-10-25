using System.Collections.Generic;
using UnityEngine;
using NAV.Core.Settings;
using NAV.Gameplay.Building;
using NAV.World.Biomes;
using NAV.World.Generation;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// The world's background sound: each biome's day loop (birds, cicadas, the sea), night loop
    /// and wind loop (BiomeDefinition's Ambience fields), mixed by how much of each biome lies
    /// around the listener (the centre and a ring ~30 m out, so a border blends and the sea is
    /// heard from the shore), cross-faded between day and night around sunrise/sunset, birds and
    /// cicadas hushed by rain, wind louder in bad weather, plus the rain itself (light/heavy loops
    /// by the weather's rain rate) - quieter under a roof. One looping 2D AudioSource per clip,
    /// each easing to its target volume; everything scaled by the Ambience volume setting.
    /// </summary>
    public class AmbienceController : MonoBehaviour
    {
        private const float SampleInterval = 0.25f;
        private const float FadeSeconds = 1.5f;
        // Day/night cross-fade, hours either side of sunrise/sunset.
        private const float TwilightHours = 0.75f;

        [SerializeField] private WorldGenerator _worldGenerator;
        [SerializeField] private WorldClock _clock;
        [SerializeField] private WeatherSystem _weather;
        [Tooltip("Optional - defaults to Camera.main.")]
        [SerializeField] private Transform _listener;
        [Tooltip("Optional - found by its PlayerMotor. Used for the under-a-roof check.")]
        [SerializeField] private Transform _player;

        [Header("Rain")]
        [SerializeField] private AudioClip _lightRain;
        [SerializeField] private AudioClip _heavyRain;
        [Tooltip("Weather rain rate (particles/s) that counts as heavy rain.")]
        [SerializeField, Min(1f)] private float _heavyRainRate = 4000f;
        [SerializeField, Range(0f, 1f)] private float _rainVolume = 0.7f;

        [Header("Mix")]
        [Tooltip("Distance (m) of the sampling ring around the listener.")]
        [SerializeField, Min(1f)] private float _sampleRadius = 30f;
        [Tooltip("Rain and wind under a roof (a building piece overhead).")]
        [SerializeField, Range(0f, 1f)] private float _indoorFactor = 0.45f;

        private readonly Dictionary<AudioClip, AudioSource> _sources = new();
        private readonly Dictionary<AudioClip, float> _targets = new();
        private readonly Dictionary<BiomeDefinition, float> _biomeWeights = new();
        private float _nextSample;
        private float _indoor;

        private void Awake()
        {
            if (_clock == null || _weather == null)
            {
                Debug.LogError($"{nameof(AmbienceController)} on '{name}' is missing a required reference (Clock/Weather).", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_listener == null)
            {
                if (UnityEngine.Camera.main == null)
                {
                    return;
                }

                _listener = UnityEngine.Camera.main.transform;
            }

            if (Time.unscaledTime >= _nextSample)
            {
                _nextSample = Time.unscaledTime + SampleInterval;
                SampleBiomes();
                _indoor = IsUnderRoof() ? 1f : 0f;
                ComputeTargets();
            }

            float master = GameSettings.GetVolume(AudioCategory.Ambience);
            float step = Time.unscaledDeltaTime / FadeSeconds;
            foreach (KeyValuePair<AudioClip, AudioSource> pair in _sources)
            {
                _targets.TryGetValue(pair.Key, out float target);
                AudioSource source = pair.Value;
                source.volume = Mathf.MoveTowards(source.volume, target * master, step);
            }
        }

        /// <summary>Weights of the biomes at the listener (double) and at 8 points around it.</summary>
        private void SampleBiomes()
        {
            _biomeWeights.Clear();
            if (_worldGenerator == null)
            {
                return;
            }

            Vector3 centre = _listener.position;
            AddWeight(_worldGenerator.GetBiomeAt(centre), 2f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                AddWeight(_worldGenerator.GetBiomeAt(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _sampleRadius), 1f);
            }

            float total = 0f;
            foreach (float weight in _biomeWeights.Values)
            {
                total += weight;
            }

            if (total <= 0f)
            {
                return;
            }

            var keys = new List<BiomeDefinition>(_biomeWeights.Keys);
            foreach (BiomeDefinition biome in keys)
            {
                _biomeWeights[biome] /= total;
            }
        }

        private void AddWeight(BiomeDefinition biome, float weight)
        {
            if (biome == null)
            {
                return;
            }

            _biomeWeights.TryGetValue(biome, out float current);
            _biomeWeights[biome] = current + weight;
        }

        private void ComputeTargets()
        {
            var keys = new List<AudioClip>(_targets.Keys);
            foreach (AudioClip clip in keys)
            {
                _targets[clip] = 0f;
            }

            float day = DayFactor();
            bool hasWeather = _weather.Current != null;
            float rainRate = hasWeather ? _weather.Evaluate(w => w.RainRate) : 0f;
            float overcast = hasWeather ? _weather.Evaluate(w => w.Overcast) : 0f;
            float rain = Mathf.Clamp01(rainRate / _heavyRainRate);
            float outdoor = Mathf.Lerp(1f, _indoorFactor, _indoor);

            // Creatures fall quiet in the rain; the wind picks up with the clouds.
            float life = 1f - 0.8f * Mathf.Clamp01(rain * 2f);
            float wind = (1f + overcast * 1.2f) * outdoor;

            foreach (KeyValuePair<BiomeDefinition, float> pair in _biomeWeights)
            {
                BiomeDefinition biome = pair.Key;
                float weight = pair.Value;
                if (biome.DayAmbience == biome.NightAmbience)
                {
                    // One loop day and night (the sea) - weather doesn't silence it.
                    Add(biome.DayAmbience, weight * biome.AmbienceVolume);
                }
                else
                {
                    Add(biome.DayAmbience, weight * biome.AmbienceVolume * day * life);
                    Add(biome.NightAmbience, weight * biome.AmbienceVolume * (1f - day) * life);
                }

                Add(biome.WindAmbience, weight * biome.WindVolume * wind);
            }

            // Light rain fades into heavy as the rate climbs.
            float heavy = Mathf.Clamp01((rain - 0.35f) / 0.5f);
            Add(_lightRain, Mathf.Clamp01(rain * 3f) * (1f - heavy) * _rainVolume * outdoor);
            Add(_heavyRain, heavy * _rainVolume * outdoor);
        }

        private void Add(AudioClip clip, float volume)
        {
            if (clip == null || volume <= 0f)
            {
                return;
            }

            if (!_sources.ContainsKey(clip))
            {
                CreateSource(clip);
            }

            _targets.TryGetValue(clip, out float current);
            _targets[clip] = Mathf.Clamp01(current + volume);
        }

        private void CreateSource(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0f;
            source.priority = 32;
            // Started at a random point so two loops of the same length never line up.
            source.time = Random.Range(0f, clip.length * 0.9f);
            source.Play();
            _sources[clip] = source;
            _targets[clip] = 0f;
        }

        /// <summary>1 by day, 0 at night, cross-fading over TwilightHours around sunrise/sunset.</summary>
        private float DayFactor()
        {
            TimeOfDaySettings settings = _clock.Settings;
            float hour = _clock.Hour;
            float sinceSunrise = Mathf.DeltaAngle(settings.SunriseHour * 15f, hour * 15f) / 15f;
            float toSunset = Mathf.DeltaAngle(hour * 15f, settings.SunsetHour * 15f) / 15f;
            float morning = Mathf.Clamp01(0.5f + sinceSunrise / (2f * TwilightHours));
            float evening = Mathf.Clamp01(0.5f + toSunset / (2f * TwilightHours));
            return Mathf.SmoothStep(0f, 1f, Mathf.Min(morning, evening));
        }

        /// <summary>A building piece over the player (not the camera - it may be outside the
        /// hut while the player stands in it).</summary>
        private bool IsUnderRoof()
        {
            if (_player == null)
            {
                var motor = FindAnyObjectByType<NAV.Gameplay.Player.PlayerMotor>();
                _player = motor != null ? motor.transform : _listener;
            }

            Vector3 head = _player.position + Vector3.up * 1.6f;
            return Physics.Raycast(head, Vector3.up, out RaycastHit hit, 15f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<BuildingPiece>() != null;
        }
    }
}
