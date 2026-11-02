using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    /// <summary>
    /// The table screen's UI Toolkit layer stack (Galaxy101_OyunMasasi_3D, 1280×720 canvas):
    /// galaxy background (stars, pg halo, planet) → 3D view (camera RT with a transparent clear) → vignette →
    /// anchored layer (opponent avatars + plates, BURAYA AT) → top shade → HUD (top bar, toast,
    /// my player panel, sort buttons and the opening meter). Drawing reuses the menu's MenuView helpers and
    /// theme tokens, and the menu's gamma presenter, so both screens blend in CSS byte space.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class TableHud : MonoBehaviour
    {
        public const float Width = 1280, Height = 720;

        /// <summary>Everything the HUD shows. <see cref="TableGame"/> fills it; the default mirrors T05 (my draw turn).</summary>
        public sealed class State
        {
            public string Phase = "draw"; // deal, draw, discard, opp, fin
            public int Active = 0, DeckCount = 24, Chips = 125400, Unread = 0, OpenSum = 88, OpenBar = 101, TurnSeconds = 30, Pairs, PairsBar = 5;
            /// <summary>The table's turn length (G101-Masa clockFor: every seat's clock, a bot's too, runs turnSeconds).</summary>
            public int TableTurnSeconds = 30;
            public int[] TileCounts = { 21, 21, 21, 21 };
            /// <summary>Opened total (series) or pair count per seat; 0 = not opened.</summary>
            public int[] Opened = { 0, 0, 117, 0 };
            public bool[] OpenedPairs = new bool[4];
            public bool TurnHint = true, ShowIndicator = true, Warn, FirstTurn, CanDraw, CanOpen, CanPair;
            /// <summary>Source action gauge: current sort mode and the unmultiplied points left after opening.</summary>
            public bool PairGauge;
            public int LeftPoints;
            /// <summary>Time.realtimeSinceStartup when the active turn began (the ring counts down from it); -1 = HUD build time.</summary>
            public float TurnStartedAt = -1;
            /// <summary>Faz 4: seats filled while waiting, 3-2-1 countdown, finish banner, hand result, leave card.</summary>
            public bool[] Seated = { true, true, true, true };
            public int Countdown, Hand = 1;
            public float CountdownAt;
            public bool ExitOpen;
            public MomentInfo Moment;
            public ResultInfo Result;
            /// <summary>spec/17: this hand's penalty total per seat (badges), the tile taken from the pile (YERDEN) and
            /// whether only the deck is left after putting it back.</summary>
            public int[] Penalties = new int[4];
            public int PickedTile = -1;
            public bool DeckOnly, ShowPutBack;
            /// <summary>T28: hand counter and the XP / level pill (bot tables are XP-only).</summary>
            public int MatchHands = 5, Level = 24;
            public long XpInLevel = 6240, XpNeed = 9000;
            public MatchInfo Match;
            public IReadOnlyList<ScoreHand> ScoreHistory = Array.Empty<ScoreHand>();
            /// <summary>T33/T47–T49: every okey on the table with what it stands for, drag targets and refusals; the
            /// tooltip under one okey; the band text while dragging a tile that can take an okey.</summary>
            public readonly List<OkeyMark> Okeys = new List<OkeyMark>();
            public string DragBanner;
            /// <summary>spec/21: the deal ceremony card and the "Zar at" button while dealing.</summary>
            public DealInfoCard Deal;
            /// <summary>spec/22: Geri topla, Elden bit, and Aç below 101 (orange: a wrong opening costs +101).</summary>
            public bool CanUndo, CanFromHand, ShortOpen, ShortPairs;
            /// <summary>potRun / potPair: the Aç and Çift aç labels' figures (the sorted rack's when nothing is laid out).</summary>
            public int OpenPotential, PairsPotential;
            /// <summary>Online: the table's name ("Gece Kuşları · Tekli") and stake for the top bar; null at a bot table.</summary>
            public string RoomName; public long RoomStake;
            /// <summary>v4 openVals: Geri bırak (R-32), İşle (R-25, something fits), Yere çift aç (R-24) and their states.</summary>
            public bool ShowReturn, ShowIsle, CanIsle, ShowLayPair, CanLayPair;
            /// <summary>protocol.json reconnect: "Bağlantı koptu · Deneme n/5" (0 = connected); GaveUp leaves only "Lobiye dön".</summary>
            public int NetAttempt; public bool NetGaveUp;
            /// <summary>The account opened on another device: the card says so and offers only "Lobiye dön".</summary>
            public bool NetReplaced;
            /// <summary>G101-Masa tOpened ("Elini açtın · şimdi bir taş at"): my discard after a first turn once I have opened (the prototype never sets _seenOpen).</summary>
            public bool OpenedThisTurn;
        }

        public sealed class OkeyMark { public int Id; public string Number, Color; public bool Target, Invalid; }

        /// <summary>Message kinds (spec/17 §5). Penalties, errors and warnings flash red, the rest green.</summary>
        public enum ToastKind { Penalty, Info, Success, Warning, Error }

        private sealed class Toast { public ToastKind Kind; public string Text; public float Until, ShownAt; public bool Sticky; }
        private sealed class BalloonInfo { public string Text; public bool Danger; public float Until, ShownAt, Duration = 2.5f; }
        private Toast flash;
        private readonly Dictionary<int, BalloonInfo> balloons = new Dictionary<int, BalloonInfo>();
        private readonly List<int> expired = new List<int>(4); // per-frame scratch: no list allocated while nothing expires
        private VisualElement toastLayer;

        /// <summary>World position of a tile view (the board's), for labels that follow a tile.</summary>
        public Func<int, Vector3?> TilePosition;

        /// <summary>The flashed message's text (tests, diagnostics).</summary>
        public string ActiveToast => flash?.Text;

        /// <summary>
        /// G101-Masa flash(text, bad): one pill under the top bar (top 132) for 2.6 s; a new message replaces the text and
        /// restarts the time without replaying the entrance. Sticky messages stay until dismissed.
        /// </summary>
        public void Post(ToastKind kind, string text, string sub = null, int amount = 0, bool sticky = false)
        {
            float now = Time.realtimeSinceStartup;
            if (amount > 0) text += " · +" + view?.N(amount) + L(" ceza", " penalty");
            if (!string.IsNullOrEmpty(sub)) text += " · " + sub;
            flash = new Toast { Kind = kind, Text = text, Until = now + 2.6f, ShownAt = flash != null ? flash.ShownAt : now, Sticky = sticky };
            RenderToasts();
        }

        public void Dismiss(string text)
        {
            if (flash == null || !flash.Text.StartsWith(text, StringComparison.Ordinal)) return;
            flash = null; RenderToasts();
        }

        /// <summary>Avatar balloon (2.5 s): white for events, danger gradient for an opponent's penalty.</summary>
        public void Balloon(int seat, string text, bool danger)
        {
            float now = Time.realtimeSinceStartup;
            balloons[seat] = new BalloonInfo { Text = text, Danger = danger, Until = now + 2.5f, ShownAt = now };
            if (view != null) Render(Current);
        }

        /// <summary>The state router's freeze: avatar balloons (CSS bubble 2.8 s forwards) have played out at the canonical instant.</summary>
        public void SettleForFreeze()
        {
            if (balloons.Count == 0) return;
            balloons.Clear();
            if (view != null) Render(Current);
        }

        private void Update()
        {
            if (view == null) return;
            float now = Time.realtimeSinceStartup; bool dirty = false;
            if (flash != null && !flash.Sticky && now > flash.Until) { flash = null; RenderToasts(); }
            if (balloons.Count > 0)
            {
                expired.Clear();
                foreach (var pair in balloons) if (now > pair.Value.Until) expired.Add(pair.Key);
                foreach (var seat in expired) { balloons.Remove(seat); dirty = true; }
            }
            UpdateWindows();
            UpdateFlatInteraction();
            UpdateFlatEffects();
            if (dirty) Render(Current);
        }

        /// <summary>Finish banner (spec/10 §7–§8): seat −1 = no avatar (TAŞ BİTTİ).</summary>
        public sealed class MomentInfo { public string Title, Sub; public int Seat = -1; public float StartedAt; }

        /// <summary>Hand result card (T24). Bot tables are offline: XP only, no chips.</summary>
        public sealed class ResultInfo
        {
            public string Title, Sub, Head, Chips, ChipColor;
            public int Xp, TotalXp, Winner, Seconds = 15, Multiplier = 1;
            public bool Unscored, LastHand;
            public float StartedAt;
            public readonly List<ResultRow> Rows = new List<ResultRow>();
        }

        /// <summary>
        /// One result row (T42–T44): hand points (B × M), their explanation ("−101 × 2 = −202"), this hand's penalty with
        /// its reason, multiplier badges (gold: finish multipliers; grey: "Çift açtı · el ×2") and the running total.
        /// </summary>
        public sealed class ResultRow
        {
            public int Seat, Total, Penalty;
            public int? Hand;
            public string Note, Explain, PenaltyWhy;
            public readonly List<(string Text, bool Gold)> Badges = new List<(string, bool)>();
        }

        /// <summary>Match end (T45–T46): standings, XP breakdown with the level bar, points by hand.</summary>
        public sealed class MatchInfo
        {
            public string Kick, Head, Sub;
            public bool Won;
            public int Place, Xp, LevelFrom, LevelTo, Hands;
            public float FromFraction, ToFraction, StartedAt;
            public bool LevelUp;
            public string XpLeft;
            public readonly List<(int Seat, int Total)> Standings = new List<(int, int)>();
            public readonly List<(string Label, string Times, int Value)> XpLines = new List<(string, string, int)>();
            public int?[][] ByHand;
        }

        /// <summary>HUD buttons: exit, settings, chat, gift, sort-run, sort-pair, open, pairs.</summary>
        public event Action<string> Command;

        /// <summary>Act as if a HUD button with this command was pressed (keyboard shortcuts, tests).</summary>
        public void Press(string command) { if (!HandleLocal(command)) Command?.Invoke(command); }

        [SerializeField] private TableScene table;

        private static readonly Dictionary<string, string> ThemeIds = new Dictionary<string, string> { ["Satürn"] = "saturn", ["Mars"] = "mars", ["Neptün"] = "neptune", ["Jüpiter"] = "jupiter", ["Venüs"] = "venus" };
        private UIDocument document;
        private MenuModel model;
        private LocalizationBridge loc;
        private MenuView view;
        private GalaxyBackground background;
        private RenderTexture viewTarget;
        private VisualElement canvas, view3d, anchorLayer, hudLayer;
        private JArray seats;
        private readonly Dictionary<string, VisualElement> anchors = new Dictionary<string, VisualElement>();
        private readonly List<Action<float>> motions = new List<Action<float>>();
        /// <summary>The message toasts' own motions: redrawn with the toasts, so a dismissed toast's animation goes with it.</summary>
        private readonly List<Action<float>> toastMotions = new List<Action<float>>();
        private int viewMsaa = 4; private float viewScaleCap = 3f;
        private float turnStarted;

        public State Current { get; private set; } = new State();
        /// <summary>The menu profile (level, XP) the table writes to.</summary>
        public MenuModel Model => model;
        public VisualElement Canvas => canvas;
        /// <summary>The HUD's view (themes, fonts, texts): the table scene's own screens (the loading screen of G01/T50/T51) draw with it.</summary>
        public MenuView View => view;
        public TableScene Table => table;

        private void Start() { if (view == null) Initialize(); }

        public string L(string tr, string en) => view != null ? view.L(tr, en) : tr;
        public string SeatName(int k) => seats != null ? (string)Seat(k)["name"] : "";
        /// <summary>
        /// A server match's seat: name, initials, colour, level, whether the server's bot plays it, and the account's
        /// league, win rate and hands for the player card (none for a bot: the sample figures never show for a real seat).
        /// </summary>
        public void SetSeat(int k, string name, string color, int level, bool bot, string league = null, int winRate = 0, int hands = 0)
        {
            if (seats == null) return;
            // Server bots arrive as "Bot · Ayşe": the badge says BOT, the plate the name.
            if (bot && name != null && name.StartsWith("Bot · ")) name = name.Substring(6);
            var seat = Seat(k); seat["name"] = name; seat["ini"] = Galaxy101.ProfileValidation.Initials(name ?? "");
            if (!string.IsNullOrEmpty(color)) seat["color"] = color;
            seat["lv"] = Math.Max(1, level); seat["bot"] = bot;
            seat["rank"] = league == null ? "BOT" : (string)model.Content["leaderboard"]?["leagues"]?[view.Loc.Language]?[league] ?? league;
            seat["win"] = Math.Max(0, Math.Min(100, winRate)); seat["hands"] = Math.Max(0, hands);
        }
        public string SeatFirstName(int k) => seats != null ? FirstName(k) : "";

        public void Initialize(string persistencePath = null)
        {
            if (view != null) return;
            if (table == null) table = FindAnyObjectByType<TableScene>() ?? throw new InvalidOperationException("TableHud needs a TableScene.");
            if (table.CameraRig == null) table.Build();
            document = GetComponent<UIDocument>();
            if (document.panelSettings == null) document.panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            if (document.panelSettings == null) throw new InvalidOperationException("Missing authored Galaxy101 PanelSettings.");
            (GetComponent<MenuGammaPresenter>() ?? gameObject.AddComponent<MenuGammaPresenter>()).Initialize(document);

            var content = Resources.Load<TextAsset>("Galaxy101/content"); var themes = Resources.Load<TextAsset>("Galaxy101/themes");
            if (content == null || themes == null) throw new InvalidOperationException("Galaxy101 menu data resources are missing.");
            model = new MenuModel(content.text, themes.text, persistencePath ?? GalaxyPreferencesBridge.ResolveStatePath());
            model.Planet = table.Planet; // the HUD wears the table's theme tokens
            loc = new LocalizationBridge(model);
            view = new MenuView(document.rootVisualElement, model, new MenuServices(model), loc);
            seats = (JArray)JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Table/table_content").text)["seats"];
            // My avatar wears the account's own frame; the others' sample frames sit as the reference draws them (G12, G30:
            // gold for the right and left players, plain across).
            Seat(0)["frame"] = (int?)model.Profile?["frame"] ?? 0;
            Seat(1)["frame"] = 1; Seat(2)["frame"] = 0; Seat(3)["frame"] = 1;

            var root = document.rootVisualElement; root.Clear();
            canvas = new VisualElement { name = "table-canvas", pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("galaxy-canvas"); canvas.AddToClassList("theme-" + (ThemeIds.TryGetValue(table.Planet, out var id) ? id : "saturn"));
            root.Add(canvas);

            var backdrop = Layer("table-background"); canvas.Add(backdrop);
            background = new GameObject("Galaxy background").AddComponent<GalaxyBackground>(); background.transform.SetParent(transform, false);
            background.Initialize(backdrop); background.Configure(table.Planet, (bool?)model.Settings["motion"] ?? true, (string)model.Settings["quality"] ?? "Yüksek");
            ApplyEffectQuality();
            background.UseTableLayout(TableTheme.Load(table.Planet).Pg);
            background.SetFocused(Application.isFocused || Application.isBatchMode);

            view3d = Layer("table-3d"); canvas.Add(view3d); ResizeViewTarget();
            InitializeTwoDimensional();
            var vignette = Layer("table-vignette"); canvas.Add(vignette);
            view.Decorate(vignette, "radial-gradient(ellipse 80% 70% at 50% 58%,transparent 60%,rgba(0,0,0,.42))");
            anchorLayer = Layer("table-anchors"); canvas.Add(anchorLayer);
            var shade = Layer("table-top-shade"); shade.style.bottom = StyleKeyword.Auto; shade.style.height = 120; canvas.Add(shade);
            view.Decorate(shade, "linear-gradient(rgba(3,5,14,.78) 0%,rgba(3,5,14,.5) 45%,transparent)");
            hudLayer = Layer("table-hud"); canvas.Add(hudLayer);
            toastLayer = Layer("table-toasts"); canvas.Add(toastLayer);
            InitializeWindows(persistencePath == null);
            InitializeFlatSettings();
            AppVersionLabel.Attach(canvas);

            turnStarted = Time.realtimeSinceStartup;
            Render(Current);
            // Each panel update (every rendered frame), not a fixed 16 ms that beats against a 144 Hz display.
            // At the g101-clock freeze the reference's CSS animations stand at the canonical instant, long after they began:
            // entrances and finite fills have run out. Motions then read a clock far ahead; the figures that come from
            // state (seconds left) read the real clock.
            canvas.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup + (GameClock.Frozen ? FreezeLead : 0);
                foreach (var motion in motions) motion(now); foreach (var motion in toastMotions) motion(now); foreach (var motion in windowMotions) motion(now);
            }).Every(0);
            Debug.Log("GALAXY101_TABLE_HUD_READY theme=" + table.Planet + " language=" + loc.Language);
        }

        private const float FreezeLead = 1000;

        private static VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            return layer;
        }

        /// <summary>
        /// The 3D camera draws at the canvas' device-pixel size (same rule as the galaxy background), up to the effect
        /// quality's cap (Yüksek 3× — a 4K screen gets native pixels, not an upscale; Orta 1.5×; Düşük 1×) and with its
        /// MSAA (4×, 2×, off). Above 2.2× the pixels are dense enough that Yüksek draws 2× MSAA, keeping a 4K frame's cost
        /// near the old 2.2× × 4× budget.
        /// </summary>
        private void ResizeViewTarget()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / Width, Screen.height / Height), 1, viewScaleCap);
            int w = Mathf.RoundToInt(Width * scale), h = Mathf.RoundToInt(Height * scale);
            int msaa = scale > 2.2f ? Mathf.Min(viewMsaa, 2) : viewMsaa;
            if (viewTarget != null && viewTarget.width == w && viewTarget.height == h && viewTarget.antiAliasing == msaa) return;
            var previous = viewTarget;
            viewTarget = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Galaxy101 table view", antiAliasing = msaa, hideFlags = HideFlags.DontSave };
            viewTarget.Create();
            table.RenderInto(viewTarget);
            view3d.style.backgroundImage = Background.FromRenderTexture(viewTarget);
            if (previous != null) { previous.Release(); Kill(previous); }
        }

        private void LateUpdate()
        {
            if (view == null) return;
            if (IsTwoDimensional)
            {
                foreach (var pair in anchors)
                {
                    bool visible = FlatAnchor(pair.Key, out var point);
                    pair.Value.style.left = point.x; pair.Value.style.top = point.y;
                    pair.Value.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
                }
                return;
            }
            ResizeViewTarget();
            var cam = table.CameraRig.Camera;
            foreach (var pair in anchors)
            {
                Vector3? world = dynamicAnchors.TryGetValue(pair.Key, out var follow) ? follow() : table.AnchorWorld(pair.Key);
                if (!world.HasValue) { pair.Value.style.visibility = Visibility.Hidden; continue; }
                var vp = cam.WorldToViewportPoint(world.Value);
                float x = vp.x * Width, y = (1 - vp.y) * Height;
                // clampA(): avatars stay inside the HUD-free band.
                if (pair.Key[0] == 'a') { x = Mathf.Clamp(x, 84, Width - 84); y = Mathf.Clamp(y, 150, Height - 140); }
                pair.Value.style.left = x; pair.Value.style.top = y;
                pair.Value.style.visibility = vp.z > 0 ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private JObject Seat(int k) => (JObject)seats[k];
        private string FirstName(int k) => ((string)Seat(k)["name"]).Split(' ')[0];

        // ---------- render ----------
        // Entrance animations of rebuilt HUD parts run from when the part first appeared (a result card re-rendered by its
        // countdown keeps still), keyed by what the part shows; a part gone for one render starts afresh next time.
        private readonly Dictionary<string, float> firstSeen = new Dictionary<string, float>();
        private readonly HashSet<string> seenNow = new HashSet<string>();
        private float Since(string key)
        {
            seenNow.Add(key);
            if (!firstSeen.TryGetValue(key, out float at)) firstSeen[key] = at = Time.realtimeSinceStartup;
            return at;
        }

        public void Render(State state)
        {
            var chipsBefore = Current.Chips;
            Current = state ?? throw new ArgumentNullException(nameof(state));
            if (view == null) return; // rendered once Initialize runs
            if (Window == "scoreboard")
            {
                if (state.Result != null || state.Match != null) CloseWindow();
                else if (ScoreHistoryChanged) RenderWindows(true);
            }
            // An open gift drawer or player card shows the balance and what it affords: a wallet change redraws it.
            if (state.Chips != chipsBefore && (Window == "gift" || Window == "card")) RenderWindows(true);
            seenNow.Clear();
            motions.Clear(); anchors.Clear(); dynamicAnchors.Clear(); anchorLayer.Clear(); hudLayer.Clear();
            for (int k = 1; k <= 3; k++) BuildOpponent(k, state);
            bool myTurn = state.Active == 0;
            if (!IsTwoDimensional && myTurn && state.Phase == "discard" && state.TurnHint) BuildDiscardLabel();
            BuildTopBar(state);
            BuildToast(state);
            BuildMe(state);
            // G101-Masa showActions: playing (not the waiting room) and not dealing.
            if (state.Phase != "seat" && state.Phase != "deal")
            {
                if (IsTwoDimensional) BuildFlatActions(state); else BuildActions(state);
            }
            if (state.Phase == "seat") BuildWaiting(state);
            if (state.Phase == "deal" && state.Deal != null) BuildDeal(state.Deal);
            // countPop (2.4 → 1 → 0.7, forwards to opacity 0) has played out at the g101-clock freeze.
            if (state.Countdown > 0 && !GameClock.Frozen) BuildCountdown(state);
            if (state.Moment != null) BuildMoment(state.Moment);
            if (state.Result != null) BuildResult(state.Result);
            if (state.Match != null) BuildMatchEnd(state.Match);
            if (state.ExitOpen) BuildExit();
            if (state.NetAttempt > 0 || state.NetGaveUp) BuildNet(state);
            // No HUD marks on table okeys (REMOVED.md T33 / T48; the 3D tiles carry the take-okey glow, T47).
            // The tile taken from the pile carries only its ← badge (3D); no HUD frame (G31, T38).
            RenderFlatTable();
            ApplyGaps(hudLayer); ApplyGaps(anchorLayer);
            RenderToasts();
            LateUpdate();
            foreach (var key in firstSeen.Keys.Where(k => !seenNow.Contains(k)).ToList()) firstSeen.Remove(key);
        }

        private readonly Dictionary<string, Func<Vector3?>> dynamicAnchors = new Dictionary<string, Func<Vector3?>>();

        private VisualElement Anchor(string key, Func<Vector3?> world)
        {
            var anchor = Anchor(key); dynamicAnchors[key] = world; return anchor;
        }

        private VisualElement Anchor(string key)
        {
            var anchor = new VisualElement { name = "anchor-" + key, pickingMode = PickingMode.Ignore };
            MenuView.Place(anchor, 0, 0, 0, 0); anchorLayer.Add(anchor); anchors[key] = anchor; return anchor;
        }

        private string Status(int k, State s, bool me)
        {
            if (s.Phase == "seat") return L((string)Seat(k)["rank"], (string)Seat(k)["rank"]);
            // G101-Masa status: opponents "Ceza +n · " first when penalised; then openedS / notOpenedS (no tile count).
            string penalty = !me && Penalty(k, s) > 0 ? L("Ceza +" + Penalty(k, s) + " · ", "Penalty +" + Penalty(k, s) + " · ") : "";
            if (s.Opened[k] > 0 && s.OpenedPairs[k]) return penalty + L("Açtı · " + s.Opened[k] + " çift", "Opened · " + s.Opened[k] + " pairs");
            if (s.Opened[k] > 0) return penalty + L("Açtı · " + s.Opened[k], "Opened · " + s.Opened[k]);
            return penalty + L("Açmadı", "Not opened");
        }
        private static int Penalty(int k, State s) => s.Penalties != null && k < s.Penalties.Length ? s.Penalties[k] : 0;
        /// <summary>stc: #FF8A94 for a penalised opponent, #8EF0B0 once opened, else --mt.</summary>
        private static string StatusColor(int k, State s, bool me) => !me && Penalty(k, s) > 0 ? "#FF8A94" : s.Opened[k] > 0 ? "#8EF0B0" : "var(--mt)";

        private void BuildOpponent(int k, State s)
        {
            var anchor = Anchor("a" + k);
            var column = Centered(anchor); column.style.alignItems = Align.Center;
            if (!s.Seated[k]) { BuildEmptySeat(column, k); return; }
            var avatar = Avatar(Seat(k), 62, k == s.Active && s.Phase == "opp", s.TableTurnSeconds, false, "table.p.sec[" + (k - 1) + "]");
            avatar.name = "opponent-" + k; avatar.tooltip = (string)Seat(k)["name"]; Clickable(avatar, "card:" + k); column.Add(avatar);
            if (balloons.TryGetValue(k, out var balloon)) column.Add(BalloonElement(k, balloon));
            var plate = new VisualElement { pickingMode = PickingMode.Ignore }; plate.style.marginTop = 6; plate.style.alignItems = Align.Center;
            Pad(plate, 4, 12, 5, 12); view.Decorate(plate, "rgba(5,8,22,.66)", 12); Border(plate, 1, new Color(1, 1, 1, .08f)); GalaxyFilters.BackdropBlur(plate, 6);
            // D-62: the prototype's table.p.*[i], i = 0 right, 1 across, 2 left.
            // G101-Masa table.p.name[i]: the name alone (no BOT chip: a bot's name already says so).
            var nameRow = Row(5); var seatName = Text((string)Seat(k)["name"], "Montserrat-700", 14, "#F4F1E8"); seatName.name = "table.p.name[" + (k - 1) + "]"; nameRow.Add(seatName);
            plate.Add(nameRow);
            var status = Text(Status(k, s, false), "Baloo2-800", 11, StatusColor(k, s, false)); status.name = "table.p.status[" + (k - 1) + "]"; status.style.marginTop = 1; plate.Add(status);
            column.Add(plate);
        }

        /// <summary>Empty seat (T01): dashed ring with a blinking dot and a "Davet et" pill.</summary>
        private void BuildEmptySeat(VisualElement column, int k)
        {
            var ring = new VisualElement { name = "empty-seat-" + k, pickingMode = PickingMode.Ignore };
            ring.style.width = ring.style.height = 62; ring.style.alignItems = Align.Center; ring.style.justifyContent = Justify.Center;
            view.Decorate(ring, "rgba(5,8,22,.45)", 31);
            var dashed = new CssDashedCircle(62, 2, new Color(1, 1, 1, .3f)); MenuView.Place(dashed, 0, 0, 62, 62); ring.Add(dashed);
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 10; view.Decorate(dot, "var(--a1)", 5); ring.Add(dot);
            // dots 1.2s: the dot breathes in and out.
            motions.Add(t => dot.style.opacity = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(t * Mathf.PI / 1.2f)));
            column.Add(ring);
            var invite = Text(L("Davet et", "Invite"), "Baloo2-800", 12, "#F4F1E8"); invite.name = "table.p.invite[" + (k - 1) + "]"; invite.style.marginTop = 6;
            Pad(invite, 6, 14, 6, 14); view.Decorate(invite, "var(--pnH)", 15, 3, "var(--pnS)"); GalaxyFilters.HoverBrightness(invite, 1.25f);
            column.Add(Clickable(invite, "invite:" + k));
        }

        /// <summary>"MASA HAZIRLANIYOR" card at top 470 with the n/4 fill bar (T01–T02).</summary>
        private void BuildWaiting(State s)
        {
            int seated = s.Seated.Count(x => x);
            var row = FullRow(470);
            var card = new VisualElement { name = "table-waiting", pickingMode = PickingMode.Ignore }; card.style.alignItems = Align.Center;
            Pad(card, 18, 28, 18, 28); view.Decorate(card, "rgba(5,8,22,.62)", 24); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.BackdropBlur(card, 8);
            var title = Text(L("MASA HAZIRLANIYOR", "SETTING UP THE TABLE"), "Baloo2-800", 11, "var(--a1)"); title.name = "table.t.waitTitle"; MenuView.Tracking(title, 2, 11); card.Add(title);
            var sub = Text(seated == 4 ? L("Masa doldu · oyun başlıyor", "Table full · starting") : L("Oyuncular bekleniyor · " + seated + "/4", "Waiting for players · " + seated + "/4"), "Montserrat-700", 22, "#F4F1E8");
            sub.name = "table.waitSub"; sub.style.marginTop = 10; card.Add(sub);
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.width = 200; track.style.height = 8; track.style.marginTop = 10; track.style.overflow = Overflow.Hidden;
            view.Decorate(track, "rgba(255,255,255,.14)", 4);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.style.position = Position.Absolute; fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = 200 * seated / 4f; view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 4); GalaxyFilters.Shadow(fill, 0, 0, 6, view.C("var(--gl)"));
            track.Add(fill); card.Add(track); row.Add(card); hudLayer.Add(row);
        }

        /// <summary>3-2-1 (countPop 1 s): 140 px logo digits, scale 2.4 → 1 in the first quarter, then 0.7 while fading.</summary>
        private void BuildCountdown(State s)
        {
            var row = FullRow(250);
            var digit = Text(s.Countdown.ToString(), "Montserrat-800", 140, "var(--a1)"); digit.name = "table-countdown";
            digit.style.textShadow = new TextShadow { offset = new Vector2(0, 6), blurRadius = 0, color = view.C("var(--lS)") };
            GalaxyFilters.Shadow(digit, 0, 0, 15, view.C("var(--gl)"));
            float began = s.CountdownAt;
            motions.Add(t =>
            {
                float u = Mathf.Clamp01(t - began);
                float scale = u < .25f ? Mathf.Lerp(2.4f, 1, TableConstants.EaseOutCubic(u / .25f)) : u < .6f ? 1 : Mathf.Lerp(1, .7f, (u - .6f) / .4f);
                digit.style.scale = new Scale(Vector2.one * scale); digit.style.opacity = u < .25f ? u / .25f : u < .6f ? 1 : 1 - (u - .6f) / .4f;
            });
            row.Add(digit); hudLayer.Add(row);
        }

        /// <summary>Finish banner (T23) at top 96: spinning gold ring around the finisher, logo-style title, pops in.</summary>
        private void BuildMoment(MomentInfo m)
        {
            var row = FullRow(96);
            var banner = Row(18); banner.name = "table-moment"; Pad(banner, 16, 30, 16, m.Seat >= 0 ? 18 : 30);
            view.Decorate(banner, "rgba(5,8,22,.72)", 28); Border(banner, 2, view.C("var(--a1)")); GalaxyFilters.BackdropBlur(banner, 10);
            GalaxyFilters.Shadow(banner, 0, 0, 25, view.C("var(--gl)"));
            if (m.Seat >= 0)
            {
                var holder = new VisualElement { pickingMode = PickingMode.Ignore }; holder.style.width = holder.style.height = 76;
                var spin = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(spin, -8, -8, 92, 92);
                view.Decorate(spin, "conic-gradient(var(--a1),var(--lg1),var(--a2),var(--a1))", 46); holder.Add(spin);
                holder.Add(Avatar(Seat(m.Seat), 76, false, 0, false, level: false));   // table.mo: frame and initials, no level badge
                motions.Add(t => { spin.style.rotate = new Rotate(t * 150 % 360); holder.style.translate = new Translate(0, -6 * Mathf.Abs(Mathf.Sin(t * Mathf.PI / 2))); });
                banner.Add(holder);
            }
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            var title = Text(m.Title, "Montserrat-800", 40, "var(--a1)"); MenuView.Tracking(title, 2, 40); title.name = "table-moment-title";
            title.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = view.C("var(--lS)") };
            texts.Add(title);
            var sub = Text(m.Sub, "Baloo2-800", 14, "var(--ic)"); sub.style.marginTop = 4; texts.Add(sub);
            banner.Add(texts);
            float began = m.StartedAt;
            // popIn .55s cubic-bezier(.2,1.3,.4,1): 0 → 1.1 → 1.
            motions.Add(t => { float u = Mathf.Clamp01((t - began) / .55f); float k = u < .6f ? Mathf.Lerp(0, 1.1f, TableConstants.EaseOutCubic(u / .6f)) : Mathf.Lerp(1.1f, 1, (u - .6f) / .4f); banner.style.scale = new Scale(Vector2.one * k); });
            row.Add(banner); hudLayer.Add(row);
        }

        /// <summary>Dimmed, blurred full-canvas backdrop that keeps pointer presses off the 3D view.</summary>
        private VisualElement Backdrop(string name, string command)
        {
            var layer = Layer(name); layer.pickingMode = PickingMode.Position; layer.style.alignItems = Align.Center; layer.style.justifyContent = Justify.Center;
            var dim = Layer(name + "-dim"); dim.pickingMode = PickingMode.Position; dim.style.backgroundColor = new Color(0, 0, 0, .55f); GalaxyFilters.BackdropBlur(dim, 6);
            if (command != null) Clickable(dim, command);
            layer.Add(dim); hudLayer.Add(layer); return layer;
        }

        private VisualElement Card(float width, int radius, string key)
        {
            var card = new VisualElement { pickingMode = PickingMode.Position }; card.style.width = width;
            view.Decorate(card, "var(--pn)", radius, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f));
            GalaxyFilters.Shadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            float began = Since(key);
            // modalIn .35s: y +18 → 0, scale .95 → 1.
            motions.Add(t => { float u = MenuView.CssBezier(.2f, 1.2f, .4f, 1, Mathf.Clamp01((t - began) / .35f)); card.style.translate = new Translate(0, 18 * (1 - u)); card.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.95f, 1, u)); card.style.opacity = Mathf.Clamp01(u); });
            return card;
        }

        /// <summary>
        /// G101-Masa result card (G34/T24/T42/T43): rank, player, full hand score and total; hand XP beside the treats.
        /// </summary>
        private void BuildResult(ResultInfo r)
        {
            var layer = Backdrop("table-result", null);
            var card = Card(822, 30, "result"); card.name = "table-result-card"; card.style.overflow = Overflow.Hidden;
            var header = Row(18); Pad(header, 26, 30, 18, 30);
            view.Decorate(header, "linear-gradient(180deg,rgba(255,255,255,.06),transparent)", 0);
            var left = new VisualElement { pickingMode = PickingMode.Ignore }; left.style.flexGrow = 1;
            var caption = Row(0); caption.name = "table.res.title"; caption.style.height = 18;
            foreach (string part in new[] { r.Title, "\u00a0·\u00a0", r.Sub })
            {
                var partText = Text(part, "Baloo2-800", 11, "var(--mt)"); partText.style.height = 18; MenuView.Tracking(partText, 2, 11); caption.Add(partText);
            }
            left.Add(caption);
            var head = Text(r.Head, "Montserrat-800", 40, "var(--a1)"); head.name = "table.res.head"; head.style.height = 44; head.style.marginTop = 4;
            head.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = view.C("var(--lS)") }; left.Add(head); header.Add(left);
            CssGradientText.Attach(head, view.C("var(--lg1)"), view.C("var(--a1)"), view.C("var(--a2)"));
            var right = new VisualElement { pickingMode = PickingMode.Ignore }; right.style.alignItems = Align.FlexEnd;
            // chipsCap: "EL" over the hand count until the match's last hand, then "ÇİP".
            var chipsCap = Caption(r.LastHand ? L("ÇİP", "CHIPS") : L("EL", "HAND")); chipsCap.name = "table.t.chipsCap"; right.Add(chipsCap);
            var chipsRow = Row(8); chipsRow.style.marginTop = 2;
            var coin = view.ChipIcon(26); chipsRow.Add(coin);
            var chips = Text(r.Chips ?? Current.Hand + " / " + Current.MatchHands, "Montserrat-700", 30, r.ChipColor ?? "var(--ic)"); chips.name = "table.res.chips"; chips.style.height = 30; chipsRow.Add(chips); right.Add(chipsRow);
            header.Add(right); card.Add(header);

            BuildScoreBody(card, r);

            var strip = Row(12); strip.style.marginTop = 16; strip.style.marginLeft = 30; strip.style.marginRight = 30; Pad(strip, 10, 14, 10, 14); view.Decorate(strip, "var(--dp)", 16);
            var gifts = Caption(L("ALDIĞIN İKRAMLAR", "TREATS RECEIVED")); gifts.name = "table.t.giftsGot"; strip.Add(gifts);
            if (received.Count == 0) { var none = Text(L("Bu elde ikram gelmedi", "No treats this hand"), "Baloo2-700", 13, "var(--mt)"); none.name = "table.t.noGifts"; none.style.flexGrow = 1; none.style.flexShrink = 1; strip.Add(none); }
            else
            {
                // spec/13 §3: the treats I received this hand, 38 px pictures.
                var got = Row(6); got.name = "result-gifts"; got.style.flexGrow = 1; got.style.flexShrink = 1; got.style.overflow = Overflow.Hidden;
                foreach (var kind in received) { var img = new VisualElement { pickingMode = PickingMode.Position, tooltip = GiftName(kind, false) }; img.style.width = img.style.height = 38; img.style.flexShrink = 0; if (GiftThumb(kind) is Texture2D thumb) img.style.backgroundImage = thumb; got.Add(img); }
                strip.Add(got);
            }
            var xp = Text("+" + view.N(r.Xp) + " XP", "Baloo2-800", 13, "#8EF0B0"); xp.name = "table.res.xp"; strip.Add(xp);
            card.Add(strip);

            var buttons = Row(12); buttons.style.justifyContent = Justify.FlexEnd; Pad(buttons, 20, 30, 26, 30);
            var leave = Text(L("Masadan kalk", "Leave table"), "Montserrat-700", 17, "#F4F1E8"); leave.name = "table.askExit#2"; Pad(leave, 15, 24, 15, 24);
            view.Decorate(leave, "var(--pnH)", 18, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(leave, 1.2f); buttons.Add(Clickable(leave, "exit"));
            var next = new VisualElement { name = "table.nextHand", pickingMode = PickingMode.Position }; next.style.overflow = Overflow.Hidden; Pad(next, 15, 34, 15, 34);
            view.Decorate(next, "linear-gradient(var(--a1),var(--a2))", 18, 5, "var(--aS)");
            PulsePanelCommand(next, 2, view.C("var(--gl)"));
            var bar = new VisualElement { pickingMode = PickingMode.Ignore }; bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.top = 0; bar.style.bottom = 0; bar.style.backgroundColor = new Color(1, 1, 1, .32f);
            next.Add(bar);
            bool last = r.LastHand;
            var label = Text(last ? L("Maçı bitir", "Finish match") : L("Sonraki el · ", "Next hand · ") + r.Seconds, "Montserrat-800", 18, "var(--aT)"); label.name = "table.nextLabel"; next.Add(label);
            float start = r.StartedAt; int total = r.Seconds;
            // barFill 15s + the countdown in the label ("Sonraki el · 11"); the last hand leads to the match end.
            motions.Add(t =>
            {
                float elapsed = Mathf.Max(0, t - start), counted = Mathf.Max(0, Time.realtimeSinceStartup - start);
                bar.style.width = Length.Percent(total <= 0 ? 100 : Mathf.Clamp01(elapsed / total) * 100);
                label.text = last ? L("Maçı bitir", "Finish match") : L("Sonraki el · ", "Next hand · ") + Mathf.Max(0, total - Mathf.FloorToInt(counted));
            });
            buttons.Add(Clickable(next, "next"));
            card.Add(buttons);
            layer.Add(card);
        }

        private Label Caption(string text) { var label = Text(text, "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(label, 1.4f, 10); return label; }

        /// <summary>
        /// Match end (T45–T46): trophy or silver place, final standings with medal discs, XP earned with its breakdown and
        /// the level bar (a level-up fills, flashes and restarts with "SEVİYE ATLADIN · SV 25"), points by hand, confetti
        /// when I win.
        /// </summary>
        private void BuildMatchEnd(MatchInfo m)
        {
            var layer = Backdrop("table-match", null); layer.Q<VisualElement>("table-match-dim").style.backgroundColor = new Color(0, 0, 0, .7f);
            if (m.Won) Confetti(layer, m.StartedAt);
            var card = Card(1060, 30, "match"); card.name = "table-match-card"; card.style.flexDirection = FlexDirection.Row; Pad(card, 24, 24, 22, 24);
            // Left: badge, title, standings.
            var left = new VisualElement { pickingMode = PickingMode.Ignore }; left.style.width = 380; left.style.marginRight = 22;
            var top = Row(16);
            var medal = new VisualElement { pickingMode = PickingMode.Ignore }; medal.style.width = medal.style.height = 84; medal.style.alignItems = Align.Center; medal.style.justifyContent = Justify.Center;
            view.Decorate(medal, m.Won ? "linear-gradient(var(--a1),var(--a2))" : "linear-gradient(#E9EDF5,#9AA3B5)", 22, 5, m.Won ? "var(--aS)" : "#5D6475");
            if (m.Won) medal.Add(view.Icon("crown", 40, "var(--aT)")); else medal.Add(Text(m.Place.ToString(), "Montserrat-800", 40, "#2B3040"));
            top.Add(medal);
            var titles = new VisualElement { pickingMode = PickingMode.Ignore };
            titles.Add(Caption(m.Kick));
            var head = Text(m.Head, "Montserrat-800", 34, "var(--a1)"); head.name = "match-head"; head.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = view.C("var(--lS)") }; titles.Add(head);
            var sub = Text(m.Sub, "Baloo2-800", 12, "var(--ic)"); sub.style.whiteSpace = WhiteSpace.Normal; sub.style.maxWidth = 260; titles.Add(sub);
            top.Add(titles); left.Add(top);
            var standingsCap = Caption(TableStrings.Get("msRank", Lang)); standingsCap.style.marginTop = 16; left.Add(standingsCap);
            string[] medals = { "linear-gradient(var(--a1),var(--a2))", "linear-gradient(#E9EDF5,#9AA3B5)", "linear-gradient(#E8B48A,#B5764A)", "rgba(255,255,255,.14)" };
            for (int i = 0; i < m.Standings.Count; i++)
            {
                var (seat, total) = m.Standings[i];
                var row = Row(10); row.name = "match-row-" + seat; row.style.marginTop = 8; Pad(row, 10, 14, 10, 12); view.Decorate(row, seat == 0 ? "var(--pnH)" : "var(--dp)", 14);
                if (i == 0) Border(row, 2, view.C("var(--a1)"));
                var disc = new VisualElement { pickingMode = PickingMode.Ignore }; disc.style.width = disc.style.height = 24; disc.style.alignItems = Align.Center; disc.style.justifyContent = Justify.Center;
                view.Decorate(disc, medals[Mathf.Min(i, 3)], 12); disc.Add(Text((i + 1).ToString(), "Montserrat-800", 11, i < 3 ? "#2B2008" : "#F4F1E8")); row.Add(disc);
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 28; dot.style.alignItems = Align.Center; dot.style.justifyContent = Justify.Center;
                view.Decorate(dot, (string)Seat(seat)["color"], 14); dot.Add(Text((string)Seat(seat)["ini"], "Montserrat-700", 11, "#fff")); row.Add(dot);
                var name = Text((string)Seat(seat)["name"] + (seat == 0 ? L(" (sen)", " (you)") : ""), "Montserrat-700", 15, "#F4F1E8"); name.style.flexGrow = 1; row.Add(name);
                row.Add(Text(total < 0 ? "−" + view.N(-total) : view.N(total), "Montserrat-800", 16, "#F4F1E8"));
                left.Add(row);
            }
            card.Add(left);

            // Right: XP + level bar, points by hand, buttons.
            var right = new VisualElement { pickingMode = PickingMode.Ignore }; right.style.flexGrow = 1;
            var xpBox = new VisualElement { name = "match-xp", pickingMode = PickingMode.Ignore }; Pad(xpBox, 14, 16, 14, 16); view.Decorate(xpBox, "var(--dp)", 18);
            var xpTop = Row(16);
            var xpLeft = new VisualElement { pickingMode = PickingMode.Ignore }; xpLeft.style.flexGrow = 1;
            xpLeft.Add(Caption(TableStrings.Get("msXp", Lang)));
            var xpValue = Text("+" + view.N(m.Xp) + " XP", "Montserrat-800", 30, "#6BE8B0"); xpValue.name = "match-xp-value"; xpLeft.Add(xpValue);
            xpTop.Add(xpLeft);
            var lines = new VisualElement { pickingMode = PickingMode.Ignore }; lines.style.width = 260;
            foreach (var (labelText, times, value) in m.XpLines)
            {
                var line = Row(8); line.style.marginBottom = 4;
                var l = Text(labelText, "Baloo2-800", 11, "var(--ic)"); l.style.flexGrow = 1; line.Add(l);
                line.Add(Text(times, "Baloo2-700", 11, "var(--mt)"));
                var v = Text("+" + view.N(value), "Baloo2-800", 11, "#6BE8B0"); v.style.minWidth = 44; v.style.unityTextAlign = TextAnchor.MiddleRight; line.Add(v);
                lines.Add(line);
            }
            xpTop.Add(lines); xpBox.Add(xpTop);
            var levelRow = Row(10); levelRow.style.marginTop = 10;
            var from = LevelChip(m.LevelFrom); levelRow.Add(from);
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.flexGrow = 1; track.style.height = 10; track.style.overflow = Overflow.Hidden; view.Decorate(track, "rgba(255,255,255,.12)", 5);
            var prev = new VisualElement { pickingMode = PickingMode.Ignore }; prev.style.position = Position.Absolute; prev.style.left = 0; prev.style.top = 0; prev.style.bottom = 0; view.Decorate(prev, "linear-gradient(90deg,var(--a2),var(--a1))", 5);
            var gain = new VisualElement { name = "match-xp-gain", pickingMode = PickingMode.Ignore }; gain.style.position = Position.Absolute; gain.style.top = 0; gain.style.bottom = 0; gain.style.backgroundColor = view.C("#5BE3A6");
            track.Add(prev); track.Add(gain); levelRow.Add(track);
            var to = LevelChip(m.LevelFrom + 1); levelRow.Add(to);
            xpBox.Add(levelRow);
            var levelFoot = Row(10); levelFoot.style.marginTop = 8; levelFoot.style.justifyContent = Justify.Center;
            var levelUp = Text(TableStrings.Get("msLevelUp", Lang, m.LevelTo), "Baloo2-800", 10, "var(--aT)"); levelUp.name = "match-level-up"; MenuView.Tracking(levelUp, 1.2f, 10);
            Pad(levelUp, 3, 10, 3, 10); view.Decorate(levelUp, "linear-gradient(var(--a1),var(--a2))", 9, 3, "var(--aS)"); levelUp.style.display = DisplayStyle.None;
            levelFoot.Add(levelUp);
            levelFoot.Add(Text(m.XpLeft, "Baloo2-800", 10, "var(--mt)"));
            xpBox.Add(levelFoot);
            float began = m.StartedAt; float a = m.FromFraction, b = m.ToFraction; bool up = m.LevelUp; int lvTo = m.LevelTo;
            // The bar fills over 0.8 s; a level-up fills to the end, flashes 300 ms, then the new level fills from zero.
            motions.Add(t =>
            {
                float u = t - began;
                if (!up)
                {
                    float k = TableConstants.EaseOutCubic(Mathf.Clamp01(u / .8f));
                    prev.style.width = Length.Percent(a * 100); gain.style.left = Length.Percent(a * 100); gain.style.width = Length.Percent((b - a) * k * 100);
                    return;
                }
                if (u < 1.1f)
                {
                    float k = TableConstants.EaseOutCubic(Mathf.Clamp01(u / .8f));
                    prev.style.width = Length.Percent(a * 100); gain.style.left = Length.Percent(a * 100); gain.style.width = Length.Percent((1 - a) * k * 100);
                    track.style.opacity = u > .8f ? 0.6f + 0.4f * Mathf.Cos((u - .8f) / .3f * Mathf.PI * 2) : 1;
                }
                else
                {
                    float k = TableConstants.EaseOutCubic(Mathf.Clamp01((u - 1.1f) / .8f));
                    track.style.opacity = 1; prev.style.width = 0; gain.style.left = 0; gain.style.width = Length.Percent(b * k * 100);
                    if (levelUp.style.display != DisplayStyle.Flex) { levelUp.style.display = DisplayStyle.Flex; ((Label)from[0]).text = L("SV ", "LV ") + lvTo; ((Label)to[0]).text = L("SV ", "LV ") + (lvTo + 1); }
                    float p = Mathf.Clamp01((u - 1.1f) / .5f); levelUp.style.scale = new Scale(Vector2.one * (p < .6f ? Mathf.Lerp(0, 1.1f, p / .6f) : Mathf.Lerp(1.1f, 1, (p - .6f) / .4f)));
                }
            });
            right.Add(xpBox);

            var byHand = new VisualElement { name = "match-by-hand", pickingMode = PickingMode.Ignore }; byHand.style.marginTop = 12; Pad(byHand, 12, 16, 12, 16); view.Decorate(byHand, "var(--dp)", 18);
            var headRow = Row(0); var hc = Caption(TableStrings.Get("msByHand", Lang)); hc.style.flexGrow = 1; headRow.Add(hc);
            for (int h = 1; h <= m.Hands; h++) headRow.Add(Cell(Caption(TableStrings.Get("msHandCol", Lang, h)), 56, true));
            headRow.Add(Cell(Caption(L("TOPLAM", "TOTAL")), 70, true));
            byHand.Add(headRow);
            foreach (var (seat, total) in m.Standings.OrderBy(x => x.Seat))
            {
                var row = Row(0); row.style.marginTop = 8;
                var who = Row(8); who.style.flexGrow = 1;
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 18; view.Decorate(dot, (string)Seat(seat)["color"], 9); who.Add(dot);
                who.Add(Text((string)Seat(seat)["name"], "Montserrat-700", 12, seat == 0 ? "var(--a1)" : "#F4F1E8"));
                row.Add(who);
                for (int h = 0; h < m.Hands; h++)
                {
                    int? v = m.ByHand != null && seat < m.ByHand.Length && h < m.ByHand[seat].Length ? m.ByHand[seat][h] : null;
                    row.Add(Cell(Text(v.HasValue ? (v.Value < 0 ? "−" + view.N(-v.Value) : view.N(v.Value)) : "—", "Montserrat-700", 12, v.HasValue && v.Value < 0 ? "#6BE8B0" : "var(--ic)"), 56, true));
                }
                row.Add(Cell(Text(total < 0 ? "−" + view.N(-total) : view.N(total), "Montserrat-800", 12, "#F4F1E8"), 70, true));
                byHand.Add(row);
            }
            right.Add(byHand);

            var buttons = Row(12); buttons.style.justifyContent = Justify.FlexEnd; buttons.style.marginTop = 16;
            var lobby = Text(TableStrings.Get("msLobby", Lang), "Montserrat-700", 17, "#F4F1E8"); lobby.name = "match-lobby"; Pad(lobby, 14, 22, 14, 22);
            view.Decorate(lobby, "var(--pnH)", 16, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(lobby, 1.2f); buttons.Add(Clickable(lobby, "lobby"));
            var again = Text(TableStrings.Get("msAgain", Lang), "Montserrat-800", 17, "var(--aT)"); again.name = "match-again"; Pad(again, 14, 26, 14, 26);
            view.Decorate(again, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); buttons.Add(Clickable(again, "again"));
            right.Add(buttons);
            card.Add(right);
            layer.Add(card);
        }

        private VisualElement LevelChip(int level)
        {
            var chip = Text(L("SV ", "LV ") + level, "Baloo2-800", 10, "var(--ic)"); Pad(chip, 2, 7, 2, 7); view.Decorate(chip, "rgba(255,255,255,.1)", 7);
            var holder = new VisualElement { pickingMode = PickingMode.Ignore }; holder.Add(chip); return holder;
        }

        /// <summary>Win confetti (T23 colours): small strips falling and spinning across the card.</summary>
        private void Confetti(VisualElement layer, float began)
        {
            string[] colours = { "#FFD964", "#FF6A9A", "#7FE3FF", "#8EF0B0", "#B79CFF" };
            var random = new System.Random(101);
            for (int i = 0; i < 60; i++)
            {
                var bit = new VisualElement { pickingMode = PickingMode.Ignore }; bit.style.position = Position.Absolute; bit.style.width = 8; bit.style.height = 4;
                bit.style.backgroundColor = view.C(colours[i % colours.Length]);
                float x = (float)random.NextDouble() * Width, delay = (float)random.NextDouble() * 1.5f, speed = 120 + (float)random.NextDouble() * 160, spin = (float)random.NextDouble() * 540 - 270;
                motions.Add(t => { float u = t - began - delay; float y = -20 + u * speed; bit.style.left = x + Mathf.Sin(u * 2 + x) * 20; bit.style.top = y % (Height + 40) - 20; bit.style.rotate = new Rotate(u * spin); bit.style.opacity = u < 0 ? 0 : 1; });
                layer.Add(bit);
            }
        }

        private static VisualElement Cell(VisualElement content, float width, bool alignRight)
        {
            var cell = new VisualElement { pickingMode = PickingMode.Ignore }; if (width > 0) cell.style.width = width;
            cell.style.flexDirection = FlexDirection.Row; cell.style.justifyContent = alignRight ? Justify.FlexEnd : Justify.FlexStart; cell.style.flexShrink = 0;
            cell.Add(content); return cell;
        }

        /// <summary>Leave card (T25): the source's actual-stake warning or its no-stake practice warning.</summary>
        private void BuildExit()
        {
            var layer = Backdrop("table-exit-card", "exit-stay");
            var card = Card(460 + 56 + 2, 28, "exit"); card.name = "table-exit-confirm"; card.style.alignItems = Align.Center; Pad(card, 28, 28, 28, 28);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 64; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, "rgba(255,94,108,.16)", 20); icon.Add(view.Icon("table-exit", 32, "#FF8A94")); card.Add(icon);
            var title = Text(TableStrings.Get("exitTitle", Lang), "Montserrat-700", 24, "#F4F1E8"); title.name = "table.t.exitTitle"; title.style.marginTop = 12;
            title.style.whiteSpace = WhiteSpace.Normal; title.style.unityTextAlign = TextAnchor.UpperCenter; title.style.maxWidth = 460; title.style.flexShrink = 1; MenuView.CssLines(title, 24 * 1.2f); card.Add(title);
            long stake = FreeGifts ? 0 : Current.RoomStake;
            string warning = stake > 0 ? TableStrings.Get("exitBody", Lang).Replace(L("5.000", "5,000"), stake.ToString("N0", new CultureInfo(L("tr-TR", "en-US")))) :
                L("Maç bitmeden kalkarsan yerine bot oynar. Antrenman masasında bahis yoktur.", "If you leave before the match ends, a bot takes your seat. Practice tables have no stake.");
            var body = Text(warning, "Baloo2-700", 15, "var(--mt)"); body.name = "table.t.exitBody";
            body.style.whiteSpace = WhiteSpace.Normal; body.style.maxWidth = 360; body.style.unityTextAlign = TextAnchor.UpperCenter; body.style.marginTop = 12; MenuView.CssLines(body, 15 * 1.5f); card.Add(body);
            var buttons = Row(10); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 20;
            var stay = Text(L("Masada kal", "Stay"), "Montserrat-700", 17, "var(--aT)"); stay.name = "table.stay#2"; stay.style.flexGrow = 1; stay.style.flexBasis = 0; stay.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(stay, 14, 14, 14, 14); view.Decorate(stay, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); buttons.Add(Clickable(stay, "exit-stay"));
            var leave = Text(L("Kalk", "Leave"), "Montserrat-700", 17, "#fff"); leave.name = "table.leave"; leave.style.flexGrow = 1; leave.style.flexBasis = 0; leave.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(leave, 14, 14, 14, 14); view.Decorate(leave, "linear-gradient(#FF7A86,#E8455A)", 16, 5, "#A3283A"); buttons.Add(Clickable(leave, "exit-leave"));
            card.Add(buttons); layer.Add(card);
        }

        /// <summary>
        /// The message slot (G101-Masa: left:0;right:0;justify-content:center): centred on the canvas. A long bot-table
        /// header (wider than the design's "Tekli · 101 · Bahis") would sit under a wide message; only then the message
        /// steps right of it.
        /// </summary>
        private VisualElement MessageRow(float top)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.top = top; row.style.alignItems = Align.Center;
            row.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var room = hudLayer?.Q<VisualElement>("table.roomLabel"); if (room == null || row.childCount == 0) return;
                var toast = row[0]; float scale = row.layout.width > 0 ? row.worldBound.width / row.layout.width : 1;
                // Centred position (without this row's own shift) against the header's right edge + 12 px.
                float left = row.worldBound.center.x - row.resolvedStyle.translate.x * scale - toast.layout.width * scale / 2, clear = room.worldBound.xMax + 12 * scale;
                float shift = left < clear ? (clear - left) / scale : 0;
                if (Mathf.Abs(row.resolvedStyle.translate.x - shift) > .5f) row.style.translate = new Translate(shift, 0);
            });
            return row;
        }

        private static VisualElement FullRow(float top)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.top = top; row.style.alignItems = Align.Center;
            return row;
        }

        private void BuildDiscardLabel()
        {
            var label = Text(L("BURAYA AT", "DISCARD HERE"), "Baloo2-800", 10, "var(--aT)"); label.name = "table.t.discHere"; MenuView.Tracking(label, 1.4f, 10);
            Pad(label, 4, 9, 4, 9); view.Decorate(label, "var(--a1)", 8, 3, "var(--aS)");
            // G101-Masa sets transform: translate(-50%,-62px) and animation: floatA on the same element; the running
            // animation's transform (translateY 0 → −6px → 0, ease-in-out) replaces it, so the browser draws the label's
            // top-left corner at the anchor. That is the reference picture, so the label sits there too.
            var box = new VisualElement { pickingMode = PickingMode.Ignore }; box.style.position = Position.Absolute; box.style.left = 0; box.style.top = 0;
            Anchor("disc").Add(box); box.Add(label);
            motions.Add(t => { float f = Mathf.Repeat(t, 1.4f) / 1.4f; box.style.translate = new Translate(0, -6 * (0.5f - 0.5f * Mathf.Cos(f * 2 * Mathf.PI))); });
        }

        /// <summary>Child box at the anchor point, translated by (−50%, −50%) or (−50%, dy px).</summary>
        private static VisualElement Centered(VisualElement anchor, float dy = 0, bool middle = true)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore }; box.style.position = Position.Absolute; box.style.left = 0; box.style.top = 0;
            box.style.translate = middle ? new Translate(Length.Percent(-50), Length.Percent(-50)) : new Translate(Length.Percent(-50), dy);
            anchor.Add(box); return box;
        }

        // ---------- top bar ----------
        private void BuildTopBar(State s)
        {
            var bar = new VisualElement { name = "table-top-bar", pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.right = 0; bar.style.top = 0;
            bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center; Pad(bar, 14, 20, 14, 20); bar.userData = 12f;
            hudLayer.Add(bar);

            bar.Add(Clickable(IconButton("table-exit", 22, L("Masadan çık", "Leave table"), "table.askExit"), "exit"));
            var logo = Text("GALAXY 101", "Montserrat-800", 19, "var(--a1)"); MenuView.Tracking(logo, 3, 19);
            logo.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 0, color = view.C("var(--lS)") };
            logo.style.minWidth = 146; bar.Add(logo);   // the browser's 19 px Montserrat with 3 px tracking is 146 px wide
            CssGradientText.Attach(logo, view.C("var(--lg1)"), view.C("var(--a1)"), view.C("var(--a2)"));

            // G101-Masa table.roomLabel: "Test masası · Tekli" · "Bahis 5.000" (against bots "Botlara karşı · Tekli" · "Antrenman · bahis yok").
            var room = Row(8); room.name = "table.roomLabel"; Pad(room, 7, 13, 7, 13); view.Decorate(room, "var(--dp)", 999);
            var roomName = Text(s.RoomName ?? L("Botlara karşı · Tekli", "Vs bots · Singles"), "Baloo2-800", 12, "var(--ic)"); room.Add(roomName);
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 4; view.Decorate(dot, "var(--mt)", 2); room.Add(dot);
            var bet = Text(s.RoomName != null ? L("Bahis ", "Bet ") + view.N(s.RoomStake) : L("Antrenman · bahis yok", "Practice · no stake"), "Baloo2-800", 12, "var(--ic)"); room.Add(bet);
            bar.Add(room);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.flexGrow = 1; bar.Add(spacer);

            // Deck pill: height 44, padding 0 16 0 8, gap 9, inset shadow; the mini tile 21×28.
            var deck = Row(9); deck.style.height = 44; Pad(deck, 0, 16, 0, 8); view.Decorate(deck, "var(--dp)", 999);
            var mini = new VisualElement { pickingMode = PickingMode.Ignore }; mini.style.width = 21; mini.style.height = 28; mini.style.overflow = Overflow.Hidden;
            view.Decorate(mini, "linear-gradient(var(--tf1),var(--tf2))", 5);
            var edge = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(edge, 0, 25, 21, 3); edge.style.backgroundColor = view.C("var(--te)"); mini.Add(edge);
            deck.Add(mini);
            var deckLabel = Text(L("Taş", "Deck"), "Baloo2-800", 12, "var(--mt)"); deckLabel.name = "table.t.deck"; deck.Add(deckLabel);
            var count = Text(s.DeckCount.ToString(), "Montserrat-700", 17, "#F4F1E8"); count.name = "table.deckCount"; count.style.minWidth = 35; count.style.unityTextAlign = TextAnchor.MiddleRight; deck.Add(count);
            bar.Add(deck);

            // Chips pill (table.chipsLabel): the dashed gold coin and the balance, as at every table of the prototype (T28 too).
            var chips = Row(9); chips.name = "table-chips"; chips.style.height = 44; Pad(chips, 0, 16, 0, 8); view.Decorate(chips, "var(--dp)", 999);
            var coin = view.ChipIcon(28); chips.Add(coin);
            // chipsLabel: toLocaleString('tr-TR') in both languages.
            var balance = Text(NumberFormat.Number(s.Chips, "tr"), "Montserrat-700", 17, "#F4F1E8"); balance.name = "table.chipsLabel"; balance.style.minWidth = 92; balance.style.unityTextAlign = TextAnchor.MiddleRight; chips.Add(balance);
            bar.Add(chips);

            bar.Add(Clickable(IconButton("table-scoreboard", 22, L("Puan tablosu", "Scoreboard"), "table.openSb"), "scoreboard"));
            AddViewToggle(bar);
            var settings = Clickable(IconButton("settings", 24, L("Ayarlar", "Settings"), "table.openSettings"), "settings"); bar.Add(settings);
        }

        private VisualElement IconButton(string icon, int size, string tooltip, string name)
        {
            var button = new VisualElement { name = name, tooltip = tooltip, focusable = true };
            button.style.width = button.style.height = 44; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.Center; button.style.flexShrink = 0;
            view.Decorate(button, "var(--pnH)", 14, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(button, 1.25f);
            button.Add(view.Icon(icon, size)); return button;
        }

        // ---------- toast ----------
        /// <summary>UF-520: the one message slot in 3D, just under the top bar (14 + 44 + 14 px) and above the top seat.</summary>
        private const float MessageTop = 73;
        private void BuildToast(State s)
        {
            string text = null, bg = null, fg = null;
            if (s.DragBanner != null) { text = s.DragBanner; bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "draw" && s.Active == 0 && s.DeckOnly) { text = TableStrings.Get("drawDeckOnly", Lang); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "deal" && s.Deal == null) { text = L("Taşlar dağıtılıyor…", "Dealing tiles…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            else if (s.Phase == "draw" && s.Active == 0) { string n = FirstName(3); text = L("Sıra sende · desteden ya da " + n + "'ın taşından çek", "Your turn · draw from the deck or " + n + "'s tile"); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "discard" && s.Active == 0)
            {
                text = s.FirstTurn ? L("İlk hamle senin · ıstakadan bir taş at", "You start · discard a tile from your rack")
                    : s.OpenedThisTurn ? TableStrings.Get("tOpened", Lang) : L("Atacağın taşı halkaya sürükle ya da çift tıkla", "Drag a tile onto the ring or double-click it");
                bg = "#3CCB6E"; fg = "#062B14";
            }
            else if (s.Active > 0) { string n = FirstName(s.Active); text = L(n + " oynuyor…", n + " is playing…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            if (!s.TurnHint && s.Phase != "deal" && s.Phase != "fin") text = null;
            // G101-Masa: the time warning overrides it on my turn; a table moment and the deal ceremony hide it.
            if (s.Warn && s.Active == 0 && s.Phase != "deal") { text = L("Süren bitiyor!", "Time is running out!"); bg = "linear-gradient(#FF7A86,#E8455A)"; fg = "#fff"; }
            if (s.Moment != null || s.Deal != null) text = null;
            if (text == null) return;

            // UF-520: in 3D the band is a compact pill under the top bar (MessageTop), so the header, deck and chips stay
            // readable; a flash takes the same slot while it shows. 2D keeps the design's band over the board.
            bool flat = IsTwoDimensional;
            var row = flat ? MessageRow(16) : FullRow(MessageTop); row.name = "table-toast-row";
            if (flat) { row.style.top = 396; row.style.bottom = StyleKeyword.Auto; }
            var toast = Row(flat ? 10 : 7); if (flat) Pad(toast, 9, 18, 9, 10); else Pad(toast, 4, 13, 4, 6);
            view.Decorate(toast, bg, 20); GalaxyFilters.Shadow(toast, 0, flat ? 8 : 4, flat ? 12 : 8, new Color(0, 0, 0, .35f));
            float dot = flat ? 22 : 16;
            var bullet = new VisualElement { pickingMode = PickingMode.Ignore }; bullet.style.width = bullet.style.height = dot; bullet.style.alignItems = Align.Center; bullet.style.justifyContent = Justify.Center;
            view.Decorate(bullet, "rgba(0,0,0,.18)", (int)(dot / 2));
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore }; diamond.style.width = diamond.style.height = flat ? 8 : 6; diamond.style.rotate = new Rotate(45); view.Decorate(diamond, fg, 2);
            bullet.Add(diamond); toast.Add(bullet);
            var label = Text(text, "Baloo2-800", flat ? 14 : 12, fg); label.name = "table-toast"; toast.Add(label);
            // toastIn .3s (ease) when the band appears; the design keeps the element while its text changes, so a new
            // message does not replay it.
            float began = Since("turn-toast");
            motions.Add(now =>
            {
                float k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((now - began) / .3f)); toast.style.opacity = k; toast.style.translate = new Translate(0, -14 * (1 - k));
                var display = !flat && flash != null ? DisplayStyle.None : DisplayStyle.Flex;
                if (row.style.display != display) row.style.display = display;
            });
            row.Add(toast); hudLayer.Add(row);
        }

        private string Lang => loc != null ? loc.Language : "tr";

        // ---------- messages: G101-Masa flash ----------
        private void RenderToasts()
        {
            if (toastLayer == null) return;
            toastLayer.Clear(); toastMotions.Clear();
            if (flash == null || view == null) return;
            // top 132, centred; padding 10 18 10 12, gap 10, a check mark in the text colour, 800 14px Baloo 2, shadow
            // 0 8px 24px rgba(0,0,0,.35); bad: danger gradient / #fff, otherwise #3CCB6E / #062B14.
            bool bad = flash.Kind == ToastKind.Penalty || flash.Kind == ToastKind.Error || flash.Kind == ToastKind.Warning;
            string bg = bad ? "linear-gradient(#FF7A86,#E8455A)" : "#3CCB6E", fg = bad ? "#fff" : "#062B14";
            bool flat = IsTwoDimensional;
            var row = new VisualElement { name = "table-message-row", pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.top = flat ? 132 : MessageTop; row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center;
            var toast = Row(flat ? 10 : 7); toast.name = "table-message"; if (flat) Pad(toast, 10, 18, 10, 12); else Pad(toast, 4, 13, 4, 8);
            view.Decorate(toast, bg, 999); GalaxyFilters.Shadow(toast, 0, flat ? 8 : 4, flat ? 12 : 8, new Color(0, 0, 0, .35f));
            toast.Add(view.Icon("tick", flat ? 18 : 14, fg));
            toast.Add(Text(flash.Text, "Baloo2-800", flat ? 14 : 12, fg));
            float began = flash.ShownAt;
            // toastIn .3s ease.
            toastMotions.Add(now => { float k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((now - began) / .3f)); toast.style.opacity = k; toast.style.translate = new Translate(0, -14 * (1 - k)); });
            row.Add(toast); ApplyGaps(row); toastLayer.Add(row);
        }

        private VisualElement BalloonElement(int seat, BalloonInfo b)
        {
            var bubble = Text(b.Text, "Baloo2-800", 13, b.Danger ? "#fff" : "#14183A"); bubble.name = "balloon-" + seat;
            bubble.style.position = Position.Absolute; bubble.style.bottom = Length.Percent(100); bubble.style.marginBottom = 6; Pad(bubble, 7, 13, 7, 13);
            if (seat == 1) bubble.style.right = 0; else if (seat == 3) bubble.style.left = 0;
            else { bubble.style.left = Length.Percent(50); bubble.style.translate = new Translate(Length.Percent(-50), 0); }
            view.Decorate(bubble, b.Danger ? "linear-gradient(#FF7A86,#E8455A)" : "#fff", 14); GalaxyFilters.Shadow(bubble, 0, 6, 9, new Color(0, 0, 0, .35f));
            float began = b.ShownAt;
            // bubble 2.5s (chat 2.8s): appear 12 %, hold, rise and fade over the last 15 %.
            float duration = b.Duration;
            if (seat == 0) { bubble.style.bottom = StyleKeyword.Auto; bubble.style.left = 40; bubble.style.top = -58; bubble.style.translate = new Translate(0, 0); bubble.style.borderBottomLeftRadius = 4; }
            motions.Add(t => { float u = Mathf.Clamp01((t - began) / duration); bubble.style.opacity = u < .12f ? u / .12f : u > .85f ? (1 - u) / .15f : 1; bubble.style.translate = new Translate(seat == 2 ? Length.Percent(-50) : 0, u > .85f ? -10 * (u - .85f) / .15f : 0); });
            if (seat == 0) bubble.style.borderBottomLeftRadius = 4;
            return bubble;
        }

        // ---------- my panel ----------
        private void BuildMe(State s)
        {
            var panel = Row(12); panel.name = "table-me"; panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.bottom = 16;
            if (IsTwoDimensional) { panel.style.left = 26; panel.style.bottom = 14; panel.style.flexDirection = FlexDirection.Column; panel.style.alignItems = Align.Center; }
            bool active = s.Active == 0 && (s.Phase == "draw" || s.Phase == "discard");
            var me = Avatar(Seat(0), 74, active, s.TurnSeconds, s.Warn, "table.me.sec"); panel.Add(me);
            if (balloons.TryGetValue(0, out var mine)) me.Add(BalloonElement(0, mine));
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            if (IsTwoDimensional) column.style.alignItems = Align.Center;
            var myName = Text((string)Seat(0)["name"], "Montserrat-700", 16, "#F4F1E8"); myName.name = "table.me.name"; column.Add(myName);
            var status = Text(Status(0, s, true), "Baloo2-800", 12, StatusColor(0, s, true)); status.name = "table.me.status"; status.style.marginTop = 1; column.Add(status);
            var buttons = Row(8); buttons.style.marginTop = 6; buttons.pickingMode = PickingMode.Position;
            var chat = Square("table.openChat", "linear-gradient(160deg,#7AA6FF,#3D6BF0)", "#2448B8", L("Sohbet", "Chat"));
            chat.Add(view.Icon("table-chat", 22, "#fff"));
            if (Unread > 0)
            {
                var badge = Text(Unread.ToString(), "Baloo2-800", 10, "#fff"); badge.style.position = Position.Absolute; badge.style.top = -6; badge.style.right = -6;
                badge.style.minWidth = 18; badge.style.height = 18; badge.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(badge, 0, 4, 0, 4); view.Decorate(badge, "#FF5E6C", 9);
                chat.Add(badge);
            }
            buttons.Add(Clickable(chat, "chat"));
            var gift = Square("table.openGiftAny", "linear-gradient(160deg,var(--a1),var(--a2))", "var(--aS)", L("Hediye gönder", "Send a gift"));
            GalaxyFilters.Shadow(gift, 0, 0, 9, view.C("var(--gl)")); gift.Add(view.Icon("table-gift", 22, "var(--aT)")); buttons.Add(Clickable(gift, "gift"));
            column.Add(buttons); panel.Add(column);
            hudLayer.Add(panel);
        }

        private VisualElement Square(string name, string background, string shadow, string tooltip)
        {
            var button = new VisualElement { name = name, tooltip = tooltip, focusable = true };
            button.style.width = button.style.height = 42; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.Center;
            view.Decorate(button, background, 14, 4, shadow);
            // inset 0 1px 0 rgba(255,255,255,.4|.5): a one-pixel highlight along the top edge.
            button.style.borderTopWidth = 1; button.style.borderTopColor = new Color(1, 1, 1, .4f);
            return button;
        }

        /// <summary>Seat avatar (frameOf(): gold frame 1 with studs, plain a1/dp conic frame 0), level badge and turn ring.</summary>
        private VisualElement Avatar(JObject seat, int size, bool active, int turnSeconds, bool warn, string secName = null, bool level = true)
        {
            var host = new VisualElement { pickingMode = PickingMode.Ignore }; host.style.width = host.style.height = size; host.style.flexShrink = 0;
            float began = Current.TurnStartedAt >= 0 ? Current.TurnStartedAt : turnStarted;
            if (active)
            {
                var ring = new ArcRing(view.C(warn ? "#FF5E6C" : "var(--a1)"), new Color(1, 1, 1, .12f)); MenuView.Place(ring, -7, -7, size + 14, size + 14); host.Add(ring);
                // @keyframes ring {from {--p:100} to {--p:0}}: the gold part is the time left. At the g101-clock freeze the
                // animation (ring Ns linear forwards) has run out at the canonical instant: only the track remains.
                motions.Add(t => ring.Fraction = GameClock.Frozen ? 0 : 1 - (t - began) / Mathf.Max(1, turnSeconds));
            }
            bool gold = (int?)seat["frame"] == 1;
            var face = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(face, 0, 0, size, size);
            if (gold)
            {
                // box-shadow: 0 0 0 2px #5A3A08, 0 0 16px rgba(255,201,74,.55)
                var outline = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(outline, -2, -2, size + 4, size + 4); view.Decorate(outline, "#5A3A08", size / 2 + 2);
                GalaxyFilters.Shadow(outline, 0, 0, 8, new Color(1, .788f, .29f, .55f)); host.Add(outline);
            }
            view.Decorate(face, gold ? "conic-gradient(from 210deg,#8A5A12,#FFE9A8 12%,#E8A317 25%,#FFF6D0 38%,#B8790F 52%,#FFE08A 66%,#C88A1A 80%,#8A5A12)" : "conic-gradient(var(--a1) 0 245deg,var(--dp) 245deg)", size / 2);
            host.Add(face);
            var inner = new VisualElement { pickingMode = PickingMode.Ignore }; inner.style.position = Position.Absolute;
            inner.style.left = inner.style.right = inner.style.top = inner.style.bottom = 5; inner.style.alignItems = Align.Center; inner.style.justifyContent = Justify.Center;
            view.Decorate(inner, (string)seat["color"], size / 2); Border(inner, 3, view.C(gold ? "#3A2604" : "var(--pn)"));
            inner.Add(Text((string)seat["ini"], "Montserrat-700", size >= 74 ? 22 : 19, "#fff")); host.Add(inner);
            if (gold)
            {
                var stud = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(stud, size / 2f - 4.5f, -4, 9, 9); stud.style.rotate = new Rotate(45);
                view.Decorate(stud, "linear-gradient(135deg,#FFF6D0,#E8A317)", 2); Border(stud, 1.5f, view.C("#5A3A08"));
                GalaxyFilters.Shadow(stud, 0, 0, 3, new Color(1, .788f, .29f, .8f)); host.Add(stud);
            }
            var badge = Text(L("SV ", "LV ") + (int)seat["lv"], "Baloo2-800", 10, "var(--aT)"); Pad(badge, 1, 7, 1, 7); view.Decorate(badge, "var(--a1)", 7);
            var badgeRow = new VisualElement { pickingMode = PickingMode.Ignore }; badgeRow.style.position = Position.Absolute; badgeRow.style.left = 0; badgeRow.style.right = 0; badgeRow.style.bottom = -7; badgeRow.style.alignItems = Align.Center;
            badgeRow.Add(badge); if (level) host.Add(badgeRow);
            if (active && secName != null) host.Add(TurnSeconds(secName, size >= 74, began, turnSeconds));
            return host;
        }

        /// <summary>
        /// G101-Masa table.me.sec / table.p.sec[i]: the active seat's seconds left ("28 sn"), gold, red at or under the warning
        /// (set.warn || 5). Mine sits centred 17 px above the avatar (34×26, 14 px); an opponent's at top −10, right −18 (30×23, 12 px).
        /// </summary>
        private VisualElement TurnSeconds(string name, bool mine, float began, int turnSeconds)
        {
            var label = Text("", "Montserrat-800", mine ? 14 : 12, "var(--aT)"); label.name = name;
            label.style.position = Position.Absolute; label.style.height = mine ? 26 : 23; label.style.minWidth = mine ? 34 : 30;
            label.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(label, 0, mine ? 7 : 6, 0, mine ? 7 : 6);
            if (mine) { label.style.top = -17; label.style.left = new Length(50, LengthUnit.Percent); label.style.translate = new Translate(Length.Percent(-50), 0); }
            else { label.style.top = -10; label.style.right = -18; }
            GalaxyFilters.Shadow(label, 0, mine ? 4 : 3, mine ? 10 : 8, new Color(0, 0, 0, .45f));
            if (mine) { label.style.borderTopWidth = 1; label.style.borderTopColor = new Color(1, 1, 1, .35f); }
            int warnAt = Settings != null && Settings.WarnSeconds > 0 ? Settings.WarnSeconds : 5;
            int shown = -1; bool? low = null;
            void Update(float t)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(turnSeconds - (Time.realtimeSinceStartup - began) - .001f));
                if (left != shown) { shown = left; label.text = left + (Lang == "en" ? "s" : " sn"); }
                bool isLow = left <= warnAt;
                if (low == isLow) return;
                low = isLow; view.Decorate(label, isLow ? "linear-gradient(#FF7A86,#E8455A)" : "linear-gradient(var(--a1),var(--a2))", mine ? 13 : 12);
                label.style.color = view.C(isLow ? "#fff" : "var(--aT)");
            }
            Update(Time.realtimeSinceStartup); motions.Add(Update);
            return label;
        }

        // ---------- actions ----------
        private void BuildActions(State s) => BuildActionPanel(s);

        private VisualElement Clickable(VisualElement element, string command)
        {
            element.pickingMode = PickingMode.Position; GalaxyFilters.PressSink(element);
            element.RegisterCallback<ClickEvent>(_ => Press(command));
            return element;
        }

        private VisualElement SortButton(string name, string text)
        {
            var button = Text(text, "Montserrat-700", 12, "#F4F1E8"); button.name = name; button.focusable = true; button.pickingMode = PickingMode.Position;
            button.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(button, 6, 12, 6, 12);
            view.Decorate(button, "var(--pnH)", 11, 3, "var(--pnS)"); GalaxyFilters.HoverBrightness(button, 1.25f);
            return button;
        }

        // ---------- small helpers ----------
        private Label Text(string text, string font, int size, string color)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font);
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.flexShrink = 0; return label;
        }

        private static VisualElement Row(float gap)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore }; row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
            row.userData = gap; return row;
        }

        /// <summary>CSS flex `gap` for rows built with <see cref="Row"/>: margin-left on every in-flow child after the first.</summary>
        private static void ApplyGaps(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(e =>
            {
                if (!(e.userData is float gap)) return;
                bool first = true;
                foreach (var child in e.Children())
                {
                    if (child.style.position.value == Position.Absolute) continue;
                    if (!first) child.style.marginLeft = gap; first = false;
                }
            });
        }

        private static void Pad(VisualElement e, float top, float right, float bottom, float left)
        { e.style.paddingTop = top; e.style.paddingRight = right; e.style.paddingBottom = bottom; e.style.paddingLeft = left; }

        private static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = width;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = color;
        }

        private static void Kill(UnityEngine.Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        private void OnDestroy()
        {
            DestroyTwoDimensional();
            DestroySharedSettings();
            if (table != null && table.CameraRig != null) table.RenderInto(null);
            if (viewTarget != null) { viewTarget.Release(); Kill(viewTarget); }
            FramePacing.DisplayChanged -= OnDisplayChanged;
            view?.Dispose(); loc?.Dispose();
        }
    }
}
