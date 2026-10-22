using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Application;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galaxy101.Table
{
    /// <summary>
    /// Game flow on the 3D table (spec/10): deal → turns (me: draw → discard, 30 s, auto-play on timeout; bots with the
    /// prototype's pacing) → finish / deck out → next hand. Rules, dealing, scoring and bot decisions come from the
    /// shared engine (Okey101.Core LocalSession + BotPlanner); this class owns timing, my rack arrangement, the staged
    /// table actions of my turn (Aç, Çift aç, Yere aç, işle) and feeds the board and the HUD.
    /// </summary>
    public sealed class TableGame : MonoBehaviour
    {
        public const int Me = 0;

        [SerializeField] private TableScene table;
        [SerializeField] private TableHud hud;
        [Tooltip("Seat played from this device; -1 lets bots play every seat (soak tests).")]
        [SerializeField] private int humanSeat = Me;
        [Tooltip("0 = random seed per hand.")]
        [SerializeField] private uint seed;
        [SerializeField] private float turnSeconds = 30, warnSeconds = 5;
        [SerializeField] private bool autoSort = true, doubleClickDiscard = true, turnHint = true;
        [Tooltip("Scene the exit button returns to (the Galaxy 101 main menu).")]
        [SerializeField] private string menuScene = "Bootstrap";

        public enum Phase { Waiting, Seating, Deal, Draw, Discard, Opponent, Finished }

        public LocalSession Session { get; private set; }
        public TableBoard Board { get; private set; }
        public RackGrid Rack { get; } = new RackGrid();
        public TileFace Indicator { get; private set; }
        public Phase Current { get; private set; } = Phase.Waiting;
        public int Active { get; private set; } = -1;
        public int HandNumber { get; private set; }
        public int TurnsPlayed { get; private set; }
        public int[] Totals { get; } = new int[4];
        public bool DoubleClickDiscard => doubleClickDiscard;
        public RoundResult LastResult { get; private set; }
        public int LastDrawn { get; private set; } = -1;
        /// <summary>Engine version the board last reconciled (commands complete asynchronously).</summary>
        public long SyncedVersion { get; private set; } = -1;
        public bool HumanTurn => humanSeat == Me && Active == Me && (Current == Phase.Draw || Current == Phase.Discard);
        public bool CanDraw => HumanTurn && Current == Phase.Draw;
        public bool CanDrawSide => CanDraw && !deckOnly;
        public int PickedTile => pickedTile >= 0 && Rack.TryFind(pickedTile, out _) ? pickedTile : -1;
        public IReadOnlyList<(string Why, int Amount)> PenaltyReasons(int seat) => penaltyReasons[seat];
        public bool CanDiscard => HumanTurn && Current == Phase.Discard;
        public bool CanArrange => Current != Phase.Waiting && Current != Phase.Seating && Current != Phase.Deal;

        // ---------- Faz 4: seating, finish moment, hand result, leave card ----------
        /// <summary>Seats taken while the table fills (spec/10 §1); seat 0 is me.</summary>
        public bool[] Seated { get; } = { true, false, false, false };
        public int Countdown { get; private set; }
        public bool ResultOpen { get; private set; }
        public bool ExitOpen { get; private set; }
        /// <summary>XP this session (bot tables are offline and award XP only; no chips).</summary>
        public int XpEarned { get; private set; }
        public int LastXp { get; private set; }
        private string momentTitle, momentSub; private int momentSeat = -1; private bool momentOn;
        private float countdownAt, resultAt, momentAt;
        private TaskCompletionSource<bool> nextHandSignal;
        // Match (spec/17 §7): hands played in this match, points per hand per seat, XP snapshot at the match start.
        public int MatchHand { get; private set; }
        public bool MatchOpen { get; private set; }
        private readonly List<int?>[] handScores = { new List<int?>(), new List<int?>(), new List<int?>(), new List<int?>() };
        private int handsWon, handsOther, matchStartLevel, xpBonus, place;
        private long matchStartXp;
        private TaskCompletionSource<string> matchSignal;
        private bool seating = true;
        public bool IOpened => OpenedKind(Me) != OpeningKind.None;
        public bool CanMeld => CanDiscard && IOpened;
        public event Action Changed;

        private readonly BotPlanner planner = new BotPlanner();
        private readonly List<TableAction> staged = new List<TableAction>();
        private readonly List<(float At, TaskCompletionSource<bool> Done)> timers = new List<(float, TaskCompletionSource<bool>)>();
        private CancellationTokenSource life;
        private TaskCompletionSource<bool> humanDone;
        private float turnStarted, turnStartedReal;
        private bool warned;
        // spec/17: penalties of this hand per seat with their reasons (badges, toasts, result table), the tile taken from the
        // previous discard until it is used or put back, and "deck only" after putting it back.
        private readonly int[] handPenalties = new int[4];
        private readonly List<(string Why, int Amount)>[] penaltyReasons = { new List<(string, int)>(), new List<(string, int)>(), new List<(string, int)>(), new List<(string, int)>() };
        private int pickedTile = -1;
        private bool deckOnly;
        // T33/T47–T49: the rack tile being dragged (okey targets), the okey under the pointer / long-pressed, a refused okey.
        private int dragTile = -1, tipOkey = -1, invalidOkey = -1, glowTile = -1;
        private float glowUntil;
        private bool tipTake;
        private float tipUntil, invalidUntil;
        private System.Random random;
        private bool leaving;

        private void Awake()
        {
            if (table == null) table = FindAnyObjectByType<TableScene>();
            if (hud == null) hud = FindAnyObjectByType<TableHud>();
        }

        private void Start()
        {
            if (table == null) table = FindAnyObjectByType<TableScene>();
            if (hud == null) hud = FindAnyObjectByType<TableHud>();
            if (table.CameraRig == null) table.Build();
            Board = new TableBoard(table);
            Board.IsOkey = id => TileCatalog.IsRealJoker(new TileId(id), Indicator);
            Session = new LocalSession();
            life = new CancellationTokenSource();
            random = seed != 0 ? new System.Random((int)seed) : new System.Random();
            if (hud != null) { hud.Command += OnHudCommand; hud.TilePosition = id => Board.ViewOf(id) is TileView v && v != null ? v.transform.position : (Vector3?)null; }
            _ = Run(life.Token);
        }

        /// <summary>Test hook: configure before Start (seat −1 = bots everywhere).</summary>
        public void Configure(int human, uint fixedSeed, float seconds = 30, bool seatPlayers = true, RoundState firstHand = null)
        { humanSeat = human; seed = fixedSeed; turnSeconds = seconds; seating = seatPlayers; initialState = firstHand; }

        private RoundState initialState;

        private void OnDestroy()
        {
            life?.Cancel();
            foreach (var t in timers) t.Done.TrySetCanceled(); timers.Clear();
            humanDone?.TrySetCanceled();
            if (hud != null) hud.Command -= OnHudCommand;
            Board?.Clear();
            Session?.Dispose();
        }

        // ---------- time ----------
        private Task Seconds(float seconds)
        {
            var done = new TaskCompletionSource<bool>(); timers.Add((Time.time + seconds, done)); return done.Task;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = timers.Count - 1; i >= 0; i--) if (timers[i].At <= now) { var t = timers[i]; timers.RemoveAt(i); t.Done.TrySetResult(true); }
            Board?.Tick();
            if (HumanTurn)
            {
                float left = turnSeconds - (now - turnStarted);
                if (!warned && warnSeconds > 0 && left <= warnSeconds) { warned = true; hud?.Post(TableHud.ToastKind.Warning, T("msg.timeWarn"), sticky: true); Notify(); }
                if (left <= 0) { humanDone?.TrySetResult(false); }
            }
            if (ExitOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CancelExit();
            if (tipOkey >= 0 && tipTake && Time.unscaledTime > tipUntil) { tipOkey = -1; Notify(); }
            if (invalidOkey >= 0 && Time.unscaledTime > invalidUntil) { invalidOkey = -1; Notify(); }
            if (glowTile >= 0 && Time.unscaledTime > glowUntil) { glowTile = -1; Notify(); }
        }

        // ---------- flow ----------
        private async Task Run(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    uint handSeed = seed != 0 ? seed + (uint)HandNumber : (uint)random.Next(1, int.MaxValue);
                    if (HandNumber == 0 && initialState != null) Session.Start(initialState);
                    else if (HandNumber == 0) Session.Start(handSeed, humanSeat == Me ? Me : (int?)null); else Session.StartNextRound(handSeed);
                    HandNumber++; LastResult = null; staged.Clear(); LastDrawn = -1; pickedTile = -1; deckOnly = false;
                    for (int k = 0; k < 4; k++) { handPenalties[k] = 0; penaltyReasons[k].Clear(); }
                    var view = Session.GetView(Me);
                    Indicator = TileCodes.IndicatorFace(view.Indicator);
                    if (HandNumber == 1 && seating) await SeatPlayers();
                    Rack.Deal(view.OwnHand.Select(t => t.Value).ToList());
                    Current = Phase.Deal; Active = -1; Notify();
                    float landed = Board.Deal(BuildBoard(view), view.ActivePlayer); SyncedVersion = view.Version;
                    await Seconds(landed);
                    SyncBoard(); // a hand resumed mid-game also has melds and discards
                    while (!token.IsCancellationRequested)
                    {
                        view = Session.GetView(Me);
                        if (view.Result != null) break;
                        if (view.ActivePlayer == humanSeat) await PlayHuman(token); else await PlayBot(view.ActivePlayer, token);
                        TurnsPlayed++;
                    }
                    if (token.IsCancellationRequested) return;
                    await ShowResult();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private async Task PlayBot(int seat, CancellationToken token)
        {
            Active = seat; Current = Phase.Opponent; Notify();
            await Seconds(0.12f);
            var view = Session.GetView(seat);
            TurnPlan witness = null;
            if (view.Phase == RoundPhase.AwaitingDraw)
            {
                await Seconds(0.6f);
                var draw = await Task.Run(() => planner.ChooseDraw(view, token), token);
                await Submit(new DrawCommand(Meta(view), draw.Source), true);
                witness = draw.Witness; view = Session.GetView(seat);
            }
            await Seconds(0.43f);
            var plan = await Task.Run(() => planner.PlanTurn(view, witness, token), token);
            if (plan.Actions.Count > 0)
            {
                // Show the melds landing before the discard (prototype: işle +1150 ms, at +1800 ms).
                var projected = TableEvaluator.Evaluate(PlayerViewProjector.ForEvaluation(view).Value, plan.Actions);
                if (projected.Success) { Board.Sync(BuildBoard(Session.GetView(Me), projected.Value, seat)); Notify(); }
                await Seconds(0.65f);
            }
            var receipt = await Submit(new CommitTurnCommand(Meta(view), plan), false);
            if (!receipt.Accepted)
            {
                // A witness plan can go stale only through a bug; let the planner try again without it.
                Debug.LogWarning("GALAXY101_TABLE bot plan rejected: " + receipt.Error);
                view = Session.GetView(seat); plan = await Task.Run(() => planner.PlanTurn(view, token), token);
                await Submit(new CommitTurnCommand(Meta(view), plan), true);
            }
            await Seconds(0.15f);
        }

        private async Task PlayHuman(CancellationToken token)
        {
            var view = Session.GetView(Me);
            Active = Me; staged.Clear(); warned = false; pickedTile = -1; deckOnly = false;
            Current = view.Phase == RoundPhase.AwaitingDraw ? Phase.Draw : Phase.Discard;
            turnStarted = Time.time; turnStartedReal = Time.realtimeSinceStartup;
            humanDone = new TaskCompletionSource<bool>();
            using (token.Register(() => humanDone.TrySetCanceled())) { Notify(); bool acted = await humanDone.Task; if (!acted) { await AutoPlay(token); hud?.Post(TableHud.ToastKind.Warning, T("msg.timeUp")); } }
            humanDone = null; hud?.Dismiss(T("msg.timeWarn"));
        }

        /// <summary>Timeout (spec/10 §3): draw from the deck, then discard the drawn tile (else the last tile); the planner covers rule corners.</summary>
        private async Task AutoPlay(CancellationToken token)
        {
            if (pickedTile >= 0 && Current == Phase.Discard && staged.Count == 0) await PutBackAsync();
            if (Current == Phase.Draw) { if (!await Draw(DrawSource.Stock, null)) return; await Seconds(0.7f); }
            // The drawn tile, else the rightmost one; never the okey (it would cost +101) nor the tile taken from the pile.
            bool Throwable(int id) => id != PickedTile && !TileCatalog.IsRealJoker(new TileId(id), Indicator);
            int pick = LastDrawn >= 0 && Rack.TryFind(LastDrawn, out _) && Throwable(LastDrawn) ? LastDrawn : Rack.Tiles.Where(Throwable).DefaultIfEmpty(Rack.Tiles.Last()).Last();
            if (await TryCommit(pick, false)) return;
            staged.Clear();
            var view = Session.GetView(Me);
            var plan = await Task.Run(() => planner.PlanTurn(view, token), token);
            var receipt = await Submit(new CommitTurnCommand(Meta(view), plan), true);
            if (receipt.Accepted) AfterCommit();
        }

        /// <summary>
        /// Waiting for players (spec/10 §1, T01–T02): lobby camera, the deck on the felt, bots join at 1.3 / 2.7 / 4.1 s
        /// (Ayşe, Zeynep, Burak; "Davet et" seats one at once); 0.9 s after the table fills a 3-2-1 countdown runs while
        /// the camera descends to the player view.
        /// </summary>
        private async Task SeatPlayers()
        {
            Current = Phase.Seating; Active = -1;
            for (int k = 1; k < 4; k++) Seated[k] = false;
            Board.Clear(); Board.LayoutDeck(106);
            table.CameraRig.GoTo(TableCameraRig.View.Lobby, 0);
            Notify();
            float start = Time.time; var joins = new[] { (2, 1.3f), (1, 2.7f), (3, 4.1f) };
            while (!Seated.All(x => x))
            {
                foreach (var (seat, at) in joins) if (!Seated[seat] && Time.time - start >= at) Invite(seat);
                await Seconds(0.05f);
            }
            await Seconds(0.9f);
            table.CameraRig.GoTo(TableCameraRig.View.Player, 3f);
            for (int n = 3; n >= 1; n--) { Countdown = n; countdownAt = Time.realtimeSinceStartup; Notify(); await Seconds(1); }
            Countdown = 0;
        }

        /// <summary>"Davet et": the empty seat fills immediately.</summary>
        public void Invite(int seat)
        {
            if (Current != Phase.Seating || seat < 1 || seat > 3 || Seated[seat]) return;
            Seated[seat] = true; Notify();
        }

        /// <summary>
        /// Finish moment (spec/10 §7–§8: 3.8 s banner for a finisher, 2.4 s "TAŞ BİTTİ"), then the hand result (T24) for
        /// 15 s or until "Sonraki el". Bot tables award XP only: +340 for the hand winner, +60 otherwise.
        /// </summary>
        private async Task ShowResult()
        {
            var view = Session.GetView(Me); LastResult = view.Result;
            for (int k = 0; k < 4; k++) Totals[k] += view.Result.Scores[k].Total ?? 0;
            int winner = HandWinner(view.Result);
            if (MatchHand == 0) SnapshotProgress();
            MatchHand++;
            for (int k = 0; k < 4; k++) handScores[k].Add(view.Result.Scores[k].Total);
            var rules = TableRules.Current;
            LastXp = humanSeat == Me ? (winner == Me ? rules.XpHandWin : rules.XpHandOther) : 0; XpEarned += LastXp; AddXp(LastXp);
            if (winner == Me) handsWon++; else handsOther++;
            Current = Phase.Finished; Active = -1;
            var info = view.Result.FinishInfo;
            Debug.Log("GALAXY101_TABLE_HAND_END hand=" + HandNumber + " reason=" + info.Reason + " winner=" + info.Winner
                + " scores=" + string.Join(",", view.Result.Scores.Select(x => x.Total?.ToString() ?? "-")) + " turns=" + TurnsPlayed);
            if (info.Winner.HasValue)
            {
                int w = info.Winner.Value; momentSeat = w;
                momentTitle = w == Me ? L("BİTİRDİN!", "YOU FINISHED!") : Upper(Name(w)) + L(" BİTİRDİ!", " FINISHED!");
                momentSub = info.Reason == FinishReason.AllPairs ? L("Çiftle bitirdi · el puanlanmadı", "Finished with pairs · hand not scored")
                    : L("Son taşını desteye kapattı · ", "Closed the last tile on the deck · ") + Signed(view.Result.Scores[w].Total ?? -101) + L(" puan", " points");
            }
            else { momentSeat = -1; momentTitle = L("TAŞ BİTTİ", "DECK EMPTY"); momentSub = L("En düşük puanlı oyuncu eli alır", "Lowest score takes the hand"); }
            momentOn = true; momentAt = Time.realtimeSinceStartup; Notify();
            await Seconds(info.Winner.HasValue ? 3.8f : 2.4f);
            momentOn = false;
            ResultOpen = true; resultAt = Time.realtimeSinceStartup; nextHandSignal = new TaskCompletionSource<bool>(); Notify();
            await Task.WhenAny(nextHandSignal.Task, Seconds(15));
            ResultOpen = false; nextHandSignal = null; Notify();
            if (MatchHand >= TableRules.Current.MatchHands) await ShowMatchEnd();
        }

        /// <summary>
        /// Match end (T45–T46): lowest total wins; place bonus XP (500 / 200 / 80 / 0), then "Tekrar oyna" starts a new
        /// match or "Lobiye dön" leaves. Waits for the player.
        /// </summary>
        private async Task ShowMatchEnd()
        {
            var order = Enumerable.Range(0, 4).OrderBy(k => Totals[k]).ThenBy(k => k).ToList();
            place = order.IndexOf(Me) + 1;
            xpBonus = humanSeat == Me ? TableRules.Current.XpPlace[place - 1] : 0; AddXp(xpBonus); XpEarned += xpBonus;
            MatchOpen = true; matchStartedAt = Time.realtimeSinceStartup; matchSignal = new TaskCompletionSource<string>(); Notify();
            Debug.Log("GALAXY101_TABLE_MATCH_END place=" + place + " totals=" + string.Join(",", Totals));
            string choice = await matchSignal.Task;
            MatchOpen = false; matchSignal = null;
            if (choice == "lobby") { LeaveNow(); return; }
            MatchHand = 0; handsWon = handsOther = 0;
            for (int k = 0; k < 4; k++) { Totals[k] = 0; handScores[k].Clear(); }
            Notify();
        }

        private float matchStartedAt;

        /// <summary>"Tekrar oyna" / "Lobiye dön" on the match end card.</summary>
        public void ChooseAfterMatch(string choice) => matchSignal?.TrySetResult(choice);

        private void SnapshotProgress()
        {
            var model = hud != null ? hud.Model : null;
            matchStartLevel = model?.Level ?? 1; matchStartXp = model?.Xp ?? 0;
        }

        /// <summary>Bot tables award XP only; it is kept in the menu profile (level needs 500 × level XP).</summary>
        private void AddXp(long amount)
        {
            var model = hud != null ? hud.Model : null;
            if (model == null || amount <= 0) return;
            int level = model.Level; long xp = model.Xp;
            TableRules.Current.Add(ref level, ref xp, amount);
            model.Level = level; model.Xp = xp; model.Persist();
        }

        /// <summary>Hand result v2 (T42–T44): explained points, penalty column with reasons, multiplier badges.</summary>
        private TableHud.ResultInfo ResultInfo(PlayerView view)
        {
            var info = LastResult.FinishInfo; int winner = HandWinner(LastResult);
            var scores = LastResult.Scores;
            var result = new TableHud.ResultInfo
            {
                Title = L(MatchHand + ". EL BİTTİ", "HAND " + MatchHand + " OVER"),
                Sub = info.Winner.HasValue ? Upper(FirstName(info.Winner.Value)) + L(" BİTİRDİ", " FINISHED") : info.Reason == FinishReason.AllPairs ? Upper(T("noteFinPairs")) : L("TAŞ BİTTİ", "DECK EMPTY"),
                Head = winner == Me ? L("Kazandın!", "You won!") : FirstName(winner) + L(" kazandı", " won"),
                Xp = LastXp, TotalXp = XpEarned, Winner = winner, StartedAt = resultAt, Seconds = 15,
                Multiplier = scores.Max(x => x.M), Unscored = info.Reason == FinishReason.AllPairs, LastHand = MatchHand >= TableRules.Current.MatchHands,
            };
            foreach (int k in Enumerable.Range(0, 4).OrderBy(k => scores[k].Total ?? 0).ThenBy(k => k))
            {
                var score = scores[k]; var kind = k == Me ? view.OwnOpening : view.Players[k].Opening;
                bool finished = info.Winner == k;
                var row = new TableHud.ResultRow
                {
                    Seat = k, Total = Totals[k], Penalty = score.HasScore ? score.P : 0,
                    Hand = score.HasScore ? score.B * score.M : (int?)null,
                    Note = finished ? L("bitirdi", "finished")
                        : kind == OpeningKind.Pairs ? T("notePairs") : kind != OpeningKind.None ? L("açtı", "opened") : L("açmadı", "not opened"),
                    PenaltyWhy = string.Join(", ", penaltyReasons[k].Select(r => r.Why).Distinct()),
                };
                if (finished && info.Reason == FinishReason.DiscardWin)
                {
                    if (info.DiscardedRealJoker) row.Badges.Add((T("mulOkey"), true));
                    if (info.WinnerOpening == OpeningKind.Pairs) row.Badges.Add((T("mulPairs"), true));
                    if (info.WinnerOpenedThisTurn) row.Badges.Add((T("mulHand"), true));
                }
                if (!finished && kind == OpeningKind.Pairs && score.HasScore) row.Badges.Add((T("pairsHand"), false));
                if (score.HasScore) row.Explain = Explain(score, finished, kind);
                result.Rows.Add(row);
            }
            return result;
        }

        /// <summary>"−101 × 2 = −202", "17 × 2 × 2 = 68": the base, the pairs ×2, then each finish ×2.</summary>
        private static string Explain(ScoreBreakdown score, bool finished, OpeningKind kind)
        {
            int baseValue = finished ? -101 : kind == OpeningKind.None ? 202 : score.N + 101 * score.J;
            var factors = new List<int>();
            if (!finished && kind == OpeningKind.Pairs) factors.Add(2);
            for (int m = score.M; m > 1; m /= 2) factors.Add(2);
            if (factors.Count == 0) return null;
            string Number(int v) => v < 0 ? "−" + (-v).ToString("N0", new System.Globalization.CultureInfo("tr-TR")) : v.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));
            return Number(baseValue) + string.Concat(factors.Select(f => " × " + f)) + " = " + Number(score.B * score.M);
        }

        /// <summary>Match end card data (T45–T46).</summary>
        private TableHud.MatchInfo MatchInfo()
        {
            var rules = TableRules.Current; int hands = rules.MatchHands;
            var model = hud?.Model;
            int level = model?.Level ?? 1; long xp = model?.Xp ?? 0;
            var order = Enumerable.Range(0, 4).OrderBy(k => Totals[k]).ThenBy(k => k).ToList();
            int leader = order[0];
            var m = new TableHud.MatchInfo
            {
                Kick = T("msKick", hands), Won = place == 1, Place = place, Hands = hands, StartedAt = matchStartedAt,
                Head = place == 1 ? T("msWin") : T("msPlace", place),
                Sub = place == 1 ? T("msWinSub", hands) : T("msLoseSub", FirstName(leader), Totals[Me] - Totals[leader]),
                Xp = handsWon * rules.XpHandWin + handsOther * rules.XpHandOther + xpBonus,
                LevelFrom = matchStartLevel, LevelTo = level, LevelUp = level > matchStartLevel,
                FromFraction = (float)matchStartXp / rules.XpForLevel(matchStartLevel), ToFraction = (float)xp / rules.XpForLevel(level),
                XpLeft = T("msXpLeft", (rules.XpForLevel(level) - xp).ToString("N0", new System.Globalization.CultureInfo("tr-TR"))),
                ByHand = handScores.Select(list => list.ToArray()).ToArray(),
            };
            foreach (int k in order) m.Standings.Add((k, Totals[k]));
            if (handsWon > 0) m.XpLines.Add((T("msXpWins"), handsWon + " × " + rules.XpHandWin, handsWon * rules.XpHandWin));
            if (handsOther > 0) m.XpLines.Add((T("msXpOther"), handsOther + " × " + rules.XpHandOther, handsOther * rules.XpHandOther));
            if (place <= 3 && xpBonus > 0) m.XpLines.Add((T("msXp" + place), "", xpBonus));
            return m;
        }

        /// <summary>Lowest hand score takes the hand; a pairs finish (unscored) goes to the finisher.</summary>
        private static int HandWinner(RoundResult result)
        {
            if (result.FinishInfo.Reason == FinishReason.AllPairs && result.FinishInfo.Winner.HasValue) return result.FinishInfo.Winner.Value;
            int best = 0;
            for (int k = 1; k < 4; k++) if ((result.Scores[k].Total ?? int.MaxValue) < (result.Scores[best].Total ?? int.MaxValue)) best = k;
            return best;
        }

        /// <summary>"Sonraki el": skip the rest of the 15 s.</summary>
        public void NextHand() => nextHandSignal?.TrySetResult(true);

        private string Name(int seat) => hud != null ? hud.SeatName(seat) : "P" + seat;
        private string FirstName(int seat) => hud != null ? hud.SeatFirstName(seat) : "P" + seat;
        private string Upper(string text) => text.ToUpper(new System.Globalization.CultureInfo(hud != null && hud.L("tr", "en") == "en" ? "en-US" : "tr-TR"));
        private static string Signed(int value) => value > 0 ? "+" + value : value < 0 ? "−" + (-value) : "0";

        // ---------- commands ----------
        private static CommandMeta Meta(PlayerView view) => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);

        private async Task<CommandReceipt> Submit(GameCommand command, bool required)
        {
            var receipt = await Execute(command);
            if (receipt.Accepted) { Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort); SyncBoard(); }
            else if (required && receipt.Error != ErrorCode.Cancelled) throw new InvalidOperationException("Table command rejected: " + receipt.Error);
            return receipt;
        }

        /// <summary>Draw for me: from the deck or the previous player's last discard; <paramref name="slot"/> = drop target.</summary>
        public async Task<bool> Draw(DrawSource source, RackSlot? slot)
        {
            if (!CanDraw || source == DrawSource.PreviousDiscard && deckOnly) return false;
            var view = Session.GetView(Me);
            Current = Phase.Discard; // block double draws while the command runs
            var receipt = await Execute(new DrawCommand(Meta(view), source));
            if (!receipt.Accepted) { Current = Phase.Draw; Message(TableHud.ToastKind.Error, L("Çekilemedi", "Could not draw")); return false; }
            var after = Session.GetView(Me); int drawn = after.PendingDraw.Tile.Value;
            Rack.Add(drawn, TileCodes.FaceFor(Indicator), autoSort, slot); LastDrawn = drawn;
            if (source == DrawSource.PreviousDiscard) { pickedTile = drawn; Message(TableHud.ToastKind.Info, T("msg.canPutBack")); }
            SyncBoard(); Notify(); return true;
        }

        /// <summary>GERİ BIRAK (T39): the unused pile tile goes back; this turn then draws from the deck only.</summary>
        public void PutBack() { _ = PutBackAsync(); }

        private async Task PutBackAsync()
        {
            if (!CanDiscard || PickedTile < 0) return;
            staged.Clear();
            var receipt = await Execute(new ReturnDiscardCommand(Meta(Session.GetView(Me))));
            if (!receipt.Accepted) { Message(TableHud.ToastKind.Error, L("Bu hamle geçersiz", "That move is not allowed")); return; }
            Board.ForceDuration(pickedTile, 0.3f); // T39: back on the pile in 300 ms
            pickedTile = -1; deckOnly = true; LastDrawn = -1;
            Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort);
            Current = Phase.Draw; SyncBoard(); Notify();
        }

        public async void Discard(int id)
        {
            if (!CanDiscard || !Rack.TryFind(id, out _)) return;
            if (id == PickedTile) { Message(TableHud.ToastKind.Error, T("msg.cantDiscardPicked")); return; }
            await TryCommit(id, true);
        }

        /// <summary>spec/17 §3: no confirmation; a playable tile or the okey is thrown knowingly and the penalty is written.</summary>
        private async Task<bool> TryCommit(int id, bool explain)
        {
            var view = Session.GetView(Me);
            var plan = new TurnPlan(staged.ToArray(), new TileId(id), true);
            var receipt = await Execute(new CommitTurnCommand(Meta(view), plan));
            if (receipt.Accepted) { Rack.Remove(id); AfterCommit(); return true; }
            if (!explain) return false;
            switch (receipt.Error)
            {
                case ErrorCode.DiscardMustBeUsed: Message(TableHud.ToastKind.Error, T("msg.cantDiscardPicked")); break;
                case ErrorCode.OpeningRequired: Message(TableHud.ToastKind.Error, T("msg.openFirst")); break;
                default: Message(TableHud.ToastKind.Error, L("Bu hamle geçersiz", "That move is not allowed")); break;
            }
            return false;
        }

        // ---------- penalties (spec/17 §3) ----------
        /// <summary>
        /// Submit and explain every penalty the command caused: whoever's total grew, and why — the actor's discard
        /// (okey or playable tile), the previous player feeding an opening (value × 10), an okey taken from its owner's
        /// meld, or a short opening. Mine become a Penalty toast, opponents' an avatar balloon; opponents' openings an
        /// event balloon.
        /// </summary>
        private async Task<CommandReceipt> Execute(GameCommand command)
        {
            int actor = command.Meta.Actor;
            var before = Session.GetView(actor);
            var penalties = before.Players.Select(p => p.Penalties).ToArray(); penalties[actor] = before.OwnPenalties;
            var openings = before.Players.Select(p => p.Opening).ToArray(); openings[actor] = before.OwnOpening;
            var pending = before.PendingDraw;
            var receipt = await Session.SubmitAsync(command, life.Token);
            if (!receipt.Accepted) return receipt;
            var after = Session.GetView(Me);
            for (int k = 0; k < 4; k++)
            {
                int now = k == Me ? after.OwnPenalties : after.Players[k].Penalties;
                int delta = now - penalties[k];
                if (delta > 0) ExplainPenalty(command, actor, k, delta, pending);
                var opening = k == Me ? after.OwnOpening : after.Players[k].Opening;
                if (k != Me && openings[k] == OpeningKind.None && opening != OpeningKind.None)
                    hud?.Balloon(k, opening == OpeningKind.Pairs ? T("ev.pairs", FirstName(k), OpenedValue(k)) : T("ev.opened", FirstName(k), OpenedValue(k)), false);
            }
            return receipt;
        }

        private void ExplainPenalty(GameCommand command, int actor, int seat, int amount, DrawReceipt pending)
        {
            handPenalties[seat] += amount;
            string why, mine, theirs;
            if (command is ShortOpeningCommand shortOpening)
            {
                why = "why.short";
                var eval = HandEvaluation.Of(Rack, Indicator);
                mine = shortOpening.Opening == OpeningKind.Pairs ? T("pen.shortPairs", eval.Pairs.Count) : T("pen.short", eval.Sum);
                theirs = T("ev.penShort", FirstName(seat));
            }
            else if (seat == actor && command is CommitTurnCommand commit)
            {
                bool okey = TileCatalog.IsRealJoker(commit.Plan.Discard, Indicator);
                why = okey ? "why.okey" : "why.playable";
                mine = T(okey ? "pen.okey" : "pen.playable");
                theirs = T(okey ? "penBubbleOkey" : "penBubbleTile");
            }
            else if (seat == (actor + 3) % 4 && pending != null && pending.Source == DrawSource.PreviousDiscard)
            {
                int number = amount / 10;
                // T34: the tile that fed the opening glows in the opener's meld for 1.2 s.
                glowTile = pending.Tile.Value; glowUntil = Time.unscaledTime + 1.2f;
                why = "why.fed"; mine = T("pen.fed", number, FirstName(actor)); theirs = T("ev.penFed", FirstName(seat), number, amount);
                penaltyReasons[seat].Add((T(why, number), amount));
                Announce(seat, mine, theirs, amount, null); return;
            }
            else { why = "why.okeyTaken"; mine = T("pen.okeyTaken", FirstName(actor)); theirs = T("ev.penOkeyTaken"); }
            penaltyReasons[seat].Add((T(why), amount));
            Announce(seat, mine, theirs, amount, command is ShortOpeningCommand ? T("pen.returned") : null);
        }

        private void Announce(int seat, string mine, string theirs, int amount, string sub)
        {
            if (seat == Me && humanSeat == Me) hud?.Post(TableHud.ToastKind.Penalty, mine, sub, amount);
            else hud?.Balloon(seat, theirs, true);
        }

        private void AfterCommit()
        {
            staged.Clear(); LastDrawn = -1; pickedTile = -1; deckOnly = false;
            Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort);
            Current = Phase.Opponent; Active = -1; SyncBoard(); Notify();
            humanDone?.TrySetResult(true);
        }

        /// <summary>Aç / Yere aç: rack segments that form runs or sets go to the table (staged until the discard).</summary>
        public void OpenRuns()
        {
            if (!CanDiscard) return;
            var eval = HandEvaluation.Of(Rack, Indicator);
            var opening = OpenedKind(Me);
            if (opening == OpeningKind.Pairs) { Message(TableHud.ToastKind.Error, T("msg.pairsOnly")); return; }
            if (opening == OpeningKind.None && eval.Sum < 101) { _ = ShortOpening(OpeningKind.Series); return; }
            if (eval.Groups.Count == 0) return;
            var actions = opening == OpeningKind.None
                ? new List<TableAction> { new OpenGroups(OpeningKind.Series, eval.Groups) }
                : eval.Groups.Select(g => (TableAction)new AddGroup(g)).ToList();
            Stage(actions, eval.Groups);
        }

        /// <summary>Çift aç: at least five rack pairs (or more pairs once opened with pairs).</summary>
        public void OpenPairs()
        {
            if (!CanDiscard) return;
            var eval = HandEvaluation.Of(Rack, Indicator);
            var opening = OpenedKind(Me);
            if (opening == OpeningKind.Series) return;
            if (opening == OpeningKind.None && eval.Pairs.Count < 5) { _ = ShortOpening(OpeningKind.Pairs); return; }
            if (eval.Pairs.Count == 0) return;
            var actions = opening == OpeningKind.None ? new List<TableAction> { new OpenGroups(OpeningKind.Pairs, eval.Pairs) } : eval.Pairs.Select(p => (TableAction)new AddGroup(p)).ToList();
            Stage(actions, eval.Pairs);
        }

        /// <summary>Eksik açılış: the engine writes +101; the groups stay on the rack (spec/17 §3, T35).</summary>
        private async Task ShortOpening(OpeningKind kind)
        {
            if (staged.Count > 0) { staged.Clear(); Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort); }
            await Execute(new ShortOpeningCommand(Meta(Session.GetView(Me)), kind));
            SyncBoard(); Notify();
        }

        private void Stage(List<TableAction> actions, List<MeldProposal> groups)
        {
            var next = staged.Concat(actions).ToList();
            var result = Evaluate(next);
            if (!result.Success) { Message(TableHud.ToastKind.Error, L("Bu perler açılamaz", "Those sets cannot be laid")); return; }
            staged.Clear(); staged.AddRange(next);
            foreach (var group in groups) foreach (var tile in group.Tiles) Rack.Remove(tile.Id.Value);
            SyncBoard(); Notify();
        }

        // ---------- okeys on the table (spec/17 §2, §8) ----------
        /// <summary>Every okey (real joker) in a table meld with the face it stands for, as the board shows it.</summary>
        public List<(int Id, TileFace Face, int Meld, int Owner)> TableOkeys()
        {
            var list = new List<(int, TileFace, int, int)>();
            if (Session == null || Current == Phase.Waiting || Current == Phase.Seating) return list;
            foreach (var meld in (staged.Count > 0 && Active == Me ? Projection() : PlayerViewProjector.ForEvaluation(Session.GetView(Me)).Value).Table)
                foreach (var tile in meld.Group.Tiles)
                    if (TileCatalog.IsRealJoker(tile.Id, Indicator)) list.Add((tile.Id.Value, tile.RepresentedFace, meld.Id, meld.Owner));
            return list;
        }

        public bool IsTableOkey(int id) => TableOkeys().Any(o => o.Id == id);

        private string FaceName(TileFace face)
            => T("color." + (face.Color == TileColor.Red ? "R" : face.Color == TileColor.Blue ? "B" : face.Color == TileColor.Yellow ? "Y" : "K")) + " " + face.Number;

        private static string FaceColor(TileFace face)
            => face.Color == TileColor.Red ? "#D7263D" : face.Color == TileColor.Blue ? "#1F5FBF" : face.Color == TileColor.Yellow ? "#E09A12" : "#24242C";

        /// <summary>The input reports the rack tile being dragged (−1 when released): matching okeys light up (T47).</summary>
        public void SetDragTile(int id) { if (dragTile == id) return; dragTile = id; Notify(); }

        /// <summary>Hover shows what an okey stands for; a 500 ms press shows how to take it (T33, T48).</summary>
        public void ShowOkeyTip(int id, bool take)
        {
            if (tipOkey == id && tipTake == take) { tipUntil = Time.unscaledTime + 2.5f; return; }
            tipOkey = id; tipTake = take; tipUntil = Time.unscaledTime + 2.5f; Notify();
        }

        public void HideOkeyTip() { if (tipOkey < 0 || tipTake) return; tipOkey = -1; Notify(); }

        /// <summary>
        /// Take an okey from the table (§8, T47): only after opening, only with the real tile it stands for. The tile takes
        /// the okey's place, the okey comes to my rack; whoever laid it pays +101 when the turn is committed.
        /// </summary>
        public void TakeOkey(int tileId, int okeyId)
        {
            if (!CanDiscard || !Rack.TryFind(tileId, out _)) return;
            var okey = TableOkeys().FirstOrDefault(o => o.Id == okeyId);
            if (okey.Id != okeyId) return;
            if (OpenedKind(Me) == OpeningKind.None) { Message(TableHud.ToastKind.Error, T("msg.openFirst")); return; }
            bool fits = !TileCatalog.IsRealJoker(new TileId(tileId), Indicator) && TileCatalog.GetNonJokerFace(new TileId(tileId), Indicator) == okey.Face;
            var next = staged.Concat(new[] { (TableAction)new SwapJoker(okey.Meld, new TileId(okeyId), new TileId(tileId)) }).ToList();
            if (!fits || !Evaluate(next).Success)
            {
                invalidOkey = okeyId; invalidUntil = Time.unscaledTime + 1.2f;
                Message(TableHud.ToastKind.Error, T("msg.okeyOnlyWith", FaceName(okey.Face))); return;
            }
            staged.Clear(); staged.AddRange(next);
            Rack.Remove(tileId); Rack.Add(okeyId, TileCodes.FaceFor(Indicator), autoSort);
            Board.ForceDuration(okeyId, 0.45f); // T47: the okey flies to the rack in 450 ms
            SyncBoard(); Notify();
        }

        /// <summary>İşle: drop one rack tile onto a table meld (front or back; jokers take the face that fits).</summary>
        public void LayOff(int id, int meldId)
        {
            if (!CanMeld && !(CanDiscard && staged.OfType<OpenGroups>().Any())) { Message(TableHud.ToastKind.Error, T("msg.openFirst")); return; }
            var state = Projection(); var meld = state.Table.FirstOrDefault(m => m.Id == meldId);
            if (meld == null || meld.Group.Kind == MeldKind.Pair) { Message(TableHud.ToastKind.Error, T("msg.cantIsle")); return; }
            if (Rack.Count <= 1) { Message(TableHud.ToastKind.Error, T("msg.lastTile")); return; }
            foreach (var face in CandidateFaces(id, meld))
                foreach (bool prepend in new[] { false, true })
                {
                    var next = staged.Concat(new[] { (TableAction)new ExtendMeld(meldId, prepend, new[] { new PlacedTile(new TileId(id), face) }) }).ToList();
                    if (!Evaluate(next).Success) continue;
                    staged.Clear(); staged.AddRange(next); Rack.Remove(id); SyncBoard(); Notify(); return;
                }
            Message(TableHud.ToastKind.Error, T("msg.cantIsle"));
        }

        private IEnumerable<TileFace> CandidateFaces(int id, Meld meld)
        {
            if (!TileCatalog.IsRealJoker(new TileId(id), Indicator)) { yield return TileCatalog.GetNonJokerFace(new TileId(id), Indicator); yield break; }
            var faces = meld.Group.Tiles.Select(t => t.RepresentedFace).ToList();
            if (meld.Group.Kind == MeldKind.Run)
            {
                int lo = faces.Min(f => f.Number), hi = faces.Max(f => f.Number);
                if (hi < 13) yield return new TileFace(faces[0].Color, hi + 1);
                if (lo > 1) yield return new TileFace(faces[0].Color, lo - 1);
            }
            else foreach (TileColor c in Enum.GetValues(typeof(TileColor))) if (faces.All(f => f.Color != c)) yield return new TileFace(c, faces[0].Number);
        }

        private RuleResult<RoundState> Evaluate(IReadOnlyList<TableAction> actions)
        {
            var root = PlayerViewProjector.ForEvaluation(Session.GetView(Me));
            return root.Success ? TableEvaluator.Evaluate(root.Value, actions) : root;
        }

        /// <summary>The authoritative state with my staged actions applied (what the board shows during my turn).</summary>
        private RoundState Projection()
        {
            var root = PlayerViewProjector.ForEvaluation(Session.GetView(Me)).Value;
            if (staged.Count == 0) return root;
            var result = TableEvaluator.Evaluate(root, staged);
            return result.Success ? result.Value : root;
        }

        public void SortRack(bool runs) { if (!CanArrange) return; Rack.Sort(runs, TileCodes.FaceFor(Indicator)); SyncBoard(); Notify(); }

        /// <summary>Commit a rack rearrangement the input previewed (singlePlan / groupPlan).</summary>
        public void ApplyRackPlan(IReadOnlyDictionary<int, RackSlot> plan) { Rack.Apply(plan); SyncBoard(); Notify(); }

        // ---------- views ----------
        public void SyncBoard()
        {
            if (Board == null || Session == null) return;
            var view = Session.GetView(Me);
            Board.Sync(BuildBoard(view, staged.Count > 0 && Active == Me ? Projection() : null, Me));
            SyncedVersion = view.Version;
        }

        private BoardState BuildBoard(PlayerView view, RoundState projected = null, int projectedSeat = Me)
        {
            var state = new BoardState { Rack = Rack, Stock = view.StockCount, Indicator = view.Indicator.Value };
            for (int k = 0; k < 4; k++)
            {
                state.HandCounts[k] = k == Me ? Rack.Count : view.Players[k].HandCount;
                state.Discards[k].AddRange(view.Discards[k].Select(t => t.Value));
            }
            var melds = projected != null ? projected.Table : view.Table;
            if (projected != null && projectedSeat != Me) state.HandCounts[projectedSeat] = projected.Players[projectedSeat].Hand.Count;
            foreach (var meld in melds) state.Melds.Add((meld.Id, meld.Owner, DisplayOrder(meld)));
            return state;
        }

        private static int[] DisplayOrder(Meld meld)
        {
            var tiles = meld.Group.Tiles;
            return (meld.Group.Kind == MeldKind.Run ? tiles.OrderBy(t => t.RepresentedFace.Number) : tiles.AsEnumerable()).Select(t => t.Id.Value).ToArray();
        }

        public OpeningKind OpenedKind(int seat)
        {
            if (Session == null) return OpeningKind.None;
            var view = Session.GetView(Me);
            var kind = seat == Me ? view.OwnOpening : view.Players[seat].Opening;
            if (kind == OpeningKind.None && seat == Me) { var open = staged.OfType<OpenGroups>().FirstOrDefault(); if (open != null) return open.Opening; }
            return kind;
        }

        /// <summary>Opened total (series: tile values on the table) or pair count.</summary>
        public int OpenedValue(int seat)
        {
            var kind = OpenedKind(seat); if (kind == OpeningKind.None) return 0;
            var table = seat == Me && staged.Count > 0 ? Projection().Table : Session.GetView(Me).Table;
            var mine = table.Where(m => m.Owner == seat).ToList();
            return kind == OpeningKind.Pairs ? mine.Count(m => m.Group.Kind == MeldKind.Pair) : mine.Sum(m => m.Group.Tiles.Sum(t => t.RepresentedFace.Number));
        }

        private string L(string tr, string en) => hud != null ? hud.L(tr, en) : tr;

        private string T(string key, params object[] args) => TableStrings.Get(key, hud != null ? hud.L("tr", "en") : "tr", args);

        public void Message(TableHud.ToastKind kind, string text, string sub = null) => hud?.Post(kind, text, sub);

        private void OnHudCommand(string command)
        {
            switch (command)
            {
                case "open": OpenRuns(); break;
                case "pairs": OpenPairs(); break;
                case "sort-run": SortRack(true); break;
                case "sort-pair": SortRack(false); break;
                case "exit": RequestExit(); break;
                case "putback": PutBack(); break;
                case "exit-stay": CancelExit(); break;
                case "exit-leave": ConfirmExit(); break;
                case "next": NextHand(); break;
                case "again": ChooseAfterMatch("again"); break;
                case "lobby": ChooseAfterMatch("lobby"); break;
                default:
                    if (command.StartsWith("invite:", StringComparison.Ordinal) && int.TryParse(command.Substring(7), out int seat)) Invite(seat);
                    break;
            }
        }

        /// <summary>Exit button (T25): the confirmation card; Esc or "Masada kal" closes it, "Kalk" leaves.</summary>
        public void RequestExit() { if (leaving) return; ExitOpen = true; Notify(); }
        public void CancelExit() { if (!ExitOpen) return; ExitOpen = false; Notify(); }

        /// <summary>"Kalk": green notice, then the main menu after 1.7 s (spec/10 §10).</summary>
        public void ConfirmExit()
        {
            if (leaving) return;
            leaving = true; ExitOpen = false; life?.Cancel();
            Message(TableHud.ToastKind.Success, L("Masadan kalktın · lobiye dönülüyor", "You left the table · returning to the lobby"));
            Debug.Log("GALAXY101_TABLE_EXIT scene=" + menuScene);
            StartCoroutine(LeaveAfter(1.7f));
        }

        /// <summary>"Lobiye dön": straight to the main menu.</summary>
        private void LeaveNow()
        {
            if (leaving) return;
            leaving = true; life?.Cancel();
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(menuScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private System.Collections.IEnumerator LeaveAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(menuScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private void Notify()
        {
            if (hud != null && Session != null && Current != Phase.Waiting) hud.Render(HudState());
            Changed?.Invoke();
        }

        public TableHud.State HudState()
        {
            var view = Session.GetView(Me);
            var eval = HandEvaluation.Of(Rack, Indicator);
            var s = new TableHud.State
            {
                Phase = Current == Phase.Seating ? "seat" : Current == Phase.Deal ? "deal" : Current == Phase.Draw ? "draw" : Current == Phase.Discard ? "discard" : Current == Phase.Finished ? "fin" : "opp",
                Active = Active, DeckCount = view.StockCount, TurnSeconds = Mathf.RoundToInt(turnSeconds), TurnStartedAt = turnStartedReal,
                Warn = HumanTurn && warned, TurnHint = turnHint, ShowIndicator = Current != Phase.Deal && Current != Phase.Seating, FirstTurn = view.Phase == RoundPhase.PlanningTurn && view.PendingDraw == null,
                Pairs = eval.Pairs.Count, PickedTile = PickedTile, DeckOnly = deckOnly && HumanTurn, ShowPutBack = PickedTile >= 0 && staged.Count == 0,
                Countdown = Countdown, CountdownAt = countdownAt, ExitOpen = ExitOpen,
                Hand = Current == Phase.Finished || MatchOpen ? MatchHand : MatchHand + 1, MatchHands = TableRules.Current.MatchHands,
            };
            for (int k = 0; k < 4; k++) s.Seated[k] = Current != Phase.Seating || Seated[k];
            if (Current == Phase.Seating) s.DeckCount = 106;
            if (hud?.Model != null) { s.Level = hud.Model.Level; s.XpInLevel = hud.Model.Xp; s.XpNeed = TableRules.Current.XpForLevel(hud.Model.Level); }
            if (momentOn) s.Moment = new TableHud.MomentInfo { Title = momentTitle, Sub = momentSub, Seat = momentSeat, StartedAt = momentAt };
            for (int k = 0; k < 4; k++)
            {
                s.TileCounts[k] = k == Me ? Rack.Count : view.Players[k].HandCount;
                s.Opened[k] = OpenedValue(k); s.OpenedPairs[k] = OpenedKind(k) == OpeningKind.Pairs;
                s.Penalties[k] = k == Me ? view.OwnPenalties : view.Players[k].Penalties;
            }
            s.OpenSum = s.Opened[Me] > 0 ? s.Opened[Me] : eval.Sum;
            s.GlowTile = glowTile;
            var dragFace = dragTile >= 0 && !TileCatalog.IsRealJoker(new TileId(dragTile), Indicator) ? TileCatalog.GetNonJokerFace(new TileId(dragTile), Indicator) : (TileFace?)null;
            bool canTake = CanDiscard && OpenedKind(Me) != OpeningKind.None;
            foreach (var okey in TableOkeys())
            {
                bool target = canTake && dragFace.HasValue && dragFace.Value == okey.Face;
                s.Okeys.Add(new TableHud.OkeyMark { Id = okey.Id, Number = okey.Face.Number.ToString(), Color = FaceColor(okey.Face), Target = target, Invalid = okey.Id == invalidOkey });
                if (target) s.DragBanner = T("takeOkeyBanner", FaceName(okey.Face));
                if (okey.Id == tipOkey) { s.OkeyTipId = tipOkey; s.OkeyTip = tipTake ? T("takeOkeyHint", FaceName(okey.Face)) : T("okStandsFor", FaceName(okey.Face)); }
            }
            // spec/17 §3: Aç / Çift aç stay pressable below the threshold (a short opening costs +101).
            s.CanOpen = CanDiscard && (s.Opened[Me] > 0 ? !s.OpenedPairs[Me] && eval.Groups.Count > 0 : true);
            s.CanPair = CanDiscard && (s.OpenedPairs[Me] ? eval.Pairs.Count > 0 : s.Opened[Me] == 0);
            if (ResultOpen && LastResult != null) s.Result = ResultInfo(view);
            if (MatchOpen) s.Match = MatchInfo();
            return s;
        }
    }
}
