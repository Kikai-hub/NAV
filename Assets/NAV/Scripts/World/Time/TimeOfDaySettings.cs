using UnityEngine;

namespace NAV.World.TimeOfDay
{
    /// <summary>
    /// Tunables for the day/night cycle (GDD 5): how long a day lasts in real time, when the
    /// sun rises/sets, and how light, ambient and sky look over the day. WorldClock reads the
    /// timing half; DayNightLighting (Presentation) reads the look half. Namespace is
    /// TimeOfDay rather than "Time" (the folder name) so it never shadows UnityEngine.Time
    /// for code inside NAV.World.
    ///
    /// Two different 0-1 scales are used by the curves below - each field's tooltip says which:
    /// "day progress" runs 0 (sunrise) -> 1 (sunset) and only exists during the day; "time of
    /// day" runs 0 (00:00) -> 1 (24:00) and covers the whole cycle.
    /// </summary>
    [CreateAssetMenu(fileName = "TimeOfDaySettings", menuName = "NAV/World/Time Of Day Settings")]
    public class TimeOfDaySettings : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Real-time minutes for one full 24-hour in-game day.")]
        [SerializeField, Min(1f)] private float _dayLengthMinutes = 24f;
        [SerializeField, Range(0f, 24f)] private float _newGameStartHour = 8f;
        [SerializeField, Range(0f, 24f)] private float _sunriseHour = 6f;
        [SerializeField, Range(0f, 24f)] private float _sunsetHour = 20f;

        [Header("Sun & Moon Path")]
        [Tooltip("Compass direction (degrees) the sun/moon arc is centred on at its highest point.")]
        [SerializeField] private float _pathYaw = 330f;
        [SerializeField, Range(5f, 90f)] private float _maxSunElevation = 65f;
        [SerializeField, Range(5f, 90f)] private float _maxMoonElevation = 50f;

        [Header("Sun (over day progress 0 = sunrise .. 1 = sunset)")]
        [SerializeField] private Gradient _sunColor = DefaultSunColor();
        [SerializeField] private AnimationCurve _sunIntensity = DefaultSunIntensity();

        [Header("Moon (the same light, after sunset)")]
        [SerializeField] private Color _moonColor = new(0.55f, 0.65f, 0.95f);
        [SerializeField, Min(0f)] private float _moonIntensity = 0.3f;

        [Header("Moon Phases (one phase per night)")]
        [Tooltip("Phases in one lunar cycle = nights it takes, spread evenly from new moon. 8 = new, waxing crescent, first quarter, waxing gibbous, FULL, waning gibbous, last quarter, waning crescent.")]
        [SerializeField, Range(2, 16)] private int _moonPhaseCount = 8;
        [Tooltip("Phase index shown on the first night of a new game (0 = new moon; with 8 phases, 4 = full).")]
        [SerializeField, Min(0)] private int _firstNightMoonPhase = 4;
        [Tooltip("Moonlight (relative to full moon) on a new-moon night, so it's dark but not pitch black; other phases land between this and 1 by how much of the disc is lit.")]
        [SerializeField, Range(0f, 1f)] private float _darkMoonLight = 0.3f;

        [Header("Ambient & Sky (over time of day 0 = 00:00 .. 1 = 24:00)")]
        [SerializeField] private Gradient _ambientSky = DefaultAmbientSky();
        [SerializeField] private Gradient _ambientEquator = DefaultAmbientEquator();
        [SerializeField] private AnimationCurve _skyExposure = DefaultSkyExposure();
        [SerializeField] private Gradient _skyTint = DefaultSkyTint();
        [Tooltip("Degrees the skybox turns per in-game day, so clouds drift slowly.")]
        [SerializeField] private float _skyRotationPerDay = 12f;

        public float DayLengthMinutes => _dayLengthMinutes;
        public float NewGameStartHour => _newGameStartHour;
        public float SunriseHour => _sunriseHour;
        public float SunsetHour => _sunsetHour;
        public float PathYaw => _pathYaw;
        public float MaxSunElevation => _maxSunElevation;
        public float MaxMoonElevation => _maxMoonElevation;
        public Gradient SunColor => _sunColor;
        public AnimationCurve SunIntensity => _sunIntensity;
        public Color MoonColor => _moonColor;
        public float MoonIntensity => _moonIntensity;
        public Gradient AmbientSky => _ambientSky;
        public Gradient AmbientEquator => _ambientEquator;
        public AnimationCurve SkyExposure => _skyExposure;
        public Gradient SkyTint => _skyTint;
        public float SkyRotationPerDay => _skyRotationPerDay;

        /// <summary>In-game hours that pass per real-time second.</summary>
        public float HoursPerSecond => 24f / (_dayLengthMinutes * 60f);

        public int MoonPhaseCount => _moonPhaseCount;

        public bool IsDaytime(float hour) => GetDayProgress(hour) >= 0f;

        /// <summary>
        /// Which phase (0 .. MoonPhaseCount-1) the moon shows. A night counts as the day it began
        /// on, and the phase only steps at midday - when the moon is below the horizon - so it
        /// never visibly changes while it's up.
        /// </summary>
        public int GetMoonPhaseIndex(int day, float hour)
        {
            float midday = Wrap(_sunriseHour + Wrap(_sunsetHour - _sunriseHour) * 0.5f);
            int night = hour < midday ? day - 2 : day - 1;
            int index = (night + _firstNightMoonPhase) % _moonPhaseCount;
            return index < 0 ? index + _moonPhaseCount : index;
        }

        /// <summary>Position in the lunar cycle for a phase index: 0 = new, 0.5 = full, towards 1 =
        /// waning back to new.</summary>
        public float GetMoonCycle(int phaseIndex) => (float)phaseIndex / _moonPhaseCount;

        /// <summary>Lit fraction of the disc (0 = new, 1 = full) at a cycle position.</summary>
        public static float GetMoonIllumination(float cycle) => 0.5f - 0.5f * Mathf.Cos(cycle * 2f * Mathf.PI);

        /// <summary>Multiplier on MoonIntensity for a cycle position - crescent and new-moon nights are darker.</summary>
        public float GetMoonLightFactor(float cycle) => Mathf.Lerp(_darkMoonLight, 1f, GetMoonIllumination(cycle));

        /// <summary>0 at sunrise .. 1 at sunset, or -1 while it's night.</summary>
        public float GetDayProgress(float hour)
        {
            float length = Wrap(_sunsetHour - _sunriseHour);
            float since = Wrap(hour - _sunriseHour);
            return since <= length ? since / length : -1f;
        }

        /// <summary>0 at sunset .. 1 at the next sunrise, or -1 while it's day.</summary>
        public float GetNightProgress(float hour)
        {
            float length = Wrap(_sunriseHour - _sunsetHour);
            float since = Wrap(hour - _sunsetHour);
            return since < length ? since / length : -1f;
        }

        private static float Wrap(float hours) => Mathf.Repeat(hours, 24f);

        private void OnValidate()
        {
            if (Mathf.Approximately(_sunriseHour, _sunsetHour))
            {
                _sunsetHour = Mathf.Repeat(_sunriseHour + 12f, 24f);
            }
        }

        // --- Defaults (placeholder look: warm dawn/dusk, white noon, dark blue night) ---

        private static Gradient MakeGradient(params (float time, Color color)[] keys)
        {
            var colorKeys = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
            }

            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static Gradient DefaultSunColor() => MakeGradient(
            (0f, new Color(1f, 0.5f, 0.25f)),
            (0.12f, new Color(1f, 0.85f, 0.65f)),
            (0.5f, new Color(1f, 0.97f, 0.92f)),
            (0.88f, new Color(1f, 0.82f, 0.6f)),
            (1f, new Color(1f, 0.45f, 0.2f)));

        private static AnimationCurve DefaultSunIntensity() => new(
            new Keyframe(0f, 0f), new Keyframe(0.08f, 1.2f), new Keyframe(0.25f, 2f),
            new Keyframe(0.75f, 2f), new Keyframe(0.92f, 1.2f), new Keyframe(1f, 0f));

        // Hours as fractions of the day: 05:00 = 0.21, 06:00 = 0.25, 08:00 = 0.33,
        // 18:00 = 0.75, 20:00 = 0.83, 21:00 = 0.875.
        private static Gradient DefaultAmbientSky() => MakeGradient(
            (0f, new Color(0.05f, 0.07f, 0.13f)),
            (0.21f, new Color(0.06f, 0.08f, 0.15f)),
            (0.27f, new Color(0.55f, 0.45f, 0.45f)),
            (0.36f, new Color(0.6f, 0.68f, 0.8f)),
            (0.75f, new Color(0.6f, 0.68f, 0.8f)),
            (0.83f, new Color(0.55f, 0.38f, 0.32f)),
            (0.88f, new Color(0.06f, 0.08f, 0.15f)),
            (1f, new Color(0.05f, 0.07f, 0.13f)));

        private static Gradient DefaultAmbientEquator() => MakeGradient(
            (0f, new Color(0.04f, 0.05f, 0.09f)),
            (0.21f, new Color(0.05f, 0.06f, 0.1f)),
            (0.27f, new Color(0.5f, 0.38f, 0.3f)),
            (0.36f, new Color(0.45f, 0.47f, 0.48f)),
            (0.75f, new Color(0.45f, 0.47f, 0.48f)),
            (0.83f, new Color(0.5f, 0.32f, 0.22f)),
            (0.88f, new Color(0.05f, 0.06f, 0.1f)),
            (1f, new Color(0.04f, 0.05f, 0.09f)));

        private static AnimationCurve DefaultSkyExposure() => new(
            new Keyframe(0f, 0.12f), new Keyframe(0.21f, 0.12f), new Keyframe(0.3f, 0.9f),
            new Keyframe(0.5f, 1f), new Keyframe(0.79f, 0.9f), new Keyframe(0.88f, 0.12f),
            new Keyframe(1f, 0.12f));

        private static Gradient DefaultSkyTint() => MakeGradient(
            (0f, new Color(0.3f, 0.36f, 0.6f)),
            (0.21f, new Color(0.3f, 0.36f, 0.6f)),
            (0.27f, new Color(0.75f, 0.55f, 0.5f)),
            (0.36f, new Color(0.5f, 0.5f, 0.5f)),
            (0.75f, new Color(0.5f, 0.5f, 0.5f)),
            (0.83f, new Color(0.8f, 0.5f, 0.4f)),
            (0.88f, new Color(0.3f, 0.36f, 0.6f)),
            (1f, new Color(0.3f, 0.36f, 0.6f)));
    }
}
