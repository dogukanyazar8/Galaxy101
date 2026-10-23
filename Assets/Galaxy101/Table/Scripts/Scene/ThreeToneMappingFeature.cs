using UnityEngine;
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

        public override void Create()
        {
            var shader = Shader.Find("Hidden/Galaxy101/ThreeAces");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new TonePass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView) return;
            if (!renderingData.cameraData.camera.TryGetComponent(out ThreeToneMapping tone) || !tone.isActiveAndEnabled) return;
            pass.Exposure = tone.Exposure;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

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
