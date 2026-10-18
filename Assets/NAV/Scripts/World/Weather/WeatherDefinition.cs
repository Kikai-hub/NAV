using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.World.Weather
{
    /// <summary>One possible next weather and how likely it is (relative weight).</summary>
    [Serializable]
    public class WeatherTransition
    {
        [SerializeField] private WeatherDefinition _target;
        [SerializeField, Min(0f)] private float _weight = 1f;

        public WeatherDefinition Target => _target;
        public float Weight => _weight;
    }

    /// <summary>
    /// One weather state (GDD 5: Clear, Rain, Heavy rain, Thunderstorm): how long it lasts, what
    /// can follow it, and how it looks/sounds. WeatherSystem owns the state machine;
    /// DayNightLighting and WeatherEffects (Presentation) read the look values, blended between
    /// the previous and current weather during a transition. Multipliers are applied on top of
    /// the day/night values, so a storm at noon and a storm at night both read correctly.
    /// Purely visual for now - weather has no gameplay effect yet.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherDefinition", menuName = "NAV/World/Weather Definition")]
    public class WeatherDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;

        [Header("State Machine")]
        [SerializeField, Min(1f)] private float _minDurationSeconds = 120f;
        [SerializeField, Min(1f)] private float _maxDurationSeconds = 240f;
        [Tooltip("Weighted choices for the next weather once this one ends. Empty = this weather repeats.")]
        [SerializeField] private List<WeatherTransition> _transitions = new();

        [Header("Rain")]
        [Tooltip("Rain particles emitted per second around the camera. 0 = dry.")]
        [SerializeField, Min(0f)] private float _rainRate;

        [Header("Light (multipliers on the day/night values)")]
        [SerializeField, Range(0f, 1f)] private float _sunMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float _ambientMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float _skyExposureMultiplier = 1f;
        [Tooltip("0 = sky keeps its day/night tint, 1 = fully grey overcast.")]
        [SerializeField, Range(0f, 1f)] private float _overcast;

        [Header("Fog")]
        [Tooltip("Exponential-squared fog density. 0 = no fog (clear weather keeps fog off).")]
        [SerializeField, Min(0f)] private float _fogDensity;

        [Header("Lightning")]
        [SerializeField] private bool _hasLightning;
        [SerializeField, Min(0.5f)] private float _minLightningInterval = 6f;
        [SerializeField, Min(0.5f)] private float _maxLightningInterval = 16f;

        public string Id => _id;
        public string DisplayName => _displayName;
        public float MinDurationSeconds => _minDurationSeconds;
        public float MaxDurationSeconds => _maxDurationSeconds;
        public IReadOnlyList<WeatherTransition> Transitions => _transitions;
        public float RainRate => _rainRate;
        public float SunMultiplier => _sunMultiplier;
        public float AmbientMultiplier => _ambientMultiplier;
        public float SkyExposureMultiplier => _skyExposureMultiplier;
        public float Overcast => _overcast;
        public float FogDensity => _fogDensity;
        public bool HasLightning => _hasLightning;
        public float MinLightningInterval => _minLightningInterval;
        public float MaxLightningInterval => _maxLightningInterval;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = name;
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }

            _maxDurationSeconds = Mathf.Max(_minDurationSeconds, _maxDurationSeconds);
            _maxLightningInterval = Mathf.Max(_minLightningInterval, _maxLightningInterval);
        }
    }
}
