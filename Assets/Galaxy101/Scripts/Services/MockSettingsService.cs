using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    public sealed class MockSettingsService : ISettingsService
    {
        private readonly MenuModel model;
        public MockSettingsService(MenuModel model) { this.model = model; }
        public JObject Load() => (JObject)model.Settings.DeepClone();
        public JObject ResetToDefaults() => model.DefaultSettings();
        public bool Save(JObject draft)
        {
            if (!SettingsValidation.IsValid(draft, model.DefaultSettings())) return false;
            var next = model.DefaultSettings();
            foreach (var property in model.DefaultSettings().Properties()) next[property.Name] = draft[property.Name].DeepClone();
            var previous = model.Settings;
            model.Settings = next;
            if (!model.Persist()) { model.Settings = previous; return false; }
            ApplyResolution((int)next["resolution"]);
            model.NotifyChanged();
            return true;
        }
        public bool SetLanguage(string language)
        {
            if (language != "tr" && language != "en") return false;
            var draft = Load(); draft["lang"] = language; return Save(draft);
        }
        public bool SetPlanet(string planet)
        {
            if (string.IsNullOrEmpty(planet) || model.Themes[planet] == null) return false;
            var previous = model.Planet;
            model.Planet = planet;
            if (!model.Persist()) { model.Planet = previous; return false; }
            model.NotifyChanged(); return true;
        }
        public bool SetResolution(int height)
        {
            if (height != 720 && height != 1080 && height != 1440) return false;
            var draft = Load(); draft["resolution"] = height; return Save(draft);
        }
        private static void ApplyResolution(int height)
        {
            if (Application.isPlaying && !Application.isEditor && !Application.isBatchMode && Screen.height != height)
                Screen.SetResolution(height * 16 / 9, height, Screen.fullScreenMode);
        }
    }
}
