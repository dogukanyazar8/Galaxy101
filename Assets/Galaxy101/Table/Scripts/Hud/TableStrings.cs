using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table texts from the design handoff's string table (spec/data/table_strings_ek_tr_en.csv, copied to
    /// Resources/Galaxy101/Table/Strings). Keys with {0}…{n} placeholders are formatted with the given arguments.
    /// </summary>
    public static class TableStrings
    {
        private static Dictionary<string, (string Tr, string En)> table;

        public static string Get(string key, string language, params object[] args)
        {
            Load();
            if (!table.TryGetValue(key, out var entry)) throw new KeyNotFoundException("Missing table string: " + key);
            string text = language == "en" ? entry.En : entry.Tr;
            return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
        }

        public static bool Has(string key) { Load(); return table.ContainsKey(key); }

        private static void Load()
        {
            if (table != null) return;
            var asset = Resources.Load<TextAsset>("Galaxy101/Table/Strings/table_strings_ek") ?? throw new InvalidOperationException("Missing table string resource.");
            table = new Dictionary<string, (string, string)>();
            foreach (var row in Rows(asset.text))
                if (row.Count >= 3 && row[0] != "key") table[row[0]] = (row[1], row[2]);
        }

        /// <summary>Minimal RFC 4180 reader: quoted fields, doubled quotes, commas inside quotes.</summary>
        private static IEnumerable<List<string>> Rows(string csv)
        {
            var row = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"') { if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; } else quoted = false; }
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Clear(); yield return row; row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row; }
        }
    }
}
