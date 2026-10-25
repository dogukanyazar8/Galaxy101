using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Okey101.Core.V4.Tests
{
    /// <summary>
    /// Port of the handoff v4 golden-vector runner (<c>tests/runner.js</c>): vector kinds init | call | sim, steps {do, expect},
    /// expect {ok, error, events, noEvents, state}. State paths ("hand.racks.0", suffix "#len" length, "#set" unordered,
    /// "invariants") resolve on <see cref="MatchState.ToTree"/>, the same property names rules.js uses; matching is partial
    /// (only the keys a vector names) and events match as an ordered subsequence. No vector-specific code.
    /// </summary>
    public static class VectorRunner
    {
        /// <summary>JavaScript's undefined: a missing key or index (distinct from null).</summary>
        public static readonly object Undefined = new UndefinedValue();
        private sealed class UndefinedValue { public override string ToString() => "undefined"; }

        public const string VectorsFolder = "Packages/com.okey101.core/Tests/Editor/V4Vectors";
        public const string PackageVectorsFolder = "Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/tests/rules";

        /// <summary>
        /// The repo root: Unity runs tests from the project folder; the SDK test project (tools/v4/nunit) stamps the root into
        /// its assembly metadata because test discovery runs from the build output folder.
        /// </summary>
        public static string RepoRoot()
        {
            var env = Environment.GetEnvironmentVariable("OKEY101_REPO_ROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                foreach (var a in asm.GetCustomAttributes<AssemblyMetadataAttribute>())
                    if (a.Key == "RepoRoot" && Directory.Exists(Path.Combine(a.Value ?? "", VectorsFolder))) return a.Value;
            }
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, VectorsFolder))) return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        public static string VectorsPath(string file = null) => file == null ? Path.Combine(RepoRoot(), VectorsFolder) : Path.Combine(RepoRoot(), VectorsFolder, file);

        /// <summary>All vectors in index.json order: (file, id, vector).</summary>
        public static IEnumerable<(string file, string id, JObject vector)> LoadAll()
        {
            var index = JObject.Parse(File.ReadAllText(VectorsPath("index.json")));
            foreach (var f in index["files"])
            {
                var file = (string)f;
                var suite = JObject.Parse(File.ReadAllText(VectorsPath(file)));
                foreach (JObject t in suite["tests"]) yield return (file, (string)t["id"], t);
            }
        }

        // ---------- runner.js get / partial ----------
        public static object Get(object obj, string path)
        {
            var o = obj;
            foreach (var part in path.Split('.'))
            {
                if (o == null || o == Undefined) return Undefined;
                o = Member(o, part);
            }
            return o;
        }

        private static object Member(object o, string key)
        {
            if (o is IDictionary<string, object> d) return d.TryGetValue(key, out var v) ? v : Undefined;
            if (o is IList l)
            {
                if (key == "length") return l.Count;
                return IsIndex(key, out var i) && i < l.Count ? l[i] : Undefined;
            }
            if (o is string s)
            {
                if (key == "length") return s.Length;
                return IsIndex(key, out var i) && i < s.Length ? s[i].ToString() : Undefined;
            }
            return Undefined;
        }

        private static bool IsIndex(string key, out int i)
        {
            i = -1;
            return key.Length > 0 && key.All(char.IsDigit) && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out i);
        }

        /// <summary>runner.js partial(exp, act): primitives strictly equal; arrays same length and elementwise; objects key-subset.</summary>
        public static bool Partial(JToken exp, object act)
        {
            switch (exp.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return act == null;
                case JTokenType.Integer:
                case JTokenType.Float:
                    return IsNumber(act) && Convert.ToDecimal(act, CultureInfo.InvariantCulture) == exp.Value<decimal>();
                case JTokenType.String:
                    return act is string s && s == exp.Value<string>();
                case JTokenType.Boolean:
                    return act is bool b && b == exp.Value<bool>();
                case JTokenType.Array:
                {
                    if (!(act is IList l) || act is string) return false;
                    var ea = (JArray)exp;
                    if (l.Count != ea.Count) return false;
                    for (var i = 0; i < ea.Count; i++) if (!Partial(ea[i], l[i])) return false;
                    return true;
                }
                case JTokenType.Object:
                {
                    if (act == null || act == Undefined || !(act is IDictionary<string, object> || act is IList)) return false;
                    foreach (var p in ((JObject)exp).Properties()) if (!Partial(p.Value, Member(act, p.Name))) return false;
                    return true;
                }
                default:
                    return false;
            }
        }

        private static bool IsNumber(object o) => o is int || o is long || o is uint || o is double || o is decimal || o is float;

        // ---------- checkState / checkEvents ----------
        private static void CheckState(MatchState s, JObject map, List<string> fails, string where)
        {
            var tree = s.ToTree();
            foreach (var prop in map.Properties())
            {
                var p = prop.Name;
                JToken want = prop.Value;
                object got;
                if (p == "invariants") got = new List<object>(G101Rules.Invariants(s));
                else if (p.EndsWith("#len", StringComparison.Ordinal))
                {
                    var a = Get(tree, p.Substring(0, p.Length - 4));
                    got = a is IList l && !(a is string) ? (object)l.Count : a is string str && str.Length > 0 ? (object)str.Length : Undefined;
                }
                else if (p.EndsWith("#set", StringComparison.Ordinal))
                {
                    var a = Get(tree, p.Substring(0, p.Length - 4));
                    var items = a is IList l ? l.Cast<object>().Select(x => x?.ToString()).ToList() : new List<string>();
                    items.Sort(string.CompareOrdinal);
                    got = items.Cast<object>().ToList();
                    var w = ((JArray)want).Select(x => (string)x).ToList();
                    w.Sort(string.CompareOrdinal);
                    want = new JArray(w);
                }
                else got = Get(tree, p);
                if (!Partial(want, got)) fails.Add(where + " " + p + ": beklenen " + want.ToString(Newtonsoft.Json.Formatting.None) + ", gelen " + Show(got));
            }
        }

        private static void CheckEvents(JArray exp, List<GameEvent> evs, List<string> fails, string where)
        {
            var trees = evs.Select(e => (object)e.ToTree()).ToList();
            var j = 0;
            for (var i = 0; i < exp.Count; i++)
            {
                while (j < trees.Count && !Partial(exp[i], trees[j])) j++;
                if (j >= trees.Count)
                {
                    fails.Add(where + " olay yok: " + exp[i].ToString(Newtonsoft.Json.Formatting.None) + " · gelen: [" + string.Join(",", evs.Select(e => e.E)) + "]");
                    return;
                }
                j++;
            }
        }

        // ---------- run ----------
        public static List<string> Run(JObject t)
        {
            var fails = new List<string>();
            try
            {
                if (t["call"] is JObject c)
                {
                    var fn = (string)c["fn"];
                    object o = null;
                    if (fn == "analyzeMeld") o = G101Rules.AnalyzeMeld(G101Rules.OkeyFromIndicator((string)c["indicator"]), (string)c["kind"], Strings(c["tiles"])).ToTree();
                    else if (fn == "rigFromHands") o = Threw(() => G101Rules.RigFromHands(ParseScenarioDeal(c["deal"])));
                    else if (fn == "fromSpec") o = Threw(() => G101Rules.FromSpec(ParseSpec(c["spec"])));
                    else if (fn == "openNeedFresh")
                    {
                        var s0 = G101Rules.NewMatch(new MatchOptions { Katlama = true }, 1);
                        s0 = G101Rules.Apply(s0, GameAction.StartHand()).State;
                        o = new Dictionary<string, object> { { "run", G101Rules.OpenNeed(s0, 0, "run") }, { "pair", G101Rules.OpenNeed(s0, 0, "pair") } };
                    }
                    else if (fn == "autoDiscardTile") o = new Dictionary<string, object> { { "tile", G101Rules.AutoDiscardTile(G101Rules.FromSpec(ParseSpec(c["spec"])), (int)c["seat"]) } };
                    else if (fn == "bestMelds")
                    {
                        var b = G101Bots.BestMelds(G101Rules.OkeyFromIndicator((string)c["indicator"]), Strings(c["tiles"]));
                        o = new Dictionary<string, object> { { "value", b.Value }, { "left", b.Left.Count } };
                    }
                    if (!Partial(t["expect"], o)) fails.Add("call " + fn + ": beklenen " + t["expect"].ToString(Newtonsoft.Json.Formatting.None) + ", gelen " + Show(o));
                    return fails;
                }
                if (t["sim"] is JObject sim)
                {
                    var r = G101Bots.Simulate(ParseOpts(sim["opts"]), ParseSeed(sim["seed"], 0));
                    var ff = r.Log.FirstOrDefault(l => l.Fail != null || l.Fatal != null);
                    if (ff != null) fails.Add("sim reddedilen bot eylemi: " + (ff.Fail ?? ff.Fatal) + " " + (ff.Action != null ? Show(ff.Action.ToTree()) : ""));
                    CheckState(r.State, (t["expect"]?["state"] as JObject) ?? new JObject(), fails, "sim");
                    return fails;
                }
                var init = (JObject)t["init"];
                MatchState state;
                if (init["spec"] is JObject spec) state = G101Rules.FromSpec(ParseSpec(spec));
                else if (init["scenario"] is JObject sc)
                {
                    state = G101Rules.NewMatch(ParseOpts(sc["opts"]), ParseSeed(sc["seed"], 7));
                    state = G101Rules.Apply(state, GameAction.StartHand(G101Rules.RigFromHands(ParseScenarioDeal(sc["deal"])))).State;
                }
                else
                {
                    var m = (JObject)init["match"];
                    state = G101Rules.NewMatch(ParseOpts(m["opts"]), ParseSeed(m["seed"], 0));
                    state = G101Rules.Apply(state, GameAction.StartHand(ParseRig(init["rig"]))).State;
                }
                if (init["expect"] is JObject ie) CheckState(state, ie, fails, "init");
                var steps = t["steps"] as JArray ?? new JArray();
                for (var i = 0; i < steps.Count; i++)
                {
                    var st = (JObject)steps[i];
                    var e = st["expect"] as JObject ?? new JObject();
                    var d = (JObject)st["do"];
                    var where = "#" + (i + 1) + " " + (string)d["t"];
                    ApplyResult r;
                    if ((string)d["t"] == "botTurn")
                    {
                        // the bot's whole turn: bots.nextAction until the turn passes
                        var seat = (int)d["seat"];
                        var acc = new List<GameEvent>();
                        var cur = state;
                        var g = 20;
                        var rr = new ApplyResult { Ok = true };
                        while (g-- > 0 && cur.Status == G101Rules.StatusPlaying && cur.Hand.Turn.Seat == seat)
                        {
                            rr = G101Rules.Apply(cur, G101Bots.NextAction(cur, seat));
                            if (!rr.Ok) break;
                            acc.AddRange(rr.Events);
                            cur = rr.State;
                        }
                        r = new ApplyResult { Ok = rr.Ok, Error = rr.Error, Events = acc, State = cur };
                    }
                    else r = G101Rules.Apply(state, ParseAction(d));
                    var wantOk = e["ok"] != null ? (bool)e["ok"] : e["error"] == null || e["error"].Type == JTokenType.Null;
                    if (r.Ok != wantOk) fails.Add(where + ": ok beklenen " + Js(wantOk) + ", gelen " + Js(r.Ok) + (r.Error != null ? " (" + r.Error + ")" : ""));
                    if (e["error"] != null && e["error"].Type != JTokenType.Null && r.Error != (string)e["error"]) fails.Add(where + ": hata beklenen " + (string)e["error"] + ", gelen " + (r.Error ?? "null"));
                    if (e["events"] is JArray evs) CheckEvents(evs, r.Events, fails, where);
                    if (e["noEvents"] is JArray nev)
                        foreach (var x in nev)
                            foreach (var ev in r.Events)
                                if (Partial(x, ev.ToTree())) fails.Add(where + " istenmeyen olay: " + Show(ev.ToTree()));
                    state = r.State;
                    if (e["state"] is JObject es) CheckState(state, es, fails, where);
                }
            }
            catch (Exception ex) { fails.Add("istisna: " + ex.GetType().Name + ": " + ex.Message); }
            return fails;
        }

        private static object Threw(Action a)
        {
            try { a(); return new Dictionary<string, object> { { "threw", null } }; }
            catch (SpecException ex) { return new Dictionary<string, object> { { "threw", ex.Code } }; }
        }

        // ---------- JSON → engine inputs (rules.js defaults) ----------
        private static bool Has(JToken o, string key) => o is JObject j && j[key] != null && j[key].Type != JTokenType.Null && j[key].Type != JTokenType.Undefined;
        private static List<string> Strings(JToken a) => a == null || a.Type == JTokenType.Null ? null : a.Select(x => x.Type == JTokenType.Null ? null : (string)x).ToList();
        private static List<List<string>> StringLists(JToken a) => a == null || a.Type == JTokenType.Null ? null : a.Select(Strings).ToList();
        private static int[] Ints(JToken a) => a == null || a.Type == JTokenType.Null ? null : a.Select(x => (int)x).ToArray();
        private static bool Truthy(JToken t) => t != null && t.Type != JTokenType.Null && !(t.Type == JTokenType.Boolean && !(bool)t) && !(t.Type == JTokenType.Integer && (long)t == 0) && !(t.Type == JTokenType.String && (string)t == "");

        private static uint ParseSeed(JToken t, uint fallback) => Truthy(t) ? unchecked((uint)(long)t) : fallback;

        public static MatchOptions ParseOpts(JToken t)
        {
            var o = new MatchOptions();
            if (!(t is JObject j)) return o;
            if (Has(j, "mode")) o.Mode = (string)j["mode"];
            if (Truthy(j["hands"])) o.Hands = (int)j["hands"];
            o.Penalties = !(j["penalties"] != null && j["penalties"].Type == JTokenType.Boolean && !(bool)j["penalties"]);
            o.Isle = !(j["isle"] != null && j["isle"].Type == JTokenType.Boolean && !(bool)j["isle"]);
            o.Katlama = Truthy(j["katlama"]);
            o.KatlamaPartner = !(j["katlamaPartner"] != null && j["katlamaPartner"].Type == JTokenType.Boolean && !(bool)j["katlamaPartner"]);
            return o;
        }

        public static HandSpec ParseSpec(JToken t)
        {
            var j = (JObject)t;
            var sp = new HandSpec
            {
                Opts = Has(j, "opts") ? ParseOpts(j["opts"]) : null,
                Seed = Truthy(j["seed"]) ? (uint?)unchecked((uint)(long)j["seed"]) : null,
                HandNo = Has(j, "handNo") ? (int?)(int)j["handNo"] : null,
                Dealer = Has(j, "dealer") ? (int?)(int)j["dealer"] : null,
                Totals = Ints(j["totals"]),
                Indicator = Has(j, "indicator") ? (string)j["indicator"] : null,
                Racks = StringLists(j["racks"]),
                Discards = StringLists(j["discards"]),
                Deck = Strings(j["deck"]),
                DeckCount = Has(j, "deckCount") ? (int?)(int)j["deckCount"] : null,
                Opened = Ints(j["opened"]),
                OpenMode = Has(j, "openMode") ? j["openMode"].Select(x => x.Type == JTokenType.Null ? null : (string)x).ToArray() : null,
                Pen = Ints(j["pen"])
            };
            if (j["seats"] is JArray seats)
                sp.Seats = seats.Select(x => new SeatInfo { Bot = Truthy(x["bot"]), Left = Truthy(x["left"]), LeftSeq = Has(x, "leftSeq") ? (int)x["leftSeq"] : 0 }).ToArray();
            if (j["melds"] is JArray melds)
                sp.Melds = melds.Select(x => new SpecMeld { Owner = Has(x, "owner") ? (int)x["owner"] : 0, Kind = (string)x["kind"], Tiles = Strings(x["tiles"]) }).ToList();
            if (j["turn"] is JObject turn)
            {
                sp.TurnSeat = Has(turn, "seat") ? (int?)(int)turn["seat"] : null;
                sp.TurnPhase = Has(turn, "phase") ? (string)turn["phase"] : null;
                sp.TurnN = Has(turn, "n") ? (int?)(int)turn["n"] : null;
            }
            return sp;
        }

        public static ScenarioDeal ParseScenarioDeal(JToken t)
        {
            var j = (JObject)t;
            return new ScenarioDeal
            {
                Dealer = Has(j, "dealer") ? (int?)(int)j["dealer"] : null,
                Dice = Ints(j["dice"]),
                Indicator = Has(j, "indicator") ? (string)j["indicator"] : null,
                Racks = StringLists(j["racks"]),
                DeckTop = Strings(j["deckTop"])
            };
        }

        public static DealRig ParseRig(JToken t)
        {
            if (!(t is JObject j)) return null;
            return new DealRig { Dealer = Has(j, "dealer") ? (int?)(int)j["dealer"] : null, Dice = Ints(j["dice"]), Order = Strings(j["order"]) };
        }

        public static GameAction ParseAction(JObject d)
        {
            var a = new GameAction { T = (string)d["t"] };
            if (Has(d, "seat")) a.Seat = (int)d["seat"];
            if (Has(d, "src")) a.Src = (string)d["src"];
            if (d["melds"] is JArray melds) a.Melds = melds.Select(m => new MeldSpec((string)m["kind"], Strings(m["tiles"]))).ToList();
            if (Has(d, "tile")) a.Tile = (string)d["tile"];
            if (Has(d, "meld")) a.Meld = (string)d["meld"];
            if (Has(d, "side")) a.Side = (string)d["side"];
            if (Has(d, "index")) a.Index = (int)d["index"];
            if (Has(d, "rig")) a.Rig = ParseRig(d["rig"]);
            return a;
        }

        // ---------- display ----------
        private static string Js(bool b) => b ? "true" : "false";

        public static string Show(object o)
        {
            if (o == null) return "null";
            if (o == Undefined) return "undefined";
            if (o is string s) return "\"" + s + "\"";
            if (o is bool b) return Js(b);
            if (IsNumber(o)) return Convert.ToString(o, CultureInfo.InvariantCulture);
            if (o is IDictionary<string, object> d) return "{" + string.Join(",", d.Select(kv => "\"" + kv.Key + "\":" + Show(kv.Value))) + "}";
            if (o is IList l) return "[" + string.Join(",", l.Cast<object>().Select(Show)) + "]";
            return o.ToString();
        }
    }
}
