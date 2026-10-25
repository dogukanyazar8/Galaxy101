using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>Bot table product values from Resources/Galaxy101/Table/table_rules.json (match length, XP, level curve).</summary>
    public sealed class TableRules
    {
        public int MatchHands { get => onlineHands ?? hands; private set => hands = value; }
        private int hands = 5;
        private int? onlineHands;
        /// <summary>A server match sets its own length (3 / 5 / 7); null goes back to the bot table's value.</summary>
        public void UseHands(int? value) => onlineHands = value;
        public int XpHandWin { get; private set; } = 340;
        public int XpHandOther { get; private set; } = 60;
        public int[] XpPlace { get; private set; } = { 500, 200, 80, 0 };
        public int XpPerLevel { get; private set; } = 500;

        private static TableRules loaded;
        public static TableRules Current => loaded ?? (loaded = Load());

        private static TableRules Load()
        {
            var asset = Resources.Load<TextAsset>("Galaxy101/Table/table_rules") ?? throw new InvalidOperationException("Missing Resources/Galaxy101/Table/table_rules.json");
            var json = JObject.Parse(asset.text);
            var rules = new TableRules
            {
                MatchHands = (int)json["matchHands"], XpHandWin = (int)json["xp"]["handWin"], XpHandOther = (int)json["xp"]["handOther"],
                XpPlace = json["xp"]["place"].ToObject<int[]>(), XpPerLevel = (int)json["levelXpPerLevel"],
            };
            if (rules.MatchHands < 1 || rules.XpPlace.Length != 4 || rules.XpPerLevel < 1) throw new FormatException("table_rules.json has invalid values.");
            return rules;
        }

        /// <summary>XP needed to leave <paramref name="level"/> (375 × level, as on the server: level 24 → 9.000).</summary>
        public long XpForLevel(int level) => (long)XpPerLevel * level;

        /// <summary>Add XP; returns how many levels were gained.</summary>
        public int Add(ref int level, ref long xp, long amount)
        {
            int gained = 0; xp += amount;
            while (xp >= XpForLevel(level)) { xp -= XpForLevel(level); level++; gained++; }
            return gained;
        }
    }
}
