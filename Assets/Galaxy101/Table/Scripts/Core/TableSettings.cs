using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table settings (spec/13 §6, T15–T18): every change applies at once and is kept. Defaults come from
    /// table_content.json → settingsDefaults (nulls resolved to the design's first choices). The language and the master
    /// volume are the menu's own settings, so both screens share them; the rest lives in PlayerPrefs.
    /// </summary>
    public sealed class TableSettings
    {
        public const string PrefsKey = "galaxy101.table.settings";
        public static readonly int[] WarnChoices = { 3, 5, 10, 0 };
        public static readonly string[] Views = { "seat", "mid", "top", "2d" };

        private readonly JObject values;
        private readonly MenuModel menu;
        private readonly bool persist;

        /// <summary>Raised after a change with its key; null after "Varsayılana dön" (everything may have changed).</summary>
        public event Action<string> Changed;

        /// <param name="persist">false keeps the values in memory only (tests).</param>
        /// <summary>Diagnostics (design comparisons): the design defaults, the player's saved choices neither read nor written.</summary>
        public static bool DesignDefaultsOnly;

        public TableSettings(MenuModel menu, bool persist = true)
        {
            if (DesignDefaultsOnly) persist = false;
            this.menu = menu; this.persist = persist;
            values = Defaults();
            if (!persist || !PlayerPrefs.HasKey(PrefsKey)) return;
            try
            {
                var stored = JObject.Parse(PlayerPrefs.GetString(PrefsKey));
                foreach (var property in stored.Properties())
                    if (values[property.Name] != null && Valid(property.Name, property.Value)) values[property.Name] = property.Value;
            }
            catch (Exception ex) { Debug.LogWarning("Ignoring unreadable table settings: " + ex.Message); }
        }

        public static JObject Defaults()
        {
            var content = JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Table/table_content").text);
            var defaults = (JObject)content["settingsDefaults"].DeepClone();
            defaults.Remove("lang"); defaults.Remove("master");
            defaults["view"] = Views[0];
            defaults["tableSkin"] = TableAssetCatalog.TableSkins[0];
            defaults["rackSkin"] = TableAssetCatalog.RackSkins[0];
            defaults["tileSkin"] = TableAssetCatalog.TileSkins[0];
            defaults["bombFx"] = "nuke";
            return defaults;
        }

        public bool Bool(string key) => (bool)(menu?.Settings[key] ?? values[key] ?? throw new ArgumentException(key));
        public int Int(string key) => menu?.Settings[key] != null ? (int)menu.Settings[key] : key == "master" ? 80 : key == "fps" ? FramePacing.Auto : (int)values[key];
        /// <summary>Kare hızı (menu setting, shared): <see cref="FramePacing.Auto"/>, <see cref="FramePacing.Unlimited"/> or a cap.</summary>
        public int FrameRate => Int("fps");
        public string Text(string key) => key == "lang" ? ((string)menu?.Settings["lang"] == "en" ? "en" : "tr") : (string)(menu?.Settings[key] ?? values[key]);

        public void NotifySharedSettingsChanged(string key = null) => Changed?.Invoke(key);

        public bool AutoSort => Bool("autoSort");
        public bool DoubleClick => Bool("dbl");
        public int WarnSeconds => Int("warn");
        public bool Bubbles => Bool("bubbles");
        public bool GiftNotices => Bool("giftNotif");
        public bool EmojiEffects => Bool("emojiFx");
        public bool TurnHints => Bool("turnHint");
        public string View => Text("view");
        public string TableSkin => Text("tableSkin");
        public string RackSkin => Text("rackSkin");
        public string TileSkin => Text("tileSkin");

        public void Set(string key, JToken value)
        {
            if (!Valid(key, value)) throw new ArgumentException("Invalid table setting " + key + " = " + value);
            if (menu?.Settings[key] != null || key == "lang" || key == "master" || key == "fps") { if (!SetMenu(key, value)) return; if (key == "fps") FramePacing.Apply((int)value); }
            else
            {
                if (JToken.DeepEquals(values[key], value)) return;
                values[key] = value; Save();
            }
            Changed?.Invoke(key);
        }

        /// <summary>"Varsayılana dön": every row back to the design default, the language to Türkçe, the master volume to 80 and the frame rate to Otomatik.</summary>
        public void Reset()
        {
            var defaults = Defaults();
            if (menu != null)
            {
                var previous = menu.Settings; var next = (JObject)previous.DeepClone(); var shared = menu.DefaultSettings();
                foreach (var property in defaults.Properties())
                    if (next[property.Name] != null && shared[property.Name] != null) next[property.Name] = shared[property.Name].DeepClone();
                next["lang"] = "tr"; next["master"] = 80; next["fps"] = FramePacing.Auto;
                menu.Settings = next;
                if (!menu.Persist()) { menu.Settings = previous; Debug.LogWarning("Table settings could not reset: " + menu.LastPersistenceError); return; }
            }
            foreach (var property in defaults.Properties()) values[property.Name] = property.Value;
            Save(); menu?.NotifyChanged();
            if (FramePacing.Choice != FramePacing.Auto) FramePacing.Apply(FramePacing.Auto);
            Changed?.Invoke(null);
        }

        private bool SetMenu(string key, JToken value)
        {
            if (menu == null) return false;
            if (JToken.DeepEquals(menu.Settings[key], value)) return false;
            if (key == "master")
            {
                // Dragged, the master volume changes every frame: it applies at once, the menu file is written once the
                // slider rests (half a second after the last change) instead of per frame.
                menu.Settings[key] = value; menu.NotifyChanged();
                persistPending = true; if (!persistScheduled) PersistLater();
                return true;
            }
            var previous = menu.Settings[key];
            menu.Settings[key] = value;
            if (!menu.Persist()) { menu.Settings[key] = previous; Debug.LogWarning("Table settings could not save " + key + ": " + menu.LastPersistenceError); return false; }
            menu.NotifyChanged();
            return true;
        }

        private bool persistPending, persistScheduled;
        private async void PersistLater()
        {
            persistScheduled = true;
            do { persistPending = false; await System.Threading.Tasks.Task.Delay(500); } while (persistPending);
            persistScheduled = false;
            if (!menu.Persist()) Debug.LogWarning("Table settings could not save master: " + menu.LastPersistenceError);
        }

        // A dragged slider changes its value every frame: the value is set each time, the disk written at most once a
        // second (Unity writes PlayerPrefs on quit as well, so the last change is never lost).
        private float savedAt = float.NegativeInfinity;
        private void Save()
        {
            if (!persist) return;
            PlayerPrefs.SetString(PrefsKey, values.ToString(Newtonsoft.Json.Formatting.None));
            if (Time.realtimeSinceStartup - savedAt < 1) return;
            savedAt = Time.realtimeSinceStartup; PlayerPrefs.Save();
        }

        private static readonly HashSet<string> Volumes = new HashSet<string> { "master", "music", "sfx" };

        private static bool Valid(string key, JToken value)
        {
            if (value == null) return false;
            switch (key)
            {
                case "lang": return value.Type == JTokenType.String && ((string)value == "tr" || (string)value == "en");
                case "fps": return value.Type == JTokenType.Integer && FramePacing.IsValidChoice((long)value);
                case "warn": return value.Type == JTokenType.Integer && Array.IndexOf(WarnChoices, (int)value) >= 0;
                case "view": return value.Type == JTokenType.String && Array.IndexOf(Views, (string)value) >= 0;
                case "bombFx": return value.Type == JTokenType.String && Array.IndexOf(new[] { "nuke", "blackhole", "supernova", "meteor" }, (string)value) >= 0;
                case "tableSkin": return value.Type == JTokenType.String && Array.IndexOf(TableAssetCatalog.TableSkins, (string)value) >= 0;
                case "rackSkin": return value.Type == JTokenType.String && Array.IndexOf(TableAssetCatalog.RackSkins, (string)value) >= 0;
                case "tileSkin": return value.Type == JTokenType.String && Array.IndexOf(TableAssetCatalog.TileSkins, (string)value) >= 0;
                default:
                    if (Volumes.Contains(key)) return value.Type == JTokenType.Integer && (int)value >= 0 && (int)value <= 100;
                    return value.Type == JTokenType.Boolean;
            }
        }
    }
}
