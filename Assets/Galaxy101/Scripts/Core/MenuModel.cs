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
        public HashSet<string> Invited { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> RequestsHandled { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> SentRequests { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<int> QuestsClaimed { get; } = new HashSet<int>();
        public HashSet<string> OwnedSkins { get; } = new HashSet<string>(StringComparer.Ordinal);
        public bool DayClaimed { get; set; }
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
            defaults["resolution"] = 720;
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
                var state = new JObject { ["version"] = 1, ["Settings"] = Settings.DeepClone(), ["Profile"] = Profile.DeepClone(),
                    ["SelectedMode"] = SelectedMode, ["Planet"] = Planet };
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
                        foreach (var key in merged.Properties().Select(x => x.Name).ToArray())
                            if (settings[key] != null) merged[key] = settings[key].DeepClone();
                        if (SettingsValidation.IsValid(merged, DefaultSettings())) Settings = merged;
                    }
                    if (state["Profile"] is JObject profile && ProfileValidation.IsValid(profile))
                        Profile = ProfileValidation.Sanitize(profile);
                    if (SettingsValidation.TryInteger(state["SelectedMode"], out long mode) && mode >= 0 && mode < ((JArray)Content["modes"]).Count)
                        SelectedMode = (int)mode;
                    if (state["Planet"]?.Type == JTokenType.String && Themes[(string)state["Planet"]] != null) Planet = (string)state["Planet"];
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { LastPersistenceError = ex.GetType().Name + ": " + ex.Message; }
        }
    }
}
