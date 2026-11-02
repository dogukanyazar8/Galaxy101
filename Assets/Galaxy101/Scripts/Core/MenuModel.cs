using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Menu state. Account/economy actions are local mocks; only preferences and profile persist.</summary>
    public sealed class MenuModel
    {
        public JObject Content { get; }
        public JObject Themes { get; }
        public JObject Settings { get; set; }
        public JObject Profile { get; set; }
        public int SelectedMode { get; set; }
        public string Planet { get; set; } = "Satürn";
        public long Chips { get; set; } = 125400;
        public long Gems { get; set; } = 340;
        /// <summary>Player level and XP inside it (bot tables award XP; the level needs 500 × level XP, table_rules.json).</summary>
        public int Level { get; set; } = 24;
        public long Xp { get; set; } = 6240; // level 24 of 375 × level: 6.240 / 9.000 (profile reference)
        public HashSet<string> Invited { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> RequestsHandled { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> SentRequests { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<int> QuestsClaimed { get; } = new HashSet<int>();
        public HashSet<string> OwnedSkins { get; } = new HashSet<string>(StringComparer.Ordinal);
        public bool DayClaimed { get; set; }
        /// <summary>The player's friend code (the signed-in account's; the design's sample otherwise).</summary>
        public string FriendCode { get; set; } = "DY-4821";
        public string LastPersistenceError { get; private set; }
        public event Action Changed;
        private readonly string persistencePath;

        public MenuModel(string contentJson, string themesJson, string persistencePath = null)
        {
            Content = JObject.Parse(contentJson);
            Themes = JObject.Parse(themesJson);
            this.persistencePath = persistencePath;
            Settings = DefaultSettings();
            Profile = new JObject { ["name"] = "Doğukan Y.", ["color"] = "#FF8A5B", ["status"] = "", ["frame"] = 0 };
            if (Content["dailyQuests"] is JArray quests)
                for (int i = 0; i < quests.Count; i++)
                    if ((bool?)quests[i]["pre"] == true) QuestsClaimed.Add(i);
            Restore();
        }

        public static MenuModel LoadResources(string persistencePath = null)
        {
            var content = Resources.Load<TextAsset>("Galaxy101/content");
            var themes = Resources.Load<TextAsset>("Galaxy101/themes");
            if (content == null || themes == null) throw new InvalidOperationException("Galaxy101 content and theme resources are missing.");
            return new MenuModel(content.text, themes.text, GalaxyPreferencesBridge.ResolveStatePath(persistencePath));
        }

        public static MenuModel LoadsResources() => LoadResources();
        public JObject DefaultSettings()
        {
            var defaults = (JObject)Content["settingsDefaults"].DeepClone();
            defaults["wmode"] = "fullscreen"; // spec/19: Tam ekran / Çerçevesiz / Pencere
            defaults["res"] = GameDisplay.Native; // or "1920x1080"
            defaults["fps"] = FramePacing.Auto; // Kare hızı: 0 Otomatik, −1 Sınırsız, 30–1000 üst sınır
            defaults["showFps"] = false;
            return defaults;
        }

        public void NotifyChanged() => Changed?.Invoke();

        public bool Persist()
        {
            LastPersistenceError = null;
            if (string.IsNullOrEmpty(persistencePath)) return true;
            if (!SettingsValidation.IsValid(Settings, DefaultSettings()) ||
                !ProfileValidation.IsValid(Profile) || Themes[Planet ?? ""] == null ||
                SelectedMode < 0 || SelectedMode >= ((JArray)Content["modes"]).Count)
            {
                LastPersistenceError = "Menu preferences contain invalid values.";
                return false;
            }
            string temporary = persistencePath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(persistencePath));
                Directory.CreateDirectory(directory);
                var state = new JObject { ["version"] = StateVersion, ["Settings"] = Settings.DeepClone(), ["Profile"] = Profile.DeepClone(),
                    ["SelectedMode"] = SelectedMode, ["Planet"] = Planet, ["Progress"] = new JObject { ["level"] = Level, ["xp"] = Xp } };
                File.WriteAllText(temporary, state.ToString(Formatting.Indented));
                if (File.Exists(persistencePath)) File.Replace(temporary, persistencePath, null);
                else File.Move(temporary, persistencePath);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                LastPersistenceError = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
                { LastPersistenceError = LastPersistenceError ?? ex.GetType().Name + ": " + ex.Message; }
            }
        }

        /// <summary>Saved-state format: 2 resets the version-1 "Çift tıkla at" default (see Restore).</summary>
        private const int StateVersion = 2;

        private void Restore()
        {
            if (string.IsNullOrEmpty(persistencePath) || !File.Exists(persistencePath)) return;
            try
            {
                if (new FileInfo(persistencePath).Length > 1024 * 1024) { LastPersistenceError = "Saved menu preferences exceed the size limit."; return; }
                using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(persistencePath))) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
                {
                    var state = JObject.Load(reader);
                    if (state["Settings"] is JObject settings)
                    {
                        var merged = DefaultSettings();
                        int version = SettingsValidation.TryInteger(state["version"], out long v) ? (int)v : 1;
                        foreach (var key in merged.Properties().Select(x => x.Name).ToArray())
                        {
                            // Version 1 saved "dbl" while its default was still false (the design's default is on):
                            // those files carry the old default, not a choice, so it starts from the design's again.
                            if (version < 2 && key == "dbl") continue;
                            if (settings[key] != null) merged[key] = settings[key].DeepClone();
                        }
                        if (SettingsValidation.IsValid(merged, DefaultSettings())) Settings = merged;
                    }
                    if (state["Profile"] is JObject profile && ProfileValidation.IsValid(profile))
                        Profile = ProfileValidation.Sanitize(profile);
                    if (SettingsValidation.TryInteger(state["SelectedMode"], out long mode) && mode >= 0 && mode < ((JArray)Content["modes"]).Count)
                        SelectedMode = (int)mode;
                    if (state["Planet"]?.Type == JTokenType.String && Themes[(string)state["Planet"]] != null) Planet = (string)state["Planet"];
                    if (state["Progress"] is JObject progress && SettingsValidation.TryInteger(progress["level"], out long level) && level >= 1 && level <= 999 &&
                        SettingsValidation.TryInteger(progress["xp"], out long xp) && xp >= 0 && xp < 500L * level)
                    { Level = (int)level; Xp = xp; }
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { LastPersistenceError = ex.GetType().Name + ": " + ex.Message; }
        }
    }
}
