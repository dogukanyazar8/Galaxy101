using System;
using Newtonsoft.Json.Linq;

namespace Galaxy101.Audio
{
    /// <summary>
    /// The sound settings (pkg/sound/README.md): Ana ses (master), Efektler (sfx), Müzik (music), 0–100, and the
    /// Taş sesleri (tileSnd) and Sohbet/Bildirim sesleri (chatSnd, notifSnd) toggles. Volume rule (events.json volume.rule,
    /// g101-sound.js gainFor): gain = master/100 × (music for music, sfx otherwise)/100; 0 while a category toggle is off.
    /// </summary>
    public sealed class SoundLevels
    {
        public const string MasterKey = "master", SfxKey = "sfx", MusicKey = "music", TileKey = "tileSnd", ChatKey = "chatSnd", NotifKey = "notifSnd";

        public int Master { get; }
        public int Sfx { get; }
        public int Music { get; }
        public bool TileSounds { get; }
        public bool ChatSounds { get; }
        public bool NotificationSounds { get; }

        public SoundLevels(int master, int sfx, int music, bool tileSounds, bool chatSounds, bool notificationSounds)
        {
            Master = Clamp(master); Sfx = Clamp(sfx); Music = Clamp(music);
            TileSounds = tileSounds; ChatSounds = chatSounds; NotificationSounds = notificationSounds;
        }

        /// <summary>
        /// These levels with every sound key the settings object carries (MenuModel.Settings, a table settings snapshot);
        /// keys it lacks keep their value here — the prototype's S(): defaults overlaid with the stored settings.
        /// </summary>
        public SoundLevels With(JObject settings)
        {
            if (settings == null) return this;
            return new SoundLevels(Int(settings, MasterKey, Master), Int(settings, SfxKey, Sfx), Int(settings, MusicKey, Music),
                Bool(settings, TileKey, TileSounds), Bool(settings, ChatKey, ChatSounds), Bool(settings, NotifKey, NotificationSounds));
        }

        /// <summary>A setting toggle by key; keys the rule does not know count as on (the prototype tests === false).</summary>
        public bool Toggle(string key)
        {
            switch (key)
            {
                case TileKey: return TileSounds;
                case ChatKey: return ChatSounds;
                case NotifKey: return NotificationSounds;
                default: return true;
            }
        }

        /// <summary>master/100 × channel/100, clamped to 0…1 (channel "music" uses Müzik, any other Efektler).</summary>
        public float ChannelGain(string channel)
        {
            float v = Master / 100f * ((channel == "music" ? Music : Sfx) / 100f);
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        /// <summary>The gain of a sound category: 0 if any of its toggles is off, the channel gain otherwise.</summary>
        public float Gain(SoundCatalog catalog, string category)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.Toggles.TryGetValue(category ?? "", out var keys))
                foreach (var key in keys)
                    if (!Toggle(key)) return 0;
            return ChannelGain(catalog.ChannelOf(category));
        }

        /// <summary>The music gain (g101-sound.js musicGain()).</summary>
        public float MusicGain => ChannelGain("music");

        public override bool Equals(object obj) => obj is SoundLevels o && o.Master == Master && o.Sfx == Sfx && o.Music == Music &&
            o.TileSounds == TileSounds && o.ChatSounds == ChatSounds && o.NotificationSounds == NotificationSounds;
        public override int GetHashCode() => (Master * 101 + Sfx) * 101 + Music + (TileSounds ? 1 << 24 : 0) + (ChatSounds ? 1 << 25 : 0) + (NotificationSounds ? 1 << 26 : 0);
        public override string ToString() => $"master {Master} sfx {Sfx} music {Music} tileSnd {TileSounds} chatSnd {ChatSounds} notifSnd {NotificationSounds}";

        private static int Clamp(int v) => v < 0 ? 0 : v > 100 ? 100 : v;

        private static int Int(JObject settings, string key, int fallback)
        {
            var token = settings[key];
            if (token == null) return fallback;
            if (token.Type == JTokenType.Integer) return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)token));
            if (token.Type == JTokenType.Float) return (int)Math.Round((double)token, MidpointRounding.AwayFromZero);
            return fallback;
        }

        private static bool Bool(JObject settings, string key, bool fallback) =>
            settings[key] is JValue value && value.Type == JTokenType.Boolean ? (bool)value : fallback;
    }
}
