using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class FinishFlightMotionTests
    {
        private static readonly Pose Start = new Pose(new Vector3(-.4f, .9f, .8f), Quaternion.Euler(25, 40, 10));
        private static readonly Vector3 Target = new Vector3(0, .76f + .01f / 2 + .002f, .02f);
        private static Type Motion => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.Table.FinishFlightMotion")).FirstOrDefault(t => t != null);
        private static object Create(string style)
        {
            Assert.That(Motion, Is.Not.Null, "Missing the source 3D finish-flight sampler.");
            return Activator.CreateInstance(Motion, new object[] { Start, style });
        }
        private static object Sample(object motion, float time, Vector2 jitter = default) => Motion.GetMethod("Sample").Invoke(motion, new object[] { time, jitter });
        private static Pose Pose(object frame) => (Pose)frame.GetType().GetField("Pose").GetValue(frame);
        private static float Scale(object frame) => (float)frame.GetType().GetField("Scale").GetValue(frame);

        [TestCase("nuke", 1.75f)] [TestCase("blackhole", 1.75f)] [TestCase("supernova", 1.75f)] [TestCase("meteor", 1.8f)]
        public void EachStyleUsesItsSourceDuration(string style, float expected)
        {
            var motion = Create(style);
            Assert.That((float)Motion.GetProperty("Duration").GetValue(motion), Is.EqualTo(expected));
        }

        [Test]
        public void NonMeteorRisesTowardTheCenterBeforeCharging()
        {
            var motion = Create("nuke"); var frame = Sample(motion, .23f * 1.75f);
            var apex = new Vector3(Start.position.x * .6f, 1.34f, Start.position.z * .6f);
            Assert.That(Vector3.Distance(Pose(frame).position, Vector3.Lerp(Start.position, apex, .875f)), Is.LessThan(.00001f));
            Assert.That(Scale(frame), Is.EqualTo(1 + .875f * .15f).Within(.00001f));
            var charging = Sample(motion, .54f * 1.75f, new Vector2(.5f, -.5f));
            var expected = apex + new Vector3(-.001f, .014f, 0);
            Assert.That(Vector3.Distance(Pose(charging).position, expected), Is.LessThan(.00001f));
            Assert.That(Scale(charging), Is.EqualTo(1.275f).Within(.00001f));
        }

        [Test]
        public void NonMeteorFallUsesCubicAccelerationAndFivePiMirroredSpin()
        {
            var motion = Create("supernova"); float u = .81f;
            var frame = Sample(motion, u * 1.75f);
            var apex = new Vector3(Start.position.x * .6f, 1.34f, Start.position.z * .6f);
            Assert.That(Vector3.Distance(Pose(frame).position, Vector3.Lerp(apex, Target, .125f)), Is.LessThan(.00001f));
            Assert.That(Scale(frame), Is.EqualTo(1.35f).Within(.00001f));
            var final = new Quaternion(-Mathf.Sqrt(.5f), 0, 0, Mathf.Sqrt(.5f));
            var yaw = Quaternion.AngleAxis(-.3f * Mathf.Rad2Deg, Vector3.up); final = yaw * final;
            float k = 1 - Mathf.Pow(-2 * u + 2, 3) / 2;
            var expected = Quaternion.AngleAxis(-(1 - k) * 5 * Mathf.PI * Mathf.Rad2Deg, Vector3.up) * final;
            Assert.That(Quaternion.Angle(Pose(frame).rotation, expected), Is.LessThan(.03f));
        }

        [Test]
        public void MeteorLeavesTheRackDisappearsThenFallsFromItsOffset()
        {
            var motion = Create("meteor");
            var rise = Sample(motion, .16f * 1.8f);
            Assert.That(Vector3.Distance(Pose(rise).position, Start.position + Vector3.up * (1.7f * Mathf.Pow(.5f, 2.2f))), Is.LessThan(.00001f));
            var hold = Sample(motion, .4f * 1.8f);
            Assert.That(Pose(hold).position, Is.EqualTo(Start.position + Vector3.up * 1.7f));
            Assert.That(Scale(hold), Is.EqualTo(.001f));
            var fall = Sample(motion, .75f * 1.8f);
            Assert.That(Vector3.Distance(Pose(fall).position, Target + new Vector3(.75f, 1.5f, -.55f) * .75f), Is.LessThan(.00001f));
            Assert.That(Scale(fall), Is.EqualTo(1.2625f).Within(.00001f));
        }

        [TestCase("nuke")] [TestCase("blackhole")] [TestCase("supernova")] [TestCase("meteor")]
        public void EveryFinishSettlesAtTheFixedTableCenter(string style)
        {
            var motion = Create(style); var final = Sample(motion, 3);
            Assert.That(Pose(final).position, Is.EqualTo(Target));
            Assert.That(Scale(final), Is.EqualTo(1));
            Assert.That(Vector3.Dot(Pose(final).rotation * Vector3.forward, Vector3.up), Is.GreaterThan(.9999f));
        }
    }
}
