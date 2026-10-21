using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    public sealed class GalaxyCaptureTests
    {
        private static Type Driver => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.GalaxyCaptureDriver")).FirstOrDefault(t => t != null);

        [Test]
        public void CapturePlanCoversEveryReferenceScreenshotExactlyOnce()
        {
            Assert.That(Driver,Is.Not.Null);
            var actual = (string[])Driver.GetProperty("ReferenceFiles").GetValue(null);
            var expected = Directory.GetFiles(Path.Combine(UnityEngine.Application.dataPath,"../Design/galaxy101_unity_handoff/screenshots"),"*.png").Select(Path.GetFileName).ToArray();
            Assert.That(actual,Is.EquivalentTo(expected));
            Assert.That(actual.Distinct(StringComparer.Ordinal).Count(),Is.EqualTo(actual.Length));
        }

        [Test]
        public void CaptureIsDisabledWithoutExplicitCommandLineFlag()
        {
            Assert.That(Driver,Is.Not.Null);
            Assert.That(Driver.GetMethod("ResolveOutput").Invoke(null,new object[] {new[]{"game.exe","-batchmode"}}),Is.Null);
        }

        [TestCase("relative/captures")]
        [TestCase("")]
        public void CaptureRejectsAmbiguousOutputLocations(string output)
        {
            Assert.That(Driver,Is.Not.Null);
            var error = Assert.Throws<TargetInvocationException>(() => Driver.GetMethod("ResolveOutput").Invoke(null,new object[] {new[]{"game.exe","-galaxyCaptureOutput",output}}));
            Assert.That(error.InnerException,Is.TypeOf<ArgumentException>());
        }
    }
}
