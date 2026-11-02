using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public class BackgroundTests
    {
        private static Type Require(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101." + name)).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Missing native background implementation: " + name);
            return type;
        }

        [Test]
        public void ClockDoesNotAccumulatePausedTimeOrJumpWhenResumed()
        {
            var type = Require("BackgroundTimeState");
            var clock = Activator.CreateInstance(type);
            var advance = type.GetMethod("Advance");
            advance.Invoke(clock, new object[] { 0.25, true });
            advance.Invoke(clock, new object[] { 90.0, false });
            advance.Invoke(clock, new object[] { 0.25, true });
            Assert.That((double)type.GetProperty("Seconds").GetValue(clock), Is.EqualTo(0.5).Within(1e-9));
        }

        [TestCase("Satürn", 5, true)]
        [TestCase("Mars", 23, false)]
        [TestCase("Neptün", 37, false)]
        [TestCase("Jüpiter", 11, false)]
        [TestCase("Venüs", 51, false)]
        public void PlanetSelectionPreservesSeedAndEnablesRingsOnlyForSaturn(string name, int seed, bool rings)
        {
            var type = Require("PlanetDefinition");
            var planet = type.GetMethod("For").Invoke(null, new object[] { name });
            Assert.That(type.GetField("Seed").GetValue(planet), Is.EqualTo(seed));
            Assert.That(type.GetField("Rings").GetValue(planet), Is.EqualTo(rings));
        }

        [TestCase("saturn")]
        [TestCase("mars")]
        [TestCase("neptune")]
        [TestCase("jupiter")]
        [TestCase("venus")]
        public void PrebakedSurfaceHasHighResolutionAndIndependentLatitudeWrap(string name)
        {
            var texture = Resources.Load<Texture2D>("Galaxy101/Planets/" + name);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(2048));
            Assert.That(texture.height, Is.EqualTo(1024));
            Assert.That(texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(texture.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [TestCase("Satürn", 248, 233, 188)]
        [TestCase("Mars", 119, 57, 34)]
        [TestCase("Neptün", 60, 104, 214)]
        [TestCase("Jüpiter", 211, 189, 166)]
        [TestCase("Venüs", 218, 185, 128)]
        public void EditorBakerMatchesIndependentOriginalJavascriptPixelFixture(string name, int red, int green, int blue)
        {
            var type = Require("PlanetTextureBaker");
            var pixels = (Color32[])type.GetMethod("GeneratePixels").Invoke(null, new object[] {name, 64});
            var pixel = pixels[17 * 64 + 29];
            Assert.That((int)pixel.r, Is.EqualTo(red));
            Assert.That((int)pixel.g, Is.EqualTo(green));
            Assert.That((int)pixel.b, Is.EqualTo(blue));
        }

        [Test]
        public void DestroyingBackgroundReleasesOwnedMaterialsAndMeshes()
        {
            int beforeMaterials = Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name.StartsWith("Galaxy101"));
            int beforeMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Galaxy101"));
            var owner = new GameObject("Background resource lifetime test");
            try
            {
                var type = Require("GalaxyBackground");
                var background = owner.AddComponent(type);
                type.GetMethod("Initialize").Invoke(background, new object[] { new VisualElement() });
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name.StartsWith("Galaxy101")), Is.GreaterThan(beforeMaterials));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
            Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m => m.name.StartsWith("Galaxy101")), Is.EqualTo(beforeMaterials));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Galaxy101")), Is.EqualTo(beforeMeshes));
        }

        [TestCase("Galaxy101Planet")]
        [TestCase("Galaxy101Gradient")]
        [TestCase("Galaxy101Stars")]
        public void NativeShadersCompileWithoutErrors(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            Assert.That(shader, Is.Not.Null);
            string messages = string.Join("\n", UnityEditor.ShaderUtil.GetShaderMessages(shader).Select(m => m.message + " at line " + m.line));
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader), Is.False, resource + "\n" + messages);
        }

        [Test]
        public void TenPercentStarOpacityProducesTenPercentCanvasContribution()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to validate canvas color compositing.");
            var target=new RenderTexture(8,8,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var mesh=new Mesh();var material=new Material(Resources.Load<Shader>("Galaxy101Stars"));
            var commands=new UnityEngine.Rendering.CommandBuffer();Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                target.Create();mesh.vertices=new[]{new Vector3(0,0),new Vector3(1280,0),new Vector3(1280,720),new Vector3(0,720)};
                mesh.uv=Enumerable.Repeat(new Vector2(.5f,.5f),4).ToArray();mesh.uv2=new Vector2[4];
                mesh.colors=Enumerable.Repeat(new Color(1,1,1,.1f),4).ToArray();mesh.triangles=new[]{0,1,2,0,2,3};
                commands.SetRenderTarget(target);commands.SetViewport(new Rect(0,0,8,8));commands.ClearRenderTarget(false,true,Color.black);
                commands.DrawMesh(mesh,Matrix4x4.identity,material);Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;pixels=new Texture2D(8,8,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,8,8),0,0);pixels.Apply();
                Assert.That(pixels.GetPixel(4,4).r,Is.EqualTo(.1f).Within(.015f),"Alpha must multiply the canvas color before sRGB conversion; converting white first incorrectly produces about 35% brightness.");
            }
            finally
            {
                RenderTexture.active=previous;commands.Release();target.Release();
                if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void HaloCompositesItsDeclaredCssOpacityInCanvasColorSpace()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to validate canvas color compositing.");
            var target=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var mesh=new Mesh();var material=new Material(Resources.Load<Shader>("Galaxy101Gradient"));
            var commands=new UnityEngine.Rendering.CommandBuffer();Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                target.Create();mesh.vertices=new[]{new Vector3(0,0),new Vector3(1280,0),new Vector3(1280,720),new Vector3(0,720)};mesh.triangles=new[]{0,1,2,0,2,3};
                material.SetTexture("_CanvasBackdrop",Texture2D.blackTexture);material.SetColor("_Halo",new Color(1,1,1,.2f));material.SetColor("_GlowAtmosphere",Color.clear);material.SetFloat("_Flattening",1);
                commands.SetRenderTarget(target);commands.SetViewport(new Rect(0,0,1280,720));commands.ClearRenderTarget(false,true,Color.black);
                commands.DrawMesh(mesh,Matrix4x4.identity,material,0,1);Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                // At the center, CSS halo .2 followed by broad glow .2*.55 yields .2 + .8*.11 = .288.
                Assert.That(pixels.GetPixel(640,420).r,Is.EqualTo(.288f).Within(.015f));
            }
            finally
            {
                RenderTexture.active=previous;commands.Release();target.Release();
                if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AuthVignettePreservesCenterAndDarkensEdgesLikeTheReference()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to validate canvas color compositing.");
            var target=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var mesh=new Mesh();var material=new Material(Resources.Load<Shader>("Galaxy101Gradient"));
            var commands=new UnityEngine.Rendering.CommandBuffer();Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                target.Create();mesh.vertices=new[]{new Vector3(0,0),new Vector3(1280,0),new Vector3(1280,720),new Vector3(0,720)};mesh.triangles=new[]{0,1,2,0,2,3};
                material.SetTexture("_CanvasBackdrop",Texture2D.whiteTexture);material.SetFloat("_AuthLayout",1);
                commands.SetRenderTarget(target);commands.SetViewport(new Rect(0,0,1280,720));commands.ClearRenderTarget(false,true,Color.black);
                commands.DrawMesh(mesh,Matrix4x4.identity,material,0,2);Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active=target;pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                Assert.That(pixels.GetPixel(640,360).r,Is.EqualTo(1).Within(.015f),"reference vignette is transparent at the center");
                Assert.That(pixels.GetPixel(1,1).r,Is.EqualTo(.65f).Within(.02f),"reference vignette darkens the far corner by 35 percent");
            }
            finally
            {
                RenderTexture.active=previous;commands.Release();target.Release();
                if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AuthLayoutUsesReferenceBackgroundAndFadesPlanetHalo()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to validate canvas color compositing.");
            var owner=new GameObject("Auth background layout");
            try
            {
                var type=Require("GalaxyBackground");var background=owner.AddComponent(type);
                var configure=type.GetMethod("UseAuthLayout");Assert.That(configure,Is.Not.Null);
                type.GetMethod("Initialize").Invoke(background,new object[]{new VisualElement()});
                configure.Invoke(background,new object[]{new Vector4(310,340,1.05f,.4f),new Color(1,.8f,.5f,.35f)});
                type.GetMethod("RenderOnce").Invoke(background,new object[]{1280,720});
                var material=(Material)type.GetField("gradientMaterial",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(background);
                Assert.That(material.GetVector("_Shape"),Is.EqualTo(new Vector4(640,252,1024,504)));
                Assert.That(material.GetFloat("_AuthLayout"),Is.EqualTo(1));
                Assert.That(material.GetVector("_AuthPlacement"),Is.EqualTo(new Vector4(310,340,1.05f,.4f)));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ExplicitOffscreenCaptureRendersGradientAndPlanetWithoutAdvancingPausedTime()
        {
            if(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to validate framebuffer pixels.");
            var owner = new GameObject("Background framebuffer test");
            Texture2D pixels = null;
            var previous = RenderTexture.active;
            try
            {
                var type = Require("GalaxyBackground");
                var background = owner.AddComponent(type);
                type.GetMethod("Initialize").Invoke(background,new object[] {new VisualElement()});
                type.GetMethod("Configure").Invoke(background,new object[] {"Satürn",false,"Yüksek"});
                type.GetMethod("SetFocused").Invoke(background,new object[] {false});
                type.GetMethod("RenderOnce").Invoke(background,new object[] {1280,720});
                var output = (RenderTexture)type.GetProperty("Output").GetValue(background);
                Assert.That(output.IsCreated(),Is.True);
                pixels = new Texture2D(1280,720,TextureFormat.RGBA32,false);
                RenderTexture.active = output;
                pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
                var corner = pixels.GetPixel(20,20);
                var planet = pixels.GetPixel(640,420);
                string captureDirectory=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../artifacts/galaxy101"));
                System.IO.Directory.CreateDirectory(captureDirectory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(captureDirectory,"background-test.png"),pixels.EncodeToPNG());
                Debug.Log("GALAXY_BACKGROUND_PROBE corner="+corner+" y300="+pixels.GetPixel(640,300)+" y420="+planet+" x600y420="+pixels.GetPixel(600,420)+" x680y420="+pixels.GetPixel(680,420));
                Assert.That(corner.b,Is.GreaterThan(corner.r),"Saturn theme background must be blue.");
                Assert.That(planet.r+planet.g+planet.b,Is.GreaterThan((corner.r+corner.g+corner.b)*1.5f),"Planet must render over the gradient.");
                var clock = type.GetProperty("TimeState").GetValue(background);
                Assert.That((double)clock.GetType().GetProperty("Seconds").GetValue(clock),Is.EqualTo(0));
            }
            finally
            {
                RenderTexture.active = previous;
                if(pixels!=null) UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
