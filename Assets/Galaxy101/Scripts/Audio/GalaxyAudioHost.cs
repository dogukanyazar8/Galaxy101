using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galaxy101.Audio
{
    /// <summary>
    /// Unity side of <see cref="GalaxyAudio"/> (one object, kept across scenes): AudioSources for the effects and the music
    /// loops, an AudioListener while the scene has none, the screen tracking, the ui.tap on pointer elements and the
    /// cursor. Muted runs create no AudioSource or listener (the legacy probes count them).
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class GalaxyAudioHost : MonoBehaviour
    {
        /// <summary>Frames between checks for another AudioListener (the scenes bring none today; a camera may).</summary>
        private const int ListenerCheckFrames = 30;
        /// <summary>Unity voice priority: 0 is the most important, 128 the default. Music never gives way.</summary>
        private const int MusicPriority = 0, DefaultPriority = 128;

        private SoundEngine engine;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> effects = new List<AudioSource>();
        private readonly Dictionary<MusicVoice, AudioSource> music = new Dictionary<MusicVoice, AudioSource>();
        private readonly Stack<AudioSource> spareMusic = new Stack<AudioSource>();
        private readonly List<MusicVoice> voices = new List<MusicVoice>();
        private readonly UiPicker picker = new UiPicker();
        private CursorDriver cursor;
        private AudioListener listener;
        private bool preloaded;

        internal void Initialize(SoundEngine soundEngine)
        {
            engine = soundEngine;
            try { cursor = new CursorDriver(CursorSet.Load(), picker); }
            catch (System.Exception ex) { Debug.LogWarning("Galaxy cursor disabled: " + ex.Message); }
        }

        /// <summary>Plays an audible decision of the engine on a free effect source.</summary>
        internal void Perform(SoundPlay play)
        {
            if (!play.Audible || engine.Muted) return;
            Preload();
            var clip = Clip(play.Event.Clip);
            if (clip == null) return;
            EnsureListener();
            var source = FreeEffect();
            source.clip = clip;
            source.volume = play.Volume;
            // The catalogue's prio (1–6) orders Unity's voice stealing: a higher prio is a lower Unity number.
            source.priority = DefaultPriority - play.Event.Priority;
            source.Play();
        }

        private void Update()
        {
            if (engine == null) return;
            double now = GalaxyAudio.Now;
            var screen = engine.SetScreen(GalaxyAudio.ResolveScreen(), now);
            if (screen.HasValue) Perform(screen.Value);
            UpdateMusic(now);
            if (Time.frameCount % ListenerCheckFrames == 0) { UpdateListener(); ReleaseWhenIdle(); }
            Tap();
            cursor?.Update();
        }

        /// <summary>g101-sound.js: a press on an element whose cursor is pointer (it or five parents) plays ui.tap; the 3D view never does.</summary>
        private void Tap()
        {
            Vector2 position;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                position = mouse.position.ReadValue();
            else if (Pointer.current != null && Pointer.current != mouse && Pointer.current.press.wasPressedThisFrame)
                position = Pointer.current.position.ReadValue();
            else return;
            var target = picker.Pick(position);
            if (target != null && CursorRules.IsTap(target)) GalaxyAudio.Play("ui.tap");
        }

        private void UpdateMusic(double now)
        {
            foreach (var ended in engine.Tick(now))
            {
                if (!music.TryGetValue(ended, out var source)) continue;
                source.Stop(); source.clip = null;
                music.Remove(ended); spareMusic.Push(source);
            }
            voices.Clear();
            if (engine.CurrentMusic != null) voices.Add(engine.CurrentMusic);
            voices.AddRange(engine.FadingMusic);
            foreach (var voice in voices)
            {
                if (!music.TryGetValue(voice, out var source))
                {
                    var clip = Clip(voice.Track.Clip);
                    if (clip == null) continue;
                    EnsureListener();
                    source = spareMusic.Count > 0 ? spareMusic.Pop() : NewSource();
                    source.clip = clip; source.loop = voice.Track.Loop; source.priority = MusicPriority;
                    source.volume = SoundEngine.MusicVolume(voice, now);
                    source.Play();
                    music[voice] = source;
                }
                source.volume = SoundEngine.MusicVolume(voice, now);
            }
        }

        /// <summary>
        /// Outside the Galaxy screens (no screen, no music, nothing playing) the host gives its AudioSources and listener
        /// back, so a legacy scene keeps its own audio ownership; they come back with the next sound.
        /// </summary>
        private void ReleaseWhenIdle()
        {
            if (engine.Screen != null || engine.CurrentMusic != null || engine.FadingMusic.Count > 0 || music.Count > 0) return;
            if (effects.Count == 0 && spareMusic.Count == 0 && listener == null) return;
            foreach (var source in effects) if (source.isPlaying) return;
            foreach (var source in effects) Destroy(source);
            foreach (var source in spareMusic) Destroy(source);
            effects.Clear(); spareMusic.Clear();
            if (listener != null) { Destroy(listener); listener = null; }
        }

        private AudioSource FreeEffect()
        {
            foreach (var source in effects)
                if (!source.isPlaying) return source;
            var created = NewSource();
            effects.Add(created);
            return created;
        }

        private AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.dopplerLevel = 0;
            return source;
        }

        /// <summary>Loads every effect clip once, so the first ui.tap does not wait for Resources.</summary>
        private void Preload()
        {
            if (preloaded) return;
            preloaded = true;
            foreach (var ev in engine.Catalog.Events.Values) Clip(ev.Clip);
        }

        private AudioClip Clip(string path)
        {
            if (clips.TryGetValue(path, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning("Missing sound clip Resources/" + path);
            else if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            clips[path] = clip;
            return clip;
        }

        private void EnsureListener()
        {
            if (listener != null) return;
            listener = gameObject.AddComponent<AudioListener>();
            UpdateListener();
        }

        /// <summary>One listener only: ours stays off while a scene brings its own.</summary>
        private void UpdateListener()
        {
            if (listener == null) return;
            bool other = false;
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
                if (l != listener && l.isActiveAndEnabled) { other = true; break; }
            listener.enabled = !other;
        }
    }
}
