using UnityEngine;
using NAV.Core.Settings;
using NAV.World.Biomes;
using NAV.World.Generation;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// The one owner of the scene fog (RenderSettings) - before it the view distance and the
    /// weather each wrote their own fog mode and fought over it. Exponential-squared fog whose
    /// density sums four parts the way their optical depths add (k = sqrt of the sum of squares):
    ///   - the view-distance edge: fully fogged (2% left) at GameSettings' View Distance, where
    ///     the camera stops drawing (ThirdPersonCameraController sets the far clip) - the edge of
    ///     the world, and of what chunk streaming loads, is never seen;
    ///   - the weather (WeatherDefinition.FogDensity - rain and storms close in);
    ///   - the biome around the camera (BiomeDefinition.FogDensity - the dense forest's gloom),
    ///     eased in and out over a few seconds as the camera crosses into it;
    ///   - morning haze for a few hours after sunrise, thinner when it's overcast already.
    /// Colour: the real sky just above the horizon where the camera looks (a tiny skybox-only
    /// render every quarter second, read back asynchronously), over DayNightLighting's
    /// ambient-based colour - so distant hills fade into the sky by day, at sunset and in rain -
    /// then multiplied by the biome's fog colour (0.5 grey = unchanged) by that colour's alpha.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class AtmosphereFog : MonoBehaviour
    {
        // exp(-(d k)^2) = 0.02 at d = view distance -> k = sqrt(-ln 0.02) / d.
        private const float EdgeDepth = 1.978f;

        [SerializeField] private DayNightLighting _lighting;
        [SerializeField] private WeatherSystem _weather;
        [SerializeField] private WorldClock _clock;
        [Tooltip("Optional - without it there's no biome fog.")]
        [SerializeField] private WorldGenerator _worldGenerator;
        [Tooltip("Optional - defaults to Camera.main.")]
        [SerializeField] private Transform _camera;

        [Header("Morning Haze")]
        [SerializeField, Min(0f)] private float _morningHazeDensity = 0.006f;
        [Tooltip("Hours after sunrise the haze takes to lift.")]
        [SerializeField, Min(0.1f)] private float _morningHazeHours = 3f;

        [Header("Biome Blend")]
        [Tooltip("Seconds to ease into a biome's fog after crossing its border.")]
        [SerializeField, Min(0.1f)] private float _biomeBlendSeconds = 4f;

        [Header("Horizon Colour")]
        [Tooltip("How much the fog takes the colour of the real sky just above the horizon (sampled from the skybox where the camera looks) over DayNightLighting's ambient-based colour. Distant hills then fade into the sky instead of standing out as grey silhouettes.")]
        [SerializeField, Range(0f, 1f)] private float _horizonMatch = 0.85f;
        [SerializeField, Min(0.05f)] private float _horizonSampleSeconds = 0.25f;

        private const int HorizonSampleSize = 8;

        private float _biomeDensity;
        private Color _biomeColor = new(0.5f, 0.5f, 0.5f, 0f);
        private UnityEngine.Camera _skyCamera;
        private RenderTexture _skyTexture;
        private float _nextHorizonSample;
        private bool _hasHorizon;
        private Color _horizonTarget;
        private Color _horizon;

        /// <summary>The density last applied (for tests and debug HUDs).</summary>
        public float Density { get; private set; }

        private void Awake()
        {
            if (_lighting == null || _weather == null || _clock == null)
            {
                Debug.LogError($"{nameof(AtmosphereFog)} on '{name}' is missing a required reference (Lighting/Weather/Clock).", this);
                enabled = false;
                return;
            }

            if (_camera == null && UnityEngine.Camera.main != null)
            {
                _camera = UnityEngine.Camera.main.transform;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            BuildSkyCamera();
        }

        private void OnDestroy()
        {
            if (_skyTexture != null)
            {
                _skyTexture.Release();
                Destroy(_skyTexture);
            }
        }

        /// <summary>A disabled camera that draws nothing but the skybox into a tiny texture.</summary>
        private void BuildSkyCamera()
        {
            _skyTexture = new RenderTexture(HorizonSampleSize, HorizonSampleSize, 16, RenderTextureFormat.ARGB32) { name = "AtmosphereFog Horizon" };
            var go = new GameObject("Horizon Sampler") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            _skyCamera = go.AddComponent<UnityEngine.Camera>();
            _skyCamera.enabled = false;
            _skyCamera.cullingMask = 0;
            _skyCamera.clearFlags = CameraClearFlags.Skybox;
            _skyCamera.fieldOfView = 20f;
            _skyCamera.targetTexture = _skyTexture;
        }

        /// <summary>Renders the sky a few degrees above the horizon where the camera looks and
        /// reads its average colour back without stalling (AsyncGPUReadback).</summary>
        private void SampleHorizon()
        {
            if (_skyCamera == null || _camera == null || Time.unscaledTime < _nextHorizonSample)
            {
                return;
            }

            _nextHorizonSample = Time.unscaledTime + _horizonSampleSeconds;
            _skyCamera.transform.SetPositionAndRotation(_camera.position, Quaternion.Euler(-4f, _camera.eulerAngles.y, 0f));
            _skyCamera.Render();
            UnityEngine.Rendering.AsyncGPUReadback.Request(_skyTexture, 0, request =>
            {
                if (request.hasError || this == null)
                {
                    return;
                }

                Unity.Collections.NativeArray<Color32> pixels = request.GetData<Color32>();
                Color sum = Color.black;
                foreach (Color32 pixel in pixels)
                {
                    sum += (Color)pixel;
                }

                _horizonTarget = sum / Mathf.Max(1, pixels.Length);
                _horizonTarget.a = 1f;
                if (!_hasHorizon)
                {
                    _horizon = _horizonTarget;
                    _hasHorizon = true;
                }
            });
        }

        private void LateUpdate()
        {
            SampleHorizon();
            float edge = EdgeDepth / Mathf.Max(50f, GameSettings.Current.EffectiveViewDistance);
            float weather = _weather.Current != null ? _weather.Evaluate(w => w.FogDensity) : 0f;
            float overcast = _weather.Current != null ? _weather.Evaluate(w => w.Overcast) : 0f;

            TimeOfDaySettings time = _clock.Settings;
            float sinceSunrise = _clock.Hour - time.SunriseHour;
            float haze = sinceSunrise >= 0f && sinceSunrise < _morningHazeHours
                ? _morningHazeDensity * (1f - sinceSunrise / _morningHazeHours) * (1f - overcast)
                : 0f;

            BiomeDefinition biome = _worldGenerator != null && _camera != null ? _worldGenerator.GetBiomeAt(_camera.position) : null;
            float targetBiomeDensity = biome != null ? biome.FogDensity : 0f;
            Color targetBiomeColor = biome != null ? biome.FogColor : new Color(0.5f, 0.5f, 0.5f, 0f);
            float ease = 1f - Mathf.Exp(-Time.deltaTime / _biomeBlendSeconds);
            _biomeDensity = Mathf.Lerp(_biomeDensity, targetBiomeDensity, ease);
            _biomeColor = Color.Lerp(_biomeColor, targetBiomeColor, ease);

            Density = Mathf.Sqrt(edge * edge + weather * weather + _biomeDensity * _biomeDensity + haze * haze);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Density;

            // Base: the sky's own colour at the horizon (eased - turning towards the sunset
            // shouldn't flash the fog), over the ambient-based one; then the biome tint.
            Color baseColor = _lighting.FogColor;
            if (_hasHorizon)
            {
                _horizon = Color.Lerp(_horizon, _horizonTarget, 1f - Mathf.Exp(-Time.deltaTime / 0.6f));
                baseColor = Color.Lerp(baseColor, _horizon, _horizonMatch);
            }

            Color tinted = baseColor * (_biomeColor * 2f);
            tinted.a = 1f;
            RenderSettings.fogColor = Color.Lerp(baseColor, tinted, _biomeColor.a);
        }
    }
}
