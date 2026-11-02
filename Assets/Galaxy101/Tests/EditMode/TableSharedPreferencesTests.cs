using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class TableSharedPreferencesTests
    {
        private static MenuModel Menu() => new MenuModel(Resources.Load<TextAsset>("Galaxy101/content").text, Resources.Load<TextAsset>("Galaxy101/themes").text);

        [Test]
        public void TableReadsTheCanonicalMenuPreferencesAfterAnExternalEdit()
        {
            var menu = Menu(); var settings = new TableSettings(menu, false);
            menu.Settings["dbl"] = false; menu.Settings["music"] = 61; menu.Settings["warn"] = 10;
            settings.NotifySharedSettingsChanged();
            Assert.That(settings.DoubleClick, Is.False);
            Assert.That(settings.Int("music"), Is.EqualTo(61));
            Assert.That(settings.WarnSeconds, Is.EqualTo(10));
        }

        [Test]
        public void TablePreferenceWritesReachTheMenuWithoutOverwritingViewOrSkins()
        {
            var menu = Menu(); var settings = new TableSettings(menu, false);
            settings.Set("view", "2d"); settings.Set("tileSkin", "neon");
            settings.Set("dbl", true); settings.Set("music", 37);
            Assert.That((bool)menu.Settings["dbl"], Is.True);
            Assert.That((int)menu.Settings["music"], Is.EqualTo(37));
            Assert.That(settings.View, Is.EqualTo("2d"));
            Assert.That(settings.TileSkin, Is.EqualTo("neon"));
        }

        [Test]
        public void AVersionOneSaveStartsDoubleClickFromTheDesignDefault()
        {
            // Version-1 files stored "dbl": false while that was the default; a version-2 file keeps the player's choice.
            string Load(int version)
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "galaxy-dbl-" + System.Guid.NewGuid().ToString("N") + ".json");
                System.IO.File.WriteAllText(path, "{\"version\":" + version + ",\"Settings\":{\"dbl\":false,\"music\":44}}");
                try
                {
                    var model = new MenuModel(Resources.Load<TextAsset>("Galaxy101/content").text, Resources.Load<TextAsset>("Galaxy101/themes").text, path);
                    return (bool)model.Settings["dbl"] + "/" + (int)model.Settings["music"];
                }
                finally { System.IO.File.Delete(path); }
            }
            Assert.That(Load(1), Is.EqualTo("True/44"));
            Assert.That(Load(2), Is.EqualTo("False/44"));
        }

        [Test]
        public void ResetRestoresSharedMenuDefaultsAndTheTableViewTogether()
        {
            var menu = Menu(); var settings = new TableSettings(menu, false);
            menu.Settings["dbl"] = false; menu.Settings["music"] = 17; menu.Settings["warn"] = 10;
            settings.Set("view", "2d"); settings.Set("tileSkin", "neon");
            settings.Reset();
            Assert.That(settings.DoubleClick, Is.True, "The shared default is the table's (G101-Masa SET_DEFAULTS dbl: true): double-click discards out of the box.");
            Assert.That(settings.Int("music"), Is.EqualTo(60));
            Assert.That(settings.WarnSeconds, Is.EqualTo(5));
            Assert.That(settings.View, Is.EqualTo("seat"));
            Assert.That(settings.TileSkin, Is.EqualTo("classic"));
        }
    }
}
