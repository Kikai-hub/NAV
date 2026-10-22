using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NAV.Core.Settings
{
    /// <summary>
    /// The player's settings (GameSettingsData): loaded from persistentDataPath/settings.json
    /// before the first scene, applied to the engine (display, frame rate, quality, URP asset,
    /// master volume), and saved whenever the Settings window changes something. Everything
    /// that depends on a setting reads Current and listens to Changed - category volumes
    /// (HitFeedback, WeatherEffects), camera sensitivity/FOV (ThirdPersonCameraController),
    /// object draw distance (ThirdPersonCameraController), key bindings (PlayerInputHandler).
    ///
    /// A static class, not a scene object: the same settings serve the main menu and the game,
    /// and must be in effect before either scene's objects wake up (same reasoning as
    /// SaveSystem). In the editor the URP asset and QualitySettings changes would otherwise stay
    /// in the project after Play Mode, so their original values are restored on exit.
    /// </summary>
    public static class GameSettings
    {
        private const string FileName = "settings.json";

        private static GameSettingsData _current;

        /// <summary>Raised after any change is applied and saved.</summary>
        public static event Action Changed;

        public static GameSettingsData Current
        {
            get
            {
                if (_current == null)
                {
                    Load();
                }

                return _current;
            }
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _current = null;
            Changed = null;
            Load();
            ApplyEngineSettings();
        }

        private static void Load()
        {
            _current = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    _current = JsonUtility.FromJson<GameSettingsData>(File.ReadAllText(FilePath));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(GameSettings)}: couldn't read '{FilePath}' ({e.Message}) - using defaults.");
            }

            if (_current == null)
            {
                _current = new GameSettingsData();
            }
        }

        /// <summary>Applies the engine-level settings and notifies listeners, without writing
        /// the file - for live changes (a slider being dragged). Save() when done.</summary>
        public static void Apply()
        {
            ApplyEngineSettings();
            Changed?.Invoke();
        }

        /// <summary>Apply() + Save().</summary>
        public static void Commit()
        {
            Apply();
            Save();
        }

        /// <summary>Resets one group of settings to defaults (Settings window's Reset button)
        /// and applies them.</summary>
        public static void ResetAudio()
        {
            var defaults = new GameSettingsData();
            Current.MasterVolume = defaults.MasterVolume;
            Current.EffectsVolume = defaults.EffectsVolume;
            Current.AmbienceVolume = defaults.AmbienceVolume;
            Current.MusicVolume = defaults.MusicVolume;
            Apply();
        }

        public static void ResetControls()
        {
            var defaults = new GameSettingsData();
            Current.MouseSensitivity = defaults.MouseSensitivity;
            Current.InvertY = defaults.InvertY;
            Current.BindingOverrides = string.Empty;
            Apply();
        }

        public static void ResetGraphics()
        {
            var defaults = new GameSettingsData();
            Current.ResolutionWidth = defaults.ResolutionWidth;
            Current.ResolutionHeight = defaults.ResolutionHeight;
            Current.DisplayMode = defaults.DisplayMode;
            Current.VSync = defaults.VSync;
            Current.FrameRateLimit = defaults.FrameRateLimit;
            Current.FieldOfView = defaults.FieldOfView;
            ApplyPreset(Current, defaults.Preset);
            Apply();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(_current, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(GameSettings)}: couldn't write '{FilePath}': {e.Message}");
            }
        }

        public static float GetVolume(AudioCategory category)
        {
            return Mathf.Clamp01(Current.GetVolume(category));
        }

        /// <summary>Loads the saved binding overrides into an actions asset (the player's
        /// PlayerInput copy, or the Settings window's). Empty = default bindings.</summary>
        public static void ApplyBindingOverrides(InputActionAsset actions)
        {
            if (actions == null)
            {
                return;
            }

            actions.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(Current.BindingOverrides))
            {
                actions.LoadBindingOverridesFromJson(Current.BindingOverrides);
            }
        }

        /// <summary>Sets the graphics fields a preset stands for (not Custom).</summary>
        public static void ApplyPreset(GameSettingsData data, GraphicsPreset preset)
        {
            data.Preset = preset;
            switch (preset)
            {
                case GraphicsPreset.Low:
                    data.RenderScale = 0.75f; data.Shadows = ShadowLevel.Low; data.Msaa = 1; data.TextureQuality = 1; data.ObjectDrawDistance = 150f;
                    break;
                case GraphicsPreset.Medium:
                    data.RenderScale = 0.9f; data.Shadows = ShadowLevel.Low; data.Msaa = 1; data.TextureQuality = 0; data.ObjectDrawDistance = 250f;
                    break;
                case GraphicsPreset.High:
                    data.RenderScale = 1f; data.Shadows = ShadowLevel.Medium; data.Msaa = 1; data.TextureQuality = 0; data.ObjectDrawDistance = 0f;
                    break;
                case GraphicsPreset.Ultra:
                    data.RenderScale = 1f; data.Shadows = ShadowLevel.High; data.Msaa = 4; data.TextureQuality = 0; data.ObjectDrawDistance = 0f;
                    break;
            }
        }

        private static void ApplyEngineSettings()
        {
            GameSettingsData data = _current;

            AudioListener.volume = Mathf.Clamp01(data.MasterVolume);

            if (!Application.isEditor)
            {
                Resolution current = Screen.currentResolution;
                int width = data.ResolutionWidth > 0 ? data.ResolutionWidth : current.width;
                int height = data.ResolutionHeight > 0 ? data.ResolutionHeight : current.height;
                Screen.SetResolution(width, height, data.DisplayMode);
            }

            RememberEditorOriginals();
            QualitySettings.vSyncCount = data.VSync ? 1 : 0;
            Application.targetFrameRate = !data.VSync && data.FrameRateLimit > 0 ? data.FrameRateLimit : -1;
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(data.TextureQuality, 0, 2);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(data.RenderScale, 0.5f, 1f);
                urp.msaaSampleCount = data.Msaa == 2 || data.Msaa == 4 || data.Msaa == 8 ? data.Msaa : 1;
                switch (data.Shadows)
                {
                    case ShadowLevel.Off:
                        urp.shadowDistance = 0f;
                        break;
                    case ShadowLevel.Low:
                        urp.shadowDistance = 30f; urp.mainLightShadowmapResolution = 1024; urp.shadowCascadeCount = 2;
                        break;
                    case ShadowLevel.Medium:
                        urp.shadowDistance = 50f; urp.mainLightShadowmapResolution = 2048; urp.shadowCascadeCount = 4;
                        break;
                    default:
                        urp.shadowDistance = 90f; urp.mainLightShadowmapResolution = 4096; urp.shadowCascadeCount = 4;
                        break;
                }
            }
        }

#if UNITY_EDITOR
        // Runtime changes to the URP asset and QualitySettings would otherwise be written into the
        // project. Remember what they were when Play Mode started and put them back on exit.
        private static bool _editorOriginalsSaved;
        private static int _origVSync;
        private static int _origMipLimit;
        private static float _origRenderScale;
        private static int _origMsaa;
        private static float _origShadowDistance;
        private static int _origShadowResolution;
        private static int _origCascades;

        private static void RememberEditorOriginals()
        {
            if (_editorOriginalsSaved || !Application.isPlaying)
            {
                return;
            }

            _editorOriginalsSaved = true;
            _origVSync = QualitySettings.vSyncCount;
            _origMipLimit = QualitySettings.globalTextureMipmapLimit;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                _origRenderScale = urp.renderScale;
                _origMsaa = urp.msaaSampleCount;
                _origShadowDistance = urp.shadowDistance;
                _origShadowResolution = urp.mainLightShadowmapResolution;
                _origCascades = urp.shadowCascadeCount;
            }

            UnityEditor.EditorApplication.playModeStateChanged += RestoreEditorOriginals;
        }

        private static void RestoreEditorOriginals(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            UnityEditor.EditorApplication.playModeStateChanged -= RestoreEditorOriginals;
            _editorOriginalsSaved = false;
            QualitySettings.vSyncCount = _origVSync;
            QualitySettings.globalTextureMipmapLimit = _origMipLimit;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = _origRenderScale;
                urp.msaaSampleCount = _origMsaa;
                urp.shadowDistance = _origShadowDistance;
                urp.mainLightShadowmapResolution = _origShadowResolution;
                urp.shadowCascadeCount = _origCascades;
            }
        }
#else
        private static void RememberEditorOriginals()
        {
        }
#endif
    }
}
