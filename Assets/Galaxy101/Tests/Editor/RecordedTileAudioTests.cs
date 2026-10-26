using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Galaxy101.Audio;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>EK-413: exercise the event-to-clip boundary with the retained physical recording, without Unity audio output.</summary>
    public sealed class RecordedTileAudioTests
    {
        private const string Resources = "Assets/Galaxy101/Resources/";
        private static SoundCatalog Catalog => SoundCatalog.Parse(File.ReadAllText(Resources + SoundCatalog.ResourcePath + ".json"));

        [TestCase("drew", "tile.draw", "draw")]
        [TestCase("discarded", "tile.discard", "discard")]
        [TestCase("opened", "meld.lay", "place")]
        [TestCase("laid", "meld.lay", "place")]
        [TestCase("added", "meld.add", "place")]
        [TestCase("okeySwapped", "okey.swap", "place")]
        public void EngineTileEventsLoadThePhysicalRecording(string engineEvent, string soundId, string recording)
        {
            var catalog = Catalog;
            var cue = new TableSounds(catalog).ForEngine(engineEvent, 0, 0);
            Assert.That(cue.HasValue, Is.True);
            var play = new SoundEngine(catalog).Play(cue.Value.Id, 0, cue.Value.Seat, cue.Value.Volume);
            Assert.That(play.Audible, Is.True);
            Assert.That(play.Event.Id, Is.EqualTo(soundId));
            Assert.That(play.Event.Clip, Is.EqualTo("Galaxy101/Audio/RecordedTiles/" + recording));
            Assert.That(play.Event.WavSeconds, Is.EqualTo(.28f));
            Assert.That(File.Exists(Resources + play.Event.Clip + ".wav"), Is.True, "the runtime Resources path must resolve to a shipped recording");
        }

        [TestCase("draw", "ed9f02c5ac8ea7c37960bb991cab821e1da7c5fd5665ddf5be080a1aa712d2e4")]
        [TestCase("place", "4309e7e2a8175612b556065613b0b1fc365adc2d59d466b218c7f7138fa854cc")]
        [TestCase("discard", "66b4737c2798c44d72572a7df2f49ee0073e5fb59031b77c43b3b002babad9b0")]
        public void ShippedRecordingIsTheExistingPreparationScriptsOutput(string recording, string expectedSha256)
        {
            string path = Resources + "Galaxy101/Audio/RecordedTiles/" + recording + ".wav";
            Assert.That(File.Exists(path), Is.True, path);
            using var sha = SHA256.Create();
            Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(), Is.EqualTo(expectedSha256));
        }

        [Test]
        public void UnmappedTileActionsRetainTheirDistinctPackageClips()
        {
            var catalog = Catalog;
            Assert.That(catalog.Events["tile.slide"].Clip, Is.EqualTo("Galaxy101/Audio/Sfx/tile_slide"));
            Assert.That(catalog.Events["deal.shuffle"].Clip, Is.EqualTo("Galaxy101/Audio/Sfx/deal_shuffle"));
            Assert.That(catalog.Events["deal.indicator"].Clip, Is.EqualTo("Galaxy101/Audio/Sfx/deal_indicator"));
            Assert.That(catalog.Events["deal.distribute"].Clip, Is.EqualTo("Galaxy101/Audio/Sfx/deal_distribute"));
            Assert.That(catalog.Events["dice.roll"].Clip, Is.EqualTo("Galaxy101/Audio/Sfx/dice_roll"));
        }

        [TestCase("tile.draw")]
        [TestCase("tile.discard")]
        [TestCase("meld.lay")]
        [TestCase("meld.add")]
        [TestCase("okey.swap")]
        public void RecordedTileEventsKeepGainToggleAndPerEventConcurrency(string id)
        {
            var catalog = Catalog;
            var engine = new SoundEngine(catalog);
            var ev = catalog.Events[id];
            for (int i = 0; i < ev.Poly; i++)
            {
                var play = engine.Play(id, 0);
                Assert.That(play.Outcome, Is.EqualTo(SoundOutcome.Played));
                Assert.That(play.Volume, Is.EqualTo(.72f).Within(1e-5));
            }
            Assert.That(engine.Play(id, 0).Outcome, Is.EqualTo(SoundOutcome.Limited));
            Assert.That(engine.Play(id, ev.Ms / 1000.0).Audible, Is.True, "the recorded WAV length does not change the v4 concurrency window");
            engine.SetLevels(new SoundLevels(50, 40, 30, false, true, true), 1);
            Assert.That(engine.Play(id, 1).Outcome, Is.EqualTo(SoundOutcome.Silent));
            Assert.That(engine.Play("ui.tap", 1).Volume, Is.EqualTo(.2f).Within(1e-5));
            engine.SetLevels(new SoundLevels(50, 40, 30, true, true, true), 2);
            Assert.That(engine.Play(id, 2).Volume, Is.EqualTo(.2f).Within(1e-5));
        }

        [Test]
        public void OpponentDrawKeepsTheSeventyPercentShare()
        {
            var catalog = Catalog;
            var cue = new TableSounds(catalog).ForEngine("drew", 2, 0).Value;
            var play = new SoundEngine(catalog).Play(cue.Id, 0, cue.Seat, cue.Volume);
            Assert.That(play.Volume, Is.EqualTo(.504f).Within(1e-5));
        }

        [Test]
        public void LogRecordsAttemptsIncludingMutedSilentAndLimitedEvents()
        {
            var catalog = Catalog;
            var engine = new SoundEngine(catalog, muted: true);
            Assert.That(engine.Play("tile.draw", 1, 2).Outcome, Is.EqualTo(SoundOutcome.Muted));
            engine.SetMuted(false, 2);
            engine.SetLevels(new SoundLevels(80, 90, 55, false, true, true), 2);
            Assert.That(engine.Play("tile.draw", 2, 2).Outcome, Is.EqualTo(SoundOutcome.Silent));
            engine.SetLevels(catalog.Defaults, 3);
            for (int i = 0; i < catalog.Events["tile.draw"].Poly; i++) engine.Play("tile.draw", 3, 2);
            Assert.That(engine.Play("tile.draw", 3, 2).Outcome, Is.EqualTo(SoundOutcome.Limited));
            Assert.That(engine.Log.Select(e => e.Id), Is.All.EqualTo("tile.draw"));
            Assert.That(engine.Log.Count, Is.EqualTo(5));
            Assert.That(engine.Log.First().Time, Is.EqualTo(1));
            Assert.That(engine.Log.Last().Seat, Is.EqualTo(2));
            for (int i = 0; i < 201; i++) engine.Play("tile.draw", 4 + i, 2);
            Assert.That(engine.Log.Count, Is.EqualTo(200));
            Assert.That(engine.Log.First().Time, Is.EqualTo(5));
            Assert.That(engine.Log.Last().Time, Is.EqualTo(204));
        }
    }
}
