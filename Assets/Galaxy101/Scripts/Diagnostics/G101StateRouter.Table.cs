#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using Galaxy101.Table;
using Newtonsoft.Json.Linq;
using Okey101.Core.V4;
using UnityEngine;

namespace Galaxy101.Diagnostics
{
    public sealed partial class G101StateRouter
    {
        /// <summary>Table states (states.json groups dagitim, oyun, masa): G01–G34, T01–T51.</summary>
        private static bool IsTableState(string id) => id.Length == 3 && (id[0] == 'G' || id[0] == 'T') && char.IsDigit(id[1]) && char.IsDigit(id[2]);

        /// <summary>The fixed Test masası of g101-state.js (SEATS): bet 5000, 5 hands, 30 s; the prototype's level → league.</summary>
        private static readonly (string Name, string Color, int Level)[] TestSeats = { (null, null, 0), ("Ayşe K.", "#FF6B9A", 31), ("Burak T.", "#4FC3F7", 18), ("Zeynep A.", "#9CCC65", 42) };
        private const long TestBet = 5000;

        private TableGame Game => FindAnyObjectByType<TableGame>();
        private TableHud Hud => FindAnyObjectByType<TableHud>();
        private string scenarioId;
        private string tableFlow;
        /// <summary>The table state being run: timed extras of an earlier state never reach the next state's table.</summary>
        private int tableRun;

        /// <summary>
        /// A table state: its states.json path step by step (g101-state.js step()): acct → [scenario] → table {flow, bots,
        /// noWait} → waitDeal / masa … / set Masa {…} / wait / waitMy / waitPen. The table is the offline v4 match (the
        /// server's TableRuntime) with the run's seed; the flows' prepared hands are TableScenarios (client-tests.json).
        /// </summary>
        private IEnumerator TableState(string id, string theme, string lang)
        {
            var path = (JArray)Entry(id)["path"];
            scenarioId = null; tableFlow = null; tableRun++;
            TableSettings.DesignDefaultsOnly = true;
            foreach (JObject step in path)
            {
                if (step["acct"] != null) { yield return OpenMenu(theme, lang, false); continue; }
                if (step["scenario"] != null) { scenarioId = (string)step["scenario"]; continue; }
                if (step["table"] is JObject t) { yield return OpenTable(t); continue; }
                if (step["wait"] != null) { yield return Settle((int)step["wait"] / 1000f); continue; }
                if (step["waitDeal"] != null) { yield return WaitDeal(step["waitDeal"].Type == JTokenType.Null ? null : (string)step["waitDeal"]); continue; }
                if (step["waitMy"] != null) { yield return WaitMy(step["waitMy"]); continue; }
                if (step["waitPen"] is JObject pen) { yield return WaitPen((int?)pen["seat"] ?? 0, (int?)pen["after"] ?? 300); continue; }
                if (step["masa"] != null) { yield return Masa((string)step["masa"], step["args"] as JArray ?? new JArray()); continue; }
                if (step["set"] != null) { yield return Set((string)step["set"], step["s"] as JObject, step["deep"] as JObject); continue; }
                throw new NotSupportedException("table step " + step.ToString(Newtonsoft.Json.Formatting.None));
            }
        }

        /// <summary>table {flow, bots, noWait}: the Test masası (or Botlara karşı), the prototype's go(flow) 400 ms after the table is up.</summary>
        private IEnumerator OpenTable(JObject t)
        {
            tableFlow = t["flow"]?.Type == JTokenType.String ? (string)t["flow"] : null;
            bool bots = (bool?)t["bots"] == true, noWait = (bool?)t["noWait"] == true;
            var lang = (string)Menu.Model.Settings["lang"];
            string L(string tr, string en) => lang == "en" ? en : tr;
            var launch = new TableLaunch
            {
                Setup = new TableSetup
                {
                    Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = scenarioId != null ? TableScenarios.ScenarioSeed : Current.Seed, TurnMs = TableTimings.DefaultTurnMs,
                    FirstHandRig = scenarioId != null ? TableScenarios.Rig(scenarioId) : null,
                },
                Manual = true,
            };
            if (bots) { launch.Table = TableGame.DefaultTable(); launch.VsBots = true; launch.Chips = AccountChips; }
            else
            {
                var table = new V4Table { Id = "state", Name = L("Test masası", "Test table"), Mode = "solo", Hands = 5, Bet = TestBet, TurnSec = 30, Status = "playing", Rules = new V4TableRules() };
                table.Seats.Add(new V4Seat { User = new V4User { Id = "me", Name = AccountName, Color = AccountColor } });
                for (int k = 1; k < 4; k++) table.Seats.Add(new V4Seat { Bot = true, User = new V4User { Id = "s" + k, Name = TestSeats[k].Name, Color = TestSeats[k].Color, Level = TestSeats[k].Level } });
                launch.Table = table; launch.Room = L("Test masası · Tekli", "Test table · Singles"); launch.Stake = TestBet; launch.Chips = AccountChips - TestBet; launch.Seats = TestSeats;
            }
            TableGame.Pending = launch;
            _ = Menu.StartLocalAsync(MainMenuController.GalaxyTableScene);
            float until = Time.realtimeSinceStartup + 30;
            while (Game == null || Game.Model == null || Hud == null || Hud.View == null) { if (Time.realtimeSinceStartup > until) throw new TimeoutException("table did not open"); yield return null; }
            if (noWait)
            {
                // T50 / G01 / T51: the loading screen over the table, which waits full ("Masa doldu · oyun başlıyor"), as the
                // prototype's loading layer lies over G101-Masa.
                Game.ShowTableFull();
                var loading = new TableLoadingOverlay(Hud.View, Hud.transform);
                loading.Show(5, bots ? null : L("KLASİK · TEKLİ · 101 · 5 EL", "CLASSIC · SINGLES · 101 · 5 HANDS"));
                Loading = loading;
                StartCoroutine(LoadingProgress(loading));
                yield break;
            }
            yield return Settle(.4f);
            yield return Go(tableFlow);
            // DEMO (G101-Masa dealFinish): 2.6 s after a real deal ends, seat 2 treats the table to tea.
            if (tableFlow == null || tableFlow == "deal" || tableFlow == "seat") StartCoroutine(TeaAfterDeal());
        }

        private IEnumerator TeaAfterDeal()
        {
            var game = Game; var hud = Hud; int run = tableRun;
            while (game != null && run == tableRun && !(game.Model.HasHand && game.Model.Deal == null && !game.Ceremony.Running && game.Current != TableGame.Phase.Deal)) { if (GameClock.Frozen) yield break; yield return null; }
            if (game == null || run != tableRun) yield break;
            yield return After(2.6f, () => { if (run == tableRun && hud != null) hud.BotGift(2, "tea", -1, TableStrings.Get("quick.4", hud.L("tr", "en"))); });
        }

        private TableLoadingOverlay Loading;

        /// <summary>The prototype's tickLoad: every 50 ms 0.6–2.2 % (the seeded stream), message by the percentage.</summary>
        private IEnumerator LoadingProgress(TableLoadingOverlay loading)
        {
            float pct = 0;
            while (loading.Visible && !GameClock.Frozen)
            {
                if (!loading.ErrorVisible) { pct = Mathf.Min(90, pct + .6f + (float)GameClock.Random() * 1.6f); loading.SetProgress(pct / 100f); }
                float next = Time.realtimeSinceStartup + .05f;
                while (Time.realtimeSinceStartup < next) yield return null;
            }
        }

        /// <summary>G101-Masa go(key): the flow's setup and its timed extras (client-tests.json flows).</summary>
        private IEnumerator Go(string flow)
        {
            var game = Game; var hud = Hud;
            string key = flow == "gift" || flow == "chat" || flow == "exit" || flow == "net" || flow == "settings" ? "play" : flow;
            if (flow == "deal") { yield return Settle(.25f); game.RunFlow("deal"); yield break; }
            game.RunFlow(key);
            if (flow == null || flow == "seat") yield break;
            // Timed extras run alongside the path's next steps (the prototype's this.at).
            int run = tableRun;
            void At(float seconds, Action action) => StartCoroutine(After(seconds, () => { if (run == tableRun && game != null) action(); }));
            string quick4 = TableStrings.Get("quick.4", hud.L("tr", "en"));
            switch (flow)
            {
                case "open": At(1.1f, () => game.OpenRuns()); break;
                case "gift": At(.3f, () => { hud.OpenGifts("2"); hud.Press("gift-tab:0"); hud.Press("gift-pick:tea"); }); At(.6f, () => hud.BotGift(2, "tea", -1, quick4)); break;
                case "chat": At(.3f, () => hud.OpenChat()); At(1.4f, () => hud.Say(1, TableStrings.Get("bot.4", hud.L("tr", "en")))); break;
                case "exit": At(.4f, () => game.RequestExit()); break;
                case "net": At(.5f, () => game.ShowConnectionLost()); break;
                case "settings": At(.3f, () => hud.OpenSettings(2)); break;
                case "isle": At(.6f, () => hud.Post(TableHud.ToastKind.Info, TableStrings.Get("isleTestHint", hud.L("tr", "en")))); break;
                case "penIslek": At(.6f, () => hud.Post(TableHud.ToastKind.Info, TableStrings.Get("penIslekTest", hud.L("tr", "en")))); break;
                case "penOpen": At(.6f, () => hud.Post(TableHud.ToastKind.Info, TableStrings.Get("penOpenTest", hud.L("tr", "en")))); break;
            }
            // DEMO (DEMO_LIST.md "Bot ikramı"): the prototype's tea from seat 2 on the play and gift flows.
            if (flow == "play") At(1.6f, () => hud.BotGift(2, "tea", -1, quick4));
        }

        private static IEnumerator After(float seconds, Action action)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            if (!GameClock.Frozen) action();
        }

        private static readonly string[] CeremonyOrder = { "stack", "roll", "rolling", "indicator", "extra", "deal", "centre", "fill", "start" };

        /// <summary>waitDeal: the prototype's deal step (dice, ind, extra, dist, center, fill) or null = the deal is over.</summary>
        private IEnumerator WaitDeal(string step)
        {
            var game = Game;
            string want = step == "dice" ? "roll" : step == "ind" ? "indicator" : step == "dist" ? "deal" : step == "center" ? "centre" : step;
            float until = Time.realtimeSinceStartup + 60;
            while (true)
            {
                if (Time.realtimeSinceStartup > until) throw new TimeoutException("deal " + step);
                if (want == null) { if (!game.Ceremony.Running && game.Model.Deal == null && game.Current != TableGame.Phase.Deal && game.Model.HasHand) break; }
                else if (game.Ceremony.Running && Array.IndexOf(CeremonyOrder, game.Ceremony.Step) >= Array.IndexOf(CeremonyOrder, want)) break;
                yield return null;
            }
        }

        /// <summary>waitMy: my turn, after the draw (the prototype's phase 'discard').</summary>
        private IEnumerator WaitMy(JToken after)
        {
            var game = Game; float until = Time.realtimeSinceStartup + 90;
            while (!(game.HumanTurn && game.CanDiscard)) { if (Time.realtimeSinceStartup > until) throw new TimeoutException("my turn"); yield return null; }
            yield return Settle(after.Type == JTokenType.Boolean ? .6f : (int)after / 1000f);
        }

        private IEnumerator WaitPen(int seat, int after)
        {
            var game = Game; float until = Time.realtimeSinceStartup + 30;
            while (game.Model.Pen[game.Abs(seat)] <= 0) { if (Time.realtimeSinceStartup > until) throw new TimeoutException("penalty"); yield return null; }
            yield return Settle(after / 1000f);
        }

        private int RackTile(string code)
        {
            var game = Game;
            foreach (var id in game.Rack.Tiles) if (TileIds.Code(id) == code || TileIds.ToV4(id) == code) return id;
            throw new InvalidOperationException("no " + code + " on the rack");
        }

        /// <summary>masa &lt;method&gt;: the prototype's Masa methods the catalogue calls.</summary>
        private IEnumerator Masa(string method, JArray args)
        {
            var game = Game; var hud = Hud;
            switch (method)
            {
                case "rollDice": game.RollDice(); break;
                case "openHand": game.OpenRuns(); break;
                case "isle": game.IsleAll(); break;
                case "undoTurn": game.Undo(); break;
                case "bomb": game.FinishFromHand(); break;   // R-53: nothing below the bar (G25–G29)
                case "drawFrom": _ = game.Draw((string)args[0] == "side", null); break;
                case "scDiscard": game.Discard(TileIds.ToInt((string)args[0])); break;
                case "scDiscardCode": game.Discard(RackTile((string)args[0])); break;
                case "scOpen": game.OpenWith(((JArray)args[0]).Select(g => (IList<string>)g.ToObject<List<string>>())); break;
                case "scSel":
                {
                    int id = RackTile((string)args[0]);
                    FindAnyObjectByType<RackInput>()?.Select(id); game.SetSelected(id);
                    break;
                }
                case "scSwap":
                {
                    int tile = RackTile((string)args[0]); int owner = (int)args[1], index = (int)args[2];
                    var sc = TableScenarios.Create(tableFlow == "penIslek" ? "penIslek" : "isle");
                    var meld = sc.Melds[(owner, index)];
                    var okey = game.TableOkeys().First(o => o.Meld == meld);
                    game.TakeOkey(tile, okey.Id);
                    break;
                }
                case "scDrag":
                {
                    string code = (string)args[0], to = (string)args[1]; float k = (float)args[2];
                    int id = code == "$side" ? game.PickedTile : RackTile(code);
                    if (id < 0) throw new InvalidOperationException("nothing to drag: " + code);
                    game.SetDragTile(id);
                    var pose = to == "ring" ? game.SideRing() : game.Targets.Count > 0 ? game.Targets[0].Pose : throw new InvalidOperationException("no target for " + code);
                    game.SimulateDrag(id, pose, k);
                    break;
                }
                case "setSet":
                    foreach (var p in (JObject)args[0]) hud.Settings.Set(p.Key, p.Value);
                    break;
                case "botGift":
                {
                    int from = (int)args[0]; string kind = (string)args[1]; var to = args[2]; string msg = args.Count > 3 ? (string)args[3] : null;
                    hud.BotGift(from, kind, to.Type == JTokenType.String ? -1 : (int)to, string.IsNullOrEmpty(msg) ? null : msg);
                    break;
                }
                default: throw new NotSupportedException("masa " + method);
            }
            yield return Settle(.1f);
        }

        /// <summary>set Masa {card | giftTab, giftSel | setTab} and set App {gameOn:false} deep {ld:{err:true}} (T51).</summary>
        private IEnumerator Set(string target, JObject s, JObject deep)
        {
            if (target == "App")
            {
                if (deep?["ld"]?["err"] != null && Loading != null) Loading.ShowError(() => Loading.Hide(), () => { });
                yield return Settle(.08f); yield break;
            }
            var hud = Hud;
            if (s["card"] != null) hud.OpenCard((int)s["card"]);
            if (s["giftTab"] != null) { hud.Press("gift-tab:" + (int)s["giftTab"]); if (s["giftSel"] != null && s["giftSel"].Type != JTokenType.Null) hud.Press("gift-pick:" + (string)s["giftSel"]); }
            if (s["setTab"] != null) hud.OpenSettings((int)s["setTab"]);
            yield return Settle(.08f);
        }
    }
}
#endif
