using UnityEngine;
using UnityEngine.Rendering;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// Volume settings for the sun shafts (SunShaftsFeature on the PC renderer, 2026-11-13). Put on
    /// the post-process profiles so they blend with the time of day like the rest of the grading:
    /// strongest at golden hour, none under overcast. Intensity 0 = the effect is skipped.
    /// </summary>
    [VolumeComponentMenu("NAV/Sun Shafts")]
    [SupportedOnRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset))]
    public sealed class SunShaftsVolume : VolumeComponent
    {
        [Tooltip("How bright the shafts are (0 = off).")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 3f);

        [Tooltip("Sky brightness below this gives no shafts.")]
        public ClampedFloatParameter threshold = new ClampedFloatParameter(0.2f, 0f, 2f);

        [Tooltip("How far across the screen the shafts reach (fraction of the way to the sun).")]
        public ClampedFloatParameter length = new ClampedFloatParameter(0.75f, 0.1f, 1f);

        [Tooltip("Radius round the sun (screen heights) whose sky feeds the shafts.")]
        public ClampedFloatParameter sunRadius = new ClampedFloatParameter(1.0f, 0.05f, 2f);

        [Tooltip("Colour of the shafts, multiplied by the sun's own colour.")]
        public ColorParameter tint = new ColorParameter(new Color(1f, 0.85f, 0.65f), false, false, true);

        public bool IsActive() => intensity.value > 0f;
    }
}
