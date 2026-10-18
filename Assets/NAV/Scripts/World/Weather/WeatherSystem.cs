using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.World.Weather
{
    /// <summary>
    /// The weather state machine (GDD 5): holds the current WeatherDefinition for a random
    /// duration in its [Min, Max] range, then picks the next one from its weighted Transitions.
    /// Each change blends from the previous weather over TransitionSeconds - Blend goes 0 -> 1 -
    /// so Presentation can lerp every look value instead of snapping. Runs on scaled time
    /// (paused with the game). Pure state + events, like WorldClock: DayNightLighting and
    /// WeatherEffects render it, SaveManager persists it.
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [Tooltip("Every weather that can occur - also how a saved weather id is resolved on load.")]
        [SerializeField] private List<WeatherDefinition> _weathers = new();
        [SerializeField] private WeatherDefinition _initialWeather;
        [SerializeField, Min(0.1f)] private float _transitionSeconds = 30f;

        [Header("Debug")]
        [Tooltip("Pick a weather and use the context menu 'Force Debug Weather' (Play Mode) to switch to it immediately.")]
        [SerializeField] private WeatherDefinition _debugWeather;

        private readonly System.Random _rng = new();
        private float _blendTimer;

        public WeatherDefinition Current { get; private set; }

        /// <summary>The weather being blended away from (equals Current once Blend reaches 1).</summary>
        public WeatherDefinition Previous { get; private set; }

        /// <summary>0 = still looks like Previous, 1 = fully Current.</summary>
        public float Blend => _transitionSeconds > 0f ? Mathf.Clamp01(_blendTimer / _transitionSeconds) : 1f;

        /// <summary>Seconds until the state machine picks the next weather.</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>Fires with the new weather whenever it changes.</summary>
        public event Action<WeatherDefinition> WeatherChanged;

        private void Awake()
        {
            _weathers.RemoveAll(w => w == null);
            if (_weathers.Count == 0)
            {
                Debug.LogError($"{nameof(WeatherSystem)} on '{name}' has no weathers assigned.", this);
                enabled = false;
                return;
            }

            WeatherDefinition start = _initialWeather != null ? _initialWeather : _weathers[0];
            Previous = start;
            Current = start;
            _blendTimer = _transitionSeconds;
            RemainingSeconds = RollDuration(start);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _blendTimer += dt;
            RemainingSeconds -= dt;

            if (RemainingSeconds <= 0f)
            {
                SetWeather(PickNext(Current), blend: true);
            }
        }

        /// <summary>Lerps any per-weather value between Previous and Current by Blend.</summary>
        public float Evaluate(Func<WeatherDefinition, float> selector)
        {
            if (Current == null)
            {
                return 0f;
            }

            return Mathf.Lerp(selector(Previous), selector(Current), Blend);
        }

        public WeatherDefinition FindById(string id)
        {
            return _weathers.Find(w => w.Id == id);
        }

        /// <summary>Switches immediately with no blend and a given remaining time - used by
        /// SaveManager on load. Unknown ids are ignored (keeps the initial weather).</summary>
        public void Restore(string weatherId, float remainingSeconds)
        {
            WeatherDefinition weather = FindById(weatherId);
            if (weather == null)
            {
                return;
            }

            SetWeather(weather, blend: false);
            if (remainingSeconds > 0f)
            {
                RemainingSeconds = remainingSeconds;
            }
        }

        public void SetWeather(WeatherDefinition weather, bool blend)
        {
            if (weather == null)
            {
                return;
            }

            // Mid-blend switches start from whatever is currently visible (approximately).
            Previous = blend ? (Blend >= 0.5f ? Current : Previous) : weather;
            Current = weather;
            _blendTimer = blend ? 0f : _transitionSeconds;
            RemainingSeconds = RollDuration(weather);
            Debug.Log($"{nameof(WeatherSystem)}: weather -> {weather.DisplayName} for {RemainingSeconds:F0}s.", this);
            WeatherChanged?.Invoke(weather);
        }

        [ContextMenu("Force Debug Weather")]
        private void ForceDebugWeather()
        {
            if (Application.isPlaying && _debugWeather != null)
            {
                SetWeather(_debugWeather, blend: false);
            }
        }

        private WeatherDefinition PickNext(WeatherDefinition from)
        {
            float total = 0f;
            foreach (WeatherTransition transition in from.Transitions)
            {
                if (transition.Target != null)
                {
                    total += transition.Weight;
                }
            }

            if (total <= 0f)
            {
                return from;
            }

            double roll = _rng.NextDouble() * total;
            foreach (WeatherTransition transition in from.Transitions)
            {
                if (transition.Target == null)
                {
                    continue;
                }

                roll -= transition.Weight;
                if (roll <= 0d)
                {
                    return transition.Target;
                }
            }

            return from;
        }

        private float RollDuration(WeatherDefinition weather)
        {
            return Mathf.Lerp(weather.MinDurationSeconds, weather.MaxDurationSeconds, (float)_rng.NextDouble());
        }
    }
}
