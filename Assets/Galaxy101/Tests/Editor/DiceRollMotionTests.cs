using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class DiceRollMotionTests
    {
        private static readonly Vector3 Start = new Vector3(0, 1.18f, .6f), Land = new Vector3(.14f, .775f, -.02f);
        private static readonly Pose Rest = new Pose(new Vector3(.2f, .775f, -.02f), Quaternion.Euler(17, 29, 41));
        private static Type Motion => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.Table.DiceRollMotion")).FirstOrDefault(t => t != null);

        private static object Create(float a = 0, float b = 0, float wobble = 0, bool returning = false, Quaternion? final = null)
        {
            Assert.That(Motion, Is.Not.Null, "The native die must sample the table3d.js segmented trajectory.");
            return Activator.CreateInstance(Motion, new object[] { Start, Land, final ?? Quaternion.identity, Vector3.right, Vector3.forward, a, b, wobble, Rest, returning });
        }
        private static Pose Sample(object motion, float elapsed) => (Pose)Motion.GetMethod("Sample", BindingFlags.Public | BindingFlags.Instance).Invoke(motion, new object[] { elapsed });
        private static int Impacts(object motion, float elapsed) => (int)Motion.GetMethod("ImpactsAt", BindingFlags.Public | BindingFlags.Instance).Invoke(motion, new object[] { elapsed });

        [TestCase(.16f, .32f, .354375f)]
        [TestCase(.415f, .73f, .05f)]
        [TestCase(.575f, .865f, .019f)]
        [TestCase(.685f, .9325f, .007f)]
        public void EachFlightUsesItsSourceTravelAndHeight(float normalized, float fraction, float height)
        {
            var pose = Sample(Create(), normalized * 1.75f);
            var expected = Vector3.Lerp(Start, Land, fraction); expected.y = Land.y + height;
            Assert.That(Vector3.Distance(pose.position, expected), Is.LessThan(.00001f));
        }

        [Test]
        public void FeltRollUsesItsSeparateTravelCurve()
        {
            var pose = Sample(Create(), .8425f * 1.75f);
            float f = .955f + .045f * (1 - Mathf.Pow(.5f, 2.4f));
            var expected = Vector3.Lerp(Start, Land, f); expected.y = Land.y;
            Assert.That(Vector3.Distance(pose.position, expected), Is.LessThan(.00001f));
        }

        [Test]
        public void ReturnToHandLasts450MillisecondsAndUsesSmootherstep()
        {
            var motion = Create(returning: true);
            var initial = Sample(motion, 0);
            Assert.That(Vector3.Distance(initial.position, Rest.position), Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(initial.rotation, Rest.rotation), Is.LessThan(.02f));
            float k = .103515625f; // smootherstep(.25), not cubic easing.
            var expected = Vector3.Lerp(Rest.position, Start, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * .06f);
            var quarter = Sample(motion, .1125f);
            Assert.That(Vector3.Distance(quarter.position, expected), Is.LessThan(.00001f));
            var half = Sample(motion, .225f);
            Assert.That(half.position.y, Is.EqualTo((Rest.position.y + Start.y) / 2 + .06f).Within(.00001f));
            Assert.That(Vector3.Distance(Sample(motion, .45f).position, Start), Is.LessThan(.00001f));
        }

        [Test]
        public void SpinUsesBothIndependentAxesInPremultiplyOrder()
        {
            var final = Quaternion.Euler(30, 80, 10);
            var pose = Sample(Create(13, 3, final: final), .4f * 1.75f);
            float r = 13 * Mathf.Pow(1 - .4f / .955f, 2.6f), r2 = 3 * Mathf.Pow(1 - .4f / .63f, 2);
            var expected = Quaternion.AngleAxis(r * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(r2 * Mathf.Rad2Deg, Vector3.forward) * final;
            Assert.That(Quaternion.Angle(pose.rotation, expected), Is.LessThan(.03f));
        }

        [Test]
        public void RotatingEdgeRaisesTheCenterAboveTheFelt()
        {
            float u = .415f, r = 13 * Mathf.Pow(1 - u / .955f, 2.6f);
            float lift = .015f * .82f * (Mathf.Abs(Mathf.Cos(r)) + Mathf.Abs(Mathf.Sin(r)) - 1);
            Assert.That(Sample(Create(13), u * 1.75f).position.y, Is.EqualTo(Land.y + .05f + lift).Within(.00001f));
        }

        [Test]
        public void FinalWobbleDecaysAndTheAnnouncedFaceSettlesExactly()
        {
            var final = Quaternion.AngleAxis(123, Vector3.up) * Quaternion.FromToRotation(Vector3.left, Vector3.up);
            var motion = Create(13, 3, .27f, final: final);
            float w = (.98f - .955f) / (1 - .955f);
            float r = .27f * Mathf.Exp(-4.5f * w) * Mathf.Cos(w * 9.5f) * (1 - w);
            var expected = Quaternion.AngleAxis(r * Mathf.Rad2Deg, Vector3.right) * final;
            Assert.That(Quaternion.Angle(Sample(motion, .98f * 1.75f).rotation, expected), Is.LessThan(.03f));
            foreach (float elapsed in new[] { 1.75f, 2.4f })
            {
                var pose = Sample(motion, elapsed);
                Assert.That(pose.position, Is.EqualTo(Land));
                Assert.That(Quaternion.Angle(pose.rotation, final), Is.LessThan(.02f));
                Assert.That(Vector3.Dot(pose.rotation * Vector3.left, Vector3.up), Is.GreaterThan(.9999f));
            }
        }

        [Test]
        public void ImpactThresholdsExcludeTheReturnAndCatchUpAcrossSkippedFrames()
        {
            var motion = Create(returning: true);
            Assert.That(Impacts(motion, .45f), Is.Zero);
            Assert.That(Impacts(motion, .45f + .2999f * 1.75f), Is.Zero);
            Assert.That(Impacts(motion, .45f + .3001f * 1.75f), Is.EqualTo(1));
            Assert.That(Impacts(motion, .45f + .5001f * 1.75f), Is.EqualTo(2));
            Assert.That(Impacts(motion, .45f + .6301f * 1.75f), Is.EqualTo(3));
            Assert.That(Impacts(motion, 2.2f), Is.EqualTo(3));
            var end = Sample(motion, 2.2f);
            Assert.That(end.position, Is.EqualTo(Land));
        }
    }
}
