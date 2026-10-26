using System;
using System.Collections.Generic;
using System.Globalization;
using Galaxy101.Contracts;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        // v4 texts: Resources/Galaxy101/Strings/strings_v4.json (lane D's copy of the package's strings/strings.json).
        // S("sit") is lobby.STR.sit of G101-Lobi's STR table; {0}, {1} … are the STR function's parameters in order.
        // The few STR functions that pick between two sentences keep one branch in the package (EK-412): those are
        // composed here from the prototype's own STR line, cited where they are built.
        private static Dictionary<string, (string Tr, string En)> texts;
        private static Dictionary<string, string> errorKeys;

        private static void LoadTexts()
        {
            if (texts != null) return;
            texts = new Dictionary<string, (string, string)>(StringComparer.Ordinal); errorKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Galaxy101/Strings/strings_v4");
            if (asset == null) { Debug.LogWarning("Galaxy101/Strings/strings_v4 is missing"); return; }
            var root = JObject.Parse(asset.text);
            foreach (var p in (JObject)root["strings"]) texts[p.Key] = ((string)p.Value["tr"] ?? "", (string)p.Value["en"] ?? "");
            foreach (var p in (JObject)root["errors"]) { var key = (string)p.Value["key"]; if (key != null) errorKeys[p.Key] = key; }
        }

        /// <summary>lobby.STR.<paramref name="key"/> in the current language, {n} filled from <paramref name="args"/>.</summary>
        private string S(string key, params object[] args) => V4("lobby.STR." + key, args);

        /// <summary>Any v4 text by its full key (err.E_*, table.STR.*).</summary>
        private string V4(string key, params object[] args)
        {
            LoadTexts();
            if (!texts.TryGetValue(key, out var pair)) { Debug.LogWarning("v4 text missing: " + key); return key; }
            return view.L(Fill(pair.Tr, args), Fill(pair.En, args));
        }

        private static string Fill(string text, object[] args)
        {
            for (int i = 0; i < args.Length; i++) text = text.Replace("{" + i + "}", Convert.ToString(args[i], CultureInfo.InvariantCulture));
            return text;
        }

        private bool En => view.Loc.Language == "en";

        /// <summary>
        /// A server answer in v4 terms: the package's error codes (protocol.json errors) and the codes the current
        /// server sends for the same cases (TableErrors, ErrorCodes) — the adapter keeps both until the lobby speaks v4.
        /// </summary>
        public static string V4Code(string code)
        {
            switch (code)
            {
                case TableErrors.WrongPin: return "E_PIN_WRONG";
                case TableErrors.Full: return "E_TABLE_FULL";
                case TableErrors.Closed: return "E_TABLE_CLOSED";
                case TableErrors.Playing: return "E_TABLE_PLAYING";
                case TableErrors.NotHost: return "E_NOT_OWNER";
                case TableErrors.NotReady: return "E_TABLE_NOT_FULL";
                case ErrorCodes.InsufficientFunds: return "E_NO_CHIPS";
                case ErrorCodes.Conflict: return "E_ALREADY_SEATED";
                default: return code;
            }
        }

        /// <summary>The package's text for an error code (strings_v4 errors[code].key), or null for a silent one.</summary>
        private string ErrorText(string code)
        {
            LoadTexts();
            return errorKeys.TryGetValue(V4Code(code), out var key) ? V4(key) : null;
        }

        /// <summary>
        /// genTR(word) of G101-Lobi: the Turkish genitive after an apostrophe by the last vowel — Doğukan → Doğukan’ın,
        /// Mert → Mert’in, Ayşe → Ayşe’nin.
        /// </summary>
        public static string Genitive(string word)
        {
            var tr = new CultureInfo("tr-TR"); string w = (word ?? "").Trim(), lower = w.ToLower(tr), vowels = "aeıioöuü";
            char v = 'a';
            for (int i = lower.Length - 1; i >= 0; i--) if (vowels.IndexOf(lower[i]) >= 0) { v = lower[i]; break; }
            string suffix = v == 'a' || v == 'ı' ? "ın" : v == 'e' || v == 'i' ? "in" : v == 'o' || v == 'u' ? "un" : "ün";
            bool endsInVowel = lower.Length > 0 && vowels.IndexOf(lower[lower.Length - 1]) >= 0;
            return w + "’" + (endsInVowel ? "n" : "") + suffix;
        }

        /// <summary>
        /// "Son görülme" age as the prototype's FRIENDS data writes it (seen: '2 sa' / '2 h', 'dün' / 'yesterday',
        /// '3 gün' / '3 days'); under an hour "N dk" / "N min" (no prototype value, EK-803).
        /// </summary>
        private string SeenAgo(DateTimeOffset at)
        {
            var span = DateTimeOffset.UtcNow - at;
            if (span.TotalMinutes < 60) { int m = Math.Max(1, (int)span.TotalMinutes); return view.L(m + " dk", m + " min"); }
            if (span.TotalHours < 24) { int h = (int)span.TotalHours; return view.L(h + " sa", h + " h"); }
            int d = (int)span.TotalDays;
            return d == 1 ? view.L("dün", "yesterday") : view.L(d + " gün", d + " days");
        }

        /// <summary>When a recent player last played with you, as FRIENDS.ago writes it: 'bugün', 'dün', '3 gün önce'.</summary>
        private string PlayedAgo(DateTimeOffset at)
        {
            int d = (int)(DateTimeOffset.UtcNow - at).TotalDays;
            return d <= 0 ? view.L("bugün", "today") : d == 1 ? view.L("dün", "yesterday") : view.L(d + " gün önce", d + " days ago");
        }
    }
}
