using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Okey101.Application.Tests
{
    public sealed class LocalSettingsTests
    {
        private string directory, path;
        [SetUp] public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "okey-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); path = Path.Combine(directory, "settings.json");
        }
        [TearDown] public void Cleanup()
        { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        [Test] public void SavedSettingsRoundTripAndReplaceExistingFile()
        {
            var settings = new LocalSettings { MasterVolume = .35f, Fullscreen = true, ReducedMotion = true };
            settings.Save(path); var loaded = LocalSettings.Load(path);
            Assert.That(loaded.MasterVolume, Is.EqualTo(.35f)); Assert.That(loaded.Fullscreen && loaded.ReducedMotion, Is.True);
            settings.MasterVolume = .7f; settings.Save(path);
            Assert.That(LocalSettings.Load(path).MasterVolume, Is.EqualTo(.7f));
            Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
        }
        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("{\"Version\":99,\"MasterVolume\":0.2,\"Fullscreen\":true,\"ReducedMotion\":true}")]
        [TestCase("{\"Version\":1,\"MasterVolume\":5,\"Fullscreen\":true,\"ReducedMotion\":true}")]
        public void CorruptSettingsFallBackWithDiagnostic(string json)
        {
            File.WriteAllText(path,json); var logs = new List<string>(); var settings = LocalSettings.Load(path, logs.Add);
            Assert.That(settings.MasterVolume, Is.EqualTo(1)); Assert.That(settings.Fullscreen || settings.ReducedMotion, Is.False);
            Assert.That(logs, Has.Count.EqualTo(1)); Assert.That(File.ReadAllText(path), Is.EqualTo(json));
        }
        [Test] public void OversizedSettingsAreRejectedWithoutReplacingUserFile()
        {
            File.WriteAllText(path,new string('x',65537)); var logs = new List<string>(); LocalSettings.Load(path,logs.Add);
            Assert.That(logs, Has.Count.EqualTo(1)); Assert.That(new FileInfo(path).Length, Is.EqualTo(65537));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-.1f)] [TestCase(1.1f)]
        public void InvalidVolumeCannotOverwriteSavedSettings(float value)
        {
            var settings = new LocalSettings(); settings.Save(path); var saved = File.ReadAllText(path);
            settings.MasterVolume = value;
            Assert.Throws<ArgumentOutOfRangeException>(()=>settings.Save(path)); Assert.That(File.ReadAllText(path), Is.EqualTo(saved));
        }
        [Test] public void MissingSettingsUseDefaultsWithoutWritingFile()
        { Assert.That(LocalSettings.Load(path).MasterVolume, Is.EqualTo(1)); Assert.That(File.Exists(path), Is.False); }
        [Test] public void FailedReplacementPreservesOldFileAndCleansTemporaryFile()
        {
            new LocalSettings().Save(path);var previous=File.ReadAllText(path);
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(()=>new LocalSettings {MasterVolume=.2f}.Save(path));
            Assert.That(File.ReadAllText(path),Is.EqualTo(previous));Assert.That(Directory.GetFiles(directory).Length,Is.EqualTo(1));
        }
        [Test] public void NextRoundRequiresTerminalState()
        {
            using(var session=new LocalSession())
            {session.Start(71,0);Assert.Throws<InvalidOperationException>(()=>session.StartNextRound(72));Assert.That(session.GetView(0).Version,Is.Zero);}
        }
    }
}
