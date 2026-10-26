using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table texts from the design handoff's string tables, copied to Resources/Galaxy101/Table/Strings:
    /// table_strings (spec/data/table_strings_tr_en.csv), table_strings_ek (the extra-states handoff, wins on a clash) and
    /// table_strings_extra (keys the reference page only has in script: tile styles). Keys with {0}…{n} placeholders are
    /// formatted with the given arguments.
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
            table = new Dictionary<string, (string, string)>();
            foreach (var name in new[] { "table_strings", "table_strings_ek", "table_strings_extra" })
            {
                var asset = Resources.Load<TextAsset>("Galaxy101/Table/Strings/" + name) ?? throw new InvalidOperationException("Missing table string resource: " + name);
                foreach (var row in Rows(asset.text))
                    if (row.Count >= 3 && row[0] != "key") table[row[0]] = (row[1], row[2]);
            }
            // The v4 package's flat CSV has a distinct Resources key from strings_v4.json (the lobby's JSON).
            // ("app.STR.ldMsg.0", "table.STR.quick.0", …); the table's own tables above keep their keys.
            var v4 = Resources.Load<TextAsset>("Galaxy101/Strings/strings_v4_flat");
            if (v4 != null)
                foreach (var row in Rows(v4.text.TrimStart((char)0xFEFF)))
                    if (row.Count >= 3 && row[0] != "key" && !table.ContainsKey(row[0])) table[row[0]] = (row[1], row[2]);
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
