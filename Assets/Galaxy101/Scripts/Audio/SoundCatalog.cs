using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Audio
{
    /// <summary>One sound event of the v4 catalogue (pkg/sound/events.json, g101-sound.js EVENTS).</summary>
    public sealed class SoundEvent
    {
        public string Id { get; internal set; }
        /// <summary>ui, game, tile or chat: picks the volume channel and the setting toggles.</summary>
        public string Category { get; internal set; }
        /// <summary>prio: an event at or above <see cref="DuckRule.MinPriority"/> ducks the music.</summary>
        public int Priority { get; internal set; }
        /// <summary>poly: at most this many copies of the event at once.</summary>
        public int Poly { get; internal set; }
        /// <summary>ms: the v4 concurrency window, independent of the clip's recorded or synthesized duration.</summary>
        public int Ms { get; internal set; }
        /// <summary>Resources path of the clip (Galaxy101/Audio/Sfx/… or RecordedTiles/… for EK-413).</summary>
        public string Clip { get; internal set; }
        public float WavSeconds { get; internal set; }
        public string Trigger { get; internal set; }
    }

    /// <summary>A music loop (pkg/sound/music, g101-sound.js MUSIC).</summary>
    public sealed class MusicTrack
    {
        public string Id { get; internal set; }
        public string Clip { get; internal set; }
        public bool Loop { get; internal set; }
        public float Seconds { get; internal set; }
    }

    /// <summary>mixing.duck: a loud event pulls the music down, then lets it back up (g101-sound.js duck()).</summary>
    public sealed class DuckRule
    {
        public int MinPriority { get; internal set; }
        /// <summary>Music level while ducked, as a share of the music gain.</summary>
        public float MusicLevel { get; internal set; }
        public float DownSeconds { get; internal set; }
        /// <summary>Seconds after the duck started when the music is back at full gain.</summary>
        public float RestoreAtSeconds { get; internal set; }
    }

    /// <summary>
    /// Galaxy101/Audio/sound_events.json (lane D's copy of pkg/sound/events.json + g101-sound.js): events, music, the
    /// volume rule's channels and toggles, the defaults, mixing (duck, cross-fade), the engine event map and the screen
    /// music. Every number the audio code uses comes from here.
    /// </summary>
    public sealed class SoundCatalog
    {
        public const string ResourcePath = "Galaxy101/Audio/sound_events";

        public IReadOnlyDictionary<string, SoundEvent> Events { get; private set; }
        public IReadOnlyDictionary<string, MusicTrack> Music { get; private set; }
        /// <summary>Category → volume channel ("sfx" or "music").</summary>
        public IReadOnlyDictionary<string, string> Channels { get; private set; }
        /// <summary>Category → setting keys that must all be on for the category to sound.</summary>
        public IReadOnlyDictionary<string, string[]> Toggles { get; private set; }
        /// <summary>volume.defaults: the sound engine's values while no setting is stored.</summary>
        public SoundLevels Defaults { get; private set; }
        /// <summary>volume.opponentDrawVolume: an opponent's draw plays tile.draw at this share.</summary>
        public float OpponentDrawVolume { get; private set; }
        public DuckRule Duck { get; private set; }
        public float CrossfadeOutSeconds { get; private set; }
        public float FadeInSeconds { get; private set; }
        /// <summary>mixing.sync: sound id → the animation length (ms) it must start with.</summary>
        public IReadOnlyDictionary<string, int> SyncMs { get; private set; }
        /// <summary>engineEvents: rules engine / protocol event → candidate sound ids (one unless a rule picks).</summary>
        public IReadOnlyDictionary<string, string[]> EngineEvents { get; private set; }
        /// <summary>screenMusic: screen → music id; "*" for every other screen.</summary>
        public IReadOnlyDictionary<string, string> ScreenMusic { get; private set; }
        public string ScreenChangeSound { get; private set; }
        public IReadOnlyList<string> ScreenChangeSkipFrom { get; private set; }

        private static SoundCatalog shared;

        /// <summary>The catalogue from Resources (parsed once).</summary>
        public static SoundCatalog Load()
        {
            if (shared != null) return shared;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing sound catalogue: Resources/" + ResourcePath + ".json");
            return shared = Parse(asset.text);
        }

        public static SoundCatalog Parse(string json)
        {
            var root = JObject.Parse(json);
            var catalog = new SoundCatalog();

            var events = new Dictionary<string, SoundEvent>(StringComparer.Ordinal);
            foreach (var property in Require<JObject>(root, "events").Properties())
            {
                var e = (JObject)property.Value;
                events[property.Name] = new SoundEvent
                {
                    Id = property.Name,
                    Category = Text(e, "cat"),
                    Priority = (int)Require<JValue>(e, "prio"),
                    Poly = (int)Require<JValue>(e, "poly"),
                    Ms = (int)Require<JValue>(e, "ms"),
                    Clip = Text(e, "clip"),
                    WavSeconds = (float?)e["wavSeconds"] ?? 0,
                    Trigger = (string)e["trigger"],
                };
            }
            catalog.Events = events;

            var music = new Dictionary<string, MusicTrack>(StringComparer.Ordinal);
            foreach (var property in Require<JObject>(root, "music").Properties())
            {
                var m = (JObject)property.Value;
                music[property.Name] = new MusicTrack { Id = property.Name, Clip = Text(m, "clip"), Loop = (bool?)m["loop"] ?? true, Seconds = (float?)m["seconds"] ?? 0 };
            }
            catalog.Music = music;

            catalog.Channels = Require<JObject>(root, "channels").Properties().ToDictionary(p => p.Name, p => (string)p.Value, StringComparer.Ordinal);
            catalog.Toggles = Require<JObject>(root, "toggles").Properties().ToDictionary(p => p.Name, p => p.Value.Values<string>().ToArray(), StringComparer.Ordinal);

            var volume = Require<JObject>(root, "volume");
            var d = Require<JObject>(volume, "defaults");
            catalog.Defaults = new SoundLevels((int)Require<JValue>(d, "master"), (int)Require<JValue>(d, "sfx"), (int)Require<JValue>(d, "music"),
                (bool?)d["tileSnd"] ?? true, (bool?)d["chatSnd"] ?? true, (bool?)d["notifSnd"] ?? true);
            catalog.OpponentDrawVolume = (float)Require<JValue>(volume, "opponentDrawVolume");

            var mixing = Require<JObject>(root, "mixing");
            var duck = Require<JObject>(mixing, "duck");
            catalog.Duck = new DuckRule
            {
                MinPriority = (int)Require<JValue>(duck, "minPrio"),
                MusicLevel = (float)Require<JValue>(duck, "musicLevel"),
                DownSeconds = (float)Require<JValue>(duck, "downSeconds"),
                RestoreAtSeconds = (float)Require<JValue>(duck, "restoreAtSeconds"),
            };
            catalog.CrossfadeOutSeconds = (float)Require<JValue>(mixing, "crossfadeOutSeconds");
            catalog.FadeInSeconds = (float)Require<JValue>(mixing, "fadeInSeconds");
            catalog.SyncMs = (mixing["sync"] as JObject ?? new JObject()).Properties().ToDictionary(p => p.Name, p => (int)p.Value, StringComparer.Ordinal);

            catalog.EngineEvents = Require<JObject>(root, "engineEvents").Properties().ToDictionary(
                p => p.Name,
                p => p.Value["sound"] is JArray list ? list.Values<string>().ToArray() : new[] { (string)p.Value["sound"] },
                StringComparer.Ordinal);
            catalog.ScreenMusic = Require<JObject>(root, "screenMusic").Properties().ToDictionary(p => p.Name, p => (string)p.Value, StringComparer.Ordinal);
            var change = Require<JObject>(root, "screenChange");
            catalog.ScreenChangeSound = Text(change, "sound");
            catalog.ScreenChangeSkipFrom = (change["skipFrom"] as JArray)?.Values<string>().ToArray() ?? Array.Empty<string>();

            foreach (var e in events.Values)
                if (!catalog.Channels.ContainsKey(e.Category)) throw new InvalidOperationException("Sound event " + e.Id + " has an unknown category " + e.Category);
            foreach (var pair in catalog.EngineEvents)
                foreach (var id in pair.Value)
                    if (!events.ContainsKey(id)) throw new InvalidOperationException("Engine event " + pair.Key + " maps to an unknown sound " + id);
            foreach (var id in catalog.ScreenMusic.Values)
                if (!music.ContainsKey(id)) throw new InvalidOperationException("Screen music " + id + " is not a music track");
            if (!events.ContainsKey(catalog.ScreenChangeSound)) throw new InvalidOperationException("Unknown screen change sound " + catalog.ScreenChangeSound);
            return catalog;
        }

        /// <summary>The volume channel of a category: "music" or "sfx" (g101-sound.js CAT).</summary>
        public string ChannelOf(string category) => category != null && Channels.TryGetValue(category, out var channel) ? channel : "sfx";

        /// <summary>The music a screen plays (screenMusic; "*" for any other screen); null without a screen.</summary>
        public string MusicFor(string screen)
        {
            if (screen == null) return null;
            if (ScreenMusic.TryGetValue(screen, out var id)) return id;
            return ScreenMusic.TryGetValue("*", out id) ? id : null;
        }

        private static T Require<T>(JObject parent, string key) where T : JToken =>
            parent[key] as T ?? throw new InvalidOperationException("Sound catalogue: missing " + key);

        private static string Text(JObject parent, string key) =>
            (string)parent[key] ?? throw new InvalidOperationException("Sound catalogue: missing " + key);
    }
}
