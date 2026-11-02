using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class SplashPerspectiveTests
    {
        private static Type Motion => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.SplashTileMotion")).FirstOrDefault(t => t != null);
        private static Vector3 Project(int tile, float time, Vector2 point)
        {
            Assert.That(Motion, Is.Not.Null, "The splash must reuse the native perspective renderer with its own source transform order.");
            return (Vector3)Motion.GetMethod("Project").Invoke(null, new object[] { tile, time, point });
        }

        [Test]
        public void SplashEntryRotatesTheInnerFaceInsideTheBottomOriginWrapper()
        {
            // Independent CSS matrix fixtures: child translate/rotate about center; wrapper scale/rotateX/translate about bottom.
            var a = Project(0, 0, Vector2.zero);
            Assert.That(Vector3.Distance(a, new Vector3(495.33022f, 418.89762f, 77.12790f)), Is.LessThan(.002f));
            var b = Project(0, 0, new Vector2(78, 108));
            Assert.That(Vector3.Distance(b, new Vector3(581.89538f, 419.83025f, 5.832302f)), Is.LessThan(.002f));
        }

        [Test]
        public void LateSplashTileKeepsItsEntryDepthUntilItsOwnHalfSecondDelay()
        {
            var a = Project(2, .1f, Vector2.zero); var b = Project(2, .2f, Vector2.zero);
            // Its separate floating child still moves during the held entry, but the 80-degree wrapper stays tilted.
            Assert.That(a.z, Is.GreaterThan(70)); Assert.That(b.z, Is.GreaterThan(70));
            var bottom = Project(2, .1f, new Vector2(78, 108));
            Assert.That(a.z - bottom.z, Is.EqualTo(.78f * Mathf.Cos(10 * Mathf.Deg2Rad) * 108 * Mathf.Sin(80 * Mathf.Deg2Rad) + .78f * Mathf.Sin(10 * Mathf.Deg2Rad) * 78 * Mathf.Sin(80 * Mathf.Deg2Rad)).Within(.002f));
        }

        [Test]
        public void CanonicalSplashHasNoPerspectiveDepthAndKeepsTheSourceFloatPhase()
        {
            var center = Project(1, 100, new Vector2(44, 61));
            Assert.That(center.x, Is.EqualTo(640).Within(.001f)); Assert.That(center.y, Is.EqualTo(278.9f).Within(.001f));
            Assert.That(center.z, Is.Zero.Within(.001f));
        }
    }
}
