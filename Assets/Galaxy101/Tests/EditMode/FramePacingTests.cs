using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>Kare hızı rules: offered choices per monitor, VSync / targetFrameRate policy, labels and persistence.</summary>
    public sealed class FramePacingTests
    {
        [Test] public void A60HzMonitorOffersAutoThirtyAndSixty()
        {
            Assert.That(FramePacing.Options(60, false), Is.EqualTo(new[] { FramePacing.Auto, 30, 60 }));
        }

        [Test] public void A144HzMonitorOffersTheSpecRatesBelowItThenItsMaximum()
        {
            Assert.That(FramePacing.Options(143.856, false), Is.EqualTo(new[] { FramePacing.Auto, 30, 60, 90, 120, 144 }));
        }

        [Test] public void AnOddMaximumEndsTheList()
        {
            Assert.That(FramePacing.Options(160, false), Is.EqualTo(new[] { FramePacing.Auto, 30, 60, 90, 120, 144, 160 }));
            Assert.That(FramePacing.Options(240, false).Last(), Is.EqualTo(240));
        }

        [Test] public void UnlimitedIsNoLongerOfferedButAnOldSaveStaysValid()
        {
            Assert.That(FramePacing.Options(240, false).Contains(FramePacing.Unlimited), Is.False);
            Assert.That(FramePacing.IsValidChoice(FramePacing.Unlimited), Is.True);
        }

        [Test] public void DescriptionsFollowTheReference()
        {
            Assert.That(FramePacing.Description(FramePacing.Auto, "tr", 160), Is.EqualTo("Ekranla eşitlenir (VSync) · Monitör 160 Hz"));
            Assert.That(FramePacing.Description(144, "tr", 160), Is.EqualTo("Sabit 144 FPS · Monitör 160 Hz"));
            Assert.That(FramePacing.Description(144, "en", 160), Is.EqualTo("Locked to 144 FPS · Monitor 160 Hz"));
        }

        [Test] public void DesktopAutoSyncsToTheMonitorAndCapsTurnVSyncOff()
        {
            Assert.That(FramePacing.Policy(FramePacing.Auto, false, 144), Is.EqualTo((1, -1)));
            Assert.That(FramePacing.Policy(120, false, 144), Is.EqualTo((0, 120)));
            Assert.That(FramePacing.Policy(FramePacing.Unlimited, false, 144), Is.EqualTo((0, -1)));
        }

        [Test] public void AnUnfocusedDesktopWindowDropsToThirtyFps()
        {
            Assert.That(FramePacing.Policy(FramePacing.Auto, false, 144, focused: false), Is.EqualTo((0, FramePacing.BackgroundCap)));
            Assert.That(FramePacing.Policy(FramePacing.Unlimited, false, 240, focused: false), Is.EqualTo((0, FramePacing.BackgroundCap)));
        }

        [Test] public void PhonesUseTargetFrameRateAndNeverAskMoreThanTheDisplayShows()
        {
            Assert.That(FramePacing.Policy(FramePacing.Auto, true, 120), Is.EqualTo((0, 120)));
            Assert.That(FramePacing.Policy(144, true, 90), Is.EqualTo((0, 90)));
            Assert.That(FramePacing.Policy(60, true, 120), Is.EqualTo((0, 60)));
        }

        [Test] public void LabelsNameTheMonitorRateForAuto()
        {
            Assert.That(FramePacing.Label(FramePacing.Auto, "tr", 143.9), Is.EqualTo("Otomatik · 144 Hz"));
            Assert.That(FramePacing.Label(FramePacing.Auto, "en", 60), Is.EqualTo("Auto · 60 Hz"));
            Assert.That(FramePacing.Label(165, "tr", 165), Is.EqualTo("165 FPS"));
            Assert.That(FramePacing.Label(FramePacing.Unlimited, "tr", 60), Is.EqualTo("Sınırsız"));
        }

        [TestCase(0, true)] [TestCase(-1, true)] [TestCase(30, true)] [TestCase(1000, true)]
        [TestCase(29, false)] [TestCase(-2, false)] [TestCase(1001, false)] [TestCase(1, false)]
        public void OnlyAutoUnlimitedOrThirtyToAThousandAreValid(long value, bool valid) => Assert.That(FramePacing.IsValidChoice(value), Is.EqualTo(valid));

        [Test] public void TheChoiceIsSavedAndAnOlderSaveWithoutItStartsOnAuto()
        {
            string path = Path.Combine(Path.GetTempPath(), "galaxy101-fps-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                MenuModel New() => new MenuModel(File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/content.json"), File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/themes.json"), path);
                var model = New();
                Assert.That((int)model.Settings["fps"], Is.EqualTo(FramePacing.Auto));
                var services = new MenuServices(model);
                Assert.That(services.Settings.SetFrameRate(144), Is.True);
                Assert.That(services.Settings.SetFrameRate(12), Is.False);
                Assert.That((int)New().Settings["fps"], Is.EqualTo(144), "kept across launches");

                // A save from before the setting existed keeps its other values and gets Otomatik.
                File.WriteAllText(path, "{\"version\":1,\"Settings\":{\"lang\":\"en\",\"resolution\":1080}}"); // pre-v3 keys are ignored
                var old = New();
                Assert.That((string)old.Settings["lang"], Is.EqualTo("en")); Assert.That((int)old.Settings["fps"], Is.EqualTo(FramePacing.Auto));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
