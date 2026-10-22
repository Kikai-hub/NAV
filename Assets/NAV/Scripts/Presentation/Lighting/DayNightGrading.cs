using UnityEngine;
using UnityEngine.Rendering;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// Colour grading that follows the time of day and the weather. The scene's base Global
    /// Volume carries the everyday "fairy-tale" look; this blends three more global volumes on
    /// top by weight: Golden Hour (warm, saturated - around sunrise and sunset), Night (cool
    /// moonlit blue, desaturated) and Overcast (grey, flat - follows the weather's Overcast).
    /// The look itself lives in the volumes' profiles (built by NAV/Setup/Build Post
    /// Processing); this class only decides how much of each is shown.
    /// </summary>
    public class DayNightGrading : MonoBehaviour
    {
        [SerializeField] private WorldClock _clock;
        [Tooltip("Optional - drives the Overcast volume.")]
        [SerializeField] private WeatherSystem _weather;

        [SerializeField] private Volume _goldenHour;
        [SerializeField] private Volume _night;
        [SerializeField] private Volume _overcast;

        [Tooltip("Hours either side of sunrise/sunset over which the golden hour fades in and out.")]
        [SerializeField, Min(0.1f)] private float _goldenHourSpan = 1.4f;
        [Tooltip("Hours after sunset (and before sunrise) for the night look to reach full strength.")]
        [SerializeField, Min(0.1f)] private float _nightFadeHours = 1.2f;

        private void Awake()
        {
            if (_clock == null)
            {
                Debug.LogError($"{nameof(DayNightGrading)} on '{name}' needs a WorldClock.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            TimeOfDaySettings settings = _clock.Settings;
            float hour = _clock.Hour;

            float toSunrise = HourDistance(hour, settings.SunriseHour);
            float toSunset = HourDistance(hour, settings.SunsetHour);
            float golden = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Min(toSunrise, toSunset) / _goldenHourSpan);

            float night = 0f;
            if (!settings.IsDaytime(hour))
            {
                night = Mathf.SmoothStep(0f, 1f, Mathf.Min(toSunrise, toSunset) / _nightFadeHours);
            }

            float overcast = _weather != null && _weather.Current != null ? _weather.Evaluate(w => w.Overcast) : 0f;

            SetWeight(_goldenHour, golden * (1f - overcast));
            SetWeight(_night, night);
            // At night the night look already greys things out - overcast adds only a little.
            SetWeight(_overcast, overcast * (1f - night * 0.6f));
        }

        private static float HourDistance(float a, float b)
        {
            float d = Mathf.Abs(Mathf.Repeat(a - b, 24f));
            return Mathf.Min(d, 24f - d);
        }

        private static void SetWeight(Volume volume, float weight)
        {
            if (volume != null)
            {
                volume.weight = Mathf.Clamp01(weight);
            }
        }
    }
}
