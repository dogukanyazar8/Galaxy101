using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class ShipRenderingTests
    {
        private static Type NativeType => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.GalaxyShips")).FirstOrDefault(t => t != null);

        [TestCase("Galaxy101ShipsFx")]
        [TestCase("Galaxy101ShipsResolve")]
        [TestCase("Galaxy101ShipsOcclusion")]
        [TestCase("Galaxy101ShipsGlass")]
        public void NativeShipShadersCompile(string name)
        {
            var shader = Resources.Load<Shader>(name);
            Assert.That(shader, Is.Not.Null, name);
            var messages = UnityEditor.ShaderUtil.GetShaderMessages(shader);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False, string.Join("\n", messages.Select(m => m.message + " at " + m.line)));
        }

        [Test]
        public void DynamicEffectsCanGrowThenShrinkWithoutStaleMeshIndices()
        {
            var owner = new GameObject("Ship dynamic mesh regression test");
            IDisposable effects = null;
            try
            {
                var simulationType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.ShipFlightSimulation")).First(t => t != null);
                var effectsType = NativeType.Assembly.GetType("Galaxy101.ShipEffects");
                var simulation = Activator.CreateInstance(simulationType, 42);
                effects = (IDisposable)Activator.CreateInstance(effectsType, owner.transform, simulation, 30);
                effectsType.GetMethod("SetModelReady").Invoke(effects, new object[] { 0 });
                simulationType.GetMethod("Advance").Invoke(simulation, new object[] { .9, true });
                effectsType.GetMethod("Build").Invoke(effects, null);
                var mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh; int grown = mesh.vertexCount;
                Assert.That(grown, Is.GreaterThan(0));
                effectsType.GetMethod("ClearTrails").Invoke(effects, null);
                effectsType.GetMethod("Build").Invoke(effects, null);
                Assert.That(mesh.vertexCount, Is.LessThan(grown));
                Assert.That(mesh.GetIndices(0).All(i => i >= 0 && i < mesh.vertexCount), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally { effects?.Dispose(); UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ThemeAndQualityChangesPreserveExistingTrailGeometry()
        {
            var owner = new GameObject("Ship theme trail continuity test");
            IDisposable effects = null;
            try
            {
                var simulationType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.ShipFlightSimulation")).First(t => t != null);
                var effectsType = NativeType.Assembly.GetType("Galaxy101.ShipEffects");
                var simulation = Activator.CreateInstance(simulationType, 42);
                effects = (IDisposable)Activator.CreateInstance(effectsType, owner.transform, simulation, 30);
                effectsType.GetMethod("SetModelReady").Invoke(effects, new object[] { 0 });
                simulationType.GetMethod("Advance").Invoke(simulation, new object[] { .9, true });
                effectsType.GetMethod("Build").Invoke(effects, null);
                var mesh = owner.GetComponentInChildren<MeshFilter>().sharedMesh; int before = mesh.vertexCount;
                var layer = owner.AddComponent(NativeType);
                NativeType.GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(layer, simulation);
                NativeType.GetField("effects", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(layer, effects);
                NativeType.GetMethod("Configure").Invoke(layer, new object[] { "Mars", true, "low" });
                effectsType.GetMethod("Build").Invoke(effects, null);
                Assert.That(mesh.vertexCount, Is.EqualTo(before), "Theme/quality change discarded the live trail particles");
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); effects?.Dispose(); }
        }

        [UnityTest]
        public IEnumerator OriginalGlbsRenderVisibleGeometryAndReleaseOwnedResources()
        {
            var type = NativeType; Assert.That(type, Is.Not.Null);
            int before = OwnedCount();
            var owner = new GameObject("Ship renderer lifetime test");
            try
            {
                var component = owner.AddComponent(type);
                var target = new VisualElement();
                type.GetMethod("Initialize").Invoke(component, new object[] { target });
                var task = (Task)type.GetProperty("Initialization").GetValue(component);
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 45;
                while (!task.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(task.IsCompleted, Is.True, "GLB initialization timed out");
                Assert.That((bool)type.GetProperty("IsReady").GetValue(component), Is.True);
                var keepers = Resources.LoadAll<Material>("Galaxy101/Ships/RuntimeVariants");
                var world = (GameObject)type.GetField("world", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
                var imported = world.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null && m.shader.name.StartsWith("Shader Graphs/glTF-", StringComparison.Ordinal)).Distinct().ToArray();
                Assert.That(imported.Length, Is.GreaterThan(0), "Original GLBs must use their original glTF shaders.");
                foreach (var material in imported)
                    Assert.That(keepers.Any(k => k.shader == material.shader && (!material.enableInstancing || k.enableInstancing) && DeclaredKeywords(k).SequenceEqual(DeclaredKeywords(material))), Is.True,
                        "Player build lacks original ship material variant: " + material.shader.name + " / " + string.Join(" ", material.shaderKeywords));
                type.GetMethod("RenderOnce").Invoke(component, new object[] { 320, 180 });
                var output = (RenderTexture)type.GetProperty("Output").GetValue(component);
                Assert.That(output, Is.Not.Null); Assert.That(target.pickingMode, Is.EqualTo(PickingMode.Ignore));
                var previous = RenderTexture.active; RenderTexture.active = output;
                var pixels = new Texture2D(320, 180, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); pixels.Apply();
                    Assert.That(pixels.GetPixels32().Count(p => p.a > 32), Is.GreaterThan(2), "Original ship geometry did not reach the transparent output");
                }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
            yield return null;
            Assert.That(OwnedCount(), Is.EqualTo(before), "Ship world/material/mesh/texture leaked after disposal");
        }

        [UnityTest]
        public IEnumerator DestroyDuringSharedImportDoesNotKeepTheVisualAlive()
        {
            var type = NativeType; Assert.That(type, Is.Not.Null);
            int before = OwnedCount();
            var owner = new GameObject("Cancelled ship renderer test");
            var component = owner.AddComponent(type);
            type.GetMethod("Initialize").Invoke(component, new object[] { new VisualElement() });
            var task = (Task)type.GetProperty("Initialization").GetValue(component);
            UnityEngine.Object.DestroyImmediate(owner);
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 45;
            while ((!task.IsCompleted || OwnedCount() > before) && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True); Assert.That(task.IsFaulted, Is.False);
            Assert.That(OwnedCount(), Is.EqualTo(before));
        }
        private static string[] DeclaredKeywords(Material material) => material.shaderKeywords.Where(k => material.shader.keywordSpace.FindKeyword(k).isValid).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        private static int OwnedCount() => Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Count(o => o != null && o.name.StartsWith("Galaxy101 ship", StringComparison.Ordinal) && (o is GameObject || o is Mesh || o is Material || o is Texture));
    }
}
