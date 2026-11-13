using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// Screen-space sun shafts (crepuscular rays) for URP, 2026-11-13: the bright sky round the sun
    /// is masked at quarter resolution (only where nothing stands in front - trees and houses cut
    /// it), smeared towards the sun in three radial-blur passes and added over the picture. So
    /// light "cuts" through the crowns as dark and bright bands when the sun is in or near view.
    /// Settings come from SunShaftsVolume on the post-process profiles; the colour and strength
    /// follow RenderSettings.sun (the moon at night), and the shafts fade out as the sun leaves
    /// the screen. Cheap: ~5 small fullscreen passes. Shader: NAV/Shaders/SunShafts.shader.
    /// </summary>
    public class SunShaftsFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;
        [SerializeField] private RenderPassEvent _event = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material _material;
        private SunShaftsPass _pass;

        public override void Create()
        {
            if (_shader == null)
            {
                _shader = Shader.Find("Hidden/NAV/SunShafts");
            }

            _material = _shader != null ? CoreUtils.CreateEngineMaterial(_shader) : null;
            _pass = new SunShaftsPass { renderPassEvent = _event };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if (_material == null || (type != CameraType.Game && type != CameraType.SceneView))
            {
                return;
            }

            var settings = VolumeManager.instance.stack.GetComponent<SunShaftsVolume>();
            Light sun = RenderSettings.sun;
            if (settings == null || !settings.IsActive() || sun == null || !sun.isActiveAndEnabled || sun.intensity <= 0.01f)
            {
                return;
            }

            if (!_pass.Setup(_material, settings, sun, renderingData.cameraData.camera))
            {
                return;
            }

            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        private class SunShaftsPass : ScriptableRenderPass
        {
            private const int MaskPass = 0;
            private const int BlurPass = 1;
            private const int CompositePass = 2;

            private static readonly int SunPositionId = Shader.PropertyToID("_SunShaftsSun");
            private static readonly int ParamsId = Shader.PropertyToID("_SunShaftsParams");
            private static readonly int ColorId = Shader.PropertyToID("_SunShaftsColor");
            private static readonly int StepId = Shader.PropertyToID("_SunShaftsStep");

            private Material _material;
            private float _length;

            private class PassData
            {
                public Material Material;
                public int Pass;
                public float Step;
                public TextureHandle Source;
            }

            public SunShaftsPass()
            {
                profilingSampler = new ProfilingSampler("NAV Sun Shafts");
                requiresIntermediateTexture = true;
            }

            /// <summary>Works out where the sun is on screen; false when it is too far off to matter.</summary>
            public bool Setup(Material material, SunShaftsVolume settings, Light sun, UnityEngine.Camera camera)
            {
                Vector3 towardsSun = -sun.transform.forward;
                Vector3 viewport = camera.WorldToViewportPoint(camera.transform.position + towardsSun * camera.farClipPlane * 0.5f);
                float facing = Vector3.Dot(camera.transform.forward, towardsSun);
                if (facing <= 0f)
                {
                    return false;
                }

                // Fade as the sun leaves the screen (fully gone ~0.6 screens past the edge).
                float offX = Mathf.Max(0f, Mathf.Abs(viewport.x - 0.5f) - 0.5f);
                float offY = Mathf.Max(0f, Mathf.Abs(viewport.y - 0.5f) - 0.5f);
                float fade = Mathf.Clamp01(1f - Mathf.Max(offX, offY) / 0.6f) * Mathf.Clamp01(facing * 3f);
                if (fade <= 0f)
                {
                    return false;
                }

                _material = material;
                _length = settings.length.value;
                float aspect = camera.pixelHeight > 0 ? camera.pixelWidth / (float)camera.pixelHeight : 1.7f;
                Color colour = settings.tint.value * sun.color * Mathf.Min(sun.intensity, 2f) * settings.intensity.value * fade;
                material.SetVector(SunPositionId, new Vector4(viewport.x, viewport.y, aspect, 0f));
                material.SetVector(ParamsId, new Vector4(settings.threshold.value, settings.sunRadius.value, 0f, 0f));
                material.SetColor(ColorId, colour);
                return true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureDesc desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
                desc.width = Mathf.Max(1, desc.width / 4);
                desc.height = Mathf.Max(1, desc.height / 4);
                desc.depthBufferBits = DepthBits.None;
                desc.msaaSamples = MSAASamples.None;
                desc.clearBuffer = false;
                desc.filterMode = FilterMode.Bilinear;
                desc.wrapMode = TextureWrapMode.Clamp;
                desc.name = "_SunShaftsA";
                TextureHandle a = renderGraph.CreateTexture(desc);
                desc.name = "_SunShaftsB";
                TextureHandle b = renderGraph.CreateTexture(desc);

                // Mask: bright open sky round the sun (reads depth to leave out anything standing in front).
                AddPass(renderGraph, "Sun Shafts Mask", resources.activeColorTexture, a, MaskPass, 0f, resources.cameraDepthTexture);
                // Three radial blurs, each step a finer smear towards the sun.
                AddPass(renderGraph, "Sun Shafts Blur 1", a, b, BlurPass, _length, TextureHandle.nullHandle);
                AddPass(renderGraph, "Sun Shafts Blur 2", b, a, BlurPass, _length * 0.35f, TextureHandle.nullHandle);
                AddPass(renderGraph, "Sun Shafts Blur 3", a, b, BlurPass, _length * 0.12f, TextureHandle.nullHandle);
                // Added over the frame.
                AddPass(renderGraph, "Sun Shafts Composite", b, resources.activeColorTexture, CompositePass, 0f, TextureHandle.nullHandle);
            }

            private void AddPass(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int pass, float step, TextureHandle depth)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out PassData data, profilingSampler))
                {
                    data.Material = _material;
                    data.Pass = pass;
                    data.Step = step;
                    data.Source = source;
                    builder.UseTexture(source, AccessFlags.Read);
                    if (depth.IsValid())
                    {
                        builder.UseTexture(depth, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalFloat(StepId, d.Step);
                        Blitter.BlitTexture(context.cmd, d.Source, new Vector4(1f, 1f, 0f, 0f), d.Material, d.Pass);
                    });
                }
            }
        }
    }
}
