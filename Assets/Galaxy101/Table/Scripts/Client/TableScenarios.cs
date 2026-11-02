using System;
using System.Collections.Generic;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>A prepared hand in progress for a test flow: the engine's spec (seat 0 = the person) and the person's rack layout.</summary>
    public sealed class TableScenario
    {
        public string Flow;
        public HandSpec Spec;
        /// <summary>The person's rack, two rows of 15 slots (null = empty), as engine ids.</summary>
        public string[][] Rows;
        /// <summary>Table meld ids by owner and index in the flow's meld list (prototype melds[k][g]).</summary>
        public readonly Dictionary<(int Owner, int Index), string> Melds = new Dictionary<(int, int), string>();

        public MatchState State() => G101Rules.FromSpec(Spec);
    }

    /// <summary>
    /// The prepared hands of the package's client test flows (logic/scenarios/client-tests.json: HAND_DEAL, HAND_OPEN,
    /// HAND_BOMB, AYSE_MELDS, setupPenOpenTest, setupIsleTest; DEMO-17/18, dev and state runs only) as engine hands in
    /// progress (<see cref="G101Rules.FromSpec"/>), so the flows play on the v4 engine with real bots instead of the
    /// prototype's engine-less path. Faces without a copy letter take copy a, then b; "J" is a fake okey. The others' racks
    /// hold the counts the flow shows, filled from the unused tiles in the engine's order; the deck takes 20 of the rest,
    /// the left and right players' hidden racks giving up what it lacks (the prototype's counts do not add up to 106).
    /// </summary>
    public static class TableScenarios
    {
        /// <summary>Flow keys (client-tests.json flows) that start from a prepared hand.</summary>
        public static readonly string[] Names = { "play", "open", "bomb", "isle", "penOpen", "penIslek", "end" };

        /// <summary>The developer menu's keys (spec/25) → flows.</summary>
        public static string Flow(string key)
        {
            switch (key)
            {
                case "opening": return "open";
                case "processing": return "isle";
                case "shortOpening": return "penOpen";
                case "playable": return "penIslek";
                case "fromHand": return "bomb";
                default: return key;
            }
        }

        public static bool Has(string flow) => Array.IndexOf(Names, Flow(flow)) >= 0;

        // client-tests.json hands (two rows × 15 slots, null = empty).
        private static readonly string[][] HandDeal =
        {
            new[] { "R10", "R11", "R12", "R13", null, "B9", "Y9", "K9", null, "Y4", "Y5", "Y6", null, "K12", "K5" },
            new[] { "B7", "K7", null, "J", "Y7", "B11", null, "Y12", null, "B5", "R7", null, "B12", "K11", null },
        };
        private static readonly string[][] HandOpen =
        {
            new[] { "R10", "R11", "R12", "R13", null, "B9", "Y9", "K9", null, "Y4", "Y5", "Y6", "Y7", null, null },
            new[] { "B12", "Y12", "K12", null, "B2", "K7", "R3", null, "B11", "B5", "R7", "K11", "K5", null, null },
        };
        private static readonly string[][] HandBomb =
        {
            new[] { "R1", "R2", "R3", "R4", null, "B5", "Y5", "K5", null, "K9", "K10", "K11", null, null, null },
            new[] { "Y7", "Y8", "Y9", "Y10", null, null, "R6", null, null, null, null, null, null, null, null },
        };
        private static readonly string[][] AyseMelds = { new[] { "K5", "K6", "K7", "K8" }, new[] { "R12", "B12", "Y12" }, new[] { "B9", "B10", "B11", "B12", "B13" } };
        // setupPenOpenTest / setupIsleTest (client-tests.json source).
        private static readonly string[][] PenOpenRows =
        {
            new[] { null, "R3", "R4", "R5", null, "K7", "B7", "Y7", null, "B10", "B11", "B12", null, "Y2", null },
            new[] { null, "K13", "R9", null, "Y11", "B1", null, "K4", "R13", null, "Y8", null, null, null, null },
        };
        private static readonly string[][] IsleRows =
        {
            new[] { null, null, "Y4", "B12", null, "R13", "K5", null, "Y8", "B4", null, "K13", null, null, null },
            new string[15],
        };
        private static readonly string[][][] IsleMelds =
        {
            new[] { new[] { "R1", "R2", "R3", "R4" }, new[] { "K9", "K10", "K11" }, new[] { "Y12", "B12", "K12" } },
            new[] { new[] { "R5", "R6", "R7", "R8", "R9" }, new[] { "B1", "B2", "B3" }, new[] { "Y5", "B5", "K5" }, new[] { "K2", "K3", "K4" } },
            new[] { new[] { "Y3", "B7", "Y5", "Y6" }, new[] { "R12", "Y12", "K12" }, new[] { "Y9", "Y10", "Y11" }, new[] { "B9", "B10", "B11" } },
            new[] { new[] { "K6", "K7", "K8" }, new[] { "B11", "B7", "B13" }, new[] { "Y1", "R1", "K1" }, new[] { "R10", "R11", "R12" } },
        };
        /// <summary>The flows' indicator (ind {c:'B', n:6}): the okey is B7.</summary>
        private const string Indicator = "B6";

        public static TableScenario Create(string key)
        {
            var flow = Flow(key);
            switch (flow)
            {
                case "play": return Play(flow, Without(HandDeal, 0, 14), G101Rules.PhaseDraw, 9, 0);
                case "open": return Play(flow, HandOpen, G101Rules.PhasePlay, 9, 0);
                case "bomb": return Play(flow, HandBomb, G101Rules.PhasePlay, 9, 0);
                case "end": return Play(flow, Without(HandDeal, 0, 14), G101Rules.PhasePlay, 3, 2);
                case "isle": case "penIslek": return Isle(flow);
                case "penOpen": return PenOpen(flow);
                default: throw new ArgumentException("Unknown table flow: " + key, nameof(key));
            }
        }

        private static string[][] Without(string[][] rows, int row, int index)
        {
            var copy = new[] { (string[])rows[0].Clone(), (string[])rows[1].Clone() };
            copy[row][index] = null;
            return copy;
        }

        /// <summary>
        /// Copies of each face in play: a before b; "J" takes Ja then Jb. A face asked for a third time (the prototype's
        /// hands overlap Ayşe's melds: B12) is replaced, as setupPlay's fix() does, by the face the reference drew (Spare),
        /// else by a free copy of the okey, else by the first free tile in the engine's order.
        /// </summary>
        private sealed class Tiles
        {
            private readonly HashSet<string> used = new HashSet<string>();
            public string Okey;
            /// <summary>The face setupPlay's seeded fix() drew for the third copy (seed 42), before the okey fallback.</summary>
            public string Spare;
            public string Take(string code)
            {
                foreach (var copy in new[] { "a", "b" })
                {
                    var id = code + copy;
                    if (!G101Rules.IsTile(id)) throw new ArgumentException("Not a tile face: " + code);
                    if (used.Add(id)) return id;
                }
                if (Spare != null) { var spare = Spare; Spare = null; foreach (var copy in new[] { "a", "b" }) if (used.Add(spare + copy)) return spare + copy; }
                if (Okey != null) foreach (var copy in new[] { "a", "b" }) if (used.Add(Okey + copy)) return Okey + copy;
                foreach (var id in G101Rules.AllTiles) if (!G101Rules.IsFake(id) && used.Add(id)) return id;
                throw new InvalidOperationException("No tile left for " + code + ".");
            }
            public bool Used(string id) => used.Contains(id);
            public void Mark(string id) => used.Add(id);
            public List<string> Rest()
            {
                var l = new List<string>();
                foreach (var id in G101Rules.AllTiles) if (!used.Contains(id)) l.Add(id);
                return l;
            }
        }

        private static string[][] TakeRows(Tiles tiles, string[][] rows, List<string> rack)
        {
            var o = new[] { new string[15], new string[15] };
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < 15; i++)
                    if (rows[r][i] != null) { var id = tiles.Take(rows[r][i]); o[r][i] = id; rack.Add(id); }
            return o;
        }

        private static TableScenario Spec(string flow, Tiles tiles, string[][] rows, List<string> myRack, List<SpecMeld> melds, List<List<string>> discards,
            int[] counts, int[] opened, string turnPhase, int turnSeat, int turnN, List<string> fixedOpponent = null, int fixedSeat = -1)
        {
            var racks = new List<List<string>> { myRack, new List<string>(), new List<string>(), new List<string>() };
            if (fixedOpponent != null) racks[fixedSeat] = fixedOpponent;
            var rest = tiles.Rest();
            // The prototype's flows show a deck of 20 whatever the piles hold: the left and right players' hidden racks give up
            // the tiles the deck lacks (never below 14), so the deck count reads as in the reference.
            int deficit = G101Rules.Const.Deck - (rest.Count - (counts[1] - racks[1].Count) - (counts[2] - racks[2].Count) - (counts[3] - racks[3].Count));
            counts = (int[])counts.Clone();
            for (int k = 3; deficit > 0 && (counts[1] > 14 && fixedSeat != 1 || counts[3] > 14 && fixedSeat != 3); k = 4 - k)
                if (counts[k] > 14 && k != fixedSeat) { counts[k]--; deficit--; }
            for (int k = 1; k < 4; k++)
                while (racks[k].Count < counts[k] && rest.Count > 0) { racks[k].Add(rest[0]); rest.RemoveAt(0); }
            var seats = new SeatInfo[4];
            for (int k = 0; k < 4; k++) seats[k] = new SeatInfo { Bot = k != 0 };
            var spec = new HandSpec
            {
                Opts = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, HandNo = 1, Dealer = 3, Totals = new int[4], Seats = seats, Indicator = Indicator + "a",
                Racks = racks, Discards = discards, Melds = melds, DeckCount = Math.Min(G101Rules.Const.Deck, rest.Count), Opened = opened,
                OpenMode = new[] { opened[0] > 0 ? "run" : null, opened[1] > 0 ? "run" : null, opened[2] > 0 ? "run" : null, opened[3] > 0 ? "run" : null },
                TurnSeat = turnSeat, TurnPhase = turnPhase, TurnN = turnN,
            };
            var sc = new TableScenario { Flow = flow, Spec = spec, Rows = rows };
            var index = new int[4];
            for (int m = 0; m < melds.Count; m++) sc.Melds[(melds[m].Owner, index[melds[m].Owner]++)] = "m" + (m + 1);
            return sc;
        }

        private static List<string> Ids(Tiles tiles, params string[] codes) { var l = new List<string>(); foreach (var c in codes) l.Add(tiles.Take(c)); return l; }

        private static List<SpecMeld> MeldsOf(Tiles tiles, int owner, string[][] groups, ColorNum okey, List<SpecMeld> into)
        {
            foreach (var g in groups)
            {
                var ids = new List<string>(); foreach (var c in g) ids.Add(tiles.Take(c));
                into.Add(new SpecMeld { Owner = owner, Kind = G101Rules.AnalyzeMeld(okey, "run", ids).Ok ? "run" : "group", Tiles = ids });
            }
            return into;
        }

        /// <summary>setupPlay: the person's hand, Ayşe's (seat 2) three opened melds (117), the piles, the others' counts.</summary>
        private static TableScenario Play(string flow, string[][] hand, string phase, int ayse, int turnSeat)
        {
            var tiles = new Tiles(); tiles.Take(Indicator);
            var okey = G101Rules.OkeyFromIndicator(Indicator + "a");
            tiles.Okey = okey.C + okey.N;
            // HAND_DEAL's and HAND_OPEN's B12 is a third copy beside Ayşe's two. setupPlay's fix() draws a random free face
            // for it — B3 in most reference frames (G30, T06, T11; K4 in some, docs/v4/table/UNITY_FARKLARI.md UF-503) —
            // so G13 opens 95, a short opening (R-23) in the reference too.
            tiles.Spare = "B3";
            var melds = MeldsOf(tiles, 2, AyseMelds, okey, new List<SpecMeld>());
            var discards = new List<List<string>> { Ids(tiles, "K1"), Ids(tiles, "R4", "Y13"), Ids(tiles, "B3"), Ids(tiles, "Y2", "R9") };
            var rack = new List<string>();
            var rows = TakeRows(tiles, hand, rack);
            List<string> fixedRack = null;
            if (flow == "end")
            {
                // "Ayşe işler ve biter": two tiles that extend her melds (K9 after K5–K8, B8 before B9–B13) and one to finish
                // with — the first unused tile in the engine's order that is not the okey.
                fixedRack = new List<string> { tiles.Take("K9"), tiles.Take("B8") };
                foreach (var id in tiles.Rest()) if (!G101Rules.IsOkey(okey, id) && !G101Rules.IsFake(id)) { tiles.Mark(id); fixedRack.Add(id); break; }
            }
            return Spec(flow, tiles, rows, rack, melds, discards, new[] { 0, 21, ayse, 21 }, new[] { 0, 0, 117, 0 }, phase, turnSeat, 1, fixedRack, 2);
        }

        private static TableScenario Isle(string flow)
        {
            var tiles = new Tiles(); tiles.Take(Indicator);
            var okey = G101Rules.OkeyFromIndicator(Indicator + "a");
            var melds = new List<SpecMeld>();
            for (int k = 0; k < 4; k++) MeldsOf(tiles, k, IsleMelds[k], okey, melds);
            var discards = new List<List<string>> { new List<string>(), Ids(tiles, "R2"), Ids(tiles, "B8"), Ids(tiles, "Y2") };
            var rack = new List<string>();
            var rows = TakeRows(tiles, IsleRows, rack);
            return Spec(flow, tiles, rows, rack, melds, discards, new[] { 0, 4, 3, 5 }, new[] { 118, 141, 132, 127 }, G101Rules.PhasePlay, 0, 3);
        }

        private static TableScenario PenOpen(string flow)
        {
            var tiles = new Tiles(); tiles.Take(Indicator);
            var discards = new List<List<string>> { new List<string>(), Ids(tiles, "K5"), Ids(tiles, "R10"), Ids(tiles, "Y6") };
            var rack = new List<string>();
            var rows = TakeRows(tiles, PenOpenRows, rack);
            return Spec(flow, tiles, rows, rack, new List<SpecMeld>(), discards, new[] { 0, 21, 21, 21 }, new int[4], G101Rules.PhasePlay, 0, 2);
        }

        /// <summary>The package's server scenarios (logic/scenarios/R43.json, R44.json): the first hand's deal as a rig (DEMO-17).</summary>
        public static DealRig Rig(string id)
        {
            switch (id)
            {
                case "R43":
                    return G101Rules.RigFromHands(new ScenarioDeal
                    {
                        Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a",
                        Racks = new List<List<string>>
                        {
                            new List<string> { "K9b", "R1a", "R3a", "R5b", "R7b", "R9a", "R11b", "B2a", "B5a", "B7b", "B11a", "B13b", "K4a", "K7b", "Y1a", "Y3b", "Y6a", "Y10b", "Y12b", "R10b", "B4b", "K2b" },
                            new List<string> { "R13a", "B13a", "Y13a", "R12a", "B12a", "Y12a", "K10a", "K11a", "R2a", "R4a", "R6a", "R8a", "B1a", "B3a", "B6a", "B9a", "K1a", "K3a", "K5a", "Y8a", "Y11a" },
                        },
                    });
                case "R44":
                    return G101Rules.RigFromHands(new ScenarioDeal
                    {
                        Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a",
                        Racks = new List<List<string>>
                        {
                            new List<string> { "R10a", "R11a", "Y5a", "R13a", "K13a", "B13a", "Y13a", "K10a", "K11a", "K12a", "R6a", "R1a", "R3a", "B2a", "B5b", "B9b", "K2a", "K7b", "Y1a", "Y3b", "Y10b", "B4b" },
                            new List<string> { "R7a", "B7a", "K7a", "Y7a", "R8a", "B8a", "K8a", "Y8a", "B10a", "B11a", "B12a", "K3a", "K4a", "K5a", "R12a", "R2a", "R4a", "B3a", "Y2a", "Y9a", "B6a" },
                        },
                    });
                default: throw new ArgumentException("Unknown scenario: " + id, nameof(id));
            }
        }

        /// <summary>The scenarios' seed (R43.json / R44.json seed).</summary>
        public const uint ScenarioSeed = 7;
    }
}
