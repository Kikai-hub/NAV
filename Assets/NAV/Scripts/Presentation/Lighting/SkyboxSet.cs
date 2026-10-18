using System;
using UnityEngine;

namespace NAV.Presentation.Lighting
{
    /// <summary>One painted sky and how it's shown while it's the active time-of-day phase.</summary>
    [Serializable]
    public class SkyPhase
    {
        [Tooltip("Equirectangular (lat-long) sky texture.")]
        [SerializeField] private Texture2D _texture;
        [Tooltip("0.5 grey = texture as painted.")]
        [SerializeField] private Color _tint = new(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField, Min(0f)] private float _exposure = 1f;
        [Tooltip("Moving cloud layer colour on its lit side - pick it from this sky's painted clouds so the two layers match.")]
        [SerializeField] private Color _cloudLit = Color.white;
        [Tooltip("Moving cloud layer colour on its shadowed side.")]
        [SerializeField] private Color _cloudShadow = new(0.62f, 0.7f, 0.85f, 1f);

        public Texture2D Texture => _texture;
        public Color Tint => _tint;
        public float Exposure => _exposure;
        public Color CloudLit => _cloudLit;
        public Color CloudShadow => _cloudShadow;
    }

    /// <summary>
    /// A family of painted skies (e.g. Fantasy Skybox FS003) that DayNightLighting cross-fades
    /// through over the day with the NAV/Skybox/Panoramic Blend shader:
    /// Night -> Sunrise -> Day -> Sunset -> Night, centred on TimeOfDaySettings' sunrise/sunset
    /// hours, plus an Overcast sky faded in by the weather's Overcast value. Use the asset's
    /// "Sunless"/"Moonless" variants for Day/Night - the painted sun/moon would sit in a fixed
    /// spot while the real directional light moves across the sky.
    /// </summary>
    [CreateAssetMenu(fileName = "SkyboxSet", menuName = "NAV/World/Skybox Set")]
    public class SkyboxSet : ScriptableObject
    {
        [Header("Time of day")]
        [SerializeField] private SkyPhase _night = new();
        [SerializeField] private SkyPhase _sunrise = new();
        [SerializeField] private SkyPhase _day = new();
        [SerializeField] private SkyPhase _sunset = new();
        [Tooltip("Hours each fade takes: Night->Sunrise ends at sunrise, Sunrise->Day starts there; Day->Sunset ends at sunset, Sunset->Night starts there.")]
        [SerializeField, Range(0.25f, 4f)] private float _transitionHours = 1.5f;

        [Header("Weather")]
        [Tooltip("Overcast/rainy sky the weather fades towards (by WeatherDefinition.Overcast).")]
        [SerializeField] private Texture2D _overcast;
        [SerializeField, Min(0f)] private float _overcastDayExposure = 0.9f;
        [SerializeField, Min(0f)] private float _overcastNightExposure = 0.12f;

        [Header("Orientation")]
        [Tooltip("Base rotation (degrees) of every layer; the day/night drift is added on top.")]
        [SerializeField, Range(0f, 360f)] private float _rotation;

        [Header("Sun & Moon discs (drawn where the directional light comes from)")]
        [Tooltip("Angular radius in degrees (the real sun is ~0.27 - bigger reads better in a stylised sky).")]
        [SerializeField, Range(0.1f, 10f)] private float _sunSize = 1.6f;
        [Tooltip("Brightness of the sun disc; its colour follows the time-of-day sun colour.")]
        [SerializeField, Min(0f)] private float _sunIntensity = 6f;
        [SerializeField, Range(0f, 4f)] private float _sunGlow = 0.6f;
        [SerializeField, Range(0.1f, 10f)] private float _moonSize = 2.2f;
        [SerializeField] private Color _moonColor = new(0.85f, 0.9f, 1f);
        [SerializeField, Min(0f)] private float _moonIntensity = 1.2f;
        [SerializeField, Range(0f, 4f)] private float _moonGlow = 0.25f;
        [Tooltip("0 = discs always drawn over the painted clouds, 1 = fully hidden behind them.")]
        [SerializeField, Range(0f, 1f)] private float _cloudOcclusion = 0.8f;

        [Header("Moving cloud layer (overhead, over the painted sky)")]
        [Tooltip("Tiling noise from NAV > Sky > Generate Cloud Noise Texture. Empty = no moving clouds.")]
        [SerializeField] private Texture2D _cloudNoise;
        [Tooltip("Smaller = bigger clouds.")]
        [SerializeField, Range(0.05f, 4f)] private float _cloudScale = 0.35f;
        [Tooltip("How much of the sky the clouds cover in clear weather (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _clearCoverage = 0.45f;
        [Tooltip("Coverage at full Overcast (rain/storm).")]
        [SerializeField, Range(0f, 1f)] private float _overcastCoverage = 0.85f;
        [Tooltip("Edge fuzziness: small = crisp stylised clouds, large = soft haze.")]
        [SerializeField, Range(0.01f, 0.3f)] private float _cloudSoftness = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _cloudOpacity = 0.95f;
        [Tooltip("Compass direction (degrees) the wind blows the clouds towards.")]
        [SerializeField] private float _windDirection = 45f;
        [Tooltip("Cloud drift speed in clear weather (cloud-deck units per second).")]
        [SerializeField, Min(0f)] private float _windSpeed = 0.015f;
        [Tooltip("Cloud drift speed at full Overcast - storms push clouds faster.")]
        [SerializeField, Min(0f)] private float _stormWindSpeed = 0.05f;
        [Tooltip("How fast cloud shapes change on their own, independent of the wind.")]
        [SerializeField, Min(0f)] private float _morphSpeed = 0.004f;
        [SerializeField, Range(0f, 4f)] private float _silverLining = 0.6f;

        public Texture2D Overcast => _overcast;
        public Texture2D CloudNoise => _cloudNoise;
        public float CloudScale => _cloudScale;
        public float CloudSoftness => _cloudSoftness;
        public float CloudOpacity => _cloudOpacity;
        public float SilverLining => _silverLining;
        public float MorphSpeed => _morphSpeed;

        public float GetCloudCoverage(float overcast) => Mathf.Lerp(_clearCoverage, _overcastCoverage, overcast);
        public float GetWindSpeed(float overcast) => Mathf.Lerp(_windSpeed, _stormWindSpeed, overcast);

        /// <summary>Unit vector on the cloud deck (x = east, y = north) the wind blows towards.</summary>
        public Vector2 WindDirection
        {
            get
            {
                float radians = _windDirection * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            }
        }
        public float Rotation => _rotation;
        public float SunSize => _sunSize;
        public float SunIntensity => _sunIntensity;
        public float SunGlow => _sunGlow;
        public float MoonSize => _moonSize;
        public Color MoonColor => _moonColor;
        public float MoonIntensity => _moonIntensity;
        public float MoonGlow => _moonGlow;
        public float CloudOcclusion => _cloudOcclusion;

        /// <summary>
        /// The two phases around <paramref name="hour"/> and how far it is from A to B (0..1,
        /// smoothed), plus how much of the sky is night (for dimming the overcast layer).
        /// </summary>
        public void Evaluate(float hour, float sunriseHour, float sunsetHour,
            out SkyPhase a, out SkyPhase b, out float blend, out float overcastExposure)
        {
            float dayLength = Wrap(sunsetHour - sunriseHour);
            float nightLength = 24f - dayLength;
            // Keeps the six keys in order even for very short days or nights.
            float d = Mathf.Min(_transitionHours, dayLength * 0.5f, nightLength * 0.5f);

            Span<float> hours = stackalloc float[]
            {
                sunriseHour - d, sunriseHour, sunriseHour + d,
                sunsetHour - d, sunsetHour, sunsetHour + d,
            };
            a = _night;
            b = _night;
            blend = 0f;
            for (int i = 0; i < hours.Length; i++)
            {
                int next = (i + 1) % hours.Length;
                float length = Wrap(hours[next] - hours[i]);
                float since = Wrap(hour - hours[i]);
                if (since <= length)
                {
                    a = PhaseAtKey(i);
                    b = PhaseAtKey(next);
                    blend = length > 0f ? Mathf.SmoothStep(0f, 1f, since / length) : 1f;
                    break;
                }
            }

            float nightA = a == _night ? 1f : 0f;
            float nightB = b == _night ? 1f : 0f;
            float night = Mathf.Lerp(nightA, nightB, blend);
            overcastExposure = Mathf.Lerp(_overcastDayExposure, _overcastNightExposure, night);
        }

        // Matches the key hours in Evaluate (no per-frame array allocation).
        private SkyPhase PhaseAtKey(int key) => key switch
        {
            1 => _sunrise,
            2 or 3 => _day,
            4 => _sunset,
            _ => _night,
        };

        private static float Wrap(float hours) => Mathf.Repeat(hours, 24f);
    }
}
