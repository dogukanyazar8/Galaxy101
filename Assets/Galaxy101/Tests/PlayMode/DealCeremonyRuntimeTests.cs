using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class DealCeremonyRuntimeTests
    {
        private GameObject owner;
        private TableCeremony ceremony;
        private CancellationTokenSource life;
        private Task run;
        private readonly TaskCompletionSource<bool> blockedWait = new TaskCompletionSource<bool>();
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            life?.Cancel();
            float deadline = Time.realtimeSinceStartup + 2;
            while (run != null && !run.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            ceremony?.Clear(); life?.Dispose();
            if (owner != null) Object.Destroy(owner);
            yield return null;
        }

        private TableScene Build()
        {
            owner = new GameObject("Source deal motion fixture"); owner.SetActive(false);
            var scene = owner.AddComponent<TableScene>();
            typeof(TableScene).GetField("demoLayout", Private).SetValue(scene, false);
            owner.SetActive(true); life = new CancellationTokenSource(); return scene;
        }

        [UnityTest]
        public IEnumerator BuildCreatesFifteenStacksBottomToTopWithTheSourceLeadAndStagger()
        {
            var scene = Build();
            var model = new TableClient(0);
            model.Apply(V4Messages.DealStart, new V4DealStart { HandNo = 1, Dealer = 3, BuildMs = 2572, At = 1000 });
            float began = Time.time;
            ceremony = new TableCeremony(scene, null, _ => blockedWait.Task);
            run = ceremony.Run(model, k => k, () => AssertFailState(), life.Token);
            var views = scene.TilesRoot.GetComponentsInChildren<TileView>(true);
            Assert.That(views.Length, Is.EqualTo(106));
            var first = views.Single(t => t.MotionKey == "t1");
            var lastInFirstStack = views.Single(t => t.MotionKey == "t7");
            var nextStack = views.Single(t => t.MotionKey == "t8");
            Assert.That(Start(first) - began, Is.EqualTo(.4f).Within(.00001f));
            Assert.That(Start(lastInFirstStack) - began, Is.EqualTo(.472f).Within(.00001f));
            Assert.That(Start(nextStack) - began, Is.EqualTo(.484f).Within(.00001f));
            Assert.That(lastInFirstStack.Target.position.y - first.Target.position.y, Is.EqualTo(.06f).Within(.00001f));
            Assert.That(Vector3.Distance(first.transform.position, nextStack.transform.position), Is.LessThan(.000001f), "Fresh source tiles share deckTop; individual heap cells are separate renderers.");
            Assert.That(Quaternion.Angle(first.transform.rotation, scene.SeatPose(0, 0, 0, 0, false).rotation), Is.LessThan(.01f), "Fresh Q_DOWN is independent of the top heap tile's yaw jitter.");
            Assert.That(scene.TilesRoot.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("toren_deste_")), Is.EqualTo(106));
            life.Cancel();
            yield return null;
            Assert.That(run.IsCanceled, Is.True, "Scene cancellation must interrupt a wait without waiting for the old build timer.");
            Assert.That(ceremony.Running, Is.False);
        }

        [UnityTest]
        public IEnumerator ConfirmedSkipWaitsForThePrivateHandThenFillsTopFirstWithoutRevealingOpponents()
        {
            var scene = Build();
            var model = new TableClient(0);
            model.Apply(V4Messages.DealStart, new V4DealStart { HandNo = 1, Dealer = 3, BuildMs = 2572, At = 1000 });
            float fillStarted = -1, finishWait = -1;
            ceremony = new TableCeremony(scene, null, seconds =>
            {
                if (ceremony.Step == "fill") { fillStarted = Time.time; finishWait = seconds; }
                return blockedWait.Task;
            });
            int fills = 0;
            run = ceremony.Run(model, k => k, () =>
            {
                fills++;
                var rack = new RackGrid(); rack.Deal(Enumerable.Range(0, 22).ToArray());
                return new BoardState { Rack = rack, HandCounts = new[] { 22, 21, 21, 21 }, Stock = 20 };
            }, life.Token);
            model.Apply(V4Messages.DealSkip, new V4DealSkip
            {
                Dice = new List<int> { 4, 3 }, Stack = 3, Level = 2, ExtraStack = 4, Indicator = "Y4a",
                Okey = new V4OkeyFace { C = "Y", N = 5 }, At = 1100,
            });
            yield return null;
            Assert.That(fills, Is.Zero, "The public skip event must never bind a stale or invented private hand.");
            Assert.That(run.IsCompleted, Is.False);
            model.Apply(V4Messages.HandDealt, new V4HandDealt
            {
                HandNo = 1, Rack = Enumerable.Range(0, 22).Select(TileCodes.Code).ToList(), Counts = new List<int> { 22, 21, 21, 21 },
                Deck = 20, Indicator = "Y4a", Okey = new V4OkeyFace { C = "Y", N = 5 }, At = 1100,
            });
            float deadline = Time.realtimeSinceStartup + 2;
            while (fillStarted < 0 && !run.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(run.IsFaulted, Is.False, run.Exception?.ToString());
            Assert.That(fills, Is.EqualTo(1));
            Assert.That(ceremony.Step, Is.EqualTo("fill"));
            Assert.That(finishWait, Is.EqualTo(.9908f).Within(.00001f), "Source FQ=.3 affects lead/stagger and the 650 ms finish padding.");
            var views = scene.TilesRoot.GetComponentsInChildren<TileView>(true);
            var extra = views.Single(t => t.MotionKey == "t106");
            var second = views.Single(t => t.MotionKey == "t35");
            Assert.That(Start(extra) - fillStarted, Is.EqualTo(.024f).Within(.00001f));
            Assert.That(Start(second) - fillStarted, Is.EqualTo(.0384f).Within(.00001f));
            Assert.That(Vector3.Distance(extra.Target.position, scene.SlotPose(0, 0, 2).position), Is.LessThan(.000001f), "The extra tile is on top of the first dealt stack and flies to the first local slot.");
            Assert.That(Vector3.Distance(second.Target.position, scene.SlotPose(0, 0, 3).position), Is.LessThan(.000001f));
            Assert.That(views.Count(t => t.transform.Find("tas_yuz") != null && t.transform.Find("tas_yuz").gameObject.activeSelf), Is.EqualTo(23), "Only the starter's 22 local tiles and public indicator may have a face.");
            Assert.That(extra.Duration, Is.GreaterThan(.56f), "Fast skip does not multiply the source flight duration by .3.");
        }

        private static float Start(TileView view) => (float)typeof(TileView).GetField("t0", Private).GetValue(view);
        private static BoardState AssertFailState() { Assert.Fail("A blocked build must not fill a rack."); return null; }
    }
}
