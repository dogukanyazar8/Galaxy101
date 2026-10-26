using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Galaxy101.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    /// <summary>Exercises the real Unity output adapter explicitly, including in otherwise muted batch tests.</summary>
    public sealed class AudioPlaybackTests
    {
        private GameObject owner;
        private Component host;
        private SoundEngine engine;

        [SetUp]
        public void SetUp()
        {
            engine = new SoundEngine(SoundCatalog.Load());
            owner = new GameObject("Audio playback test");
            // Keep this isolated from the static service and the active scene's automatic screen tracking.
            var type = typeof(GalaxyAudio).Assembly.GetType("Galaxy101.Audio.GalaxyAudioHost", throwOnError: true);
            host = owner.AddComponent(type);
            ((Behaviour)host).enabled = false;
            Call("Initialize", engine);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator MenuAndTableMusicUseTheCatalogLoopsAndCrossfadeOnRealSources()
        {
            double now = GalaxyAudio.Now;
            var catalog = engine.Catalog;
            engine.SetScreen("menu", now);
            Call("UpdateMusic", now + catalog.FadeInSeconds);
            var menu = Source(catalog.Music["music.menu"].Clip);
            yield return UntilPlaying(menu);
            Assert.That(menu.loop, Is.True);
            Assert.That(menu.priority, Is.Zero);
            Assert.That(menu.volume, Is.EqualTo(.44f).Within(1e-4));

            double transition = now + catalog.FadeInSeconds + 1;
            engine.SetScreen("game", transition);
            Call("UpdateMusic", transition);
            var table = Source(catalog.Music["music.table"].Clip);
            Assert.That(table, Is.Not.SameAs(menu));
            Assert.That(menu.isPlaying, Is.True, "the menu keeps playing during the crossfade");
            Assert.That(table.loop, Is.True);
            Assert.That(table.volume, Is.EqualTo(SoundEngine.Silence).Within(1e-5));
            yield return UntilPlaying(table);

            double settled = transition + Math.Max(catalog.CrossfadeOutSeconds, catalog.FadeInSeconds) + .1;
            Call("UpdateMusic", settled);
            Assert.That(menu.isPlaying, Is.False);
            Assert.That(menu.clip, Is.Null);
            Assert.That(table.volume, Is.EqualTo(.44f).Within(1e-4));
            engine.SetLevels(new SoundLevels(50, 40, 30, true, true, true), settled);
            Call("UpdateMusic", settled + 1);
            Assert.That(table.volume, Is.EqualTo(.15f).Within(1e-4), "music uses master times music, not the effects channel");
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator EffectSourcesUseTheRecordingAndRespectTheVolumeChannelsAndToggles()
        {
            double now = GalaxyAudio.Now;
            engine.SetLevels(new SoundLevels(50, 40, 30, true, true, true), now);
            Call("Perform", engine.Play("tile.draw", now));
            var draw = Source("Galaxy101/Audio/RecordedTiles/draw");
            yield return UntilPlaying(draw);
            Assert.That(draw.loop, Is.False);
            Assert.That(draw.spatialBlend, Is.Zero);
            Assert.That(draw.volume, Is.EqualTo(.2f).Within(1e-5));
            Assert.That(draw.priority, Is.EqualTo(126));

            engine.SetLevels(new SoundLevels(50, 40, 30, false, false, true), now + 1);
            int sourceCount = owner.GetComponents<AudioSource>().Length;
            foreach (string id in new[] { "tile.discard", "chat.message" })
            {
                var silent = engine.Play(id, now + 1);
                Assert.That(silent.Outcome, Is.EqualTo(SoundOutcome.Silent), id);
                Call("Perform", silent);
            }
            Assert.That(owner.GetComponents<AudioSource>().Length, Is.EqualTo(sourceCount));
            Assert.That(owner.GetComponents<AudioSource>().Any(s => s.clip == Resources.Load<AudioClip>(engine.Catalog.Events["tile.discard"].Clip)), Is.False);

            Call("Perform", engine.Play("ui.tap", now + 1));
            var ui = Source(engine.Catalog.Events["ui.tap"].Clip);
            yield return UntilPlaying(ui);
            Assert.That(ui.volume, Is.EqualTo(.2f).Within(1e-5), "category toggles do not mute UI effects");
            engine.SetLevels(new SoundLevels(0, 40, 30, true, true, true), now + 2);
            var masterOff = engine.Play("ui.error", now + 2);
            Assert.That(masterOff.Outcome, Is.EqualTo(SoundOutcome.Silent));
            sourceCount = owner.GetComponents<AudioSource>().Length;
            Call("Perform", masterOff);
            Assert.That(owner.GetComponents<AudioSource>().Length, Is.EqualTo(sourceCount));
        }

        private AudioSource Source(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            Assert.That(clip, Is.Not.Null, path);
            var source = owner.GetComponents<AudioSource>().SingleOrDefault(s => s.clip == clip);
            Assert.That(source, Is.Not.Null, path + " must be assigned to a real AudioSource");
            return source;
        }

        private void Call(string name, params object[] args)
        {
            var method = host.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(host, args);
        }

        private static IEnumerator UntilPlaying(AudioSource source)
        {
            float end = Time.realtimeSinceStartup + 10;
            while (!source.isPlaying)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "the real AudioSource did not begin playback");
                yield return null;
            }
        }
    }
}
