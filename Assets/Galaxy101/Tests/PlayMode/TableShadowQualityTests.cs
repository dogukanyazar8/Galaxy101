using System.Collections;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class TableShadowQualityTests
    {
        [UnityTest]
        public IEnumerator HighMatchesTheSourceWhileLowerQualityAndSceneExitRestoreTheirBudgets()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null);
            int resolution = pipeline.mainLightShadowmapResolution, cascades = pipeline.shadowCascadeCount, msaa = pipeline.msaaSampleCount;
            float distance = pipeline.shadowDistance;
            var owner = new GameObject("Table shadow quality contract");
            try
            {
                var table = owner.AddComponent<TableScene>();
                yield return null;
                Assert.That(pipeline.mainLightShadowmapResolution, Is.EqualTo(4096));
                Assert.That(pipeline.shadowCascadeCount, Is.EqualTo(1));
                table.SetEffectQuality(1);
                Assert.That(pipeline.mainLightShadowmapResolution, Is.EqualTo(2048));
                Assert.That(pipeline.shadowDistance, Is.GreaterThan(0));
                table.SetEffectQuality(0);
                Assert.That(pipeline.shadowDistance, Is.Zero);
                table.SetEffectQuality(2);
                Assert.That(pipeline.mainLightShadowmapResolution, Is.EqualTo(4096));
            }
            finally { Object.Destroy(owner); }
            yield return null;
            Assert.That(pipeline.mainLightShadowmapResolution, Is.EqualTo(resolution));
            Assert.That(pipeline.shadowCascadeCount, Is.EqualTo(cascades));
            Assert.That(pipeline.msaaSampleCount, Is.EqualTo(msaa));
            Assert.That(pipeline.shadowDistance, Is.EqualTo(distance));
        }
    }
}
