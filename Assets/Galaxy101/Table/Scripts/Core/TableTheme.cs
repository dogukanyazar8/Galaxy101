using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>Planet colours the table recolours its models with (table_content.json → planets_table).</summary>
    public sealed class TableTheme
    {
        public string Planet { get; private set; }
        public Color A1, A2, F1, F2, Rim, Leather, Tile, Sky, Bg1, Bg2, Bg3, Pg;

        public static TableTheme Load(string planet)
        {
            var asset = Resources.Load<TextAsset>("Galaxy101/Table/table_content");
            if (asset == null) throw new InvalidOperationException("Missing Resources/Galaxy101/Table/table_content.json");
            var planets = (JObject)JObject.Parse(asset.text)["planets_table"];
            var data = (JObject)planets[planet] ?? throw new ArgumentException("Unknown planet theme: " + planet, nameof(planet));
            Color C(string key) => Parse((string)data[key]) ?? throw new FormatException(planet + "." + key);
            // The export page / prototype theme object maps leather → pn, tile → tf1, sky → st, glow → a2.
            return new TableTheme
            {
                Planet = planet, A1 = C("a1"), A2 = C("a2"), F1 = C("f1"), F2 = C("f2"), Rim = C("rim"),
                Leather = C("pn"), Tile = C("tf1"), Sky = C("st"), Bg1 = C("bg1"), Bg2 = C("bg2"), Bg3 = C("bg3"), Pg = C("pg"),
            };
        }

        /// <summary>#rgb / #rrggbb or rgba(r,g,b,a) as written in the design tokens.</summary>
        public static Color? Parse(string css)
        {
            if (string.IsNullOrEmpty(css)) return null;
            if (ColorUtility.TryParseHtmlString(css, out var c)) return c;
            if (!css.StartsWith("rgb", StringComparison.Ordinal)) return null;
            var parts = css.Substring(css.IndexOf('(') + 1).TrimEnd(')').Split(',');
            float P(int i) => float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
            return new Color(P(0) / 255f, P(1) / 255f, P(2) / 255f, parts.Length > 3 ? P(3) : 1);
        }
    }
}
