using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class TableFinishV4Tests
    {
        private GameObject owner, tile;
        private float previousScale;
        private string previousPlanet;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            previousScale = Time.timeScale; Time.timeScale = 4;
            previousPlanet = TableScene.PlanetOverride; TableScene.PlanetOverride = "Satürn";
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousScale; TableScene.PlanetOverride = previousPlanet;
            if (tile != null) UnityEngine.Object.Destroy(tile);
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
        }
        private TableFx Build(out TableScene scene)
        {
            owner = new GameObject("source finish fixture"); scene = owner.AddComponent<TableScene>();
            var fx = owner.AddComponent<TableFx>(); fx.Init(scene); return fx;
        }
        private static int KindCount(TableFx fx, string kind)
        {
            var nodes = (IEnumerable)typeof(TableFx).GetField("finishNodes", Private).GetValue(fx);
            int count = 0;
            foreach (var node in nodes) if (node.GetType().GetField("Kind").GetValue(node).ToString() == kind) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator BurstsKeepThreePointProjectionWhileSparksAndSmokeKeepSpriteDimensions()
        {
            var fx = Build(out var scene); Time.timeScale = 0;
            fx.Burst(new Vector3(0, 1, 0), Color.white, 1, 0); fx.SparkAt(new Vector3(0, 1, 0), Color.white); fx.Smoke(new Vector3(0, 1, 0));
            var camera = scene.CameraRig.Camera;
            foreach (float fov in new[] { 40f, 60f })
            {
                camera.fieldOfView = fov;
                foreach (string collection in new[] { "bursts", "sparks", "haze" })
                {
                    var points = typeof(TableFx).GetField(collection, Private).GetValue(fx);
                    points.GetType().GetMethod("Step").Invoke(points, new object[] { 0f, camera });
                    var mesh = (Mesh)points.GetType().GetField("mesh", Private).GetValue(points);
                    var vertices = mesh.vertices;
                    float expected = collection == "bursts" ? .022f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad) : collection == "sparks" ? .03f : .02f;
                    Assert.That(Vector3.Distance(vertices[0], vertices[1]), Is.EqualTo(expected).Within(.000001f), collection + " at " + fov + " degrees");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FinishBurstsAndShocksUseEncodedMaterialsWithoutChangingSharedDiceEffects()
        {
            var fx = Build(out var scene); Time.timeScale = 0;
            var position = new Vector3(0, .8f, 0);
            fx.Burst(position, Color.white, 1, 0); fx.DiceShock(position, Color.white, 1, 1);
            fx.FinishImpact("nuke", scene.CameraRig);
            Assert.That(fx.LivePoints, Is.EqualTo(281), "The source's 280 finish points remain included beside one shared point.");
            Assert.That(fx.LiveDiceShocks, Is.EqualTo(2), "Finish and shared shocks remain counted independently.");
            var points = owner.transform.Find("finish_bursts");
            Assert.That(points, Is.Not.Null, "Finish bursts must leave the shared HDR particle renderer.");
            var renderer = points.GetComponent<MeshRenderer>();
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Galaxy101/Finish"));
            Assert.That(renderer.sharedMaterial.GetFloat("_BypassToneMapping"), Is.Zero, "Source burst PointsMaterial uses tone mapping before encoded blending.");
            Assert.That(renderer.sharedMaterial.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.One));
            Assert.That(renderer.sharedMaterial.GetFloat("_DstBlendAlpha"), Is.EqualTo((float)BlendMode.One));
            var legacy = owner.transform.Find("Fx_Bursts").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(legacy.shader.name, Is.Not.EqualTo("Galaxy101/Finish"));
            Assert.That(legacy.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.Zero), "The shared dice/gift path keeps its existing alpha contract.");

            var step = typeof(TableFx).GetMethod("StepFinish", Private);
            float start = Time.time, now = start + .45f;
            step.Invoke(fx, new object[] { now, .45f, scene.CameraRig.Camera });
            var mesh = points.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.colors[0].a, Is.EqualTo(.5f).Within(.00001f), "Finish point opacity is the source's raw 1-u.");
            var gold = mesh.colors[mesh.vertexCount - 1]; var expected = TableFx.Hex("#FFE08A").linear;
            Assert.That(gold.g, Is.EqualTo(expected.g).Within(.00001f), "The encoded shader consumes linear RGB tint, matching THREE.Color.");
            foreach (float fov in new[] { 40f, 60f })
            {
                scene.CameraRig.Camera.fieldOfView = fov;
                step.Invoke(fx, new object[] { now, 0f, scene.CameraRig.Camera });
                Assert.That(Vector3.Distance(mesh.vertices[0], mesh.vertices[1]), Is.EqualTo(.022f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad)).Within(.000001f));
            }
            var shock = owner.transform.Find("finish_shock");
            var material = shock.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Galaxy101/Finish"));
            Assert.That(material.GetFloat("_BypassToneMapping"), Is.Zero);
            Assert.That(shock.GetComponent<MeshFilter>().sharedMesh.triangles.Length, Is.EqualTo(432), "Source shock geometry has 72 segments, each formed by two triangles.");
            // Long suites increase Time.time's float ULP; use the representable elapsed time passed to the renderer.
            float elapsed = now - start;
            Assert.That(material.GetVector("_BaseColor").w, Is.EqualTo(.95f * Mathf.Pow(1 - elapsed / .7f, 1.4f)).Within(.00001f), "Shock fade must not use the shared approximation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourSourceStylesCreateTheirDistinctStagesAndExpire()
        {
            var fx = Build(out var scene);
            foreach (string style in new[] { "nuke", "blackhole", "supernova", "meteor" })
            {
                fx.FinishImpact(style, scene.CameraRig);
                switch (style)
                {
                    case "nuke":
                        Assert.That(KindCount(fx, "Plume"), Is.EqualTo(34));
                        Assert.That(KindCount(fx, "Fire"), Is.EqualTo(3));
                        Assert.That(fx.LivePoints, Is.EqualTo(280)); break;
                    case "blackhole":
                        Assert.That(KindCount(fx, "BlackRing"), Is.EqualTo(1));
                        Assert.That(KindCount(fx, "Spiral"), Is.EqualTo(80));
                        Assert.That(KindCount(fx, "Core"), Is.Zero, "Source bhcore push is commented out, so no visible black core is added.");
                        yield return Until(() => KindCount(fx, "Fire") == 2, "black-hole release at 1.15 seconds"); break;
                    case "supernova":
                        Assert.That(KindCount(fx, "Core"), Is.EqualTo(1));
                        Assert.That(KindCount(fx, "Implode"), Is.EqualTo(90));
                        yield return Until(() => KindCount(fx, "Ray") == 28, "supernova release at .56 seconds");
                        Assert.That(KindCount(fx, "Shell"), Is.EqualTo(2));
                        var dome = owner.transform.Find("finish_shell").GetComponent<MeshFilter>().sharedMesh;
                        var positions = dome.vertices; var normals = dome.normals;
                        for (int i = 0; i < positions.Length; i++) if (normals[i].sqrMagnitude > .5f)
                            Assert.That(Vector3.Dot(positions[i].normalized, normals[i]), Is.GreaterThan(.9f), "The source back-side shell needs outward mesh winding.");
                        Assert.That(KindCount(fx, "Beam"), Is.EqualTo(1));
                        Assert.That(KindCount(fx, "Ember"), Is.EqualTo(280));
                        Assert.That(KindCount(fx, "Nebula"), Is.EqualTo(10)); break;
                    case "meteor":
                        Assert.That(KindCount(fx, "Rock"), Is.EqualTo(26));
                        Assert.That(KindCount(fx, "Ember"), Is.EqualTo(260));
                        Assert.That(KindCount(fx, "Roll"), Is.EqualTo(28));
                        Assert.That(KindCount(fx, "Meteor"), Is.EqualTo(7));
                        Assert.That(KindCount(fx, "Scorch"), Is.EqualTo(1));
                        Assert.That(KindCount(fx, "Rim"), Is.EqualTo(1)); break;
                }
                yield return Until(() => fx.LiveFinishNodes == 0 && fx.LivePoints == 0 && fx.LiveDiceDust == 0 && fx.LiveDiceShocks == 0, style + " cleanup");
            }
            var normal = owner.transform.Find("finish_dust").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(normal.shader.name, Is.EqualTo("Galaxy101/Finish"));
            Assert.That(normal.shader.isSupported, Is.True);
            Assert.That(normal.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.SrcAlpha));
            Assert.That(normal.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            Assert.That(normal.GetFloat("_SrcBlendAlpha"), Is.EqualTo((float)BlendMode.One));
            Assert.That(normal.GetFloat("_DstBlendAlpha"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));
            var assets = ((IEnumerable<UnityEngine.Object>)typeof(TableFx).GetField("finishAssets", Private).GetValue(fx)).ToList();
            assets.Add(normal);
            foreach (var filter in owner.GetComponentsInChildren<MeshFilter>()) if (filter.name.StartsWith("finish_", StringComparison.Ordinal)) assets.Add(filter.sharedMesh);
            UnityEngine.Object.Destroy(owner); owner = null;
            yield return null; yield return null;
            Assert.That(assets.All(asset => asset == null), Is.True, "Every finish texture, shared geometry, batch mesh and material must be released with its table.");
        }

        [UnityTest]
        public IEnumerator MeteorFlightSettlesAtTheFixedCenterAndDestroyCancelsAnotherFlight()
        {
            var fx = Build(out var scene);
            tile = GameObject.CreatePrimitive(PrimitiveType.Cube); tile.transform.position = new Vector3(-.4f, .9f, .8f); tile.transform.localScale = Vector3.one * .03f;
            bool hit = false;
            Assert.That(fx.Finish("meteor", tile.transform, scene.CameraRig, () => hit = true), Is.EqualTo(1.8f));
            yield return Until(() => hit, "meteor tile impact");
            Assert.That(Vector3.Distance(tile.transform.position, FinishFlightMotion.Target.position), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(tile.transform.rotation, FinishFlightMotion.Target.rotation), Is.LessThan(.03f));
            Assert.That(tile.transform.localScale.x, Is.EqualTo(.03f).Within(.00001f));
            hit = false;
            fx.Finish("nuke", tile.transform, scene.CameraRig, () => hit = true);
            yield return null;
            UnityEngine.Object.Destroy(owner); owner = null;
            yield return null;
            Assert.That(hit, Is.False, "Destroying the table must cancel the pending impact callback.");
            Assert.That(tile.transform.localScale.x, Is.EqualTo(.03f).Within(.00001f));
        }

        [UnityTest]
        public IEnumerator OwnedFinishTileKeepsItsSourceHopAndLandingPopWhileDisabled()
        {
            var fx = Build(out var scene);
            tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var view = tile.AddComponent<TileView>();
            view.Snap(new Pose(new Vector3(-.4f, .9f, .8f), Quaternion.identity)); view.enabled = false;
            bool hit = false; fx.Finish("nuke", tile.transform, scene.CameraRig, () => hit = true);
            yield return Until(() => hit, "owned tile impact");
            Time.timeScale = 0;
            var hops = (IList)typeof(TableFx).GetField("finishHops", Private).GetValue(fx);
            object own = null;
            foreach (var hop in hops) if ((TileView)hop.GetType().GetField("Tile").GetValue(hop) == view) own = hop;
            Assert.That(own, Is.Not.Null, "The disabled finishing tile still receives the source hopAll effect.");
            var type = own.GetType(); float amplitude = (float)type.GetField("Amplitude").GetValue(own);
            float distance = Vector3.Distance(FinishFlightMotion.Target.position, new Vector3(0, .76f, 0));
            Assert.That(amplitude, Is.EqualTo(.012f + .05f * (1 - distance / 1.1f)).Within(.00001f));
            Assert.That((float)type.GetField("Duration").GetValue(own), Is.EqualTo(.55f));
            float began = (float)typeof(TableFx).GetField("finishPopStart", Private).GetValue(fx);
            Assert.That((float)type.GetField("Start").GetValue(own) - began, Is.EqualTo(distance * .35f).Within(.0001f));
            tile.transform.position = FinishFlightMotion.Target.position;
            hops.Clear(); hops.Add(Activator.CreateInstance(type, new object[] { view, Time.time - .275f, amplitude, .55f, true }));
            typeof(TableFx).GetField("finishPopStart", Private).SetValue(fx, Time.time - .17f);
            typeof(TableFx).GetMethod("LateUpdate", Private).Invoke(fx, null);
            Assert.That(tile.transform.position.y, Is.EqualTo(FinishFlightMotion.Target.position.y + amplitude).Within(.00001f));
            Assert.That(tile.transform.localScale.x, Is.EqualTo(1.025f).Within(.00001f));
        }

        private static IEnumerator Until(Func<bool> condition, string step)
        {
            float until = Time.realtimeSinceStartup + 15;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for " + step);
        }
    }
}
