using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>One league of the ladder (Resources/Galaxy101/Rank/ranks.json = package assets/ranks.json = g101-rank.js LEAGUES).</summary>
    public sealed class RankedLeague
    {
        public int Index;
        public string Key, Tr, En, Text, Bar, Glow, Edge;
        public long RewardChips;
        public string Name(string lang) => lang == "en" ? En : Tr;
    }

    /// <summary>
    /// The Dereceli ladder (g101-rank.js): 15 tiered steps (Bronz III … Usta I, idx 0–14) and Galaksi (idx 15, the season's
    /// top 100 above Usta I). Labels, LP deltas per place, the server's league codes (b3 … m1, gx) and the emblem files.
    /// </summary>
    public static class RankedLadder
    {
        public const int Steps = 15;
        public static readonly string[] Roman = { "", "I", "II", "III" };
        private static List<RankedLeague> leagues;
        private static int[] lowDeltas, highDeltas;

        public static IReadOnlyList<RankedLeague> Leagues { get { Load(); return leagues; } }

        private static void Load()
        {
            if (leagues != null) return;
            var asset = Resources.Load<TextAsset>("Galaxy101/Rank/ranks") ?? throw new InvalidOperationException("Missing Galaxy101/Rank/ranks.json (lane D data).");
            var json = JObject.Parse(asset.text);
            leagues = json["leagues"].Select((l, i) => new RankedLeague
            {
                Index = i, Key = (string)l["key"], Tr = (string)l["tr"], En = (string)l["en"], Text = (string)l["text"], Bar = (string)l["bar"], Glow = (string)l["glow"],
                Edge = (string)l["metal"][3], RewardChips = (long)l["seasonRewardChips"]
            }).ToList();
            lowDeltas = json["lpDeltas"]["idx<=8"].Select(v => (int)v).ToArray();
            highDeltas = json["lpDeltas"]["idx>8"].Select(v => (int)v).ToArray();
        }

        /// <summary>at(idx): the league and its tier (3 = III … 1 = I; 0 for Galaksi).</summary>
        public static (RankedLeague League, int Tier) At(int idx)
        {
            Load();
            return idx >= Steps ? (leagues[5], 0) : (leagues[Mathf.Clamp(idx, 0, Steps - 1) / 3], 3 - Mathf.Clamp(idx, 0, Steps - 1) % 3);
        }

        /// <summary>label(idx, lang): "Altın II", "Galaksi".</summary>
        public static string Label(int idx, string lang) { var a = At(idx); return a.League.Name(lang) + (a.Tier > 0 ? " " + Roman[a.Tier] : ""); }

        /// <summary>leagueOf(i) = floor(min(i, 15) / 3).</summary>
        public static int LeagueOf(int idx) => Mathf.Clamp(idx, 0, Steps) / 3;

        /// <summary>deltas(idx): LP for places 1–4 (idx ≤ 8: +30 +10 −10 −20, above: +25 +5 −12 −22).</summary>
        public static int[] Deltas(int idx) { Load(); return idx <= 8 ? lowDeltas : highDeltas; }

        /// <summary>The server's league code (ProgressResponse.League): b3 … m1, gx.</summary>
        public static string Code(int idx) => idx >= Steps ? "gx" : "bsgdm"[idx / 3] + (3 - idx % 3).ToString();

        public static int FromCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            if (code == "gx") return Steps;
            int league = "bsgdm".IndexOf(code[0]); if (league < 0 || code.Length < 2 || !char.IsDigit(code[1])) return 0;
            return league * 3 + (3 - Mathf.Clamp(code[1] - '0', 1, 3));
        }

        /// <summary>Emblem image (lane D, rasterised from the package's rank SVGs): &lt;league&gt;[_&lt;tier&gt;]_&lt;24|52|160&gt;@2x.</summary>
        public static Texture2D Emblem(string key, int tier, float size)
        {
            // g101-rank.js draws the "small" emblem below 40 px (no halo, no tile gloss): the 24 px file; 52 above it up to
            // 52 px; the 160 px file for every larger size (the same 200-unit drawing, only scaled).
            int file = size < 40 ? 24 : size <= 52 ? 52 : 160;
            string name = key + (tier > 0 && key != "galaxy" ? "_" + Roman[tier] : "") + "_" + file + "@2x";
            if (!emblems.TryGetValue(name, out var texture)) { texture = Resources.Load<Texture2D>("Galaxy101/Rank/" + name); emblems[name] = texture; }
            return texture;
        }
        private static readonly Dictionary<string, Texture2D> emblems = new Dictionary<string, Texture2D>();
    }

    /// <summary>One finished ranked match in the history list (prototype rk.hist: {place, d, mode, idx}).</summary>
    public sealed class RankedHistory
    {
        public int Place, Delta, Idx;
        public string Mode = "solo";
    }

    /// <summary>The player's season standing (prototype rankedOf(): idx, lp, prot, peak, games, top, hist).</summary>
    public sealed class RankedProfile
    {
        public int Idx, Lp, Protected, Peak, Games, Top;
        /// <summary>Galaksi place (idx 15); 0 when unknown.</summary>
        public int SeasonRank;
        /// <summary>The season number and the days left in it (head.sub, seasonTag).</summary>
        public int Season = 1, DaysLeft;
        public List<RankedHistory> History = new List<RankedHistory>();
        public RankedProfile Clone() => new RankedProfile { Idx = Idx, Lp = Lp, Protected = Protected, Peak = Peak, Games = Games, Top = Top, SeasonRank = SeasonRank, Season = Season, DaysLeft = DaysLeft, History = History.ToList() };
    }

    /// <summary>A player on a ranked table (seat, found dialog, invite list).</summary>
    public sealed class RankedPlayer
    {
        public string Id, Name, Color;
        public int Idx;
        public bool Me, Bot;
        /// <summary>Invite list: online now.</summary>
        public bool Online = true;
    }

    /// <summary>rankedSearch: seats filling while the queue searches.</summary>
    public sealed class RankedSearch
    {
        /// <summary>solo, duo or squad (a party member queued by the leader learns the mode from here).</summary>
        public string Mode;
        /// <summary>1: ±1 league, 2: ±2 after rankedWiden (30 s).</summary>
        public int Range = 1;
        public int Filled;
        /// <summary>Opponents found so far, when the source names them (the server sends only the count).</summary>
        public List<RankedPlayer> Opponents;
    }

    /// <summary>rankedFound: the match and its 10 s accept window.</summary>
    public sealed class RankedFound
    {
        public string MatchId;
        /// <summary>Seat order (Duo: you and your partner at 0 and 2, D-29).</summary>
        public List<RankedPlayer> Players = new List<RankedPlayer>();
        public List<bool> Accepted = new List<bool>();
        /// <summary>Seconds of the accept window (protocol/timings.md rankedAccept 10 000 ms).</summary>
        public float Seconds = 10;
    }

    /// <summary>matchStart for a ranked match: the transport's own start message (the online table) and who sits where.</summary>
    public sealed class RankedStart
    {
        public string Mode = "solo";
        public List<RankedPlayer> Players = new List<RankedPlayer>();
        /// <summary>Your seat (0–3) at the ranked table; −1 when unknown.</summary>
        public int Seat = -1;
        /// <summary>The server's matchStart (V4MatchStart: the table, its seats and countdownMs); null for a local demo table.</summary>
        public object Match;
    }

    /// <summary>rankChange: the result card (G101-Dereceli result prop: place, d, mode, ev, from, to).</summary>
    public sealed class RankedResult
    {
        public int Place, Delta, FromIdx, FromLp, ToIdx, ToLp, Protected;
        public string Mode = "solo";
        /// <summary>null, "tier", "league", "galaxy", "downTier", "downLeague" or "protected".</summary>
        public string Event;
        /// <summary>You left the match (counted as 4th).</summary>
        public bool Left;
        public bool Up => Event == "tier" || Event == "league" || Event == "galaxy";
    }

    /// <summary>
    /// The LP rules of applyRanked() (Galaxy 101 - Gerçek Ana Oyun.dc.html; spec/27, D-30): the place's delta, tier and
    /// league steps, 3 protected matches after reaching a new league (LP stays at 0 at the league's floor meanwhile).
    /// The server owns these; the demo and the result card's events use the same arithmetic.
    /// </summary>
    public static class RankedRules
    {
        public static RankedResult Apply(RankedProfile profile, int place, string mode, int? serverDelta = null)
        {
            int idx = profile.Idx, lp = profile.Lp, prot = profile.Protected;
            int d = serverDelta ?? RankedLadder.Deltas(Math.Min(idx, 14))[Mathf.Clamp(place, 1, 4) - 1];
            var before = (idx, lp); lp += d; string ev = null;
            if (idx == RankedLadder.Steps) { if (lp < 0) { idx = 14; lp += 100; ev = "downLeague"; } }
            else if (lp >= 100) { if (idx == 14) { idx = 15; lp -= 100; prot = 3; ev = "galaxy"; } else { idx++; lp -= 100; if (idx % 3 == 0) { prot = 3; ev = "league"; } else ev = "tier"; } }
            else if (lp < 0) { if (idx == 0) lp = 0; else if (idx % 3 == 0 && prot > 0) { lp = 0; ev = "protected"; } else { idx--; lp += 100; ev = (idx + 1) % 3 == 0 ? "downLeague" : "downTier"; } }
            if (prot > 0 && ev != "league" && ev != "galaxy") prot--;
            profile.Idx = idx; profile.Lp = lp; profile.Protected = prot; profile.Peak = Math.Max(profile.Peak, idx);
            profile.Games++; if (place == 1) profile.Top++;
            profile.History.Insert(0, new RankedHistory { Place = place, Delta = d, Mode = mode, Idx = idx });
            if (profile.History.Count > 20) profile.History.RemoveRange(20, profile.History.Count - 20);
            return new RankedResult { Place = place, Delta = d, Mode = mode, Event = ev, FromIdx = before.idx, FromLp = before.lp, ToIdx = idx, ToLp = lp, Protected = prot };
        }

        /// <summary>A rankChange message (before/after, delta, place, tierUp/leagueUp/down, protectedLeft) as a result card.</summary>
        public static RankedResult FromChange(int beforeIdx, int beforeLp, int afterIdx, int afterLp, int delta, int place, bool tierUp, bool leagueUp, bool down, int protectedLeft, string mode)
        {
            string ev = afterIdx >= RankedLadder.Steps && beforeIdx < RankedLadder.Steps ? "galaxy" : leagueUp ? "league" : tierUp ? "tier"
                : down ? (RankedLadder.LeagueOf(afterIdx) < RankedLadder.LeagueOf(beforeIdx) ? "downLeague" : "downTier")
                : delta < 0 && afterIdx == beforeIdx && afterLp == 0 && beforeLp + delta < 0 && beforeIdx % 3 == 0 ? "protected" : null;
            return new RankedResult { Place = place, Delta = delta, Mode = mode ?? "solo", Event = ev, FromIdx = beforeIdx, FromLp = beforeLp, ToIdx = afterIdx, ToLp = afterLp, Protected = protectedLeft };
        }
    }
}
