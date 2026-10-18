using UnityEngine;
using UnityEngine.Rendering;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// Renders WorldClock's time of day: drives one directional light as the sun by day and the
    /// moon by night (it swaps sides at sunrise/sunset, when its intensity is ~0, so the switch
    /// is invisible and only one shadow-casting light ever exists), sets trilight ambient
    /// colours, and dims/tints/rotates the skybox. All look values come from
    /// TimeOfDaySettings; this class owns no time state.
    ///
    /// With a Skybox Set (and the NAV/Skybox/Panoramic Blend shader) assigned, the sky is a
    /// runtime material that cross-fades the set's painted Night/Sunrise/Day/Sunset skies and
    /// fades to its Overcast sky with the weather; the settings' SkyTint/SkyExposure curves are
    /// then only used for reflections, since the painted skies already carry their own look.
    ///
    /// Otherwise the skybox is driven through a runtime copy of the scene's skybox material - editing the
    /// asset itself in Play Mode would permanently change it. With Weather Skybox Shader
    /// assigned, that copy uses NAV/Skybox/Cubemap Weather (same cubemap/tint/exposure/rotation,
    /// plus an Overcast desaturation driven by the weather). Ambient mode is switched to
    /// Trilight at startup because the scene's Skybox ambient is a static bake that would stay
    /// daylight-bright all night.
    /// </summary>
    public class DayNightLighting : MonoBehaviour
    {
        private const float MinLightElevation = 2f;
        private const float GroundAmbientFactor = 0.45f;

        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");

        private static readonly int TexId = Shader.PropertyToID("_Tex");
        private static readonly int OvercastId = Shader.PropertyToID("_Overcast");
        private const float OvercastShadowStrength = 0.3f;

        private static readonly int TexAId = Shader.PropertyToID("_TexA");
        private static readonly int TexBId = Shader.PropertyToID("_TexB");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int WeatherTexId = Shader.PropertyToID("_WeatherTex");
        private static readonly int WeatherBlendId = Shader.PropertyToID("_WeatherBlend");
        private static readonly int WeatherExposureId = Shader.PropertyToID("_WeatherExposure");
        private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int SunGlowId = Shader.PropertyToID("_SunGlow");
        private static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
        private static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
        private static readonly int MoonSizeId = Shader.PropertyToID("_MoonSize");
        private static readonly int MoonGlowId = Shader.PropertyToID("_MoonGlow");
        private static readonly int MoonPhaseId = Shader.PropertyToID("_MoonPhase");
        private static readonly int CloudOcclusionId = Shader.PropertyToID("_CloudOcclusion");
        private static readonly int CloudNoiseId = Shader.PropertyToID("_CloudNoise");
        private static readonly int CloudOffset1Id = Shader.PropertyToID("_CloudOffset1");
        private static readonly int CloudOffset2Id = Shader.PropertyToID("_CloudOffset2");
        private static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
        private static readonly int CloudCoverageId = Shader.PropertyToID("_CloudCoverage");
        private static readonly int CloudSoftnessId = Shader.PropertyToID("_CloudSoftness");
        private static readonly int CloudOpacityId = Shader.PropertyToID("_CloudOpacity");
        private static readonly int CloudLitColorId = Shader.PropertyToID("_CloudLitColor");
        private static readonly int CloudShadowColorId = Shader.PropertyToID("_CloudShadowColor");
        private static readonly int CloudLightDirId = Shader.PropertyToID("_CloudLightDir");
        private static readonly int CloudSilverId = Shader.PropertyToID("_CloudSilver");

        // Texture-UV scale of each cloud noise layer in the shader (CloudDensity) - the wind
        // offset is multiplied by these so all three move at the same ground speed, plus extra
        // relative motion so the shapes change.
        private const float CloudShapesScale = 0.25f;
        private const float CloudBillowsScale = 1.6f;
        private const float CloudMorphScale = 0.4f;
        private const float BillowsExtraSpeed = 1.3f;

        [SerializeField] private WorldClock _clock;
        [SerializeField] private Light _sun;

        [Tooltip("Optional - when assigned, the current weather dims the sun/ambient/sky, softens shadows and greys the sky.")]
        [SerializeField] private WeatherSystem _weather;

        [Tooltip("NAV/Skybox/Cubemap Weather. When assigned (and the scene skybox is a cubemap), the runtime sky uses it so overcast weather can desaturate the sky - Skybox/Cubemap can only tint it. Referenced here so it's included in builds.")]
        [SerializeField] private Shader _weatherSkyboxShader;

        [Tooltip("Optional - painted skies to cross-fade through over the day (needs Panoramic Blend Shader). Replaces the scene skybox at runtime.")]
        [SerializeField] private SkyboxSet _skyboxSet;

        [Tooltip("NAV/Skybox/Panoramic Blend. Referenced here so it's included in builds.")]
        [SerializeField] private Shader _panoramicBlendShader;

        private Material _originalSkybox;
        private Material _skyboxInstance;
        private float _baseSkyRotation;
        private bool _usesSkyboxSet;

        // Accumulated (not time * speed) so a wind change in a storm never makes clouds jump;
        // wrapped to 0..1 because the noise texture tiles exactly.
        private Vector2 _cloudShapesOffset;
        private Vector2 _cloudBillowsOffset;
        private Vector2 _cloudMorphOffset;

        private void Awake()
        {
            if (_clock == null || _sun == null || _sun.type != LightType.Directional)
            {
                Debug.LogError($"{nameof(DayNightLighting)} on '{name}' needs a WorldClock and a Directional Light.", this);
                enabled = false;
                return;
            }

            _originalSkybox = RenderSettings.skybox;
            if (_skyboxSet != null && _panoramicBlendShader != null)
            {
                _usesSkyboxSet = true;
                _skyboxInstance = new Material(_panoramicBlendShader) { name = _skyboxSet.name + " (Day/Night/Weather)" };
                _skyboxInstance.SetTexture(WeatherTexId, _skyboxSet.Overcast);
                _skyboxInstance.SetTexture(CloudNoiseId, _skyboxSet.CloudNoise);
                _baseSkyRotation = _skyboxSet.Rotation;
                RenderSettings.skybox = _skyboxInstance;
            }
            else if (_originalSkybox != null)
            {
                _skyboxInstance = CreateSkyboxInstance(_originalSkybox);
                _baseSkyRotation = _skyboxInstance.HasProperty(RotationId) ? _skyboxInstance.GetFloat(RotationId) : 0f;
                RenderSettings.skybox = _skyboxInstance;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.sun = _sun;
        }

        private void OnDestroy()
        {
            if (_skyboxInstance != null)
            {
                if (RenderSettings.skybox == _skyboxInstance)
                {
                    RenderSettings.skybox = _originalSkybox;
                }

                Destroy(_skyboxInstance);
            }
        }

        private Material CreateSkyboxInstance(Material source)
        {
            if (_weatherSkyboxShader != null && source.HasProperty(TexId))
            {
                var material = new Material(_weatherSkyboxShader) { name = source.name + " (Day/Night/Weather)" };
                material.SetTexture(TexId, source.GetTexture(TexId));
                material.SetColor(TintId, source.GetColor(TintId));
                material.SetFloat(ExposureId, source.GetFloat(ExposureId));
                material.SetFloat(RotationId, source.GetFloat(RotationId));
                return material;
            }

            return new Material(source) { name = source.name + " (Day/Night)" };
        }

        private void LateUpdate()
        {
            Apply(_clock.Settings, _clock.Hour, _clock.Day);
        }

        private void Apply(TimeOfDaySettings settings, float hour, int day)
        {
            bool hasWeather = _weather != null && _weather.Current != null;
            float sunMultiplier = hasWeather ? _weather.Evaluate(w => w.SunMultiplier) : 1f;
            float ambientMultiplier = hasWeather ? _weather.Evaluate(w => w.AmbientMultiplier) : 1f;
            float skyMultiplier = hasWeather ? _weather.Evaluate(w => w.SkyExposureMultiplier) : 1f;
            float overcast = hasWeather ? _weather.Evaluate(w => w.Overcast) : 0f;

            float dayProgress = settings.GetDayProgress(hour);
            float moonCycle = settings.GetMoonCycle(settings.GetMoonPhaseIndex(day, hour));
            float progress;
            float maxElevation;

            if (dayProgress >= 0f)
            {
                progress = dayProgress;
                maxElevation = settings.MaxSunElevation;
                _sun.color = settings.SunColor.Evaluate(progress);
                _sun.intensity = settings.SunIntensity.Evaluate(progress);
            }
            else
            {
                progress = Mathf.Max(0f, settings.GetNightProgress(hour));
                maxElevation = settings.MaxMoonElevation;
                _sun.color = settings.MoonColor;
                // Fades in/out near the horizon so the sun<->moon swap happens in darkness.
                _sun.intensity = settings.MoonIntensity * settings.GetMoonLightFactor(moonCycle)
                    * Mathf.Clamp01(Mathf.Sin(progress * Mathf.PI) * 3f);
            }

            _sun.intensity *= sunMultiplier;
            // Overcast light is diffuse - shadows fade instead of staying razor sharp in rain.
            _sun.shadowStrength = Mathf.Lerp(1f, OvercastShadowStrength, overcast);

            // East -> overhead -> west across the arc, highest at progress 0.5.
            float elevation = Mathf.Max(MinLightElevation, Mathf.Sin(progress * Mathf.PI) * maxElevation);
            float yaw = settings.PathYaw - 90f + progress * 180f;
            _sun.transform.rotation = Quaternion.Euler(elevation, yaw, 0f);

            float timeOfDay = hour / 24f;
            Color equator = settings.AmbientEquator.Evaluate(timeOfDay) * ambientMultiplier;
            Color sky = settings.AmbientSky.Evaluate(timeOfDay) * ambientMultiplier;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = equator * GroundAmbientFactor;
            // Overcast fog is a flat grey-blue matching the dimmed sky; clear fog follows the horizon.
            RenderSettings.fogColor = Color.Lerp(equator, sky, overcast);

            float exposure = settings.SkyExposure.Evaluate(timeOfDay) * skyMultiplier;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(exposure);

            if (_usesSkyboxSet)
            {
                _skyboxSet.Evaluate(hour, settings.SunriseHour, settings.SunsetHour,
                    out SkyPhase a, out SkyPhase b, out float blend, out float overcastExposure);
                _skyboxInstance.SetTexture(TexAId, a.Texture);
                _skyboxInstance.SetTexture(TexBId, b.Texture);
                _skyboxInstance.SetFloat(BlendId, blend);
                _skyboxInstance.SetColor(TintId, Color.Lerp(a.Tint, b.Tint, blend));
                _skyboxInstance.SetFloat(ExposureId, Mathf.Lerp(a.Exposure, b.Exposure, blend) * skyMultiplier);
                _skyboxInstance.SetFloat(WeatherBlendId, overcast);
                // The overcast texture is already painted dark grey - full SkyExposureMultiplier
                // on top of it turned rainy noon almost black, so it only gets half the dimming.
                _skyboxInstance.SetFloat(WeatherExposureId, overcastExposure * Mathf.Sqrt(skyMultiplier));
                _skyboxInstance.SetFloat(RotationId, Mathf.Repeat(_baseSkyRotation + (day + timeOfDay) * settings.SkyRotationPerDay, 360f));
                ApplyCelestialDiscs(settings, hour, dayProgress, moonCycle);
                ApplyClouds(overcast, skyMultiplier, a, b, blend);
            }
            else if (_skyboxInstance != null)
            {
                _skyboxInstance.SetFloat(ExposureId, exposure);
                _skyboxInstance.SetColor(TintId, settings.SkyTint.Evaluate(timeOfDay));
                _skyboxInstance.SetFloat(OvercastId, overcast);
                _skyboxInstance.SetFloat(RotationId, Mathf.Repeat(_baseSkyRotation + (day + timeOfDay) * settings.SkyRotationPerDay, 360f));
            }
        }

        /// <summary>
        /// Places the sky's sun and moon discs on the same arcs the light follows (so shadows
        /// point away from the visible body). Unlike the light, the discs keep moving below the
        /// horizon instead of swapping, so they rise and set smoothly; the shader clips them at
        /// the horizon and fades them with the weather. moonCycle (0 = new, 0.5 = full) sets the
        /// lit part of the moon.
        /// </summary>
        private void ApplyCelestialDiscs(TimeOfDaySettings settings, float hour, float dayProgress, float moonCycle)
        {
            Vector3 sunDir = ArcDirection(hour, settings.SunriseHour, settings.SunsetHour, settings.MaxSunElevation, settings.PathYaw);
            Vector3 moonDir = ArcDirection(hour, settings.SunsetHour, settings.SunriseHour, settings.MaxMoonElevation, settings.PathYaw);

            // Linear HDR colours via SetVector - SetColor would gamma-convert intensities above 1.
            Color sunColor = settings.SunColor.Evaluate(Mathf.Clamp01(dayProgress)).linear * _skyboxSet.SunIntensity;
            Color moonColor = _skyboxSet.MoonColor.linear * _skyboxSet.MoonIntensity;

            _skyboxInstance.SetVector(SunDirId, sunDir);
            _skyboxInstance.SetVector(SunColorId, sunColor);
            _skyboxInstance.SetFloat(SunSizeId, _skyboxSet.SunSize);
            _skyboxInstance.SetFloat(SunGlowId, _skyboxSet.SunGlow);
            _skyboxInstance.SetVector(MoonDirId, moonDir);
            _skyboxInstance.SetVector(MoonColorId, moonColor);
            _skyboxInstance.SetFloat(MoonSizeId, _skyboxSet.MoonSize);
            _skyboxInstance.SetFloat(MoonGlowId, _skyboxSet.MoonGlow);
            _skyboxInstance.SetFloat(MoonPhaseId, moonCycle);
            _skyboxInstance.SetFloat(CloudOcclusionId, _skyboxSet.CloudOcclusion);
        }

        /// <summary>
        /// Drives the moving cloud layer: scrolls its three noise layers with the wind (the
        /// billows faster than the shapes, the morph layer sideways, so clouds drift and change
        /// shape), colours it from the current sky phases' authored cloud colours (a lit-light
        /// formula can't match painted skies - sunset clouds came out muddy brown), shades it
        /// towards the current sun/moon light, and thickens, greys and darkens it with the weather.
        /// </summary>
        private void ApplyClouds(float overcast, float skyMultiplier, SkyPhase a, SkyPhase b, float blend)
        {
            if (_skyboxSet.CloudNoise == null)
            {
                _skyboxInstance.SetFloat(CloudCoverageId, 0f);
                return;
            }

            float dt = Time.deltaTime;
            Vector2 wind = _skyboxSet.WindDirection;
            Vector2 drift = wind * (_skyboxSet.GetWindSpeed(overcast) * dt);
            Vector2 sideways = new Vector2(-wind.y, wind.x) * (_skyboxSet.MorphSpeed * dt);

            _cloudShapesOffset = WrapUv(_cloudShapesOffset - drift * CloudShapesScale);
            _cloudBillowsOffset = WrapUv(_cloudBillowsOffset - drift * (CloudBillowsScale * BillowsExtraSpeed));
            _cloudMorphOffset = WrapUv(_cloudMorphOffset - drift * CloudMorphScale + sideways);

            _skyboxInstance.SetVector(CloudOffset1Id, new Vector4(_cloudShapesOffset.x, _cloudShapesOffset.y, _cloudBillowsOffset.x, _cloudBillowsOffset.y));
            _skyboxInstance.SetVector(CloudOffset2Id, new Vector4(_cloudMorphOffset.x, _cloudMorphOffset.y, 0f, 0f));
            _skyboxInstance.SetFloat(CloudScaleId, _skyboxSet.CloudScale);
            _skyboxInstance.SetFloat(CloudCoverageId, _skyboxSet.GetCloudCoverage(overcast));
            _skyboxInstance.SetFloat(CloudSoftnessId, _skyboxSet.CloudSoftness);
            _skyboxInstance.SetFloat(CloudOpacityId, _skyboxSet.CloudOpacity);
            _skyboxInstance.SetFloat(CloudSilverId, _skyboxSet.SilverLining);

            // Linear via SetVector (see ApplyCelestialDiscs). Weather: same half-strength dimming
            // as the overcast sky layer, and the colour drains towards grey storm cloud.
            float dim = Mathf.Sqrt(skyMultiplier);
            Color lit = StormGrey(Color.Lerp(a.CloudLit, b.CloudLit, blend), overcast) * dim;
            Color shadow = StormGrey(Color.Lerp(a.CloudShadow, b.CloudShadow, blend), overcast) * dim;
            _skyboxInstance.SetVector(CloudLitColorId, lit.linear);
            _skyboxInstance.SetVector(CloudShadowColorId, shadow.linear);
            _skyboxInstance.SetVector(CloudLightDirId, -_sun.transform.forward);
        }

        private static Vector2 WrapUv(Vector2 uv) => new(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));

        private static Color StormGrey(Color color, float overcast)
        {
            float luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            var grey = new Color(luminance * 0.7f, luminance * 0.72f, luminance * 0.76f, color.a);
            return Color.Lerp(color, grey, overcast);
        }

        /// <summary>
        /// World direction towards a body that is up from riseHour to setHour: east -> highest
        /// (maxElevation) -> west, matching the light's yaw/elevation, then the mirrored arc
        /// under the horizon until it rises again.
        /// </summary>
        private static Vector3 ArcDirection(float hour, float riseHour, float setHour, float maxElevation, float pathYaw)
        {
            float upHours = Mathf.Repeat(setHour - riseHour, 24f);
            float since = Mathf.Repeat(hour - riseHour, 24f);

            float arc;
            float elevation;
            if (since <= upHours)
            {
                float progress = upHours > 0f ? since / upHours : 0f;
                arc = progress;
                elevation = Mathf.Sin(progress * Mathf.PI) * maxElevation;
            }
            else
            {
                float progress = (since - upHours) / (24f - upHours);
                arc = 1f + progress;
                elevation = -Mathf.Sin(progress * Mathf.PI) * maxElevation;
            }

            float yaw = pathYaw - 90f + arc * 180f;
            // The light shines along +forward of this rotation, so the body is behind it.
            return Quaternion.Euler(elevation, yaw, 0f) * Vector3.back;
        }
    }
}
