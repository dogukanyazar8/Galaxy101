using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Galaxy101.Contracts;
using Galaxy101.Diagnostics;
using Galaxy101.Table;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class DealCameraRuntimeTests
    {
        private GameObject sceneOwner, gameOwner, routerOwner;
        private TableScene scene;
        private TableGame game;
        private float previousScale;
        private bool previousDefaults;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp() { previousScale = Time.timeScale; previousDefaults = TableSettings.DesignDefaultsOnly; Time.timeScale = 4; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameOwner != null) Object.Destroy(gameOwner);
            if (routerOwner != null) Object.Destroy(routerOwner);
            yield return null;
            if (sceneOwner != null) Object.Destroy(sceneOwner);
            TableGame.Pending = null; TableSettings.DesignDefaultsOnly = previousDefaults; Time.timeScale = previousScale;
            yield return null;
        }

        private IEnumerator StartDeal(bool start = true)
        {
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup
                {
                    Seed = 42, Options = new MatchOptions { Mode = "solo", Hands = 1 },
                    FirstHandRig = G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a" }),
                },
                Table = TableGame.DefaultTable(), Manual = true, Clock = () => Time.timeAsDouble * 1000,
            };
            sceneOwner = new GameObject("Deal camera scene"); scene = sceneOwner.AddComponent<TableScene>();
            gameOwner = new GameObject("Deal camera game"); game = gameOwner.AddComponent<TableGame>();
            float deadline = Time.realtimeSinceStartup + 5;
            while ((game.Model == null || game.Ceremony == null) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Model, Is.Not.Null); Assert.That(game.Ceremony, Is.Not.Null);
            if (!start) yield break;
            game.RunFlow("deal");
            while (!game.Ceremony.Running && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(scene.CameraRig.Current, Is.EqualTo(TableCameraRig.View.Deal));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator NormalDealStartsThePlayerCameraWhileTheTilesAreStillFilling()
        {
            yield return StartDeal();
            float deadline = Time.realtimeSinceStartup + 15;
            while (game.Ceremony.Step != "fill" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Ceremony.Step, Is.EqualTo("fill"));
            AssertFillCamera();
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator SkipCameraWaitsForThePrivateHandThenStartsDuringFastFill()
        {
            yield return StartDeal();
            game.Model.Apply(V4Messages.DealSkip, new V4DealSkip
            {
                Dice = new List<int> { 4, 3 }, Stack = 3, Level = 2, ExtraStack = 4, Indicator = "Y4a",
                Okey = new V4OkeyFace { C = "Y", N = 5 }, At = 1100,
            });
            yield return null;
            Assert.That(game.Model.Rack, Is.Null);
            Assert.That(game.Ceremony.Step, Is.Not.EqualTo("fill"));
            Assert.That(scene.CameraRig.Current, Is.EqualTo(TableCameraRig.View.Deal), "Public skip confirmation must not bypass the authoritative private-hand gate.");
            game.Model.Apply(V4Messages.HandDealt, new V4HandDealt
            {
                HandNo = 1, Rack = Enumerable.Range(0, 22).Select(TileCodes.Code).ToList(), Counts = new List<int> { 22, 21, 21, 21 },
                Deck = 20, Indicator = "Y4a", Okey = new V4OkeyFace { C = "Y", N = 5 }, At = 1100,
            });
            float deadline = Time.realtimeSinceStartup + 2;
            while (game.Ceremony.Step != "fill" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.Ceremony.Step, Is.EqualTo("fill"));
            AssertFillCamera();
        }

        [UnityTest]
        public IEnumerator BuildCaptureHoldIncludesThePrototypeQuarterSecondBeforeDealStart()
        {
            yield return StartDeal(false);
            routerOwner = new GameObject("Deal capture anchor fixture");
            var router = routerOwner.AddComponent<G101StateRouter>(); router.enabled = false;
            float calledAt = Time.realtimeSinceStartup;
            yield return (IEnumerator)typeof(G101StateRouter).GetMethod("Go", Private).Invoke(router, new object[] { "deal" });
            var origin = (float?)typeof(G101StateRouter).GetField("holdOrigin", Private).GetValue(router);
            Assert.That(origin.HasValue, Is.True, "G02/G03/G04 hold begins when Masa.go('deal') is called, before its 250 ms startDeal timer.");
            Assert.That(origin.Value, Is.InRange(calledAt, calledAt + .05f));
            Assert.That(Time.realtimeSinceStartup - origin.Value, Is.GreaterThanOrEqualTo(.25f));
            Assert.That(game.Ceremony.Running, Is.True);
        }

        [Test]
        public void AFollowingPhaseWaitReplacesTheEarlierBuildCaptureAnchor()
        {
            routerOwner = new GameObject("Phase capture anchor fixture");
            var router = routerOwner.AddComponent<G101StateRouter>(); router.enabled = false;
            typeof(G101StateRouter).GetField("catalogue", Private).SetValue(router, JArray.Parse("[{\"id\":\"probe\",\"path\":[{\"wait\":0}]}]"));
            typeof(G101StateRouter).GetField("holdOrigin", Private).SetValue(router, (float?)10);
            var steps = (IEnumerator)typeof(G101StateRouter).GetMethod("TableState", Private).Invoke(router, new object[] { "probe", "saturn", "tr" });
            Assert.That(steps.MoveNext(), Is.True);
            Assert.That(typeof(G101StateRouter).GetField("holdOrigin", Private).GetValue(router), Is.Null,
                "A following waitDeal/wait/masa path step must measure Ft from its own completion, not from the initial table call.");
        }

        private void AssertFillCamera()
        {
            Assert.That(game.Ceremony.Running, Is.True, "The regression occurs during fill, before the board takes over.");
            Assert.That(scene.CameraRig.Current, Is.EqualTo(TableCameraRig.View.Player), "G101-Masa dealFill calls setCamera('player', 1.4) before scheduling dealFinish.");
            Assert.That((float)typeof(TableCameraRig).GetField("duration", Private).GetValue(scene.CameraRig), Is.EqualTo(1.4f));
        }
    }
}
