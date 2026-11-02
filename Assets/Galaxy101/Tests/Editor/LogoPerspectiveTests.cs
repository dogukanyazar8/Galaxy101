using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class LogoPerspectiveTests
    {
        private static Type Motion => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.LogoTileMotion")).FirstOrDefault(t => t != null);
        private static Vector3 Project(int tile, float time, Vector2 point)
        {
            Assert.That(Motion, Is.Not.Null, "The source logo must have a native perspective transform.");
            return (Vector3)Motion.GetMethod("Project", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { tile, time, point });
        }

        [Test]
        public void EntryTranslationRotatesWithTheIndividualZRotation()
        {
            // CSS ordering: individual translate, individual rotate, then translateY(46) rotateX(-70) scale(.82).
            var center = Project(0, 0, new Vector2(39, 54));
            Assert.That(center.x, Is.EqualTo(545 + 46 * Mathf.Sin(10 * Mathf.Deg2Rad)).Within(.001f));
            Assert.That(center.y, Is.EqualTo(245.6f + 46 * Mathf.Cos(10 * Mathf.Deg2Rad)).Within(.001f));
        }

        [Test]
        public void PerspectiveUsesTheSharedContainerOriginAndUnequalEdgeWidths()
        {
            var topLeft = Project(1, 0, Vector2.zero); var topRight = Project(1, 0, new Vector2(88, 0));
            var bottomLeft = Project(1, 0, new Vector2(0, 122)); var bottomRight = Project(1, 0, new Vector2(88, 122));
            Assert.That((topRight - topLeft).magnitude, Is.GreaterThan((bottomRight - bottomLeft).magnitude), "Top tips toward the viewer during rotateX(-70deg).");
            Assert.That(topLeft.z, Is.GreaterThan(0)); Assert.That(bottomLeft.z, Is.LessThan(0));
        }

        [Test]
        public void CompletedEntryHasNoPerspectiveDistortion()
        {
            var a = Project(0, 4, Vector2.zero); var b = Project(0, 4, new Vector2(78, 0));
            Assert.That((b - a).magnitude, Is.EqualTo(78).Within(.001f));
            Assert.That(a.z, Is.Zero.Within(.001f)); Assert.That(b.z, Is.Zero.Within(.001f));
        }

        [Test]
        public void LateTileHoldsItsStartingPoseUntilItsOwnDelay()
        {
            var a = Project(2, .1f, Vector2.zero); var b = Project(2, .2f, Vector2.zero);
            // Float keeps moving, but the entry's depth and projected width remain at the held starting transform.
            Assert.That(a.z, Is.EqualTo(b.z).Within(.001f));
            var rightA = Project(2, .1f, new Vector2(78, 0)); var rightB = Project(2, .2f, new Vector2(78, 0));
            Assert.That((rightA - a).magnitude, Is.EqualTo((rightB - b).magnitude).Within(.001f));
        }
    }
}
