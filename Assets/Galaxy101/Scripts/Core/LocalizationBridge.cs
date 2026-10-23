using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Galaxy101
{
    public sealed class LocalizationRow
    {
        public string Key; public string OriginalKey; public string Tr; public string En;
    }
    public static class LocalizationCatalog
    {
        public static List<LocalizationRow> Parse(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            var records = new List<List<string>>();
            var record = new List<string>(); var field = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { record.Add(field.ToString()); field.Clear(); }
                else if ((c == '\r' || c == '\n') && !quoted)
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    record.Add(field.ToString()); field.Clear();
                    if (record.Any(x => x.Length > 0)) records.Add(record);
                    record = new List<string>();
                }
                else field.Append(c);
            }
            if (quoted) throw new FormatException("Localization CSV contains an unclosed quoted field.");
            if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
            if (records.Count == 0 || records[0].Count != 3 || records[0][0].TrimStart('\uFEFF') != "key" || records[0][1] != "tr" || records[0][2] != "en")
                throw new FormatException("Localization CSV must contain key,tr,en columns.");
            var rows = new List<LocalizationRow>();
            for (int i = 1; i < records.Count; i++)
            {
                if (records[i].Count != 3) throw new FormatException("Localization CSV row " + (i + 1) + " has an invalid column count.");
                rows.Add(new LocalizationRow { Key = records[i][0] + ".row" + i.ToString("D4", CultureInfo.InvariantCulture), OriginalKey = records[i][0], Tr = records[i][1], En = records[i][2] });
            }
            return rows;
        }

        public static List<LocalizationRow> Load(MenuModel model)
        {
            var csv = Resources.Load<TextAsset>("Galaxy101/strings_tr_en");
            if (csv == null) throw new InvalidOperationException("Galaxy101 localization CSV is missing.");
            var result = Parse(csv.text);
            var pairs = new HashSet<string>(result.Select(x => x.Tr + "\0" + x.En), StringComparer.Ordinal);
            Action<string, string> add = (tr, en) =>
            {
                if (string.IsNullOrEmpty(tr) || string.IsNullOrEmpty(en) || !pairs.Add(tr + "\0" + en)) return;
                result.Add(new LocalizationRow { Key = "content.row" + result.Count.ToString("D4", CultureInfo.InvariantCulture), OriginalKey = "content", Tr = tr, En = en });
            };
            foreach (var row in result.ToArray().Where(x => x.OriginalKey == "inline-list"))
            {
                string[] tr = row.Tr.Split(new[] { "', '" }, StringSplitOptions.None), en = row.En.Split(new[] { "', '" }, StringSplitOptions.None);
                for (int i = 0; i < Math.Min(tr.Length, en.Length); i++) add(tr[i].Trim('\''), en[i].Trim('\''));
            }
            var additional = Resources.Load<TextAsset>("Galaxy101/additional_strings");
            if (additional != null) foreach (var row in Parse(additional.text)) add(row.Tr, row.En);
            Visit(model.Content, add); Visit(model.Themes, add);
            var extra = Resources.Load<TextAsset>("Galaxy101/supplemental");
            if (extra != null) Visit(JObject.Parse(extra.text), add);
            return result;
        }

        private static void Pair(JToken tr, JToken en, Action<string, string> add)
        {
            if (tr?.Type == JTokenType.String && en?.Type == JTokenType.String) add((string)tr, (string)en);
            else if (tr is JArray ta && en is JArray ea)
                for (int i = 0; i < Math.Min(ta.Count, ea.Count); i++) Pair(ta[i], ea[i], add);
            else if (tr is JObject to && en is JObject eo)
                foreach (var p in to.Properties()) Pair(p.Value, eo[p.Name], add);
        }
        private static void Visit(JToken token, Action<string, string> add)
        {
            if (token is JObject value)
            {
                Pair(value["tr"], value["en"], add);
                foreach (var p in value.Properties())
                    if (p.Name.EndsWith("_tr", StringComparison.Ordinal)) Pair(p.Value, value[p.Name.Substring(0, p.Name.Length - 3) + "_en"], add);
                if (value["st"] is JArray status && status.Count == 2) Pair(status[0], status[1], add);
                foreach (var p in value.Properties()) Visit(p.Value, add);
            }
            else if (token is JArray array) foreach (var child in array) Visit(child, add);
        }
    }
    public sealed class LocalizationBridge : IDisposable
    {
        public const string TableName = "Galaxy101 UI";
        public string Language => (string)model.Settings["lang"] == "en" ? "en" : "tr";
        public event Action Changed;
        private readonly MenuModel model;
        private readonly List<LocalizationRow> rows;
        private readonly Dictionary<string, List<LocalizationRow>> byTurkish;
        private readonly Dictionary<string, StringTable> tables;
        private string previousLanguage;
        public LocalizationBridge(MenuModel model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            rows = LocalizationCatalog.Load(model);
            byTurkish = rows.GroupBy(x => x.Tr).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
            // Load the two string tables by name: LoadAll would also deserialize the editor-only StringTableCollection in the
            // same folder, which a player cannot read ("missing script", "different serialization layout").
            tables = new Dictionary<string, StringTable>(StringComparer.Ordinal);
            foreach (var code in new[] { "tr", "en" })
            {
                var table = Resources.Load<StringTable>("Galaxy101/Localization/" + TableName + "_" + code);
                if (table != null) tables[table.LocaleIdentifier.Code] = table;
            }
            previousLanguage = Language;
            model.Changed += OnModelChanged;
            ApplyLocale();
        }
        private LocalizationRow Row(string tr, string en)
        {
            if (tr == null || !byTurkish.TryGetValue(tr, out var candidates)) return null;
            return en == null ? candidates[0] : candidates.FirstOrDefault(x => x.En == en);
        }
        public string Text(string tr, string en = null)
        {
            if (Language == "tr") return tr ?? "";
            var row = Row(tr, en);
            if (row == null) return en ?? tr ?? "";
            if (tables.TryGetValue(Language, out var table))
            {
                var entry = table.GetEntry(row.Key);
                if (entry != null) return entry.LocalizedValue;
            }
            return row.En;
        }
        public LocalizedString Localized(string tr, string en = null)
        {
            var row = Row(tr, en);
            return row == null ? null : new LocalizedString(TableName, row.Key);
        }
        public void SetLanguage(string language) => new MockSettingsService(model).SetLanguage(language);
        private void OnModelChanged()
        {
            if (Language == previousLanguage) return;
            previousLanguage = Language; ApplyLocale(); Changed?.Invoke();
        }
        private void ApplyLocale()
        {
            if (!Application.isPlaying || !LocalizationSettings.HasSettings) return;
            // By name, like the tables: a folder-wide LoadAll would deserialize every asset in it.
            var locale = Resources.Load<Locale>("Galaxy101/Localization/Locale-" + Language);
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }
        public void Dispose() => model.Changed -= OnModelChanged;
    }
    public static class NumberFormat
    {
        public static string Number(long value, string language) => value.ToString("N0", CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "tr-TR"));
        public static string Percent(int value, string language) => language == "en" ? value.ToString(CultureInfo.InvariantCulture) + "%" : "%" + value.ToString(CultureInfo.InvariantCulture);
        public static string Money(decimal value, string language) => "₺" + value.ToString("F2", CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "tr-TR"));
    }
}
