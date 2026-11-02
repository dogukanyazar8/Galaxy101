using System.Collections;
using System.Collections.Generic;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class FinishBlendRenderingTests
    {
        private GameObject owner;
        private RenderTexture target;
        private readonly List<Material> materials = new List<Material>();

        [UnityTest]
        public IEnumerator SourceSrgbAlphaAndAdditiveBlendsKeepTheOpaqueSceneDepth()
        {
            owner = new GameObject("Source finish blend camera");
            var camera = owner.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1;
            camera.transform.position = new Vector3(0, 0, -2); camera.nearClipPlane = .01f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.allowHDR = true;
            camera.cullingMask = 1 << 29;
            var data = owner.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            owner.AddComponent<ThreeToneMapping>().EncodedFinishBlending = true;
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture = target;
            var white = Quad("source normal white", FinishMaterial(false, .5f), 0);
            yield return null; yield return null;
            AssertGray(ReadCenter(), 128, "Source normal: encoded white .5 over black");
            Assert.That((int)ReadCenter().a, Is.EqualTo(128).Within(2), "Source normal alpha");
            white.SetActive(false);
            var first = Quad("source additive white 1", FinishMaterial(true, .25f), .1f);
            var second = Quad("source additive white 2", FinishMaterial(true, .25f), 0);
            yield return null; yield return null;
            AssertGray(ReadCenter(), 128, "Source additive: encoded white .25 + .25 over black");
            Assert.That((int)ReadCenter().a, Is.EqualTo(128).Within(2), "Source additive alpha uses One/One");

            var black = new Material(Shader.Find("Universal Render Pipeline/Unlit")); materials.Add(black); black.SetColor("_BaseColor", Color.black);
            Quad("opaque depth occluder", black, -.2f);
            yield return null; yield return null;
            AssertGray(ReadCenter(), 0, "The encoded finish pass must preserve opaque depth testing");
            first.SetActive(false); second.SetActive(false);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ToneMappedFinishMatchesTheOriginalThreeRendererAtFourSamples()
        {
            owner = new GameObject("Source four-sample finish camera");
            var camera = owner.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1;
            camera.transform.position = new Vector3(0, 0, -2); camera.nearClipPlane = .01f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.allowHDR = true;
            camera.cullingMask = 1 << 29;
            owner.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            owner.AddComponent<ThreeToneMapping>().EncodedFinishBlending = true;
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; target.Create(); camera.targetTexture = target;
            var material = FinishMaterial(false, .5f); material.SetFloat("_BypassToneMapping", 0); material.SetFloat("_Galaxy101Exposure", 1.05f);
            Quad("source tone-mapped white", material, 0);
            yield return null; yield return null;
            // Captured from the original vendor renderer, not calculated with the native shader under test.
            // ref/finish/encoded-blend-source.png: white MeshBasicMaterial, opacity .5, ACESFilmic, exposure1.05.
            var actual = ReadCenter(); AssertGray(actual, 114, "Original Three.js tone-mapped fragment at 4x MSAA");
            Assert.That((int)actual.a, Is.EqualTo(128).Within(2));
            LogAssert.NoUnexpectedReceived();
        }

        private Material FinishMaterial(bool additive, float alpha)
        {
            var material = new Material(Resources.Load<Material>("Galaxy101/Table/Fx/fx_finish")); materials.Add(material);
            material.renderQueue = (int)RenderQueue.Transparent; material.SetVector("_BaseColor", new Vector4(1, 1, 1, alpha));
            material.SetFloat("_BypassToneMapping", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            return material;
        }

        private GameObject Quad(string name, Material material, float z)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = name; quad.layer = 29;
            quad.transform.SetParent(owner.transform, false); quad.transform.position = new Vector3(0, 0, z);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material; return quad;
        }

        private Color32 ReadCenter()
        {
            var previous = RenderTexture.active; var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try { RenderTexture.active = target; pixel.ReadPixels(new Rect(64, 64, 1, 1), 0, 0); pixel.Apply(); return pixel.GetPixel(0, 0); }
            finally { RenderTexture.active = previous; Object.Destroy(pixel); }
        }
        private static void AssertGray(Color32 actual, int expected, string message)
        {
            TestContext.WriteLine(message + ": " + actual);
            Assert.That((int)actual.r, Is.EqualTo(expected).Within(2), message + " red");
            Assert.That((int)actual.g, Is.EqualTo(expected).Within(2), message + " green");
            Assert.That((int)actual.b, Is.EqualTo(expected).Within(2), message + " blue");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) Object.Destroy(owner);
            foreach (var material in materials) if (material != null) Object.Destroy(material); materials.Clear();
            yield return null;
            if (target != null) { target.Release(); Object.Destroy(target); }
        }
    }
}
