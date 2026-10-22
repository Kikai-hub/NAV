using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NAV.Presentation.Lighting;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Editor
{
    /// <summary>
    /// Builds NAV's early colour grading (roadmap "Early visuals": baseline post-processing):
    /// four Volume Profiles in Settings/PostProcess - Day (the base look: Neutral tonemapping -
    /// ACES turned saturated reds orange and crushed the night - warm white balance, a little
    /// more contrast, soft bloom and vignette), Golden Hour, Night
    /// and Overcast - switches URP to HDR grading, gives clear weather a light aerial haze, and
    /// sets up the open scene: its Global Volume gets the Day profile, three more global
    /// volumes are created under it, and DayNightGrading blends them by time of day and
    /// weather. Without a WorldClock (main menu) only the Day profile is applied. Re-run to
    /// rebuild; save the scene afterwards.
    /// </summary>
    internal static class PostProcessSetup
    {
        private const string Folder = "Assets/NAV/Settings/PostProcess";
        private const string ClearWeatherPath = "Assets/NAV/ScriptableObjects/Weather/ClearWeather.asset";
        private const float ClearWeatherHaze = 0.0028f;

        [MenuItem("NAV/Setup/Build Post Processing")]
        private static void Build()
        {
            EnsureFolder(Folder);

            VolumeProfile day = BuildProfile("NAV_Day", profile =>
            {
                Add<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
                ColorAdjustments color = Add<ColorAdjustments>(profile);
                color.postExposure.Override(0.1f);
                color.contrast.Override(12f);
                color.saturation.Override(-4f);
                Add<WhiteBalance>(profile).temperature.Override(8f);
                ShadowsMidtonesHighlights smh = Add<ShadowsMidtonesHighlights>(profile);
                // Slightly blue shadows, warm highlights - the classic storybook split.
                smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f));
                smh.highlights.Override(new Vector4(1.05f, 1.02f, 0.95f, 0f));
                Bloom bloom = Add<Bloom>(profile);
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(0.35f);
                bloom.scatter.Override(0.65f);
                bloom.highQualityFiltering.Override(true);
                Vignette vignette = Add<Vignette>(profile);
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.4f);
            });

            VolumeProfile golden = BuildProfile("NAV_GoldenHour", profile =>
            {
                Add<WhiteBalance>(profile).temperature.Override(16f);
                ColorAdjustments color = Add<ColorAdjustments>(profile);
                color.saturation.Override(8f);
                color.colorFilter.Override(new Color(1f, 0.95f, 0.88f));
                Bloom bloom = Add<Bloom>(profile);
                bloom.intensity.Override(0.5f);
                bloom.threshold.Override(0.85f);
            });

            VolumeProfile night = BuildProfile("NAV_Night", profile =>
            {
                ColorAdjustments color = Add<ColorAdjustments>(profile);
                color.postExposure.Override(0.75f);
                color.saturation.Override(-30f);
                color.contrast.Override(4f);
                color.colorFilter.Override(new Color(0.72f, 0.84f, 1f));
                Add<WhiteBalance>(profile).temperature.Override(-25f);
                ShadowsMidtonesHighlights smh = Add<ShadowsMidtonesHighlights>(profile);
                smh.shadows.Override(new Vector4(0.9f, 0.95f, 1.12f, 0f));
                // Moonlit shadows stay readable instead of going pitch black.
                Add<LiftGammaGain>(profile).lift.Override(new Vector4(0.96f, 0.99f, 1.1f, 0.015f));
                Vignette vignette = Add<Vignette>(profile);
                vignette.intensity.Override(0.3f);
                vignette.color.Override(new Color(0.02f, 0.03f, 0.08f));
            });

            VolumeProfile overcast = BuildProfile("NAV_Overcast", profile =>
            {
                ColorAdjustments color = Add<ColorAdjustments>(profile);
                color.saturation.Override(-28f);
                color.contrast.Override(-6f);
                Add<WhiteBalance>(profile).temperature.Override(-12f);
            });

            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var urpSerialized = new SerializedObject(urp);
            urpSerialized.FindProperty("m_ColorGradingMode").enumValueIndex = (int)ColorGradingMode.HighDynamicRange;
            urpSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            var clear = AssetDatabase.LoadMainAssetAtPath(ClearWeatherPath);
            if (clear != null)
            {
                var clearSerialized = new SerializedObject(clear);
                clearSerialized.FindProperty("_fogDensity").floatValue = ClearWeatherHaze;
                clearSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(clear);
            }

            SetUpScene(day, golden, night, overcast);
            AssetDatabase.SaveAssets();
        }

        private static void SetUpScene(VolumeProfile day, VolumeProfile golden, VolumeProfile night, VolumeProfile overcast)
        {
            Volume baseVolume = null;
            foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.isGlobal && volume.transform.parent == null)
                {
                    baseVolume = volume;
                    break;
                }
            }

            if (baseVolume == null)
            {
                baseVolume = new GameObject("Global Volume").AddComponent<Volume>();
                baseVolume.isGlobal = true;
            }

            baseVolume.sharedProfile = day;
            baseVolume.priority = 0f;
            EditorUtility.SetDirty(baseVolume);

            WorldClock clock = Object.FindAnyObjectByType<WorldClock>();
            if (clock == null)
            {
                EditorSceneManager.MarkSceneDirty(baseVolume.gameObject.scene);
                Debug.Log("PostProcessSetup: Day profile applied (no WorldClock in this scene - no day/night grading). Save the scene.");
                return;
            }

            var grading = baseVolume.GetComponent<DayNightGrading>() ?? baseVolume.gameObject.AddComponent<DayNightGrading>();
            var serialized = new SerializedObject(grading);
            serialized.FindProperty("_clock").objectReferenceValue = clock;
            serialized.FindProperty("_weather").objectReferenceValue = Object.FindAnyObjectByType<WeatherSystem>();
            serialized.FindProperty("_goldenHour").objectReferenceValue = GetChildVolume(baseVolume.transform, "Volume_GoldenHour", golden, 1f);
            serialized.FindProperty("_night").objectReferenceValue = GetChildVolume(baseVolume.transform, "Volume_Night", night, 2f);
            serialized.FindProperty("_overcast").objectReferenceValue = GetChildVolume(baseVolume.transform, "Volume_Overcast", overcast, 3f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(baseVolume.gameObject.scene);
            Debug.Log("PostProcessSetup: Day profile + Golden Hour/Night/Overcast volumes and DayNightGrading set up. Save the scene.");
        }

        private static Volume GetChildVolume(Transform parent, string name, VolumeProfile profile, float priority)
        {
            Transform child = parent.Find(name);
            Volume volume = child != null ? child.GetComponent<Volume>() : null;
            if (volume == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                volume = go.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.sharedProfile = profile;
            volume.priority = priority;
            volume.weight = 0f;
            EditorUtility.SetDirty(volume);
            return volume;
        }

        private static VolumeProfile BuildProfile(string name, System.Action<VolumeProfile> fill)
        {
            string path = $"{Folder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            foreach (VolumeComponent component in profile.components)
            {
                Object.DestroyImmediate(component, true);
            }

            profile.components.Clear();
            fill(profile);

            foreach (VolumeComponent component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.Add<T>();
        }

        private static void EnsureFolder(string path)
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }
    }
}
