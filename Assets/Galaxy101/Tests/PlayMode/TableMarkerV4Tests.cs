using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Tests
{
    public sealed class TableMarkerV4Tests
    {
        private GameObject sceneOwner, gameOwner, hudOwner;
        private string statePath;
        private float previousScale;
        private string previousPlanet;

        [SetUp]
        public void SetUp()
        {
            previousScale = Time.timeScale;
            previousPlanet = TableScene.PlanetOverride;
            TableScene.PlanetOverride = "Satürn";
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousScale;
            TableScene.PlanetOverride = previousPlanet;
            TableGame.Pending = null;
            foreach (var owner in new[] { hudOwner, gameOwner, sceneOwner })
                if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private TableScene BuildScene()
        {
            sceneOwner = new GameObject("V4 marker scene");
            return sceneOwner.AddComponent<TableScene>();
        }

        private static MeshRenderer Renderer(TableScene scene, string name) =>
            scene.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == name);

        private static float Alpha(MeshRenderer renderer) => renderer.sharedMaterial.GetColor("_BaseColor").a;

        [UnityTest]
        public IEnumerator HaloMatchesSourceGeometryAndSelectedSkin()
        {
            var scene = BuildScene();
            var fx = sceneOwner.AddComponent<TableFx>(); fx.Init(scene);
            var halo = Renderer(scene, "masa_hale");
            Assert.That(halo, Is.Not.Null, "table3d.js masa_hale must be visible below the table");
            Assert.That(halo.bounds.size.x, Is.EqualTo(3.6f).Within(.0001f));
            Assert.That(halo.bounds.size.z, Is.EqualTo(3.6f).Within(.0001f));
            Assert.That(halo.transform.position.y, Is.EqualTo(.004f).Within(.00001f));
            Assert.That(halo.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(halo.receiveShadows, Is.False);
            Assert.That(halo.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            foreach (var sample in new[] { ("station", .25f), ("wood", .12f), ("holo", .4f), ("andromeda", .25f) })
            {
                scene.SetTableSkin(sample.Item1);
                yield return null;
                Assert.That(Alpha(halo), Is.EqualTo(ThreeBlend.Additive(sample.Item2)).Within(.0001f), sample.Item1);
                var expected = sample.Item1 == "andromeda" ? TableFx.Hex("#7F9CFF") : scene.Skins.Theme.A2;
                var actual = halo.sharedMaterial.GetColor("_BaseColor");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.0001f), sample.Item1 + " red");
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.0001f), sample.Item1 + " green");
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.0001f), sample.Item1 + " blue");
            }
            var ownedMaterial = halo.sharedMaterial;
            UnityEngine.Object.Destroy(sceneOwner); sceneOwner = null;
            yield return null;
            Assert.That(ownedMaterial == null, Is.True, "The halo material must be released with the table");
        }

        [UnityTest]
        public IEnumerator HologramUsesV4PulseWhileThePileIsCentered()
        {
            var scene = BuildScene();
            var fx = sceneOwner.AddComponent<TableFx>(); fx.Init(scene);
            yield return null;
            var holo = scene.GetComponentsInChildren<MeshRenderer>().First(r => r.name.StartsWith("holo_halka", StringComparison.Ordinal));
            float expected = ThreeBlend.Additive(.34f + Mathf.Sin(Time.time * .9f) * .05f);
            Assert.That(Alpha(holo), Is.EqualTo(expected).Within(.0001f), "table3d.js frame uses the v4 .34 / .9 / .05 pulse");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DealingMarkerFollowsIndicatorExtraAndClearsBeforeCenter()
        {
            Time.timeScale = 4;
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Clock = () => Time.timeAsDouble * 1000, SkipCountdown = true,
            };
            gameOwner = new GameObject("Marker ceremony game");
            var game = gameOwner.AddComponent<TableGame>();
            var scene = BuildScene();
            statePath = Path.Combine(Path.GetTempPath(), "g101-marker-v4-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Marker ceremony HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            yield return Until(() => game.Ceremony != null && game.Ceremony.Running, "ceremony start");
            var marker = Renderer(scene, "balya_isaret");
            Assert.That(marker, Is.Not.Null, "The deal must have a stack marker");
            Assert.That(marker.bounds.size.x, Is.EqualTo(.11f).Within(.0001f));
            Assert.That(marker.bounds.size.z, Is.EqualTo(.16f).Within(.0001f));
            Assert.That(Alpha(marker), Is.Zero.Within(.00001f), "No stack is selected during build");
            yield return Until(() => game.Ceremony.Step == "indicator", "indicator marker");
            CheckStack(scene, game, marker, game.Model.Deal.IndicatorStack);
            Assert.That(Alpha(marker), Is.GreaterThan(0));
            yield return Until(() => game.Ceremony.Step == "extra", "extra marker");
            CheckStack(scene, game, marker, game.Model.Deal.ExtraStack);
            yield return Until(() => game.Ceremony.Step == "centre", "center marker cleared");
            yield return Until(() => Alpha(marker) < .0001f, "marker fade after center");
            Assert.That(marker.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(marker.receiveShadows, Is.False);
        }

        private static void CheckStack(TableScene scene, TableGame game, MeshRenderer marker, int stack)
        {
            var expected = scene.SeatPose(game.LocalSeat(game.Model.Deal.Dealer), (stack - 7) * .057f, TY + .0016f, .36f, false).position;
            Assert.That(Vector3.Distance(marker.transform.position, expected), Is.LessThan(.0001f));
        }

        private static IEnumerator Until(Func<bool> condition, string step)
        {
            float until = Time.realtimeSinceStartup + 30;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for " + step);
        }
    }
}
