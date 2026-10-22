using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public static class ProfileValidation
    {
        public static readonly string[] AvatarColors = { "#FF8A5B", "#FF5E6C", "#E85D9A", "#8B5CF6", "#3D7BFF", "#16B6A8", "#3CCB6E", "#F5A623" };
        public static bool ValidName(string name) => name != null && Regex.IsMatch(name.Trim(), @"\A[\p{L}\p{N} ._]{3,16}\z");
        public static string Initials(string name)
        {
            var words = (name ?? "").Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            return words.Length == 0 ? "?" : string.Concat(words.Take(2).Select(x => StringInfo.GetNextTextElement(x))).ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        }
        public static bool IsValid(JObject p) => p != null && p["name"]?.Type == JTokenType.String && ValidName((string)p["name"]) &&
            p["color"]?.Type == JTokenType.String && AvatarColors.Contains((string)p["color"]) &&
            p["status"]?.Type == JTokenType.String && ((string)p["status"]).Length <= 40 &&
            SettingsValidation.TryInteger(p["frame"], out long frame) && frame >= 0 && frame <= 1;
        public static JObject Sanitize(JObject p) => new JObject { ["name"] = ((string)p["name"]).Trim(), ["color"] = (string)p["color"], ["status"] = (string)p["status"], ["frame"] = (int)p["frame"] };
    }

    public static class SettingsValidation
    {
        public static bool TryInteger(JToken value, out long number)
        {
            number = 0;
            return value?.Type == JTokenType.Integer && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }
        public static bool IsValid(JObject values, JObject defaults)
        {
            if (values == null) return false;
            foreach (var p in defaults.Properties())
            {
                var value = values[p.Name];
                if (value == null || value.Type != p.Value.Type) return false;
                if (value.Type == JTokenType.Integer)
                {
                    if (!TryInteger(value, out long number)) return false;
                    if (p.Name == "resolution") { if (number != 720 && number != 1080 && number != 1440) return false; }
                    else if (p.Name == "fps") { if (!FramePacing.IsValidChoice(number)) return false; }
                    else if (p.Name == "warn") { if (number < 1 || number > 10) return false; }
                    else if (number < 0 || number > 100) return false;
                }
                if (p.Name == "tile" && !new[] { "Küçük", "Orta", "Büyük" }.Contains((string)value)) return false;
                if (p.Name == "quality" && !new[] { "Düşük", "Orta", "Yüksek" }.Contains((string)value)) return false;
                if (p.Name == "lang" && (string)value != "tr" && (string)value != "en") return false;
            }
            return true;
        }
    }
}
