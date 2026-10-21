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

        public enum Phase { Waiting, Deal, Draw, Discard, Opponent, Finished }

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
        public bool CanDiscard => HumanTurn && Current == Phase.Discard;
        public bool CanArrange => Current != Phase.Waiting && Current != Phase.Deal;
        public bool IOpened => OpenedKind(Me) != OpeningKind.None;
        public bool CanMeld => CanDiscard && IOpened;
        public event Action Changed;

        private readonly BotPlanner planner = new BotPlanner();
        private readonly List<TableAction> staged = new List<TableAction>();
        private readonly List<(float At, TaskCompletionSource<bool> Done)> timers = new List<(float, TaskCompletionSource<bool>)>();
        private CancellationTokenSource life;
        private TaskCompletionSource<bool> humanDone;
        private float turnStarted, turnStartedReal;
        private int penaltyArmed = -1;
        private string flash; private bool flashBad; private float flashUntil;
        private bool warned;
        private System.Random random;
        private float exitArmedUntil = -1;
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
            Session = new LocalSession();
            life = new CancellationTokenSource();
            random = seed != 0 ? new System.Random((int)seed) : new System.Random();
            if (hud != null) hud.Command += OnHudCommand;
            _ = Run(life.Token);
        }

        /// <summary>Test hook: configure before Start (seat −1 = bots everywhere).</summary>
        public void Configure(int human, uint fixedSeed, float seconds = 30) { humanSeat = human; seed = fixedSeed; turnSeconds = seconds; }

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
                if (!warned && warnSeconds > 0 && left <= warnSeconds) { warned = true; Notify(); }
                if (left <= 0) { humanDone?.TrySetResult(false); }
            }
            if (flash != null && Time.unscaledTime > flashUntil) { flash = null; Notify(); }
        }

        // ---------- flow ----------
        private async Task Run(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    uint handSeed = seed != 0 ? seed + (uint)HandNumber : (uint)random.Next(1, int.MaxValue);
                    if (HandNumber == 0) Session.Start(handSeed, humanSeat == Me ? Me : (int?)null); else Session.StartNextRound(handSeed);
                    HandNumber++; LastResult = null; staged.Clear(); LastDrawn = -1;
                    var view = Session.GetView(Me);
                    Indicator = TileCodes.IndicatorFace(view.Indicator);
                    Rack.Deal(view.OwnHand.Select(t => t.Value).ToList());
                    Current = Phase.Deal; Active = -1; Notify();
                    float landed = Board.Deal(BuildBoard(view), view.ActivePlayer); SyncedVersion = view.Version;
                    await Seconds(landed);
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
            Active = Me; staged.Clear(); penaltyArmed = -1; warned = false;
            Current = view.Phase == RoundPhase.AwaitingDraw ? Phase.Draw : Phase.Discard;
            turnStarted = Time.time; turnStartedReal = Time.realtimeSinceStartup;
            humanDone = new TaskCompletionSource<bool>();
            using (token.Register(() => humanDone.TrySetCanceled())) { Notify(); bool acted = await humanDone.Task; if (!acted) await AutoPlay(token); }
            humanDone = null;
        }

        /// <summary>Timeout (spec/10 §3): draw from the deck, then discard the drawn tile (else the last tile); the planner covers rule corners.</summary>
        private async Task AutoPlay(CancellationToken token)
        {
            if (Current == Phase.Draw) { if (!await Draw(DrawSource.Stock, null)) return; await Seconds(0.7f); }
            int pick = LastDrawn >= 0 && Rack.TryFind(LastDrawn, out _) ? LastDrawn : Rack.Tiles.LastOrDefault();
            if (await TryCommit(pick, false)) return;
            staged.Clear();
            var view = Session.GetView(Me);
            var plan = await Task.Run(() => planner.PlanTurn(view, token), token);
            var receipt = await Submit(new CommitTurnCommand(Meta(view), plan), true);
            if (receipt.Accepted) AfterCommit();
        }

        private async Task ShowResult()
        {
            var view = Session.GetView(Me); LastResult = view.Result;
            for (int k = 0; k < 4; k++) Totals[k] += view.Result.Scores[k].Total ?? 0;
            Current = Phase.Finished; Active = -1; Notify();
            Debug.Log("GALAXY101_TABLE_HAND_END hand=" + HandNumber + " reason=" + view.Result.FinishInfo.Reason + " winner=" + view.Result.FinishInfo.Winner
                + " scores=" + string.Join(",", view.Result.Scores.Select(s => s.Total?.ToString() ?? "-")) + " turns=" + TurnsPlayed);
            await Seconds(5);
        }

        // ---------- commands ----------
        private static CommandMeta Meta(PlayerView view) => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);

        private async Task<CommandReceipt> Submit(GameCommand command, bool required)
        {
            var receipt = await Session.SubmitAsync(command, life.Token);
            if (receipt.Accepted) { Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort); SyncBoard(); }
            else if (required && receipt.Error != ErrorCode.Cancelled) throw new InvalidOperationException("Table command rejected: " + receipt.Error);
            return receipt;
        }

        /// <summary>Draw for me: from the deck or the previous player's last discard; <paramref name="slot"/> = drop target.</summary>
        public async Task<bool> Draw(DrawSource source, RackSlot? slot)
        {
            if (!CanDraw) return false;
            var view = Session.GetView(Me);
            Current = Phase.Discard; // block double draws while the command runs
            var receipt = await Session.SubmitAsync(new DrawCommand(Meta(view), source), life.Token);
            if (!receipt.Accepted)
            {
                Current = Phase.Draw;
                Flash(receipt.Error == ErrorCode.PreviousDiscardUnavailable || receipt.Error == ErrorCode.NoLegalUse || receipt.Error == ErrorCode.DiscardMustBeUsed
                    ? L("Bu taşı bu turda kullanamazsın", "You cannot use that tile this turn") : L("Çekilemedi", "Could not draw"), true);
                return false;
            }
            var after = Session.GetView(Me); int drawn = after.PendingDraw.Tile.Value;
            Rack.Add(drawn, TileCodes.FaceFor(Indicator), autoSort, slot); LastDrawn = drawn;
            if (source == DrawSource.PreviousDiscard) Flash(L("Yerden aldığın taşı bu turda kullanmalısın", "Use the tile you took this turn"), false);
            SyncBoard(); Notify(); return true;
        }

        public async void Discard(int id)
        {
            if (!CanDiscard || !Rack.TryFind(id, out _)) return;
            await TryCommit(id, true);
        }

        private async Task<bool> TryCommit(int id, bool explain)
        {
            var view = Session.GetView(Me);
            var plan = new TurnPlan(staged.ToArray(), new TileId(id), penaltyArmed == id);
            var receipt = await Session.SubmitAsync(new CommitTurnCommand(Meta(view), plan), life.Token);
            if (receipt.Accepted) { Rack.Remove(id); AfterCommit(); return true; }
            if (!explain) return false;
            switch (receipt.Error)
            {
                case ErrorCode.PenaltyConfirmationRequired:
                    penaltyArmed = id; Flash(L("Bu taş işlenebilir · yine de atarsan 101 ceza", "This tile can be laid off · discarding it costs 101"), true); break;
                case ErrorCode.DiscardMustBeUsed: Flash(L("Yerden aldığın taşı kullanmadan atamazsın", "Use the tile you took before discarding"), true); break;
                case ErrorCode.OpeningRequired: Flash(L("Bitirmek için önce elini açmalısın", "Open your hand before finishing"), true); break;
                default: Flash(L("Bu hamle geçersiz", "That move is not allowed"), true); break;
            }
            return false;
        }

        private void AfterCommit()
        {
            staged.Clear(); penaltyArmed = -1; LastDrawn = -1;
            Rack.Sync(Session.GetView(Me).OwnHand.Select(t => t.Value), TileCodes.FaceFor(Indicator), autoSort);
            Current = Phase.Opponent; Active = -1; SyncBoard(); Notify();
            humanDone?.TrySetResult(true);
        }

        /// <summary>Aç / Yere aç: rack segments that form runs or sets go to the table (staged until the discard).</summary>
        public void OpenRuns()
        {
            if (!CanDiscard) return;
            var eval = HandEvaluation.Of(Rack, Indicator);
            if (eval.Groups.Count == 0) { Flash(L("Istakada geçerli per yok", "No valid set on your rack"), true); return; }
            var opening = OpenedKind(Me);
            if (opening == OpeningKind.Pairs) { Flash(L("Çiftle açtın · yalnızca çift açabilirsin", "You opened with pairs · lay pairs only"), true); return; }
            var actions = opening == OpeningKind.None
                ? new List<TableAction> { new OpenGroups(OpeningKind.Series, eval.Groups) }
                : eval.Groups.Select(g => (TableAction)new AddGroup(g)).ToList();
            Stage(actions, eval.Groups, opening == OpeningKind.None && eval.Sum < 101 ? L("Açmak için en az 101 gerekli", "You need 101 to open") : null);
        }

        /// <summary>Çift aç: at least five rack pairs (or more pairs once opened with pairs).</summary>
        public void OpenPairs()
        {
            if (!CanDiscard) return;
            var eval = HandEvaluation.Of(Rack, Indicator);
            var opening = OpenedKind(Me);
            if (opening == OpeningKind.Series) return;
            if (eval.Pairs.Count == 0 || opening == OpeningKind.None && eval.Pairs.Count < 5) { Flash(L("Çift açmak için 5 çift gerekli", "You need 5 pairs to open"), true); return; }
            var actions = opening == OpeningKind.None ? new List<TableAction> { new OpenGroups(OpeningKind.Pairs, eval.Pairs) } : eval.Pairs.Select(p => (TableAction)new AddGroup(p)).ToList();
            Stage(actions, eval.Pairs, null);
        }

        private void Stage(List<TableAction> actions, List<MeldProposal> groups, string hint)
        {
            var next = staged.Concat(actions).ToList();
            var result = Evaluate(next);
            if (!result.Success)
            {
                Flash(hint ?? (result.Error == ErrorCode.InvalidOpening ? L("Açmak için en az 101 gerekli", "You need 101 to open") : L("Bu perler açılamaz", "Those sets cannot be laid")), true);
                return;
            }
            staged.Clear(); staged.AddRange(next);
            foreach (var group in groups) foreach (var tile in group.Tiles) Rack.Remove(tile.Id.Value);
            SyncBoard(); Notify();
        }

        /// <summary>İşle: drop one rack tile onto a table meld (front or back; jokers take the face that fits).</summary>
        public void LayOff(int id, int meldId)
        {
            if (!CanMeld && !(CanDiscard && staged.OfType<OpenGroups>().Any())) { Flash(L("İşlemek için önce elini aç", "Open your hand to lay off"), true); return; }
            var state = Projection(); var meld = state.Table.FirstOrDefault(m => m.Id == meldId);
            if (meld == null || meld.Group.Kind == MeldKind.Pair) { Flash(L("Bu taş o pere işlenemez", "That tile does not fit this set"), true); return; }
            if (Rack.Count <= 1) { Flash(L("Son taş işlenemez", "Your last tile cannot be laid off"), true); return; }
            foreach (var face in CandidateFaces(id, meld))
                foreach (bool prepend in new[] { false, true })
                {
                    var next = staged.Concat(new[] { (TableAction)new ExtendMeld(meldId, prepend, new[] { new PlacedTile(new TileId(id), face) }) }).ToList();
                    if (!Evaluate(next).Success) continue;
                    staged.Clear(); staged.AddRange(next); Rack.Remove(id); SyncBoard(); Notify(); return;
                }
            Flash(L("Bu taş o pere işlenemez", "That tile does not fit this set"), true);
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

        public void Flash(string text, bool bad) { flash = text; flashBad = bad; flashUntil = Time.unscaledTime + 2.6f; Notify(); }

        private void OnHudCommand(string command)
        {
            switch (command)
            {
                case "open": OpenRuns(); break;
                case "pairs": OpenPairs(); break;
                case "sort-run": SortRack(true); break;
                case "sort-pair": SortRack(false); break;
                case "exit": RequestExit(); break;
            }
        }

        /// <summary>
        /// Leave the table for the main menu. During a hand the first press only warns (the full T25 confirmation card is
        /// a later phase); a second press within 2.6 s leaves.
        /// </summary>
        public void RequestExit()
        {
            if (leaving) return;
            bool inHand = Current != Phase.Finished && Current != Phase.Waiting;
            if (inHand && Time.unscaledTime > exitArmedUntil)
            {
                exitArmedUntil = Time.unscaledTime + 2.6f;
                Flash(L("Masadan kalkmak için tekrar bas · el bitmeden kalkarsan bahis kaybedilir", "Press again to leave · leaving mid-hand forfeits the stake"), true);
                return;
            }
            leaving = true; life?.Cancel();
            Flash(L("Masadan kalktın · lobiye dönülüyor", "You left the table · returning to the lobby"), false);
            Debug.Log("GALAXY101_TABLE_EXIT scene=" + menuScene);
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
                Phase = Current == Phase.Deal ? "deal" : Current == Phase.Draw ? "draw" : Current == Phase.Discard ? "discard" : Current == Phase.Finished ? "fin" : "opp",
                Active = Active, DeckCount = view.StockCount, TurnSeconds = Mathf.RoundToInt(turnSeconds), TurnStartedAt = turnStartedReal,
                Warn = HumanTurn && warned, TurnHint = turnHint, ShowIndicator = Current != Phase.Deal, FirstTurn = view.Phase == RoundPhase.PlanningTurn && view.PendingDraw == null,
                Pairs = eval.Pairs.Count, Flash = flash, FlashBad = flashBad,
            };
            for (int k = 0; k < 4; k++)
            {
                s.TileCounts[k] = k == Me ? Rack.Count : view.Players[k].HandCount;
                s.Opened[k] = OpenedValue(k); s.OpenedPairs[k] = OpenedKind(k) == OpeningKind.Pairs;
            }
            s.OpenSum = s.Opened[Me] > 0 ? s.Opened[Me] : eval.Sum;
            s.CanOpen = CanDiscard && (s.Opened[Me] > 0 ? !s.OpenedPairs[Me] && eval.Groups.Count > 0 : eval.Sum >= 101);
            s.CanPair = CanDiscard && (s.OpenedPairs[Me] ? eval.Pairs.Count > 0 : s.Opened[Me] == 0 && eval.Pairs.Count >= 5);
            if (Current == Phase.Finished && LastResult != null)
            {
                var info = LastResult.FinishInfo;
                s.ResultLine = info.Winner.HasValue
                    ? (info.Winner.Value == Me ? L("Bitirdin!", "You finished!") : hud.SeatName(info.Winner.Value) + L(" bitirdi", " finished"))
                    : L("Taş bitti · en düşük puan eli alır", "Deck out · lowest score takes the hand");
                s.ResultLine += " · " + string.Join("  ", Enumerable.Range(0, 4).Select(k => hud.SeatFirstName(k) + " " + (LastResult.Scores[k].Total?.ToString() ?? "–")));
            }
            return s;
        }
    }
}
