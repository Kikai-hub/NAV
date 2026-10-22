using System;
using UnityEngine;

namespace NAV.Core.Settings
{
    /// <summary>Quality preset; picking one sets the graphics fields below, Custom = edited by hand.</summary>
    public enum GraphicsPreset
    {
        Low,
        Medium,
        High,
        Ultra,
        Custom
    }

    /// <summary>Main-light shadow quality (distance, shadow map, cascades - see GameSettings).</summary>
    public enum ShadowLevel
    {
        Off,
        Low,
        Medium,
        High
    }

    public enum AudioCategory
    {
        /// <summary>Combat, gathering, swings - everything the player's actions make.</summary>
        Effects,

        /// <summary>Weather and the world around.</summary>
        Ambience,

        /// <summary>Not used yet - there is no music; stored so the slider is ready.</summary>
        Music
    }

    /// <summary>
    /// Everything the Settings window edits, stored as settings.json next to the saves (see
    /// GameSettings). Plain fields with safe defaults: a file from an older build simply leaves
    /// new fields at these defaults.
    /// </summary>
    [Serializable]
    public class GameSettingsData
    {
        [Header("Audio (0..1)")]
        public float MasterVolume = 1f;
        public float EffectsVolume = 1f;
        public float AmbienceVolume = 1f;
        public float MusicVolume = 0.7f;

        [Header("Controls")]
        [Tooltip("Multiplier on the camera's base mouse sensitivity.")]
        public float MouseSensitivity = 1f;
        public bool InvertY;
        [Tooltip("Input System binding overrides (InputActionAsset.SaveBindingOverridesAsJson). Empty = defaults.")]
        public string BindingOverrides = string.Empty;

        [Header("Display")]
        [Tooltip("0 x 0 = keep the current/native resolution.")]
        public int ResolutionWidth;
        public int ResolutionHeight;
        public FullScreenMode DisplayMode = FullScreenMode.FullScreenWindow;
        public bool VSync = true;
        [Tooltip("0 = unlimited (only used with VSync off).")]
        public int FrameRateLimit;
        public float FieldOfView = 60f;

        [Header("Graphics")]
        public GraphicsPreset Preset = GraphicsPreset.High;
        public float RenderScale = 1f;
        public ShadowLevel Shadows = ShadowLevel.Medium;
        [Tooltip("MSAA samples: 1 (off), 2, 4, 8.")]
        public int Msaa = 1;
        [Tooltip("0 = full-size textures, 1 = half, 2 = quarter.")]
        public int TextureQuality;
        [Tooltip("How far (m) trees, rocks and ore are drawn (the WorldResource layer); 0 = no limit. Terrain and sky are always drawn in full.")]
        public float ObjectDrawDistance;

        public float GetVolume(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Effects: return EffectsVolume;
                case AudioCategory.Ambience: return AmbienceVolume;
                default: return MusicVolume;
            }
        }
    }
}
