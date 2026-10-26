using System;
using System.IO;
using System.Linq;
using Galaxy101.Audio;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>v4 sound and cursor rules (pkg/sound/events.json, g101-sound.js, g101-cursor.js) without audio output.</summary>
    public sealed class AudioServiceTests
    {
        private const string PackageEvents = "Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/sound/events.json";
        private const double Tolerance = 1e-4;

        private static SoundCatalog Catalog => SoundCatalog.Load();
        private static SoundEngine Engine(SoundLevels levels = null) => new SoundEngine(Catalog, levels);

        // ---------- catalogue ----------

        [Test]
        public void TheCatalogueCarriesEveryPackageEventUnchanged()
        {
            if (!File.Exists(PackageEvents)) Assert.Inconclusive("The v4 package is not in this checkout: " + PackageEvents);
            var package = JObject.Parse(File.ReadAllText(PackageEvents));
            var events = (JArray)package["events"];
            Assert.That(Catalog.Events.Count, Is.EqualTo(events.Count).And.EqualTo(28));
            foreach (JObject e in events)
            {
                var id = (string)e["id"];
                Assert.That(Catalog.Events.ContainsKey(id), id);
                var ev = Catalog.Events[id];
                Assert.That(ev.Category, Is.EqualTo((string)e["cat"]), id);
                Assert.That(ev.Priority, Is.EqualTo((int)e["prio"]), id);
                Assert.That(ev.Poly, Is.EqualTo((int)e["poly"]), id);
                Assert.That(ev.Ms, Is.EqualTo((int)e["ms"]), id);
                Assert.That(ev.Clip, Is.EqualTo("Galaxy101/Audio/Sfx/" + (string)e["file"]), id);
            }
            foreach (JObject m in (JArray)package["music"])
                Assert.That(Catalog.Music[(string)m["id"]].Clip, Is.EqualTo("Galaxy101/Audio/Music/" + Path.GetFileNameWithoutExtension((string)m["file"])));
        }

        [Test]
        public void TheMixingNumbersAreThePrototypes()
        {
            Assert.That(Catalog.Defaults, Is.EqualTo(new SoundLevels(80, 90, 55, true, true, true)));
            Assert.That(Catalog.OpponentDrawVolume, Is.EqualTo(.7f));
            Assert.That(Catalog.Duck.MinPriority, Is.EqualTo(5));
            Assert.That(Catalog.Duck.MusicLevel, Is.EqualTo(.4f));
            Assert.That(Catalog.Duck.DownSeconds, Is.EqualTo(.3f));
            Assert.That(Catalog.Duck.RestoreAtSeconds, Is.EqualTo(2.4f));
            Assert.That(Catalog.CrossfadeOutSeconds, Is.EqualTo(1.2f));
            Assert.That(Catalog.FadeInSeconds, Is.EqualTo(1.5f));
            Assert.That(Catalog.SyncMs["dice.roll"], Is.EqualTo(1450));
            Assert.That(Catalog.MusicFor("game"), Is.EqualTo("music.table"));
            foreach (var screen in new[] { "splash", "choice", "login", "menu", "lobby", "ranked" })
                Assert.That(Catalog.MusicFor(screen), Is.EqualTo("music.menu"), screen);
            Assert.That(Catalog.MusicFor(null), Is.Null);
        }

        [Test]
        public void EveryClipIsAnImportedAudioClip()
        {
            foreach (var path in Catalog.Events.Values.Select(e => e.Clip).Concat(Catalog.Music.Values.Select(m => m.Clip)))
                Assert.That(Resources.Load<AudioClip>(path), Is.Not.Null, path);
        }

        // ---------- volume rule ----------

        [Test]
        public void GainIsMasterTimesChannel()
        {
            var levels = Catalog.Defaults;
            foreach (var ev in Catalog.Events.Values)
                Assert.That(levels.Gain(Catalog, ev.Category), Is.EqualTo(.8f * .9f).Within(Tolerance), ev.Id);
            Assert.That(levels.MusicGain, Is.EqualTo(.8f * .55f).Within(Tolerance));
            var custom = new SoundLevels(50, 40, 100, true, true, true);
            Assert.That(custom.Gain(Catalog, "ui"), Is.EqualTo(.2f).Within(Tolerance));
            Assert.That(custom.MusicGain, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(new SoundLevels(0, 100, 100, true, true, true).Gain(Catalog, "game"), Is.Zero);
            Assert.That(new SoundLevels(150, -3, 100, true, true, true).Sfx, Is.Zero, "levels are clamped to 0–100");
        }

        [Test]
        public void CategoryTogglesSilenceOnlyTheirCategory()
        {
            var noTiles = new SoundLevels(80, 90, 55, false, true, true);
            Assert.That(noTiles.Gain(Catalog, "tile"), Is.Zero);
            Assert.That(noTiles.Gain(Catalog, "game"), Is.EqualTo(.72f).Within(Tolerance));
            Assert.That(noTiles.Gain(Catalog, "chat"), Is.EqualTo(.72f).Within(Tolerance));
            // g101-sound.js: chat is off when either chatSnd or notifSnd is off.
            Assert.That(new SoundLevels(80, 90, 55, true, false, true).Gain(Catalog, "chat"), Is.Zero);
            Assert.That(new SoundLevels(80, 90, 55, true, true, false).Gain(Catalog, "chat"), Is.Zero);
            Assert.That(new SoundLevels(80, 90, 55, true, true, false).Gain(Catalog, "ui"), Is.EqualTo(.72f).Within(Tolerance));
        }

        [Test]
        public void MenuSettingsDriveTheLevels()
        {
            var path = Path.Combine(Path.GetTempPath(), "galaxy-audio-" + Guid.NewGuid().ToString("N") + ".json");
            var model = MenuModel.LoadResources(path);
            var levels = Catalog.Defaults.With(model.Settings);
            Assert.That(levels.Master, Is.EqualTo((int)model.Settings["master"]));
            Assert.That(levels.Music, Is.EqualTo((int)model.Settings["music"]), "the menu's stored music (EK-402: 60, the engine default is 55)");
            Assert.That(levels.Sfx, Is.EqualTo((int)model.Settings["sfx"]));
            Assert.That(levels.ChatSounds, Is.True, "the menu has no chatSnd: the engine default stays");
            model.Settings["notifSnd"] = false;
            Assert.That(Catalog.Defaults.With(model.Settings).Gain(Catalog, "chat"), Is.Zero);
            // A table snapshot without master or notifSnd keeps them.
            var table = levels.With(new JObject { ["music"] = 30, ["sfx"] = 20, ["tileSnd"] = false, ["chatSnd"] = true });
            Assert.That(table, Is.EqualTo(new SoundLevels(levels.Master, 20, 30, false, true, levels.NotificationSounds)));
            Assert.That(levels.With(new JObject { ["master"] = "loud", ["music"] = 40.6 }).Music, Is.EqualTo(41));
            Assert.That(levels.With(new JObject { ["master"] = "loud" }).Master, Is.EqualTo(levels.Master), "non-numbers are ignored");
        }

        // ---------- play: poly, priority, mute ----------

        [Test]
        public void PolyLimitsCopiesForTheirMs()
        {
            var engine = Engine();
            // ui.tap: poly 2, 45 ms.
            Assert.That(engine.Play("ui.tap", 0).Outcome, Is.EqualTo(SoundOutcome.Played));
            Assert.That(engine.Play("ui.tap", .01).Outcome, Is.EqualTo(SoundOutcome.Played));
            Assert.That(engine.Play("ui.tap", .02).Outcome, Is.EqualTo(SoundOutcome.Limited));
            Assert.That(engine.Counting("ui.tap", .02), Is.EqualTo(2));
            Assert.That(engine.Play("ui.tap", .045).Outcome, Is.EqualTo(SoundOutcome.Played), "the first copy stopped counting at 45 ms");
            // Another event has its own count; finish: poly 1, 1200 ms.
            Assert.That(engine.Play("finish", .05).Outcome, Is.EqualTo(SoundOutcome.Played));
            Assert.That(engine.Play("finish", 1.2).Outcome, Is.EqualTo(SoundOutcome.Limited));
            Assert.That(engine.Play("finish", 1.25).Outcome, Is.EqualTo(SoundOutcome.Played));
            foreach (var ev in Catalog.Events.Values)
            {
                var fresh = Engine();
                for (int i = 0; i < ev.Poly; i++) Assert.That(fresh.Play(ev.Id, 0).Audible, Is.True, ev.Id);
                Assert.That(fresh.Play(ev.Id, 0).Outcome, Is.EqualTo(SoundOutcome.Limited), ev.Id);
                Assert.That(fresh.Play(ev.Id, ev.Ms / 1000.0).Audible, Is.True, ev.Id);
            }
        }

        [Test]
        public void VolumeIsTheCategoryGainTimesTheCueShare()
        {
            var engine = Engine();
            Assert.That(engine.Play("tile.draw", 0).Volume, Is.EqualTo(.72f).Within(Tolerance));
            Assert.That(engine.Play("tile.draw", 0, 2, Catalog.OpponentDrawVolume).Volume, Is.EqualTo(.72f * .7f).Within(Tolerance));
            engine.SetLevels(new SoundLevels(80, 90, 55, false, true, true), 0);
            Assert.That(engine.Play("tile.discard", 1).Outcome, Is.EqualTo(SoundOutcome.Silent));
            Assert.That(engine.Play("ui.tap", 1).Outcome, Is.EqualTo(SoundOutcome.Played));
            engine.SetLevels(new SoundLevels(1, 5, 55, true, true, true), 2);
            Assert.That(engine.Play("ui.tap", 2).Outcome, Is.EqualTo(SoundOutcome.Silent), "0.01 × 0.05 is below the 0.001 floor");
            Assert.That(engine.Play("no.such.event", 2).Outcome, Is.EqualTo(SoundOutcome.Unknown));
        }

        [Test]
        public void LoudEventsDuckTheMusicAndLetItBack()
        {
            var engine = Engine();
            engine.SetMusic("music.menu", 0);
            float g = Catalog.Defaults.MusicGain;
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 5), Is.EqualTo(g).Within(Tolerance));
            foreach (var ev in Catalog.Events.Values)
            {
                var quiet = Engine(); quiet.SetMusic("music.menu", 0);
                Assert.That(quiet.Play(ev.Id, 3).Ducked, Is.EqualTo(ev.Priority >= 5), ev.Id + " prio " + ev.Priority);
            }
            Assert.That(Catalog.Events.Values.Where(e => e.Priority >= 5).Select(e => e.Id), Is.EquivalentTo(new[] { "penalty", "finish", "finish.nuke" }));
            var penalty = engine.Play("penalty", 10);
            Assert.That(penalty.Ducked && penalty.Audible, Is.True);
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 10), Is.EqualTo(g).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 10.15), Is.EqualTo(g * .7f).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 10.3), Is.EqualTo(g * .4f).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 11.35), Is.EqualTo(g * .7f).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 12.4), Is.EqualTo(g).Within(Tolerance));
            // The prototype ducks before the poly check: a limited penalty still ducks.
            var again = engine.Play("penalty", 12.5);
            var limited = engine.Play("penalty", 12.6);
            Assert.That(again.Audible, Is.True);
            Assert.That(limited.Outcome, Is.EqualTo(SoundOutcome.Limited));
            Assert.That(limited.Ducked, Is.True);
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 12.9), Is.EqualTo(g * .4f).Within(Tolerance));
            // Without music nothing ducks; finish (prio 6) ducks too.
            Assert.That(Engine().Play("finish", 0).Ducked, Is.False);
            var withMusic = Engine(); withMusic.SetMusic("music.table", 0);
            Assert.That(withMusic.Play("finish.nuke", 3).Ducked, Is.True);
        }

        [Test]
        public void MutedRunsOnlyLog()
        {
            var engine = new SoundEngine(Catalog, muted: true);
            int heard = 0;
            engine.Played += _ => heard++;
            Assert.That(engine.Play("ui.tap", 0, 1).Outcome, Is.EqualTo(SoundOutcome.Muted));
            engine.SetMusic("music.menu", 0);
            Assert.That(engine.CurrentMusic, Is.Null);
            Assert.That(engine.WantedMusic, Is.EqualTo("music.menu"));
            Assert.That(heard, Is.EqualTo(1));
            Assert.That(engine.Log.Single().Seat, Is.EqualTo(1));
            for (int i = 0; i < 250; i++) engine.Play("tile.slide", i);
            Assert.That(engine.Log.Count, Is.EqualTo(SoundEngine.LogLimit));
            engine.SetMuted(false, 300);
            Assert.That(engine.CurrentMusic?.Track.Id, Is.EqualTo("music.menu"), "unmuting starts the wanted music");
            engine.SetMuted(true, 310);
            Assert.That(engine.CurrentMusic, Is.Null);
            Assert.That(engine.FadingMusic.Count, Is.EqualTo(1));
        }

        // ---------- music and screens ----------

        [Test]
        public void MusicFadesInAndCrossFades()
        {
            var engine = Engine();
            float g = Catalog.Defaults.MusicGain;
            engine.SetMusic("music.menu", 0);
            var menu = engine.CurrentMusic;
            Assert.That(SoundEngine.MusicVolume(menu, 0), Is.EqualTo(SoundEngine.Silence).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(menu, .75), Is.EqualTo((SoundEngine.Silence + g) / 2).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(menu, 1.5), Is.EqualTo(g).Within(Tolerance));
            engine.SetMusic("music.menu", 4);
            Assert.That(engine.CurrentMusic, Is.SameAs(menu), "the same music again does nothing");
            engine.SetMusic("music.table", 10);
            var table = engine.CurrentMusic;
            Assert.That(engine.FadingMusic, Is.EquivalentTo(new[] { menu }));
            Assert.That(SoundEngine.MusicVolume(menu, 10.6), Is.EqualTo((g + SoundEngine.Silence) / 2).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(menu, 11.2), Is.EqualTo(SoundEngine.Silence).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(table, 11.5), Is.EqualTo(g).Within(Tolerance));
            Assert.That(engine.Tick(11.1), Is.Empty);
            Assert.That(engine.Tick(11.25), Is.EquivalentTo(new[] { menu }));
            Assert.That(engine.FadingMusic, Is.Empty);
            engine.SetMusic(null, 20);
            Assert.That(engine.CurrentMusic, Is.Null);
            Assert.That(engine.Tick(21.25).Single(), Is.SameAs(table));
        }

        [Test]
        public void SettingsMoveTheMusicWithATimeConstant()
        {
            var engine = Engine();
            engine.SetMusic("music.menu", 0);
            float before = Catalog.Defaults.MusicGain;
            engine.SetLevels(new SoundLevels(80, 90, 100, true, true, true), 5);
            float after = .8f;
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 5), Is.EqualTo(before).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 5.1), Is.EqualTo(after + (before - after) * Mathf.Exp(-1)).Within(Tolerance));
            Assert.That(SoundEngine.MusicVolume(engine.CurrentMusic, 6.5), Is.EqualTo(after).Within(1e-3));
        }

        [Test]
        public void ScreensPlayTheChangeSoundAndPickTheMusic()
        {
            var engine = Engine();
            Assert.That(engine.SetScreen("splash", 0), Is.Null, "the first screen is silent");
            Assert.That(engine.CurrentMusic.Track.Id, Is.EqualTo("music.menu"));
            Assert.That(engine.SetScreen("choice", 1), Is.Null, "leaving splash is silent (screenChange.skipFrom)");
            Assert.That(engine.SetScreen("choice", 2), Is.Null, "no change, no sound");
            Assert.That(engine.SetScreen("login", 3)?.Event.Id, Is.EqualTo("ui.screen"));
            Assert.That(engine.SetScreen("menu", 4)?.Audible, Is.True);
            Assert.That(engine.CurrentMusic.Track.Id, Is.EqualTo("music.menu"), "menu screens share one loop");
            Assert.That(engine.FadingMusic, Is.Empty);
            Assert.That(engine.SetScreen("game", 5)?.Event.Id, Is.EqualTo("ui.screen"));
            Assert.That(engine.CurrentMusic.Track.Id, Is.EqualTo("music.table"));
            Assert.That(engine.FadingMusic.Single().Track.Id, Is.EqualTo("music.menu"));
            Assert.That(engine.SetScreen("lobby", 60)?.Audible, Is.True);
            Assert.That(engine.CurrentMusic.Track.Id, Is.EqualTo("music.menu"));
            Assert.That(engine.SetScreen(null, 70), Is.Null, "no Galaxy screen: silent, music out");
            Assert.That(engine.CurrentMusic, Is.Null);
        }

        [Test]
        public void ParamCurveFollowsWebAudio()
        {
            var curve = new ParamCurve(1);
            Assert.That(curve.Evaluate(5), Is.EqualTo(1));
            curve.Set(0, 1);
            curve.LinearTo(10, 2);
            Assert.That(curve.Evaluate(.5), Is.EqualTo(1));
            Assert.That(curve.Evaluate(1.5), Is.EqualTo(5).Within(Tolerance));
            Assert.That(curve.Evaluate(3), Is.EqualTo(10));
            Assert.That(curve.SettledAt(3), Is.True);
            curve.HoldAt(1.5);
            Assert.That(curve.Evaluate(2), Is.EqualTo(5).Within(Tolerance), "hold cancels the rest of the ramp");
            curve.TargetAt(0, 2, .5f);
            Assert.That(curve.Evaluate(2.5), Is.EqualTo(5 * Mathf.Exp(-1)).Within(Tolerance));
            Assert.That(curve.SettledAt(9), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => curve.TargetAt(1, 3, 0));
        }

        [Test]
        public void AProtocolHandEndResultNamesTheNuke()
        {
            Assert.That(TableSounds.IsNuke(JObject.Parse("{\"elden\":false,\"okeyFinish\":true}")), Is.True);
            Assert.That(TableSounds.IsNuke(JObject.Parse("{\"elden\":true,\"okeyFinish\":false}")), Is.True);
            Assert.That(TableSounds.IsNuke(JObject.Parse("{\"reason\":\"deckOut\",\"elden\":false,\"okeyFinish\":false}")), Is.False);
        }

        // ---------- cursors ----------

        [Test]
        public void CursorSetHasTheElevenCursorsAndTheirHotspots()
        {
            var set = CursorSet.Load();
            Assert.That(set.Names, Is.EqualTo(new[] { "default", "pointer", "grab", "grabbing", "drop", "text", "no", "wait", "progress", "ew", "ns" }));
            Assert.That(set.Size, Is.EqualTo(32));
            Assert.That(set.Cursors["pointer"].Hotspot, Is.EqualTo(new Vector2Int(4, 3)));
            Assert.That(set.Cursors["grab"].Hotspot, Is.EqualTo(new Vector2Int(16, 15)));
            foreach (var c in set.Cursors.Values)
            {
                Assert.That(c.Hotspot2x, Is.EqualTo(c.Hotspot * 2), c.Name);
                var texture = Resources.Load<Texture2D>(c.Png);
                var texture2x = Resources.Load<Texture2D>(c.Png2x);
                Assert.That(texture, Is.Not.Null, c.Png);
                Assert.That(texture2x, Is.Not.Null, c.Png2x);
                Assert.That(texture.width, Is.EqualTo(32), c.Png);
                Assert.That(texture2x.width, Is.EqualTo(64), c.Png2x);
                Assert.That(texture2x.isReadable, Is.True, "resampling reads the @2x pixels");
            }
        }

        [Test]
        public void CursorSizeFollowsDisplayScaleAndMode()
        {
            var set = CursorSet.Load();
            Assert.That(set.PixelSize(1, CursorSet.Normal), Is.EqualTo(32));
            Assert.That(set.PixelSize(0, CursorSet.Normal), Is.EqualTo(32), "an unknown DPI counts as 96");
            Assert.That(set.PixelSize(2, CursorSet.Normal), Is.EqualTo(64));
            Assert.That(set.PixelSize(1, CursorSet.Large), Is.EqualTo(42));
            Assert.That(set.PixelSize(1.5f, CursorSet.Normal), Is.EqualTo(48));
            var exact = set.Pick("pointer", 32);
            Assert.That(exact.Exact && exact.Source == "Galaxy101/Cursors/pointer" && exact.Hotspot == new Vector2(4, 3), Is.True);
            var hi = set.Pick("pointer", 64);
            Assert.That(hi.Exact && hi.Source == "Galaxy101/Cursors/pointer@2x" && hi.Hotspot == new Vector2(8, 6), Is.True);
            var large = set.Pick("grab", 42);
            Assert.That(large.Exact, Is.False);
            Assert.That(large.Source, Is.EqualTo("Galaxy101/Cursors/grab@2x"));
            Assert.That(large.Hotspot, Is.EqualTo(new Vector2(21, 20)), "16,15 × 1.3125");
            Assert.That(set.Pick("grab", 24).Source, Is.EqualTo("Galaxy101/Cursors/grab"));
        }

        [Test]
        public void CursorRulesReadControlsMarksAndLocks()
        {
            var root = new VisualElement();
            var button = new Button(); var caption = new Label("Oyna"); button.Add(caption); root.Add(button);
            Assert.That(CursorRules.Resolve(caption), Is.EqualTo("pointer"));
            Assert.That(CursorRules.Resolve(root), Is.EqualTo("default"));
            Assert.That(CursorRules.Resolve(null), Is.EqualTo("default"));
            button.SetEnabled(false);
            Assert.That(CursorRules.Resolve(caption), Is.EqualTo("no"));
            var field = new TextField(); root.Add(field);
            Assert.That(CursorRules.Resolve(field), Is.EqualTo("text"));
            field.isReadOnly = true;
            Assert.That(CursorRules.Resolve(field), Is.EqualTo("no"));
            var slider = new Slider(0, 100); root.Add(slider);
            Assert.That(CursorRules.Resolve(slider), Is.EqualTo("ew"));
            Assert.That(CursorRules.Resolve(new Scroller(0, 10, null, SliderDirection.Vertical)), Is.EqualTo("ns"));
            Assert.That(CursorRules.Resolve(new Toggle()), Is.EqualTo("pointer"));
            Assert.That(CursorRules.Resolve(new DropdownField()), Is.EqualTo("pointer"));
            // data-cursor on a custom element, and the prototype's role of a pressable card.
            var card = new VisualElement(); var inner = new VisualElement(); card.Add(inner); root.Add(card);
            Assert.That(CursorRules.Resolve(inner), Is.EqualTo("default"));
            GalaxyCursor.Mark(card, "pointer");
            Assert.That(CursorRules.Resolve(inner), Is.EqualTo("pointer"));
            GalaxyCursor.Mark(card, "grab");
            Assert.That(card.GetClasses().Count(c => c.StartsWith(CursorRules.ClassPrefix)), Is.EqualTo(1), "a new mark replaces the old one");
            Assert.That(CursorRules.Resolve(inner), Is.EqualTo("grab"));
            Assert.That(CursorRules.Resolve(inner, n => n == "default"), Is.EqualTo("default"), "unknown names are skipped");
            // data-cursor-lock wins over everything inside.
            GalaxyCursor.Lock(root, "wait");
            Assert.That(CursorRules.Resolve(caption), Is.EqualTo("wait"));
            Assert.That(CursorRules.Resolve(field), Is.EqualTo("wait"));
            GalaxyCursor.Mark(root, "pointer");
            Assert.That(root.ClassListContains(CursorRules.LockPrefix + "wait"), Is.True, "marking keeps the lock");
            GalaxyCursor.Lock(root, null);
            Assert.That(CursorRules.Resolve(field), Is.EqualTo("no"));
        }

        [Test]
        public void TapIsAPressOnAPointerElementWithinSixLevels()
        {
            var button = new Button();
            var chain = button as VisualElement;
            for (int i = 0; i < 5; i++) { var child = new VisualElement(); chain.Add(child); chain = child; }
            Assert.That(CursorRules.IsTap(chain), Is.True, "five levels under the button");
            var deeper = new VisualElement(); chain.Add(deeper);
            Assert.That(CursorRules.IsTap(deeper), Is.False, "the prototype looks at six elements");
            Assert.That(CursorRules.IsTap(new Label()), Is.False);
            Assert.That(CursorRules.IsTap(null), Is.False);
            var disabled = new Button(); disabled.SetEnabled(false);
            Assert.That(CursorRules.IsTap(disabled), Is.False);
            var card = new VisualElement();
            GalaxyFilters.PressSink(card);
            Assert.That(CursorRules.IsTap(card), Is.True, "PressSink marks pressable controls (lobby, auth, table HUD)");
            Assert.That(CursorRules.IsTap(new Slider()), Is.False);
        }
    }
}
