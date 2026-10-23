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
        /// <summary>Görüntü modu + çözünürlük apply and save at once (the controller applies them on Changed).</summary>
        public bool SetDisplay(string mode, string resolution)
        {
            if (!GameDisplay.IsValidMode(mode) || !GameDisplay.IsValidResolution(resolution)) return false;
            var draft = Load(); draft["wmode"] = mode; draft["res"] = resolution; return Save(draft);
        }
        /// <summary>Kare hızı applies and is saved at once, like the resolution.</summary>
        public bool SetFrameRate(int choice)
        {
            if (!FramePacing.IsValidChoice(choice)) return false;
            var draft = Load(); draft["fps"] = choice; return Save(draft);
        }
    }
}
