using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Vector3 = System.Numerics.Vector3;

namespace Galaxy101.Tests
{
    public sealed class ShipFlightTests
    {
        private static object Create(int seed = 42)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.ShipFlightSimulation")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Native ship flight simulation is missing");
            return Activator.CreateInstance(type, seed);
        }
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
        private static double Time(object target) => (double)target.GetType().GetProperty("Seconds").GetValue(target);

        [Test]
        public void ReducedMotionHasAnInitialVisibleShipWithoutAdvancingTime()
        {
            var s = Create();
            Assert.That(Field<bool>(Call(s, "GetShip", 0), "Visible"), Is.True);
            Assert.That(Time(s), Is.Zero);
        }

        [Test]
        public void FramePartitionDoesNotChangeFlightOrRandomSequence()
        {
            var a = Create(); var b = Create();
            for (int i = 0; i < 600; i++) Call(a, "Advance", 1.0 / 60, true);
            for (int i = 0; i < 200; i++) Call(b, "Advance", .05, true);
            Assert.That(Time(a), Is.EqualTo(Time(b)).Within(1e-9));
            for (int i = 0; i < 5; i++)
            {
                var pa = Call(a, "GetShip", i); var pb = Call(b, "GetShip", i);
                Assert.That(Field<Vector3>(pa, "Position"), Is.EqualTo(Field<Vector3>(pb, "Position")));
                Assert.That(Field<bool>(pa, "Visible"), Is.EqualTo(Field<bool>(pb, "Visible")));
            }
        }

        [Test]
        public void PausedIntervalsDoNotAdvanceTimeOrJumpOnResume()
        {
            var a = Create(); var b = Create();
            Call(a, "Advance", 1.0, true); Call(a, "Advance", 90.0, false); Call(a, "Advance", 1.0, true);
            Call(b, "Advance", 2.0, true);
            Assert.That(Time(a), Is.EqualTo(2).Within(1e-9));
            Assert.That(Field<Vector3>(Call(a, "GetShip", 0), "Position"), Is.EqualTo(Field<Vector3>(Call(b, "GetShip", 0), "Position")));
        }

        [Test]
        public void ThemeChangePreservesActiveCruiserPoseAndRouteProgress()
        {
            var changed = Create(); var control = Create();
            Call(changed, "Advance", 1.0, true); Call(control, "Advance", 1.0, true);
            var before = Call(changed, "GetShip", 0);
            Assert.That(Field<bool>(before, "Visible"), Is.True);
            Call(changed, "Configure", "Mars", false);
            Assert.That(Time(changed), Is.EqualTo(1).Within(1e-9));
            Assert.That(Field<Vector3>(Call(changed, "GetShip", 0), "Position"), Is.EqualTo(Field<Vector3>(before, "Position")));
            for (int frame = 0; frame < 30; frame++)
            {
                Call(changed, "Advance", 1.0 / 60, true); Call(control, "Advance", 1.0 / 60, true);
                var actual = Call(changed, "GetShip", 0); var expected = Call(control, "GetShip", 0);
                Assert.That(Field<bool>(actual, "Visible"), Is.EqualTo(Field<bool>(expected, "Visible")));
                Assert.That(Field<Vector3>(actual, "Position"), Is.EqualTo(Field<Vector3>(expected, "Position")));
                Assert.That(Field<Vector3>(actual, "Velocity"), Is.EqualTo(Field<Vector3>(expected, "Velocity")));
            }
        }

        [Test]
        public void ThemeChangeReplansWaitingCruiserWithoutRestartingItsLaunchDelay()
        {
            var changed = Create(); var control = Create();
            Call(changed, "Advance", 1.0, true); Call(control, "Advance", 1.0, true);
            Assert.That(Field<bool>(Call(changed, "GetShip", 1), "Visible"), Is.False);
            Call(changed, "Configure", "Mars", false);
            bool launched = false;
            for (int frame = 0; frame < 480; frame++)
            {
                Call(changed, "Advance", 1.0 / 60, true); Call(control, "Advance", 1.0 / 60, true);
                var expected = Call(control, "GetShip", 1);
                if (!Field<bool>(expected, "Visible")) continue;
                var actual = Call(changed, "GetShip", 1);
                Assert.That(Field<bool>(actual, "Visible"), Is.True, "Theme change restarted a waiting ship's countdown");
                Assert.That(Field<Vector3>(actual, "Position"), Is.Not.EqualTo(Field<Vector3>(expected, "Position")), "Waiting ship kept the previous planet's planned route");
                launched = true; break;
            }
            Assert.That(launched, Is.True);
        }

        [Test]
        public void ThemeChangePreservesActiveChaseLasersAndHitEffects()
        {
            var changed = Create(); var control = Create(); bool found = false;
            for (int frame = 0; frame < 7200; frame++)
            {
                Call(changed, "Advance", 1.0 / 60, true); Call(control, "Advance", 1.0 / 60, true);
                int bolts = (int)changed.GetType().GetProperty("BoltCount").GetValue(changed);
                int hits = (int)changed.GetType().GetProperty("HitCount").GetValue(changed);
                if (bolts == 0 || hits == 0 || !Field<bool>(Call(changed, "GetShip", 3), "Visible")) continue;
                found = true;
                var laser = Call(changed, "GetBolt", 0); var hit = Call(changed, "GetHit", 0); var fighter = Call(changed, "GetShip", 3);
                Call(changed, "Configure", "Mars", false);
                Assert.That((int)changed.GetType().GetProperty("BoltCount").GetValue(changed), Is.EqualTo(bolts));
                Assert.That((int)changed.GetType().GetProperty("HitCount").GetValue(changed), Is.EqualTo(hits));
                var retainedLaser = Call(changed, "GetBolt", 0); var retainedHit = Call(changed, "GetHit", 0);
                Assert.That(Field<Vector3>(retainedLaser, "Position"), Is.EqualTo(Field<Vector3>(laser, "Position")));
                Assert.That(Field<float>(retainedLaser, "Age"), Is.EqualTo(Field<float>(laser, "Age")));
                Assert.That(Field<Vector3>(retainedHit, "Position"), Is.EqualTo(Field<Vector3>(hit, "Position")));
                Assert.That(Field<float>(retainedHit, "Age"), Is.EqualTo(Field<float>(hit, "Age")));
                Assert.That(Field<Vector3>(Call(changed, "GetShip", 3), "Position"), Is.EqualTo(Field<Vector3>(fighter, "Position")));
                for (int next = 0; next < 10; next++)
                {
                    Call(changed, "Advance", 1.0 / 60, true); Call(control, "Advance", 1.0 / 60, true);
                    for (int i = 3; i < 5; i++)
                    {
                        var actual = Call(changed, "GetShip", i); var expected = Call(control, "GetShip", i);
                        Assert.That(Field<bool>(actual, "Visible"), Is.EqualTo(Field<bool>(expected, "Visible")));
                        Assert.That(Field<Vector3>(actual, "Position"), Is.EqualTo(Field<Vector3>(expected, "Position")));
                    }
                }
                break;
            }
            Assert.That(found, Is.True, "The seeded run did not reach a live chase with both laser and hit effects");
        }

        [TestCase("Satürn")]
        [TestCase("Mars")]
        [TestCase("Neptün")]
        [TestCase("Jüpiter")]
        [TestCase("Venüs")]
        public void MenuFlightsStayOutsidePlanetAndSaturnRings(string planet)
        {
            var s = Create(71); Call(s, "Configure", planet, false);
            int visible = 0;
            for (int frame = 0; frame < 3600; frame++)
            {
                Call(s, "Advance", 1.0 / 60, true);
                for (int i = 0; i < 3; i++)
                {
                    var pose = Call(s, "GetShip", i); if (!Field<bool>(pose, "Visible")) continue;
                    var p = Field<Vector3>(pose, "Position");
                    var xy = (System.Numerics.Vector2)Call(s, "Project", p);
                    if (xy.X < 0 || xy.X > 1280 || xy.Y < 0 || xy.Y > 720) continue;
                    visible++;
                    Assert.That((bool)Call(s, "HitsPlanet", p, i == 0 ? 4.2f : i == 1 ? 3.6f : 4.1f), Is.False, planet + " ship " + i);
                }
            }
            Assert.That(visible, Is.GreaterThan(300), "Rejected paths must not leave the layer empty");
        }

        [Test]
        public void TableFlightsRemainPredominantlyInTheTopBand()
        {
            var s = Create(); Call(s, "Configure", "Satürn", true);
            int visible = 0, inBand = 0;
            for (int frame = 0; frame < 3600; frame++)
            {
                Call(s, "Advance", 1.0 / 60, true);
                for (int i = 0; i < 3; i++)
                {
                    var pose = Call(s, "GetShip", i); if (!Field<bool>(pose, "Visible")) continue;
                    var xy = (System.Numerics.Vector2)Call(s, "Project", Field<Vector3>(pose, "Position"));
                    if (xy.X < 0 || xy.X > 1280 || xy.Y < 0 || xy.Y > 720) continue;
                    visible++; if (xy.Y > 10 && xy.Y < 160) inBand++;
                }
            }
            Assert.That(visible, Is.GreaterThan(300));
            Assert.That((double)inBand / visible, Is.GreaterThan(.8));
        }

        [Test]
        public void FighterChaseProducesBoundedLaserAndHitEffects()
        {
            var s = Create(); int bolts = 0, hits = 0, fighterFrames = 0;
            for (int frame = 0; frame < 7200; frame++)
            {
                Call(s, "Advance", 1.0 / 60, true);
                bolts = Math.Max(bolts, (int)s.GetType().GetProperty("BoltCount").GetValue(s));
                hits = Math.Max(hits, (int)s.GetType().GetProperty("HitSerial").GetValue(s));
                if (Field<bool>(Call(s, "GetShip", 3), "Visible")) fighterFrames++;
            }
            Assert.That(fighterFrames, Is.GreaterThan(60));
            Assert.That(bolts, Is.InRange(1, 32)); Assert.That(hits, Is.GreaterThan(0));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-.1)]
        public void InvalidDeltaIsRejected(double delta)
        {
            var s = Create();
            var e = Assert.Throws<TargetInvocationException>(() => Call(s, "Advance", delta, true));
            Assert.That(e.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
