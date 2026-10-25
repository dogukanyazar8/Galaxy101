using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Galaxy101.Audio
{
    /// <summary>
    /// The game's sound entry point (the prototype's G101Sound): <see cref="Play(string,int?,float)"/> an event of the v4
    /// catalogue, <see cref="Toast"/> for the shell's flash, <see cref="ApplySettings"/> for the volume settings and
    /// <see cref="TrackScreens"/> for the screen music and the screen change sound. A hidden host object (created on first
    /// use in a playing, non-batch player) plays the clips, the music loops, the ui.tap on pointer elements and the cursor.
    /// Batch runs (tests) and the state router's runs only log, like the prototype's muted ?state runs.
    /// </summary>
    public static class GalaxyAudio
    {
        private static SoundEngine engine;
        private static GalaxyAudioHost host;
        private static readonly List<(UnityEngine.Object owner, Func<string> screen)> screenSources = new List<(UnityEngine.Object, Func<string>)>();

        /// <summary>The sound engine (rules, log, music state); created with the Resources catalogue on first use.</summary>
        public static SoundEngine Engine
        {
            get
            {
                if (engine != null) return engine;
                engine = new SoundEngine(SoundCatalog.Load(), muted: MutedByDefault());
                EnsureHost();
                return engine;
            }
        }

        public static SoundCatalog Catalog => Engine.Catalog;
        public static TableSounds Table => TableSounds.Default;
        public static IReadOnlyList<SoundLogEntry> Log => Engine.Log;
        /// <summary>The screen the music follows (null before the first frame or outside the Galaxy screens).</summary>
        public static string Screen => engine?.Screen;
        public static double Now => Time.unscaledTimeAsDouble;

        public static bool Muted
        {
            get => Engine.Muted;
            set => Engine.SetMuted(value, Now);
        }

        /// <summary>Plays a catalogue event (G101Sound.play(id, {seat, vol})).</summary>
        public static SoundPlay Play(string id, int? seat = null, float volume = 1)
        {
            var result = Engine.Play(id, Now, seat, volume);
            if (result.Outcome == SoundOutcome.Unknown) Debug.LogWarning("Unknown sound event " + id);
            if (result.Audible) EnsureHost()?.Perform(result);
            return result;
        }

        public static SoundPlay Play(SoundCue cue) => Play(cue.Id, cue.Seat, cue.Volume);

        public static void Play(SoundCue? cue) { if (cue.HasValue) Play(cue.Value); }

        public static void Play(IEnumerable<SoundCue> cues)
        {
            if (cues == null) return;
            foreach (var cue in cues) Play(cue);
        }

        /// <summary>
        /// The shell's flash(text, kind): a gold toast (reward) plays ui.reward, a bad one (error) ui.error; ok and info are
        /// silent ("Galaxy 101 - Gerçek Ana Oyun" flash()).
        /// </summary>
        public static void Toast(string kind)
        {
            if (kind == "gold") Play("ui.reward");
            else if (kind == "bad") Play("ui.error");
        }

        /// <summary>
        /// Volume settings: every sound key the object carries (master, sfx, music, tileSnd, chatSnd, notifSnd) replaces the
        /// current level; keys it lacks keep theirs (the menu stores notifSnd, the table chatSnd — EK-402).
        /// </summary>
        public static void ApplySettings(JObject settings)
        {
            if (settings == null) return;
            Engine.SetLevels(Engine.Levels.With(settings), Now);
        }

        public static void SetLevels(SoundLevels levels) => Engine.SetLevels(levels, Now);

        /// <summary>
        /// Registers a screen source: while <paramref name="owner"/> lives, the latest registered source names the current
        /// screen each frame. A change plays ui.screen (not when leaving splash) and switches the music (table music on
        /// "game", menu music elsewhere). With no live source the Galaxy table scene counts as "game".
        /// </summary>
        public static void TrackScreens(UnityEngine.Object owner, Func<string> screen)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            screenSources.RemoveAll(s => s.owner == null || s.owner == owner);
            screenSources.Add((owner, screen));
            _ = Engine;
        }

        /// <summary>The current screen from the live sources (latest first), else "game" in the Galaxy table scene, else null.</summary>
        public static string ResolveScreen()
        {
            for (int i = screenSources.Count - 1; i >= 0; i--)
            {
                var (owner, source) = screenSources[i];
                if (owner == null) { screenSources.RemoveAt(i); continue; }
                try { return source(); }
                catch (Exception ex) { Debug.LogException(ex); screenSources.RemoveAt(i); }
            }
            return SceneManager.GetActiveScene().name == MainMenuController.GalaxyTableScene ? "game" : null;
        }

        /// <summary>
        /// Muted from the start in batch runs, under the state router (-g101State, like ?state) and in the legacy table
        /// probes (they count AudioSources).
        /// </summary>
        public static bool MutedByDefault()
        {
            if (Application.isBatchMode) return true;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("-g101State", StringComparison.Ordinal) || arg == "-okeyPractice" || arg == "-okeyA3Output" || arg == "-okeyA4Output") return true;
            return false;
        }

        private static GalaxyAudioHost EnsureHost()
        {
            if (host != null) return host;
            if (!Application.isPlaying || Application.isBatchMode || engine == null) return null;
            var go = new GameObject("Galaxy audio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<GalaxyAudioHost>();
            host.Initialize(engine);
            return host;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            engine = null; host = null; screenSources.Clear();
        }
    }
}
