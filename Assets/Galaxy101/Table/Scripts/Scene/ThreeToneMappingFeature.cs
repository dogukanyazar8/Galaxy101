using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Galaxy101.Table
{
    /// <summary>
    /// The design renderer's tone mapping (table3d.js: three.js ACESFilmicToneMapping, exposure 1.05, no bloom) for every
    /// camera carrying <see cref="ThreeToneMapping"/>. URP's own ACES is the full RRT/ODT and grades the scene darker and
    /// warmer than the prototype; this pass applies three.js's fit exactly, before URP's post stack (which the table
    /// leaves empty). Registered on the project's URP renderers (Galaxy101 → Install Table Tone Mapping).
    /// </summary>
    public sealed class ThreeToneMappingFeature : ScriptableRendererFeature
    {
        private Material material;
        private TonePass pass;
        private FinishPass finishPass;

        public override void Create()
        {
            var shader = Shader.Find("Hidden/Galaxy101/ThreeAces");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new TonePass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            finishPass = new FinishPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1 };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            if (!renderingData.cameraData.camera.TryGetComponent(out ThreeToneMapping tone) || !tone.isActiveAndEnabled) return;
            pass.Exposure = tone.Exposure;
            renderer.EnqueuePass(pass);
            if (tone.EncodedFinishBlending) renderer.EnqueuePass(finishPass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        /// <summary>
        /// Three.js tone-maps and encodes each transparent fragment before blending. Drawing these sprites into the
        /// scene HDR buffer and tone-mapping their sum changes both opacity and colour. Use a bounded 8-bit encoded
        /// target for this source pass, then decode it for the remainder of URP and the existing UI composite.
        /// </summary>
        private sealed class FinishPass : ScriptableRenderPass
        {
            private readonly Material material;
            private static readonly ShaderTagId ShaderTag = new ShaderTagId("Galaxy101Finish");
            private sealed class DrawData { public RendererListHandle Renderers; }
            public FinishPass(Material material) { this.material = material; requiresIntermediateTexture = true; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeDepthTexture.IsValid()) return;
                var source = resources.activeColorTexture;
                var description = graph.GetTextureDesc(source);
                var depth = resources.activeDepthTexture;
                description.name = "Galaxy101 encoded finish blend";
                description.colorFormat = GraphicsFormat.R8G8B8A8_UNorm;
                description.msaaSamples = graph.GetTextureDesc(depth).msaaSamples;
                description.depthBufferBits = 0; description.clearBuffer = false;
                var encoded = graph.CreateTexture(description);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, encoded, material, 1), "Galaxy101 encode finish destination");

                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                var drawing = RenderingUtils.CreateDrawingSettings(ShaderTag, rendering, camera, lights, SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.transparent, camera.camera.cullingMask);
                var renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                using (var builder = graph.AddRasterRenderPass<DrawData>("Galaxy101 source finish blending", out var data))
                {
                    data.Renderers = renderers;
                    builder.UseRendererList(renderers);
                    builder.SetRenderAttachment(encoded, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
                    builder.SetRenderFunc(static (DrawData draw, RasterGraphContext context) => context.cmd.DrawRendererList(draw.Renderers));
                }
                description = graph.GetTextureDesc(source);
                description.name = "Galaxy101 finish composite"; description.clearBuffer = false; description.depthBufferBits = 0;
                var result = graph.CreateTexture(description);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(encoded, result, material, 2), "Galaxy101 decode finish composite");
                resources.cameraColor = result;
            }
        }

        private sealed class TonePass : ScriptableRenderPass
        {
            private static readonly int ExposureId = Shader.PropertyToID("_Galaxy101Exposure");
            private readonly Material material;
            public float Exposure = ThreeToneMapping.DesignExposure;

            public TonePass(Material material) { this.material = material; requiresIntermediateTexture = true; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                description.name = "Galaxy101 three.js tone mapped"; description.clearBuffer = false; description.depthBufferBits = 0;
                var target = renderGraph.CreateTexture(description);
                material.SetFloat(ExposureId, Exposure);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, target, material, 0), "Galaxy101 three.js ACES");
                resources.cameraColor = target;
            }
        }
    }
}
