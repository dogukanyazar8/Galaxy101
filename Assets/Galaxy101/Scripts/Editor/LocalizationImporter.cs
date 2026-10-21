using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Galaxy101.Editor
{
    public static class LocalizationImporter
    {
        private const string Folder = "Assets/Galaxy101/Resources/Galaxy101/Localization";
        [MenuItem("Galaxy 101/Import TR-EN Localization")]
        public static void Import()
        {
            Directory.CreateDirectory(Folder);
            ExtractAdditionalStrings();
            AssetDatabase.Refresh();
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }
            var locales = new[] { "tr", "en" }.Select(code =>
            {
                string path = Folder + "/Locale-" + code + ".asset";
                var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
                if (locale == null) { locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale, path); }
                if (!LocalizationEditorSettings.GetLocales().Any(x => x.Identifier == locale.Identifier)) LocalizationEditorSettings.AddLocale(locale);
                return locale;
            }).ToList();
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.Clear(); selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier("tr") });
            EditorUtility.SetDirty(settings);
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationBridge.TableName) ??
                LocalizationEditorSettings.CreateStringTableCollection(LocalizationBridge.TableName, Folder, locales);
            var rows = LocalizationCatalog.Load(MenuModel.LoadResources());
            foreach (var locale in locales)
            {
                var table = collection.GetTable(locale.Identifier) as UnityEngine.Localization.Tables.StringTable ??
                    (UnityEngine.Localization.Tables.StringTable)collection.AddNewTable(locale.Identifier);
                foreach (var row in rows) table.AddEntry(row.Key, locale.Identifier.Code == "tr" ? row.Tr : row.En);
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            Debug.Log("Galaxy101 localization imported: " + rows.Count + " unique entries per language; every source CSV row retained.");
        }

        private static void ExtractAdditionalStrings()
        {
            var output = new StringBuilder("\"key\",\"tr\",\"en\"\n");
            var pattern = new Regex("\\b(?:L|Text)\\(\\s*\"(?<tr>(?:[^\"\\\\]|\\\\.)*)\"\\s*,\\s*\"(?<en>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
            int index = 0;
            foreach (string path in Directory.GetFiles("Assets/Galaxy101/Scripts", "*.cs", SearchOption.AllDirectories).OrderBy(x => x, System.StringComparer.Ordinal))
                foreach (Match match in pattern.Matches(File.ReadAllText(path)))
                {
                    string tr = Regex.Unescape(match.Groups["tr"].Value), en = Regex.Unescape(match.Groups["en"].Value);
                    output.Append("\"source.").Append(index++).Append("\",\"").Append(tr.Replace("\"", "\"\"")).Append("\",\"")
                        .Append(en.Replace("\"", "\"\"")).Append("\"\n");
                }
            string target = "Assets/Galaxy101/Resources/Galaxy101/additional_strings.csv";
            if (!File.Exists(target) || File.ReadAllText(target) != output.ToString()) File.WriteAllText(target, output.ToString(), new UTF8Encoding(false));
        }
    }
}
