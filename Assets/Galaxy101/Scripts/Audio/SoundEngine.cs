using System;
using System.Collections.Generic;
using System.Linq;

namespace Galaxy101.Audio
{
    public enum SoundOutcome
    {
        /// <summary>No such event in the catalogue.</summary>
        Unknown,
        /// <summary>Logged only: the engine is muted (batch runs, state router).</summary>
        Muted,
        /// <summary>Gain 0: a volume at 0 or the category's toggle off.</summary>
        Silent,
        /// <summary>The event already plays its poly copies.</summary>
        Limited,
        Played,
    }

    /// <summary>The engine's decision for one play() call; the Unity host plays <see cref="Event"/>.Clip at <see cref="Volume"/> when audible.</summary>
    public readonly struct SoundPlay
    {
        public readonly SoundEvent Event;
        public readonly SoundOutcome Outcome;
        public readonly float Volume;
        /// <summary>The event ducked the music (priority ≥ mixing.duck.minPrio and the music was on).</summary>
        public readonly bool Ducked;

        public SoundPlay(SoundEvent ev, SoundOutcome outcome, float volume, bool ducked)
        {
            Event = ev; Outcome = outcome; Volume = volume; Ducked = ducked;
        }

        public bool Audible => Outcome == SoundOutcome.Played;
        public override string ToString() => (Event?.Id ?? "?") + " " + Outcome + (Audible ? " " + Volume.ToString("0.###") : "") + (Ducked ? " duck" : "");
    }

    /// <summary>One played event (g101-sound.js G101Sound.log: id, time, seat).</summary>
    public readonly struct SoundLogEntry
    {
        public readonly string Id;
        public readonly double Time;
        public readonly int? Seat;
        public SoundLogEntry(string id, double time, int? seat) { Id = id; Time = time; Seat = seat; }
        public override string ToString() => Id + (Seat.HasValue ? "@" + Seat.Value : "");
    }

    /// <summary>A music loop that is playing: its volume curve and, once it fades out, when it ends.</summary>
    public sealed class MusicVoice
    {
        public MusicTrack Track { get; }
        public ParamCurve Gain { get; }
        /// <summary>When the fade-out reaches silence (infinity while it is the current music).</summary>
        public double EndsAt { get; internal set; } = double.PositiveInfinity;
        internal MusicVoice(MusicTrack track, ParamCurve gain) { Track = track; Gain = gain; }
    }

    /// <summary>
    /// The prototype's sound engine (g101-sound.js) without the audio output: the volume rule, poly limits, music
    /// ducking, the menu/table music cross-fade and the screen change sound. Times are seconds on any steady clock
    /// (Unity passes Time.unscaledTimeAsDouble). Pure C#, so the rules are tested in edit mode.
    /// </summary>
    public sealed class SoundEngine
    {
        /// <summary>g101-sound.js: the log keeps the last 200 plays.</summary>
        public const int LogLimit = 200;
        /// <summary>g101-sound.js play(): a gain at or below this plays nothing.</summary>
        public const float AudibleFloor = 0.001f;
        /// <summary>g101-sound.js setMusic(): fades start from / end at 0.0001 (WebAudio's exponential floor).</summary>
        public const float Silence = 0.0001f;
        /// <summary>g101-sound.js: a settings change moves the music with setTargetAtTime(musicGain(), t, 0.1).</summary>
        public const float SettingsTimeConstant = 0.1f;

        private readonly Dictionary<string, List<double>> live = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        private readonly List<SoundLogEntry> log = new List<SoundLogEntry>();
        private readonly List<MusicVoice> fading = new List<MusicVoice>();

        public SoundCatalog Catalog { get; }
        public SoundLevels Levels { get; private set; }
        public bool Muted { get; private set; }
        public IReadOnlyList<SoundLogEntry> Log => log;
        /// <summary>Every play() of a known event, muted or not (the prototype's g101-sfx event).</summary>
        public event Action<SoundLogEntry> Played;

        /// <summary>The music id the current screen wants (kept while muted, like mus.want).</summary>
        public string WantedMusic { get; private set; }
        public MusicVoice CurrentMusic { get; private set; }
        public IReadOnlyList<MusicVoice> FadingMusic => fading;
        public string Screen { get; private set; }

        public SoundEngine(SoundCatalog catalog, SoundLevels levels = null, bool muted = false)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Levels = levels ?? catalog.Defaults;
            Muted = muted;
        }

        /// <summary>
        /// play(id, {seat, vol}): logs the event, then — unless muted or silent — ducks the music for a loud event and
        /// admits it while fewer than poly copies are counting (each counts for ms).
        /// </summary>
        public SoundPlay Play(string id, double now, int? seat = null, float volume = 1)
        {
            if (id == null || !Catalog.Events.TryGetValue(id, out var ev)) return new SoundPlay(null, SoundOutcome.Unknown, 0, false);
            var entry = new SoundLogEntry(id, now, seat);
            log.Add(entry);
            if (log.Count > LogLimit) log.RemoveAt(0);
            Played?.Invoke(entry);
            if (Muted) return new SoundPlay(ev, SoundOutcome.Muted, 0, false);
            float gain = Levels.Gain(Catalog, ev.Category) * volume;
            if (gain <= AudibleFloor) return new SoundPlay(ev, SoundOutcome.Silent, 0, false);
            bool ducked = ev.Priority >= Catalog.Duck.MinPriority && Duck(now);
            if (!live.TryGetValue(id, out var ends)) live[id] = ends = new List<double>();
            ends.RemoveAll(end => end <= now);
            if (ends.Count >= ev.Poly) return new SoundPlay(ev, SoundOutcome.Limited, 0, ducked);
            ends.Add(now + ev.Ms / 1000.0);
            return new SoundPlay(ev, SoundOutcome.Played, gain, ducked);
        }

        /// <summary>Copies of <paramref name="id"/> still counting against its poly at <paramref name="now"/>.</summary>
        public int Counting(string id, double now) => live.TryGetValue(id, out var ends) ? ends.Count(end => end > now) : 0;

        /// <summary>New settings: the current music glides to the new music gain (setTargetAtTime, 0.1 s).</summary>
        public void SetLevels(SoundLevels levels, double now)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            if (levels.Equals(Levels)) return;
            Levels = levels;
            if (CurrentMusic == null) return;
            CurrentMusic.Gain.HoldAt(now);
            CurrentMusic.Gain.TargetAt(Levels.MusicGain, now, SettingsTimeConstant);
        }

        /// <summary>Muting fades the music out (the wanted id is kept); unmuting starts it again (the prototype's first-gesture start).</summary>
        public void SetMuted(bool muted, double now)
        {
            if (Muted == muted) return;
            Muted = muted;
            if (muted) FadeOut(now);
            else SetMusic(WantedMusic, now);
        }

        /// <summary>
        /// setMusic(id): the playing loop fades to silence over crossfadeOutSeconds while the new one fades in from
        /// silence to the music gain over fadeInSeconds. The same id again does nothing; null fades the music out.
        /// </summary>
        public void SetMusic(string id, double now)
        {
            if (id != null && !Catalog.Music.ContainsKey(id)) throw new ArgumentException("Unknown music " + id, nameof(id));
            WantedMusic = id;
            if (Muted) return;
            if (CurrentMusic?.Track.Id == id) return;
            FadeOut(now);
            if (id == null) return;
            var curve = new ParamCurve(Silence);
            curve.Set(Silence, now);
            curve.LinearTo(Levels.MusicGain, now + Catalog.FadeInSeconds);
            CurrentMusic = new MusicVoice(Catalog.Music[id], curve);
        }

        private void FadeOut(double now)
        {
            if (CurrentMusic == null) return;
            var old = CurrentMusic;
            old.Gain.HoldAt(now);
            old.Gain.LinearTo(Silence, now + Catalog.CrossfadeOutSeconds);
            old.EndsAt = now + Catalog.CrossfadeOutSeconds;
            fading.Add(old);
            CurrentMusic = null;
        }

        /// <summary>duck(): the music ramps to musicLevel × gain over downSeconds, then back to the gain by restoreAtSeconds.</summary>
        public bool Duck(double now)
        {
            if (CurrentMusic == null) return false;
            float g = Levels.MusicGain;
            var curve = CurrentMusic.Gain;
            curve.HoldAt(now);
            curve.LinearTo(g * Catalog.Duck.MusicLevel, now + Catalog.Duck.DownSeconds);
            curve.LinearTo(g, now + Catalog.Duck.RestoreAtSeconds);
            return true;
        }

        /// <summary>Drops the loops whose fade-out has ended; returns them so the host can stop their sources.</summary>
        public IReadOnlyList<MusicVoice> Tick(double now)
        {
            if (fading.Count == 0) return Array.Empty<MusicVoice>();
            var ended = fading.Where(v => now >= v.EndsAt).ToList();
            foreach (var voice in ended) fading.Remove(voice);
            return ended;
        }

        /// <summary>
        /// The shell's go(screen): leaving any screen but splash (screenChange.skipFrom) plays ui.screen, and the music
        /// follows screenMusic (table music at the table, menu music elsewhere). A null screen (no Galaxy screen, e.g. the
        /// legacy table) only fades the music out. Returns the ui.screen decision, or null when no sound is due.
        /// </summary>
        public SoundPlay? SetScreen(string screen, double now)
        {
            if (screen == Screen) return null;
            string from = Screen;
            Screen = screen;
            SoundPlay? sound = null;
            if (from != null && screen != null && !Catalog.ScreenChangeSkipFrom.Contains(from)) sound = Play(Catalog.ScreenChangeSound, now);
            SetMusic(Catalog.MusicFor(screen), now);
            return sound;
        }

        /// <summary>The music volume of a voice at <paramref name="now"/>.</summary>
        public static float MusicVolume(MusicVoice voice, double now) => voice == null ? 0 : Math.Max(0, Math.Min(1, voice.Gain.Evaluate(now)));
    }
}
