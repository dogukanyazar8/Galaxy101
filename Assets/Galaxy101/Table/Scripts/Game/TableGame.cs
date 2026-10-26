using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Audio;
using Galaxy101.Contracts;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galaxy101.Table
{
    /// <summary>How the table scene opens: set by the menu (or the state router) before the scene loads.</summary>
    public sealed class TableLaunch
    {
        /// <summary>A server match prepared by the menu (it already listens to the socket); null = an offline table.</summary>
        public ITableSession Session;
        /// <summary>Offline: the match setup (seed, options, turn time) and the table as the seats see it.</summary>
        public TableSetup Setup;
        public V4Table Table;
        /// <summary>Botlara karşı (R-94: no chips; D-42: XP) — the top bar's "Antrenman · bahis yok".</summary>
        public bool VsBots = true;
        /// <summary>Dev/state flows (client-tests.json, TableScenarios) and the package scenarios R43/R44.</summary>
        public string Flow;
        /// <summary>The flow starts the deal at once (no 3-2-1) — the prototype's "deal" flow.</summary>
        public bool SkipCountdown;
        /// <summary>Top bar: "Test masası · Tekli" and "Bahis 5.000" (null: from the table).</summary>
        public string Room, BetLabel;
        public long Stake;
        /// <summary>The wallet shown on the table (the state run's 10.000 − 5.000 bet); null = the menu's.</summary>
        public long? Chips;
        /// <summary>Seat cards: name, colour, level (null = the table's users).</summary>
        public (string Name, string Color, int Level)[] Seats;
        /// <summary>Offline clock in ms (default the shared GameClock, D-60); tests pass a scaled one.</summary>
        public Func<double> Clock;
    }

    /// <summary>
    /// The 3D table on the v4 match flow (K-01, K-09): the deal ceremony, turns, melds, penalties and hand ends all come
    /// from the server's match code — <see cref="LocalTableSession"/> offline, the v4 protocol online — through the seat's
    /// <see cref="TableClient"/>. This class keeps the presentation: my rack arrangement, the board, the ceremony, effects,
    /// gifts and the HUD; every player action is one protocol step (gDraw, gOpen, gLay, gAdd, gSwap, gUndo, gReturnSide,
    /// gDiscard, gRollDice, gReady) and every rule decision is the engine's (G101Rules on the seat's projection).
    /// </summary>
    public sealed class TableGame : MonoBehaviour
    {
        /// <summary>The person's seat on the board and HUD (seat 0 is always drawn at the bottom).</summary>
        public const int Me = 0;

        [SerializeField] private TableScene table;
        [SerializeField] private TableHud hud;
        [SerializeField] private bool autoSort = true, doubleClickDiscard = true, turnHint = true, highlightSets = true;
        [SerializeField] private float warnSeconds = 5;
        [Tooltip("Scene the exit returns to (the Galaxy 101 main menu).")]
        [SerializeField] private string menuScene = "Bootstrap";

        public enum Phase { Waiting, Seating, Deal, Draw, Discard, Opponent, Finished }

        /// <summary>Set before the table scene loads.</summary>
        public static TableLaunch Pending;
        /// <summary>Developer test menu (spec/25): the offline table opens straight into this flow.</summary>
        public static string PendingScenario;

        public ITableSession Session { get; private set; }
        public TableClient Model => Session?.Client;
        public LocalTableSession Local => Session as LocalTableSession;
        public TableLaunch Launch { get; private set; }
        public TableBoard Board { get; private set; }
        public TableFx Fx { get; private set; }
        public TableGifts Gifts { get; private set; }
        public TableCeremony Ceremony { get; private set; }
        public TableTargets Markers { get; private set; }
        public RackGrid Rack { get; } = new RackGrid();
        public Phase Current { get; private set; } = Phase.Waiting;
        /// <summary>The table seat whose turn it is (-1 none).</summary>
        public int Active { get; private set; } = -1;
        public bool DoubleClickDiscard => doubleClickDiscard;
        public int LastDrawn { get; private set; } = -1;
        public bool ExitOpen { get; private set; }
        public bool ResultOpen { get; private set; }
        /// <summary>The match is over: the last result card closes the table (no match-end screen, REMOVED.md T45/T46).</summary>
        public bool MatchOver { get; private set; }
        public long XpEarned { get; private set; }
        public int LastXp { get; private set; }
        public event Action Changed;

        private CancellationTokenSource life;
        private bool leaving, warned, seating;
        private readonly bool[] seated = { true, true, true, true };
        private int countdown; private float countdownAt;
        private float turnStartedReal;
        private string momentTitle, momentSub; private int momentSeat = -1; private bool momentOn; private float momentAt, resultAt;
        private readonly List<string>[] penaltyWhy = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        private int glowTile = -1; private float glowUntil;
        private int invalidOkey = -1; private float invalidUntil;
        private Task ceremony;
        private CancellationTokenSource ceremonyLife;
        private bool settled;
        private Pose? drawSpawn;
        private RackSlot? drawSlot;
        // R-23 shown the prototype's way (badOpen): the short opening lies on the board 1.5 s, then returns with its +101.
        private List<int[]> shortMelds; private int shortValue; private string shortMode;
        private (string Text, string Sub, int Amount)? shortPenalty;
        private float shortUntil;
        private List<MeldSpec> lastOpen;

        // ---------- seats: absolute (the server's) ↔ the table's (0 = me) ----------
        private int Base => Model != null && Model.Seat >= 0 ? Model.Seat : 0;
        public int Abs(int local) => (local + Base) % 4;
        public int LocalSeat(int abs) => ((abs - Base) % 4 + 4) % 4;
        private bool Watching => Model != null && Model.Seat < 0;

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
            Board.IsOkey = id => Model != null && Model.IsOkey(TileIds.ToV4(id));
            Board.Classify = ClassifyMeld;
            Fx = table.gameObject.GetComponent<TableFx>() ?? table.gameObject.AddComponent<TableFx>(); Fx.Init(table); Board.Fx = Fx;
            Gifts = table.gameObject.GetComponent<TableGifts>() ?? table.gameObject.AddComponent<TableGifts>(); Gifts.Init(table, Fx);
            Markers = table.gameObject.GetComponent<TableTargets>() ?? table.gameObject.AddComponent<TableTargets>(); Markers.Init(this);
            Ceremony = new TableCeremony(table, Fx, Seconds); Ceremony.Changed += Notify;
            life = new CancellationTokenSource();

            Launch = Pending ?? new TableLaunch(); Pending = null;
            if (Launch.Session == null && Launch.Flow == null && PendingScenario != null) { Launch.Flow = TableScenarios.Has(PendingScenario) ? TableScenarios.Flow(PendingScenario) : PendingScenario; }
            PendingScenario = null;
            Session = Launch.Session ?? new LocalTableSession(Launch.Setup ?? DefaultSetup(), Launch.Table ?? DefaultTable(), Launch.Clock ?? (() => GameClock.NowMs));

            if (hud != null)
            {
                hud.Command += OnHudCommand; hud.TilePosition = id => Board.ViewOf(id) is TileView v && v != null ? v.transform.position : (Vector3?)null;
                hud.Initialize(); hud.Settings.Changed += OnSetting; ReadSettings();
                hud.GiftSent += OnGiftSent; Gifts.Landed += hud.GiftLanded;
                // Chat, treats and emoji go through the match (chatSend / giftSend), offline too: the bots' answers and thanks
                // are the runtime's (D-20, seeded), and every line and flight comes back as the table's chat / gift event.
                hud.SendChatOnline = (text, quick) => Session.Chat(text, quick.HasValue ? "quick." + quick.Value : null);
                hud.SendGiftOnline = (gift, to) => Session.Gift(gift, to.HasValue ? Abs(to.Value) : (int?)null);
                hud.SendEmoteOnline = emote => { int i = Array.IndexOf(Emoji, emote); return i >= 0 ? Session.Chat(null, "emoji." + i) : Session.Chat(emote, null); };
                hud.FreeGifts = !Session.Online;   // R-94: nothing costs chips against bots
            }
            if (Session is LocalTableSession offline) StartOffline(offline);
            else AttachModel();
            Notify();
        }

        private static TableSetup DefaultSetup() => new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = (uint)UnityEngine.Random.Range(1, int.MaxValue), TurnMs = TableTimings.DefaultTurnMs };

        /// <summary>Botlara karşı (prototype startBots): three bots, no stake, 5 hands, 30 s (R-94).</summary>
        public static V4Table DefaultTable(int hands = 5, int turnSec = 30)
        {
            var t = new V4Table { Id = "bots", Name = "Botlara karşı", Mode = "solo", Hands = hands, Bet = 0, TurnSec = turnSec, VsBots = true, Status = "playing", Rules = new V4TableRules() };
            t.Seats.Add(new V4Seat { User = new V4User { Id = "me", Name = "", Level = 1 } });
            foreach (var (name, color, lv) in new[] { ("Bot Ayla", "#3CCB6E", 12), ("Bot Cem", "#4FC3F7", 15), ("Bot Defne", "#FFB547", 18) })
                t.Seats.Add(new V4Seat { Bot = true, User = new V4User { Id = "bot", Name = name, Color = color, Level = lv } });
            return t;
        }

        private void StartOffline(LocalTableSession offline)
        {
            offline.Host.Xp += (gained, reason) => { AddXp(gained); if (reason == "hand") LastXp = (int)gained; };
            AttachModel();
            var flow = Launch.Flow;
            if (flow == "seat") { _ = SeatThenStart(offline); return; }
            if (flow != null && TableScenarios.Has(flow))
            {
                var sc = TableScenarios.Create(flow);
                offline.Resume(sc.State());
                // The prototype's rack layout of the flow, slot for slot.
                Rack.Clear();
                for (int r = 0; r < 2; r++) for (int i = 0; i < RackGrid.Columns && i < sc.Rows[r].Length; i++) if (sc.Rows[r][i] != null) Rack.Set(new RackSlot(r, i), TileIds.ToInt(sc.Rows[r][i]));
                Settle(true);
                return;
            }
            offline.Begin(Launch.SkipCountdown);
        }

        /// <summary>
        /// T01–T02 (client-tests.json "seat"): the seats fill at 1.3 / 2.7 / 4.1 s (Zeynep, Ayşe, Burak), then after
        /// seatedToCountdown the match starts (matchStart: the 3-2-1 of preDealCountdown, then the deal).
        /// </summary>
        private async Task SeatThenStart(LocalTableSession offline)
        {
            seating = true; Current = Phase.Seating;
            for (int k = 1; k < 4; k++) seated[k] = false;
            Board.Clear(); Board.LayoutDeck(106);
            table.CameraRig.GoTo(TableCameraRig.View.Lobby, 0);
            Notify();
            float start = GameClock.Seconds; var joins = new[] { (2, 1.3f), (1, 2.7f), (3, 4.1f) };
            while (!seated.All(x => x))
            {
                foreach (var (seat, at) in joins) if (!seated[seat] && GameClock.Seconds - start >= at) Invite(seat);
                await Seconds(0.05f); if (life.IsCancellationRequested) return;
            }
            await Seconds(TableTimings.SeatedToCountdown / 1000f);
            seating = false;
            table.CameraRig.GoTo(hud != null ? hud.PreferredView : TableCameraRig.View.Player, 3f);
            offline.Begin();
        }

        /// <summary>"Davet et": the empty seat fills at once.</summary>
        public void Invite(int seat)
        {
            if (!seating || seat < 1 || seat > 3 || seated[seat]) return;
            seated[seat] = true; Notify();
        }

        private void AttachModel()
        {
            Model.Applied += OnMessage;
            // The socket: the reconnect card follows the session; a snapshot of another screen (the match is gone) leaves.
            if (Session is Galaxy101.Online.V4TableSession net) net.ConnectionChanged += () => { if (this == null) return; Notify(); if (net.Closed && !leaving) LeaveAfter(0.5f); };
            // A session the menu opened may already hold the start of the match (and its snapshot).
            if (Model.Table != null) { ApplySeats(); if (Model.HasHand) { if (Model.Rack != null) Rack.Deal(TileIds.ToInts(Model.Rack)); Settle(true); } else if (Model.Status == G101Rules.StatusLobby) Current = Phase.Seating; }
        }

        private void OnDestroy()
        {
            life?.Cancel(); ceremonyLife?.Cancel();
            foreach (var t in timers) t.Done.TrySetCanceled(); timers.Clear();
            if (Model != null) Model.Applied -= OnMessage;
            if (hud != null) { hud.Command -= OnHudCommand; hud.GiftSent -= OnGiftSent; if (hud.Settings != null) hud.Settings.Changed -= OnSetting; }
            Board?.Clear();
            Session?.Dispose();
        }

        // ---------- time (scene-local waits on the shared clock's frame time) ----------
        private readonly List<(float At, TaskCompletionSource<bool> Done)> timers = new List<(float, TaskCompletionSource<bool>)>();
        private Task Seconds(float seconds) { var done = new TaskCompletionSource<bool>(); timers.Add((Time.time + seconds, done)); return done.Task; }

        private void Update()
        {
            Session?.Tick();
            float now = Time.time;
            for (int i = timers.Count - 1; i >= 0; i--) if (timers[i].At <= now) { var t = timers[i]; timers.RemoveAt(i); t.Done.TrySetResult(true); }
            Board?.Tick();
            Ceremony?.Tick();
            if (Fx != null && Model != null && Model.HasHand && settled)
            {
                var left = Model.Discards[G101Rules.LeftOf(Base)];
                Fx.SetState(CanDiscard, CanDraw, CanDrawSide && left.Count > 0, Board.DeckShown);
            }
            if (HumanTurn && !warned && warnSeconds > 0 && TurnLeftMs() <= warnSeconds * 1000) { warned = true; Notify(); GalaxyAudio.Play(GalaxyAudio.Table.TurnWarning()); } // "Süren bitiyor!"
            if (Model != null && Model.Status == G101Rules.StatusLobby && Model.MatchStartAt > 0)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt((Model.MatchStartAt + Model.CountdownMs - Session.ServerNow) / 1000f), 0, 3);
                if (n != countdown) { countdown = n; countdownAt = Time.realtimeSinceStartup; Notify(); GalaxyAudio.Play(GalaxyAudio.Table.Countdown(n > 0 ? n : (int?)null)); }
            }
            if (shortMelds != null && Time.time >= shortUntil) EndShortOpening();
            if (ExitOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CancelExit();
            if (invalidOkey >= 0 && Time.unscaledTime > invalidUntil) { invalidOkey = -1; Notify(); }
            if (glowTile >= 0 && Time.unscaledTime > glowUntil) { glowTile = -1; Notify(); }
            if (ResultOpen && MatchOver && Time.realtimeSinceStartup - resultAt >= TableTimings.HandEndWait / 1000f) CloseTable();
        }

        private long TurnLeftMs() => Model == null ? 0 : Model.Deadline - Session.ServerNow;

        // ---------- the server's messages ----------
        private void OnMessage(ClientMessage m)
        {
            switch (m.Type)
            {
                case V4Messages.MatchStart: ApplySeats(); Current = Phase.Seating; break;
                case V4Messages.DealStart: BeginDeal(); GalaxyAudio.Play(GalaxyAudio.Table.DealStart()); break;
                case V4Messages.DealStep: GalaxyAudio.Play(GalaxyAudio.Table.DealStep(((V4DealStep)m.Payload).Step)); break;
                case V4Messages.DealDie: GalaxyAudio.Play(GalaxyAudio.Table.DealDie()); break;
                case V4Messages.HandDealt: if (Model.Rack != null) Rack.Deal(TileIds.ToInts(Model.Rack)); break;
                case V4Messages.Turn: OnTurn(); break;
                case V4Messages.Game: OnEvent(m.E, m.Ev); break;
                case V4Messages.HandWait: break;
                case V4Messages.MatchResult: MatchOver = true; if (!ResultOpen && !momentOn) CloseTable(); break;
                case V4Messages.Chat: OnChat((V4Chat)m.Payload); break;
                case V4Messages.Gift: OnGift((V4Gift)m.Payload); break;
                case V4Messages.Xp: { var x = (V4Xp)m.Payload; XpEarned += x.Gained; if (x.Reason == "hand") LastXp = (int)x.Gained; if (hud?.Model != null) { hud.Model.Level = x.Level; hud.Model.Xp = x.Xp; } break; }
                case V4Messages.SeatConnection: ApplySeats(); break;
                case V4Messages.TableView: case V4Messages.Snapshot: if (Model.HasHand && Model.Deal == null && !Ceremony.Running) { SyncRack(); SyncBoard(); } break;
                case V4Messages.LeftTable: { var l = (V4LeftTable)m.Payload; LeaveAfter(Math.Max(0, l.ToLobbyIn) / 1000f); break; }
                case V4Messages.TableClosed: Message(TableHud.ToastKind.Info, L("Masa kapandı", "The table was closed")); LeaveAfter(1.3f); break;
            }
            Notify();
        }

        private void BeginDeal()
        {
            Current = Phase.Deal; Active = -1; settled = false; ResultOpen = false; momentOn = false;
            Gifts.Clear(); hud?.NewHand();
            foreach (var list in penaltyWhy) list.Clear();
            LastDrawn = -1; LastXp = 0; Rack.Clear();
            ceremonyLife?.Cancel(); ceremonyLife = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
            var token = ceremonyLife.Token;
            Board.Clear(); Board.LayoutDeck(0);
            table.CameraRig.GoTo(TableCameraRig.View.Deal, 1.2f);
            ceremony = RunCeremony(token);
        }

        private async Task RunCeremony(CancellationToken token)
        {
            try { await Ceremony.Run(Model, LocalSeat, () => { if (Rack.Count == 0 && Model.Rack != null) Rack.Deal(TileIds.ToInts(Model.Rack)); return BuildBoard(); }, token); }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { Debug.LogException(e); }
            finally { Ceremony.Clear(); }
            if (token.IsCancellationRequested) return;
            Settle(false);
            table.CameraRig.GoTo(hud != null ? hud.PreferredView : TableCameraRig.View.Player, 1.4f);
            OnTurn(); Notify();
        }

        /// <summary>The board takes the model's state (after the ceremony or a resumed hand): tiles appear where they are.</summary>
        private void Settle(bool instant)
        {
            if (Rack.Count == 0 && Model.Rack != null) Rack.Deal(TileIds.ToInts(Model.Rack));
            SyncRack();
            Board.Settle(BuildBoard());
            settled = true;
            if (instant) { table.CameraRig.GoTo(hud != null ? hud.PreferredView : TableCameraRig.View.Player, 0); OnTurn(); }
        }

        private string soundedTurn;

        private void OnTurn()
        {
            if (Model.Deal != null || Ceremony.Running || !settled) return;
            if (Model.Status != G101Rules.StatusPlaying) return;
            int seat = LocalSeat(Model.TurnSeat);
            bool mine = Model.MyTurn;
            string key = Model.HandNo + ":" + Model.TurnN;
            if (mine && soundedTurn != key) { soundedTurn = key; GalaxyAudio.Play(GalaxyAudio.Table.Turn(Me, Me, false)); }
            if (Active != seat || !mine) { warned = false; }
            Active = seat;
            Current = mine ? (Model.TurnPhase == G101Rules.PhaseDraw ? Phase.Draw : Phase.Discard) : Phase.Opponent;
            turnStartedReal = Time.realtimeSinceStartup - Mathf.Max(0, Session.ServerNow - Model.TurnAt) / 1000f;
        }

        private void OnEvent(string e, IDictionary<string, object> ev)
        {
            int seat = ev != null && ev.TryGetValue("seat", out var s) && s != null ? TableClient.I(s) : -1;
            int local = seat >= 0 ? LocalSeat(seat) : -1;
            bool me = seat >= 0 && seat == Model.Seat;
            // Sound (docs/v4/audio/RAPOR.md §5): each engine event as it is animated; my turn's cue in OnTurn, the finish
            // with the hand's result (nuke for an elden or okey finish, EK-908) and hand.end when the result card opens.
            if (e != "turn" && e != "finished" && e != "handEnd") GalaxyAudio.Play(GalaxyAudio.Table.ForEngine(e, local, Me, false, Current == Phase.Deal));
            switch (e)
            {
                case "handStart": break;
                case "drew":
                {
                    if (me)
                    {
                        int id = TileIds.ToInt(ev["tile"] as string);
                        if (drawSpawn.HasValue) Board.SpawnFrom(drawSpawn.Value);
                        if (id >= 0) { Rack.Add(id, TileCodes.FaceFor(Model), autoSort, drawSlot); LastDrawn = id; }
                        drawSpawn = null; drawSlot = null;
                        if ((string)ev["src"] == "side") Message(TableHud.ToastKind.Info, T("msg.canPutBack"));
                    }
                    break;
                }
                case "sideReturned":
                    if (me) { Board.ForceDuration(TileIds.ToInt((string)ev["tile"]), 0.3f); LastDrawn = -1; } // T39: back on the pile in 300 ms
                    break;
                case "okeySwapped":
                    if (me) Board.ForceDuration(TileIds.ToInt((string)ev["okey"]), 0.45f); // T47: the okey flies to the rack
                    break;
                case "undone":
                    if (me) Message(TableHud.ToastKind.Info, L("Taşlar ıstakana döndü", "Tiles back on your rack"));
                    break;
                case "opened":
                    if (!me && local >= 0)
                        hud?.Balloon(local, (string)ev["mode"] == G101Rules.KindPair ? T("ev.pairs", FirstName(local), TableClient.I(ev["value"])) : T("ev.opened", FirstName(local), TableClient.I(ev["value"])), false);
                    break;
                case "openShort":
                    if (me) ShowShortOpening(ev);
                    else if (local >= 0) hud?.Balloon(local, T("ev.penShort", FirstName(local)), true);
                    break;
                case "penalty": Penalty(ev); break;
                case "auto": if (me) Message(TableHud.ToastKind.Warning, T("msg.timeUp")); break;
                case "discarded": case "finished": LastDrawn = -1; break;
                case "handEnd": HandEnd(); break;
                case "matchEnd": MatchOver = true; break;
                case "seatLeft": case "seatTaken": ApplySeats(); break;
            }
            if (Model.HasHand && Model.Deal == null && settled && !Ceremony.Running && shortMelds == null)
            {
                SyncRack(); SyncBoard();
                if (e == "turn" || e == "drew" || e == "sideReturned") OnTurn();
            }
        }

        /// <summary>The prototype's badOpen: the attempted sets lie on the board 1.5 s ("Açtı · 66"), then return with the penalty.</summary>
        private void ShowShortOpening(IDictionary<string, object> ev)
        {
            shortMode = (string)ev["mode"]; shortValue = TableClient.I(ev["value"]);
            shortMelds = (lastOpen ?? new List<MeldSpec> { new MeldSpec(shortMode, TableClient.Strings(ev["tiles"])) }).Select(g => TileIds.ToInts(g.Tiles).ToArray()).ToList();
            foreach (var g in shortMelds) foreach (var id in g) Rack.Remove(id);
            shortUntil = Time.time + 1.5f;
            SyncBoard();
        }

        private void EndShortOpening()
        {
            shortMelds = null; SyncRack(); SyncBoard();
            turnStartedReal = Time.realtimeSinceStartup;
            if (shortPenalty is var (text, sub, amount)) { hud?.Post(TableHud.ToastKind.Penalty, text, sub, amount); shortPenalty = null; }
            Notify();
        }

        /// <summary>A written penalty (R-40…R-44): mine as a Penalty toast, others' as a balloon; the reason in the result card.</summary>
        private void Penalty(IDictionary<string, object> ev)
        {
            int seat = TableClient.I(ev["seat"]), by = TableClient.I(ev["by"]), amount = TableClient.I(ev["amount"]);
            int local = LocalSeat(seat), byLocal = LocalSeat(by);
            string code = (string)ev["code"], mine, theirs, why, sub = null;
            switch (code)
            {
                case G101Rules.PenOpenShort:
                    why = T("why.short");
                    mine = shortMode == G101Rules.KindPair ? T("pen.shortPairs", shortValue) : T("pen.short", shortValue);
                    theirs = T("ev.penShort", FirstName(local)); sub = T("pen.returned");
                    break;
                case G101Rules.PenIslek: why = T("why.playable"); mine = T("pen.playable"); theirs = T("penBubbleTile"); break;
                case G101Rules.PenOkeyDiscard: why = T("why.okey"); mine = T("pen.okey"); theirs = T("penBubbleOkey"); break;
                case G101Rules.PenOpenedWith:
                {
                    int number = amount / G101Rules.Const.OpenedWithX;
                    // T34: the tile that fed the opening glows in the opener's meld for 1.2 s.
                    glowTile = TileIds.ToInt(ev["tile"] as string); glowUntil = Time.unscaledTime + 1.2f;
                    why = T("why.fed", number); mine = T("pen.fed", number, FirstName(byLocal)); theirs = T("ev.penFed", FirstName(local), number, amount);
                    break;
                }
                default: why = T("why.okeyTaken"); mine = T("pen.okeyTaken", FirstName(byLocal)); theirs = T("ev.penOkeyTaken"); break;
            }
            penaltyWhy[local].Add(why);
            if (seat == Model.Seat && !Watching)
            {
                if (code == G101Rules.PenOpenShort && shortMelds != null) shortPenalty = (mine, sub, amount);
                else hud?.Post(TableHud.ToastKind.Penalty, mine, sub, amount);
            }
            else hud?.Balloon(local, theirs, true);
        }

        /// <summary>The emoji row (table_content.json emoji): chat quick keys emoji.0–emoji.9.</summary>
        private static string[] emoji;
        private static string[] Emoji => emoji ?? (emoji = Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Table/table_content").text)["emoji"].ToObject<string[]>());

        private void OnChat(V4Chat c)
        {
            if (hud == null) return;
            int local = c.Seat >= 0 ? LocalSeat(c.Seat) : -1;
            string text = c.Text;
            if (c.Quick != null && c.Quick.StartsWith("emoji.", StringComparison.Ordinal) && int.TryParse(c.Quick.Substring(6), out int e) && e >= 0 && e < Emoji.Length)
            { if (local >= 0) hud.ShowEmote(local, Emoji[e]); return; }
            if (c.Quick == "bot.thanks") text = T("botThanks.0");
            else if (c.Quick != null) text = TableStrings.Has(c.Quick) ? T(c.Quick) : c.Quick;
            if (string.IsNullOrEmpty(text)) return;
            if (local >= 0) hud.Say(local, text); else hud.SystemLine(text);
            GalaxyAudio.Play(GalaxyAudio.Table.Chat(local, Me));
        }

        private void OnGift(V4Gift g)
        {
            // Every treat flies when the match says so (mine too: the drawer only asked for it).
            if (hud == null) return;
            int from = LocalSeat(g.From);
            if (g.To == null || g.To.Count == 0) return;
            GalaxyAudio.Play(GalaxyAudio.Table.GiftFlight(g.Gift, from));
            if (g.To.Count >= 3) hud.BotGift(from, g.Gift, -1);
            else foreach (var to in g.To) hud.BotGift(from, g.Gift, LocalSeat(to));
        }

        // ---------- hand end (R-50…R-58): the finish moment, then the result card ----------
        private async void HandEnd()
        {
            var r = Model.Result; if (r == null) return;
            Current = Phase.Finished; Active = -1; ExitOpen = false;
            int finisher = TableClient.I(r["finisher"]); string reason = (string)r["reason"];
            if (finisher >= 0) GalaxyAudio.Play(GalaxyAudio.Table.ForEngine("finished", LocalSeat(finisher), Me, TableSounds.IsNuke(r), false));
            var score = TableClient.Ints(r["score"]);
            bool elden = r["elden"] is bool el && el;
            if (finisher >= 0)
            {
                int w = LocalSeat(finisher); momentSeat = w;
                momentTitle = elden && w == Me && !Watching ? L("ELDEN BİTTİN!", "FINISHED FROM THE HAND!") : w == Me && !Watching ? L("BİTİRDİN!", "YOU FINISHED!") : Upper(Name(w)) + L(" BİTİRDİ!", " FINISHED!");
                momentSub = L("Son taşını desteye kapattı · ", "Closed the last tile on the deck · ") + Signed(score[finisher]) + L(" puan", " points");
                Fx.Celebrate(table.Corner(w), AvatarOnScreen(w));
                if (w != Me) table.CameraRig.Lean(table.CornerThree(w), 1.3f);
            }
            else { momentSeat = -1; momentTitle = L("TAŞ BİTTİ", "DECK EMPTY"); momentSub = L("En düşük puanlı oyuncu eli alır", "Lowest score takes the hand"); Fx.DealFx(); }
            momentOn = true; momentAt = Time.realtimeSinceStartup; Notify();
            Debug.Log("GALAXY101_TABLE_HAND_END hand=" + Model.HandNo + " reason=" + reason + " finisher=" + finisher + " scores=" + string.Join(",", score));
            await Seconds(finisher >= 0 ? 3.8f : 2.4f);
            if (this == null || life.IsCancellationRequested) return;
            if (finisher >= 0 && LocalSeat(finisher) != Me) table.CameraRig.GoTo(table.CameraRig.Current, 1.1f);
            momentOn = false;
            ResultOpen = true; resultAt = Time.realtimeSinceStartup; Notify();
            GalaxyAudio.Play(GalaxyAudio.Table.ForEngine("handEnd", null, Me, false, false));
            if (MatchOver && Model.MatchResult != null) { /* the last card counts down, then the table closes (Update) */ }
        }

        /// <summary>"Sonraki el" (gReady): every person ready → the next deal at once (D-25); after the last hand it closes the table.</summary>
        public void NextHand()
        {
            if (MatchOver) { CloseTable(); return; }
            if (Model?.Status == G101Rules.StatusHandOver) _ = Session.Ready();
        }

        /// <summary>
        /// The match is over (REMOVED.md T45/T46: no match-end screen): the table closes and the lobby shows a toast — the
        /// prototype's matchEnd (won: "Maçı kazandın! +N çip", else "Maç bitti · N. sıradasın"; a spectator "İzlediğin maç
        /// bitti"); ranked goes to the Dereceli result (S16).
        /// </summary>
        private void CloseTable()
        {
            if (leaving) return;
            var r = Model?.MatchResult; int mine = Model != null ? Model.Seat : -1;
            string toast = null;
            if (Watching) toast = L("İzlediğin maç bitti", "The match you watched is over");
            else if (r != null && mine >= 0)
            {
                long won = mine < r.Payouts.Count ? r.Payouts[mine] : 0;
                int place = 1 + Enumerable.Range(0, 4).Count(k => Model.Totals[k] < Model.Totals[mine]);
                if (r.Ranking is System.Collections.IList units)
                    foreach (IDictionary<string, object> u in units) if (TableClient.Ints(u["seats"]).Contains(mine)) place = TableClient.I(u["rank"]);
                toast = won > 0 ? L("Maçı kazandın! +" + Fmt(won) + " çip", "You won the match! +" + Fmt(won) + " chips") : L("Maç bitti · " + place + ". sıradasın", "Match over · you placed " + place + ".");
            }
            MainMenuController.PendingToast = Model?.Table?.Ranked == true ? null : toast;
            MainMenuController.FromRanked = Model?.Table?.Ranked == true;
            Leave(Session.Online);
        }

        /// <summary>The state router's "net" flow (T26, prototype dropNet): the reconnect card without a socket to lose.</summary>
        private int netDemo;
        public void ShowConnectionLost() { netDemo = 1; Notify(); }

        // ---------- player actions (one protocol step each) ----------
        public bool HumanTurn => Model != null && Model.MyTurn && settled && !Ceremony.Running;
        private LegalMoves Legal => Model?.Legal() ?? new LegalMoves();
        public bool CanDraw => HumanTurn && Legal.Draw;
        public bool CanDrawSide => HumanTurn && Legal.Side;
        public bool CanDiscard => HumanTurn && Model.TurnPhase == G101Rules.PhasePlay && shortMelds == null;
        public bool CanUndo => CanDiscard && Legal.Undo;
        public bool IOpened => Model != null && Model.Seat >= 0 && Model.Opened[Model.Seat] > 0;
        public bool CanArrange => settled && !Ceremony.Running && Current != Phase.Seating && Current != Phase.Deal && !Watching;
        /// <summary>The tile taken from the left pile this turn (R-31/R-32), while it is on my rack.</summary>
        public int PickedTile => HumanTurn && Model.SideTake != null && Model.Rack != null && Model.Rack.Contains(Model.SideTake) ? TileIds.ToInt(Model.SideTake) : -1;
        public string Indicator => Model?.Indicator;

        /// <summary>Elden bit (R-53): every tile but one in sets reaching the bar (katlamalı raises it), never opened, nothing laid this turn.</summary>
        private (List<MeldSpec> Melds, string Last)? FromHand()
        {
            if (!CanDiscard || IOpened || Legal.Undo) return null;
            return RackEval.Of(Rack, Model).Melds.FromHand(Model.OpenNeed(G101Rules.KindRun), Model.OpenNeed(G101Rules.KindPair));
        }
        public bool CanFinishFromHand => FromHand() != null;

        private async Task<string> Do(Task<string> step, bool explain = true)
        {
            string error;
            try { error = await step; }
            catch (Exception e) { Debug.LogException(e); error = "error"; }
            if (error != null && explain) Refused(error);
            Notify();
            return error;
        }

        private void Refused(string code)
        {
            GalaxyAudio.Play("ui.error");
            switch (code)
            {
                case V4Errors.E_SIDE_DISCARD: Message(TableHud.ToastKind.Error, T("msg.cantDiscardPicked")); break;
                case V4Errors.E_NOT_OPENED: case V4Errors.E_SIDE_MUST_OPEN: Message(TableHud.ToastKind.Error, T("msg.openFirst")); break;
                case V4Errors.E_OPEN_MODE: Message(TableHud.ToastKind.Error, T("msg.pairsOnly")); break;
                case V4Errors.E_LAST_TILE: Message(TableHud.ToastKind.Error, T("msg.lastTile")); break;
                case V4Errors.E_ISLE_INVALID: Message(TableHud.ToastKind.Error, T("msg.cantIsle")); break;
                case "offline": case "timeout": Message(TableHud.ToastKind.Error, L("Bağlantı yok", "No connection")); break;
                default: Message(TableHud.ToastKind.Error, L("Bu hamle geçersiz", "That move is not allowed")); break;
            }
        }

        /// <summary>Çek (gDraw): the deck or the left player's last discard (R-30); <paramref name="slot"/> = where it was dropped.</summary>
        public async Task<bool> Draw(bool side, RackSlot? slot)
        {
            if (side ? !CanDrawSide : !CanDraw) return false;
            drawSlot = slot;
            return await Do(Session.Draw(side ? "side" : "deck")) == null;
        }

        /// <summary>Geri bırak (gReturnSide, R-32): the side tile goes back and the turn's moves are undone (D-10).</summary>
        public void PutBack() { if (CanDiscard && Legal.ReturnSide) _ = Do(Session.ReturnSide()); }

        /// <summary>At (gDiscard): R-36; an okey or işlek tile is thrown knowingly and its penalty written (R-41, R-42).</summary>
        public void Discard(int id)
        {
            if (!CanDiscard || !Rack.TryFind(id, out _)) return;
            if (id == PickedTile) { Message(TableHud.ToastKind.Error, T("msg.cantDiscardPicked")); return; }
            _ = Do(Session.Discard(TileIds.ToV4(id)));
        }

        /// <summary>Aç / Yere aç (gOpen / gLay): the rack's valid run and group segments; under the bar it is the engine's short opening (R-23).</summary>
        public void OpenRuns()
        {
            if (!CanDiscard) return;
            var e = RackEval.Of(Rack, Model);
            if (IOpened && Model.OpenMode[Model.Seat] == G101Rules.KindPair) { Message(TableHud.ToastKind.Error, T("msg.pairsOnly")); return; }
            if (e.Groups.Count == 0) return;
            lastOpen = e.Groups;
            _ = Do(IOpened ? Session.Lay(e.Groups) : Session.Open(e.Groups));
        }

        /// <summary>Çift aç / Yere çift aç: the rack's pair segments (R-21; a series opener may lay pairs too, R-24).</summary>
        public void OpenPairs()
        {
            if (!CanDiscard) return;
            var e = RackEval.Of(Rack, Model);
            if (e.Pairs.Count == 0) return;
            lastOpen = e.Pairs;
            _ = Do(IOpened ? Session.Lay(e.Pairs) : Session.Open(e.Pairs));
        }

        /// <summary>Geri topla (gUndo, R-34).</summary>
        public void Undo() { if (CanUndo) _ = Do(Session.Undo()); }

        /// <summary>Elden bit: open with every set (R-53), then the last tile launched into the deck finishes (×2).</summary>
        public async void FinishFromHand()
        {
            var plan = FromHand(); if (plan == null) return;
            var (melds, last) = plan.Value;
            lastOpen = melds;
            if (await Do(Session.Open(melds)) != null) return;
            int id = TileIds.ToInt(last);
            if (Board.ViewOf(id) is TileView bomb && bomb != null)
            {
                // spec/23: the last tile is launched into the deck with the nuclear landing, then the discard finishes.
                bomb.enabled = false;
                var impact = table.DeckTop(Board.DeckShown).position;
                float wait = Fx.Nuclear(bomb.transform, impact, table.CameraRig, () => hud?.NuclearFlash());
                await Seconds(wait + 0.9f);
                if (bomb != null) bomb.enabled = true;
            }
            await Do(Session.Discard(last));
        }

        /// <summary>İşle (gAdd, R-25): one rack tile onto a table meld at the end the engine allows.</summary>
        public void LayOff(int id, int meldId, bool? prepend = null)
        {
            if (!CanDiscard) return;
            if (!IOpened) { Message(TableHud.ToastKind.Error, T("msg.openFirst")); return; }
            if (Rack.Count <= 1) { Message(TableHud.ToastKind.Error, T("msg.lastTile")); return; }
            string tile = TileIds.ToV4(id), meld = TileIds.MeldV4(meldId);
            var sides = Model.AddSides(meld, tile);
            string side = prepend.HasValue ? (prepend.Value ? "pre" : "post") : sides.FirstOrDefault();
            if (side == null || !sides.Contains(side)) { Message(TableHud.ToastKind.Error, T("msg.cantIsle")); return; }
            _ = Do(Session.Add(tile, meld, side));
        }

        /// <summary>İşle button (prototype isle()): every rack tile that fits a meld, in rack order, one gAdd each.</summary>
        public async void IsleAll()
        {
            for (int guard = 0; guard < 20 && CanDiscard && IOpened && Rack.Count > 1; guard++)
            {
                var next = Addable(); if (next == null) return;
                if (await Do(Session.Add(next.Value.Tile, next.Value.Meld, next.Value.Side)) != null) return;
            }
        }

        private (string Tile, string Meld, string Side)? Addable()
        {
            foreach (var seg in Rack.Segments())
                foreach (var id in seg.Ids)
                {
                    string tile = TileIds.ToV4(id);
                    foreach (var meld in Model.Melds.OrderBy(m => m.Owner))
                    {
                        var sides = Model.AddSides(meld.Id, tile);
                        if (sides.Count > 0) return (tile, meld.Id, sides[0]);
                    }
                }
            return null;
        }

        /// <summary>Okeyi al (gSwap, R-26): only with the real tile the okey stands for (T47, T49).</summary>
        public void TakeOkey(int tileId, int okeyId)
        {
            if (!CanDiscard || !Rack.TryFind(tileId, out _)) return;
            var okey = TableOkeys().FirstOrDefault(o => o.Id == okeyId);
            if (okey.Meld == null) return;
            if (!IOpened) { Message(TableHud.ToastKind.Error, T("msg.openFirst")); return; }
            if (!Model.SwapFits(okey.Meld, okey.Index, TileIds.ToV4(tileId)))
            {
                invalidOkey = okeyId; invalidUntil = Time.unscaledTime + 1.2f;
                Message(TableHud.ToastKind.Error, T("msg.okeyOnlyWith", FaceName(okey.Face))); Notify(); return;
            }
            _ = Do(Session.Swap(TileIds.ToV4(tileId), okey.Meld, okey.Index));
        }

        public void RollDice() { if (Model?.Deal != null && Model.Deal.Dealer == Model.Seat) _ = Do(Session.RollDice(), false); }

        public void SortRack(bool runs) { if (!CanArrange) return; Rack.Sort(runs, TileCodes.FaceFor(Model)); SyncBoard(); Notify(); }

        /// <summary>Commit a rack rearrangement the input previewed (singlePlan / groupPlan); the arrangement is the client's.</summary>
        public void ApplyRackPlan(IReadOnlyDictionary<int, RackSlot> plan) { Rack.Apply(plan); SyncBoard(); Notify(); GalaxyAudio.Play(GalaxyAudio.Table.RackMove()); }

        // ---------- okeys and lay-off targets (spec/23 işleme göstergeleri) ----------
        /// <summary>Every okey in a table meld with the face it stands for (its position, R-13).</summary>
        public List<(int Id, ColorNum Face, string Meld, int Index, int Owner)> TableOkeys()
        {
            var list = new List<(int, ColorNum, string, int, int)>();
            if (Model == null || !Model.HasHand || !Model.OkeyKnown) return list;
            foreach (var m in Model.Melds)
                for (int i = 0; i < m.Tiles.Count; i++)
                    if (Model.IsOkey(m.Tiles[i])) { var face = Model.Represents(m, i); if (face != null) list.Add((TileIds.ToInt(m.Tiles[i]), face, m.Id, i, LocalSeat(m.Owner))); }
            return list;
        }

        public bool IsTableOkey(int id) => id >= 0 && Model != null && Model.IsOkey(TileIds.ToV4(id)) && TableOkeys().Any(o => o.Id == id);

        public sealed class LayTarget { public int Meld = -1, Okey = -1; public bool Prepend; public Pose Pose; }
        private int dragTile = -1, selectedTile = -1, targetsFor = -2, targetsVersion = -1;
        private readonly List<LayTarget> targets = new List<LayTarget>();
        public void SetDragTile(int id) { if (dragTile == id) return; dragTile = id; targetsFor = -2; Notify(); }
        public void SetSelected(int id) { if (selectedTile == id) return; selectedTile = id; targetsFor = -2; }
        public int FocusTile => dragTile >= 0 ? dragTile : selectedTile;

        /// <summary>Every lay-off end and okey swap the engine allows for the focused rack tile (≤ 48), in world space.</summary>
        public IReadOnlyList<LayTarget> Targets
        {
            get
            {
                int id = FocusTile, version = Model?.Version ?? -1;
                if (id == targetsFor && version == targetsVersion) return targets;
                targetsFor = id; targetsVersion = version; targets.Clear();
                if (id < 0 || !CanDiscard || !IOpened || !Rack.TryFind(id, out _) || Rack.Count <= 1) return targets;
                string tile = TileIds.ToV4(id);
                foreach (var m in Model.Melds)
                {
                    int meld = TileIds.MeldInt(m.Id);
                    if (m.Kind == G101Rules.KindPair || !Board.Layout.TryGet(meld, out var at)) continue;
                    foreach (var side in Model.AddSides(m.Id, tile))
                    {
                        bool prepend = side == "pre";
                        int column = Mathf.Clamp(prepend ? at.Column - 1 : at.Column + at.Length, 0, BoardLayout.Columns - 1);
                        targets.Add(new LayTarget { Meld = meld, Prepend = prepend, Pose = table.BoardPose(column, at.Row) });
                        if (targets.Count >= 48) return targets;
                    }
                }
                foreach (var okey in TableOkeys())
                {
                    if (!Model.SwapFits(okey.Meld, okey.Index, tile) || !(Board.ViewOf(okey.Id) is TileView v) || v == null) continue;
                    targets.Add(new LayTarget { Okey = okey.Id, Meld = TileIds.MeldInt(okey.Meld), Pose = new Pose(v.Target.position, v.Target.rotation) });
                }
                return targets;
            }
        }

        public void UseTarget(int id, LayTarget target)
        {
            if (target.Okey >= 0) TakeOkey(id, target.Okey); else LayOff(id, target.Meld, target.Prepend);
            selectedTile = -1; targetsFor = -2;
        }

        // ---------- the board ----------
        private void SyncRack()
        {
            if (Model?.Rack == null || shortMelds != null) return;
            Rack.Sync(TileIds.ToInts(Model.Rack), TileCodes.FaceFor(Model), autoSort);
        }

        public void SyncBoard(bool instant = false)
        {
            if (Board == null || Model == null || !Model.HasHand || !settled) return;
            Board.Sync(BuildBoard(), instant);
            Board.SetValid(highlightSets && Model.OkeyKnown ? RackEval.Of(Rack, Model).ValidTiles : null);
            targetsFor = -2;
        }

        private BoardState BuildBoard()
        {
            var m = Model;
            var state = new BoardState { Rack = Rack, Stock = m.Deck, Indicator = m.Indicator != null ? TileIds.ToInt(m.Indicator) : -1 };
            for (int k = 0; k < 4; k++)
            {
                int abs = Abs(k);
                state.HandCounts[k] = k == Me && !Watching ? Rack.Count : m.Counts[abs];
                state.Discards[k].AddRange(TileIds.ToInts(m.Discards[abs]));
            }
            foreach (var meld in m.Melds) state.Melds.Add((TileIds.MeldInt(meld.Id), LocalSeat(meld.Owner), TileIds.ToInts(meld.Tiles).ToArray()));
            if (shortMelds != null) for (int i = 0; i < shortMelds.Count; i++) state.Melds.Add((9000 + i, Me, shortMelds[i]));
            return state;
        }

        /// <summary>Run or group for the board layout, and a run's first number (an okey stands where it lies, R-13).</summary>
        private (bool Run, int Start) ClassifyMeld(int[] tiles)
        {
            if (Model == null || tiles.Length == 0) return (false, 1);
            var ids = TileIds.ToV4s(tiles);
            var meld = Model.Melds.FirstOrDefault(x => x.Tiles.Count > 0 && x.Tiles[0] == ids[0]);
            string kind = meld?.Kind ?? (Model.Analyze(G101Rules.KindRun, ids).Ok ? G101Rules.KindRun : G101Rules.KindGroup);
            var a = Model.Analyze(kind, ids);
            if (!a.Ok) return (false, 1);
            return kind == G101Rules.KindRun ? (true, a.Start) : (false, kind == G101Rules.KindGroup ? a.N : 1);
        }

        private void ApplySeats()
        {
            if (hud == null || Model?.Table == null) return;
            var seats = Model.Table.Seats;
            for (int k = 1; k < 4; k++)
            {
                int abs = Abs(k);
                if (Launch.Seats != null && abs < Launch.Seats.Length && Launch.Seats[abs].Name != null)
                {
                    var (name, color, level) = Launch.Seats[abs];
                    hud.SetSeat(k, name, color, level, Model.SeatBot[abs], Rank(level), 38 + level * 7 % 20, level * 52);
                    continue;
                }
                var user = abs < seats.Count ? seats[abs]?.User : null;
                if (user != null && !string.IsNullOrEmpty(user.Name)) hud.SetSeat(k, user.Name, user.Color, user.Level, Model.SeatBot[abs] || Model.Autoplay[abs]);
            }
        }

        /// <summary>The prototype's seat rank by level (Gerçek Ana Oyun startGame: &gt;40 Usta I, &gt;28 Elmas III, &gt;18 Altın II, else Gümüş I).</summary>
        private static string Rank(int level) => level > 40 ? "usta1" : level > 28 ? "elmas3" : level > 18 ? "altin2" : "gumus1";

        // ---------- HUD ----------
        private void Notify()
        {
            if (hud != null && Model != null) hud.Render(HudState());
            Changed?.Invoke();
        }

        public TableHud.State HudState()
        {
            var m = Model;
            var eval = RackEval.Of(Rack, m);
            bool dealing = Current == Phase.Deal || Ceremony.Running;
            var s = new TableHud.State
            {
                Phase = Current == Phase.Seating ? "seat" : dealing ? "deal" : Current == Phase.Draw ? "draw" : Current == Phase.Discard ? "discard" : Current == Phase.Finished ? "fin" : "opp",
                Active = Active, DeckCount = Current == Phase.Seating ? 106 : dealing ? 0 : m.Deck, TurnSeconds = Mathf.RoundToInt(m.TurnMs / 1000f), TurnStartedAt = turnStartedReal,
                Warn = HumanTurn && warned, TurnHint = turnHint, ShowIndicator = !dealing && Current != Phase.Seating, FirstTurn = m.TurnN == 1 && m.TurnPhase == G101Rules.PhasePlay,
                Pairs = eval.Pairs.Count, PickedTile = PickedTile, DeckOnly = HumanTurn && m.TurnPhase == G101Rules.PhaseDraw && m.SideReturned, ShowPutBack = PickedTile >= 0 && CanDiscard && Legal.ReturnSide,
                Countdown = m.Status == G101Rules.StatusLobby ? countdown : 0, CountdownAt = countdownAt, ExitOpen = ExitOpen,
                Hand = Math.Max(1, m.HandNo), MatchHands = m.Hands,
            };
            for (int k = 0; k < 4; k++) s.Seated[k] = !seating || seated[k];
            if (hud?.Model != null) { s.Level = hud.Model.Level; s.XpInLevel = hud.Model.Xp; s.XpNeed = 375L * Math.Max(1, hud.Model.Level); }
            if (momentOn) s.Moment = new TableHud.MomentInfo { Title = momentTitle, Sub = momentSub, Seat = momentSeat, StartedAt = momentAt };
            for (int k = 0; k < 4; k++)
            {
                int abs = Abs(k);
                // During the ceremony the plates count what has landed (the prototype shows 0 until the racks fill).
                s.TileCounts[k] = dealing ? 0 : k == Me && !Watching ? Rack.Count + (shortMelds?.Sum(g => g.Length) ?? 0) : m.Counts[abs];
                s.Opened[k] = m.Opened[abs]; s.OpenedPairs[k] = m.OpenMode[abs] == G101Rules.KindPair;
                s.Penalties[k] = m.Pen[abs];
            }
            if (shortMelds != null) { s.Opened[Me] = shortValue; s.OpenedPairs[Me] = shortMode == G101Rules.KindPair; }
            s.OpenSum = s.Opened[Me] > 0 ? s.Opened[Me] : eval.Sum;
            s.GlowTile = glowTile;
            var dragFace = dragTile >= 0 ? m.FaceOf(TileIds.ToV4(dragTile)) : null;
            bool canTake = CanDiscard && IOpened;
            foreach (var okey in TableOkeys())
            {
                bool target = canTake && dragTile >= 0 && m.SwapFits(okey.Meld, okey.Index, TileIds.ToV4(dragTile));
                s.Okeys.Add(new TableHud.OkeyMark { Id = okey.Id, Number = okey.Face.N.ToString(), Color = FaceColor(okey.Face.C), Target = target, Invalid = okey.Id == invalidOkey });
                if (target) s.DragBanner = T("takeOkeyBanner", FaceName(okey.Face));
            }
            bool pairsOpened = IOpened && m.OpenMode[m.Seat] == G101Rules.KindPair;
            s.CanOpen = CanDiscard && eval.Groups.Count > 0 && !pairsOpened;
            s.CanPair = CanDiscard && eval.Pairs.Count > 0;
            if (ResultOpen && m.Result != null) s.Result = ResultInfo();
            s.Deal = DealCard();
            s.CanUndo = CanUndo; s.CanFromHand = CanFinishFromHand;
            // G101-Masa openVals: Geri bırak, Yere çift aç (R-24: a series opener lays pairs too), İşle when something fits.
            s.ShowReturn = CanDiscard && Legal.ReturnSide;
            s.ShowLayPair = IOpened && eval.Pairs.Count > 0; s.CanLayPair = CanDiscard && s.ShowLayPair;
            s.CanIsle = s.ShowIsle = CanDiscard && IOpened && Rack.Count > 1 && Addable() != null;
            if (Session is Galaxy101.Online.V4TableSession net) { s.NetAttempt = net.Dropped ? Math.Max(1, net.Attempt) : 0; s.NetGaveUp = net.GaveUp; }
            if (netDemo > 0) s.NetAttempt = netDemo;
            s.OpenBar = m.OpenNeed(G101Rules.KindRun); s.PairsBar = m.OpenNeed(G101Rules.KindPair);
            s.ShortOpen = CanDiscard && !IOpened && eval.Sum < s.OpenBar;
            s.ShortPairs = CanDiscard && !IOpened && eval.Pairs.Count < s.PairsBar;
            if (Launch.Room != null) s.RoomName = Launch.Room;
            else if (m.Table != null && !m.Table.VsBots) s.RoomName = m.Table.Name + " · " + (m.Table.Mode == "pairs" ? L("Eşli", "Pairs") : L("Tekli", "Singles"));
            s.RoomStake = Launch.Stake > 0 ? Launch.Stake : m.Table?.Bet ?? 0;
            if (Launch.Chips.HasValue) s.Chips = (int)Math.Min(int.MaxValue, Launch.Chips.Value);
            else if (Session.Online && Galaxy101.Online.GalaxyOnline.Instance?.Wallet is { } wallet) s.Chips = (int)Math.Min(int.MaxValue, wallet.Chips);
            return s;
        }

        private TableHud.DealInfoCard DealCard()
        {
            var d = Model?.Deal;
            if (d == null || !Ceremony.Running || Ceremony.Step == null) return null;
            string ind = Ceremony.Revealed && Model.Indicator != null ? TileIds.Code(TileIds.ToInt(Model.Indicator)) : null;
            string okey = Ceremony.Revealed && Model.OkeyKnown ? TileIds.Code(Model.OkeyColor, Model.OkeyNumber) : null;
            bool myRoll = d.Dealer == Model.Seat && d.Step == "dice" && d.Dice.Count < 2 && Ceremony.Step == "roll";
            return new TableHud.DealInfoCard
            {
                Dealer = LocalSeat(d.Dealer), DieA = Ceremony.Dice[0], DieB = Ceremony.Dice[1], LastDie = Ceremony.LastDie, Roll = myRoll ? d.Dice.Count : -1,
                Step = Ceremony.Step, Indicator = ind, Okey = okey, Fake = Ceremony.Fake, Revealed = Ceremony.Revealed, StepAt = Time.realtimeSinceStartup,
            };
        }

        /// <summary>Hand result card (T24, T42–T43): the engine's result (R-50…R-58) explained per seat.</summary>
        private TableHud.ResultInfo ResultInfo()
        {
            var r = Model.Result;
            int finisher = TableClient.I(r["finisher"]);
            var score = TableClient.Ints(r["score"]); var pen = TableClient.Ints(r["pen"]); var baseValue = TableClient.Ints(r["base"]); var pairX = TableClient.Ints(r["pairX"]);
            int mul = TableClient.I(r["mul"]);
            int winner = Enumerable.Range(0, 4).OrderBy(k => score[k]).ThenBy(k => k).First();
            int hands = Model.Hands, hand = Model.HandNo;
            float seconds = MatchOver ? TableTimings.HandEndWait / 1000f : Mathf.Max(0, (Model.NextAt - Session.ServerNow) / 1000f) + (Time.realtimeSinceStartup - resultAt);
            var result = new TableHud.ResultInfo
            {
                Title = T("resTitle", hand),
                Sub = finisher >= 0 ? T("resFin", Upper(FirstName(LocalSeat(finisher)))) : T("resSub"),
                Head = LocalSeat(winner) == Me && !Watching ? L("Kazandın!", "You won!") : FirstName(LocalSeat(winner)) + L(" kazandı", " won"),
                Xp = LastXp, TotalXp = (int)Math.Min(int.MaxValue, XpEarned), Winner = LocalSeat(winner), StartedAt = resultAt, Seconds = Mathf.RoundToInt(seconds),
                Multiplier = mul, Unscored = false, LastHand = hand >= hands,
            };
            foreach (int k in Enumerable.Range(0, 4).OrderBy(k => score[k]).ThenBy(k => k))
            {
                int local = LocalSeat(k);
                bool finished = finisher == k, pairs = Model.OpenMode[k] == G101Rules.KindPair;
                var row = new TableHud.ResultRow
                {
                    Seat = local, Total = Model.Totals[k], Penalty = pen[k], Hand = score[k] - pen[k],
                    Note = finished ? T("noteFin") : pairs ? T("notePairs") : Model.Opened[k] > 0 ? T("noteOpen") : T("noteClosed"),
                    PenaltyWhy = string.Join(", ", penaltyWhy[local].Distinct()),
                };
                if (finished)
                {
                    if (r["okeyFinish"] is bool okf && okf) row.Badges.Add((T("mulOkey"), true));
                    if (r["pairFinish"] is bool pf && pf) row.Badges.Add((T("mulPairs"), true));
                    if (r["elden"] is bool el && el) row.Badges.Add((T("mulHand"), true));
                }
                else if (pairs) row.Badges.Add((T("pairsHand"), false));
                row.Explain = Explain(baseValue[k], finished ? 1 : pairX[k], finisher >= 0 ? mul : 1);
                result.Rows.Add(row);
            }
            return result;
        }

        /// <summary>"−101 × 2 = −202", "17 × 2 × 2 = 68": the base, the pairs ×2, then each finish ×2 (R-58: penalties apart).</summary>
        private static string Explain(int baseValue, int pairX, int mul)
        {
            var factors = new List<int>();
            if (pairX > 1) factors.Add(pairX);
            for (int x = mul; x > 1; x /= 2) factors.Add(2);
            if (factors.Count == 0) return null;
            string Number(int v) => v < 0 ? "−" + (-v).ToString("N0", new System.Globalization.CultureInfo("tr-TR")) : v.ToString("N0", new System.Globalization.CultureInfo("tr-TR"));
            return Number(baseValue) + string.Concat(factors.Select(f => " × " + f)) + " = " + Number(baseValue * pairX * mul);
        }

        // ---------- XP (R-93 / D-42), against bots kept in the menu profile ----------
        private void AddXp(long amount)
        {
            XpEarned += amount;
            var model = hud?.Model;
            if (model == null || amount <= 0) return;
            int level = model.Level; long xp = model.Xp + amount;
            while (xp >= 375L * level) { xp -= 375L * level; level++; }   // R-92: 375 × level
            model.Level = level; model.Xp = xp; model.Persist();
        }

        // ---------- settings, commands, exit ----------
        private void ReadSettings()
        {
            var s = hud.Settings;
            autoSort = s.AutoSort; doubleClickDiscard = s.DoubleClick; highlightSets = s.Highlight; warnSeconds = s.WarnSeconds; turnHint = s.TurnHints;
            // The table's sliders and sound switches drive the sound service (docs/v4/audio/RAPOR.md §5.1, EK-902).
            try { GalaxyAudio.SetLevels(new SoundLevels(s.Int("master"), s.Int("sfx"), s.Int("music"), s.Bool("tileSnd"), s.Bool("chatSnd"), GalaxyAudio.Engine.Levels?.NotificationSounds ?? true)); }
            catch (Exception e) { Debug.LogWarning("GALAXY101_TABLE_SOUND_LEVELS " + e.Message); }
        }

        private void OnSetting(string key)
        {
            ReadSettings();
            if (key == null || key == "tableSkin" || key == "rackSkin") Fx?.RescanTable();
            if (key == null || key == "tileSkin") Board?.Restyle();
            if (settled && (key == null || key == "rackSkin" || key == "highlight")) SyncBoard(key != "highlight");
            Notify();
        }

        private void OnGiftSent(string kind, int from, int to)
        {
            if (!kind.StartsWith("e:", StringComparison.Ordinal)) Gifts.Send(kind, from, to);
        }

        private void OnHudCommand(string command)
        {
            switch (command)
            {
                case "roll": RollDice(); break;
                case "undo": Undo(); break;
                case "fromhand": FinishFromHand(); break;
                case "open": OpenRuns(); break;
                case "pairs": OpenPairs(); break;
                case "isle": IsleAll(); break;
                case "sort-run": SortRack(true); break;
                case "sort-pair": SortRack(false); break;
                case "exit": RequestExit(); break;
                case "putback": PutBack(); break;
                case "exit-stay": CancelExit(); break;
                case "exit-leave": ConfirmExit(); break;
                case "next": NextHand(); break;
                case "lobby": Leave(Session.Online); break;
                case "net-lobby": Leave(true); break;
                case "net-retry": if (Session is Galaxy101.Online.V4TableSession net) net.RetryNow(); else if (netDemo > 0) { netDemo = Math.Min(5, netDemo + 1); Notify(); } break;
                default:
                    if (command.StartsWith("invite:", StringComparison.Ordinal) && int.TryParse(command.Substring(7), out int seat)) Invite(seat);
                    break;
            }
        }

        /// <summary>Exit button (T25, T30): the confirmation card; Esc or "Masada kal" closes it, "Kalk" leaves.</summary>
        public void RequestExit() { if (leaving) return; if (Watching) { Leave(true); return; } ExitOpen = true; Notify(); }
        public void CancelExit() { if (!ExitOpen) return; ExitOpen = false; Notify(); }

        /// <summary>"Kalk" (tableLeave, R-60): a bot plays my seat to the end; the server answers leftTable (1300 ms to the lobby).</summary>
        public async void ConfirmExit()
        {
            if (leaving) return;
            ExitOpen = false;
            Message(TableHud.ToastKind.Success, L("Masadan kalktın · lobiye dönülüyor", "You left the table · returning to the lobby"));
            await Session.Leave();
            LeaveAfter(TableTimings.LeaveToLobby / 1000f + 0.4f);
        }

        private void LeaveAfter(float seconds) { if (!leaving) StartCoroutine(LeaveLater(seconds)); }

        private System.Collections.IEnumerator LeaveLater(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Leave(Session.Online);
        }

        /// <summary>To the menu; a server table returns into the lobby (ranked into the Dereceli screen).</summary>
        public void Leave(bool toLobby)
        {
            if (leaving) return;
            leaving = true; life?.Cancel();
            if (toLobby) MainMenuController.ReturnToLobby = true;
            Debug.Log("GALAXY101_TABLE_EXIT scene=" + menuScene);
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(menuScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        // ---------- helpers ----------
        public Vector3 AvatarOnScreen(int seat)
        {
            var cam = table.CameraRig.Camera; var world = table.AvatarPoint(seat);
            var vp = cam.WorldToViewportPoint(world);
            if (seat == Me) return world;
            float x = Mathf.Clamp(vp.x * TableHud.Width, 84, TableHud.Width - 84) / TableHud.Width, y = Mathf.Clamp((1 - vp.y) * TableHud.Height, 150, TableHud.Height - 140) / TableHud.Height;
            return cam.ViewportToWorldPoint(new Vector3(x, 1 - y, vp.z));
        }

        private string FaceName(ColorNum face) => T("color." + face.C) + " " + face.N;
        private static string FaceColor(string c) => c == "R" ? "#D7263D" : c == "B" ? "#1F5FBF" : c == "Y" ? "#E09A12" : "#24242C";
        private string Name(int seat) => hud != null ? hud.SeatName(seat) : "P" + seat;
        private string FirstName(int seat) => hud != null ? hud.SeatFirstName(seat) : "P" + seat;
        private string Upper(string text) => text.ToUpper(new System.Globalization.CultureInfo(hud != null && hud.L("tr", "en") == "en" ? "en-US" : "tr-TR"));
        private static string Signed(int value) => value > 0 ? "+" + value : value < 0 ? "−" + (-value) : "0";
        private string Fmt(long v) => v.ToString("N0", new System.Globalization.CultureInfo(hud != null && hud.L("tr", "en") == "en" ? "en-US" : "tr-TR"));
        private string L(string tr, string en) => hud != null ? hud.L(tr, en) : tr;
        private string T(string key, params object[] args) => TableStrings.Get(key, hud != null ? hud.L("tr", "en") : "tr", args);
        public void Message(TableHud.ToastKind kind, string text, string sub = null) => hud?.Post(kind, text, sub);
    }
}
