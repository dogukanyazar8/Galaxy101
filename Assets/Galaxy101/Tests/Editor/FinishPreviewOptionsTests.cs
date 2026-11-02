using System;
using Galaxy101.Diagnostics;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    public sealed class FinishPreviewOptionsTests
    {
        [TestCase("nuke", 0)]
        [TestCase("blackhole", 1300)]
        [TestCase("supernova", 700)]
        [TestCase("meteor", 8000)]
        public void RendererProofUsesOnlyKnownStylesAndBoundedSamples(string style, int sample)
        {
            var options = G101StateRouter.Parse(new[] { "-g101State=G14", "-g101FinishPreview=" + style, "-g101FinishSample=" + sample });
            Assert.That(options.FinishPreview, Is.EqualTo(style)); Assert.That(options.FinishSampleMs, Is.EqualTo(sample));
        }

        [TestCase("-g101FinishPreview=unknown")]
        [TestCase("-g101FinishSample=700")]
        [TestCase("-g101FinishPreview=nuke", "-g101FinishSample=-1")]
        [TestCase("-g101FinishPreview=nuke", "-g101FinishSample=8001")]
        [TestCase("-g101FinishPreview=nuke", "-g101FinishSample=NaN")]
        public void InvalidProofArgumentsFailBeforeTheGameStarts(params string[] arguments)
        {
            var all = new string[arguments.Length + 1]; all[0] = "-g101State=G14"; Array.Copy(arguments, 0, all, 1, arguments.Length);
            Assert.Throws<ArgumentException>(() => G101StateRouter.Parse(all));
        }

        [Test]
        public void NormalStateCaptureKeepsThePreviewDisabled()
        {
            Assert.That(G101StateRouter.Parse(new[] { "-g101State=G14" }).FinishPreview, Is.Null);
        }
    }
}
