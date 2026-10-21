using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Galaxy101.Tests
{
    public sealed class LocalizationAssetsTests
    {
        [Test] public void NativeTablesContainEveryCatalogEntryAndContextualTranslation()
        {
            var model = MenuModel.LoadResources();
            var rows = LocalizationCatalog.Load(model);
            var tables = Resources.LoadAll<StringTable>("Galaxy101/Localization");
            Assert.That(tables.Length, Is.EqualTo(2), "Run Galaxy101.Editor.LocalizationImporter.Import before acceptance tests.");
            foreach (var language in new[] { "tr", "en" })
            {
                var table = tables.Single(x => x.LocaleIdentifier.Code == language);
                foreach (var row in rows)
                {
                    var entry = table.GetEntry(row.Key);
                    Assert.That(entry, Is.Not.Null, "Missing native table key " + row.Key);
                    Assert.That(entry.LocalizedValue, Is.EqualTo(language == "tr" ? row.Tr : row.En));
                }
            }
            var english = tables.Single(x => x.LocaleIdentifier.Code == "en");
            Assert.That(english.GetEntry(rows.First(x => x.Tr == "SIRA" && x.En == "RANK").Key).LocalizedValue, Is.EqualTo("RANK"));
            Assert.That(english.GetEntry(rows.First(x => x.Tr == "SIRA" && x.En == "PLACE").Key).LocalizedValue, Is.EqualTo("PLACE"));
        }
    }
}
