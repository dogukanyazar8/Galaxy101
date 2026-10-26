using System;
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
    /// anchored layer (opponent avatars + plates, BURAYA AT, GÖSTERGE) → top shade → HUD (top bar, toast,
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
            public int[] TileCounts = { 21, 21, 21, 21 };
            /// <summary>Opened total (series) or pair count per seat; 0 = not opened.</summary>
            public int[] Opened = { 0, 0, 117, 0 };
            public bool[] OpenedPairs = new bool[4];
            public bool TurnHint = true, ShowIndicator = true, Warn, FirstTurn, CanOpen, CanPair;
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
            /// <summary>T33/T47–T49: every okey on the table with what it stands for, drag targets and refusals; the
            /// tooltip under one okey; the band text while dragging a tile that can take an okey.</summary>
            public readonly List<OkeyMark> Okeys = new List<OkeyMark>();
            public int OkeyTipId = -1;
            /// <summary>T34: the tile that fed an opening, glowing for 1.2 s.</summary>
            public int GlowTile = -1;
            public string OkeyTip, DragBanner;
            /// <summary>spec/21: the deal ceremony card and the "Zar at" button while dealing.</summary>
            public DealInfoCard Deal;
            /// <summary>spec/22: Geri topla, Elden bit, and Aç below 101 (orange: a wrong opening costs +101).</summary>
            public bool CanUndo, CanFromHand, ShortOpen, ShortPairs;
            /// <summary>Online: the table's name ("Gece Kuşları · Tekli") and stake for the top bar; null at a bot table.</summary>
            public string RoomName; public long RoomStake;
            /// <summary>v4 openVals: Geri bırak (R-32), İşle (R-25, something fits), Yere çift aç (R-24) and their states.</summary>
            public bool ShowReturn, ShowIsle, CanIsle, ShowLayPair, CanLayPair;
            /// <summary>protocol.json reconnect: "Bağlantı koptu · Deneme n/5" (0 = connected); GaveUp leaves only "Lobiye dön".</summary>
            public int NetAttempt; public bool NetGaveUp;
        }

        public sealed class OkeyMark { public int Id; public string Number, Color; public bool Target, Invalid; }

        /// <summary>Message kinds (spec/17 §5). Penalties, errors and warnings flash red, the rest green.</summary>
        public enum ToastKind { Penalty, Info, Success, Warning, Error }

        private sealed class Toast { public ToastKind Kind; public string Text; public float Until, ShownAt; public bool Sticky; }
        private sealed class BalloonInfo { public string Text; public bool Danger; public float Until, ShownAt, Duration = 2.5f; }
        private Toast flash;
        private readonly Dictionary<int, BalloonInfo> balloons = new Dictionary<int, BalloonInfo>();
        private readonly List<int> expired = new List<int>(4); // per-frame scratch: no list allocated while nothing expires
        private readonly int[] shownPenalty = new int[4];
        private readonly float[] penaltyChangedAt = { -10, -10, -10, -10 };
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
            if (dirty) Render(Current);
        }

        /// <summary>Finish banner (spec/10 §7–§8): seat −1 = no avatar (TAŞ BİTTİ).</summary>
        public sealed class MomentInfo { public string Title, Sub; public int Seat = -1; public float StartedAt; }

        /// <summary>Hand result card (T24). Bot tables are offline: XP only, no chips.</summary>
        public sealed class ResultInfo
        {
            public string Title, Sub, Head;
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
        private int viewMsaa = 4; private float viewScaleCap = 2.2f;
        private float turnStarted;

        public State Current { get; private set; } = new State();
        /// <summary>The menu profile (level, XP) the table writes to.</summary>
        public MenuModel Model => model;
        public VisualElement Canvas => canvas;
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

            var root = document.rootVisualElement; root.Clear();
            canvas = new VisualElement { name = "table-canvas", pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("galaxy-canvas"); canvas.AddToClassList("theme-" + (ThemeIds.TryGetValue(table.Planet, out var id) ? id : "saturn"));
            root.Add(canvas);

            var backdrop = Layer("table-background"); canvas.Add(backdrop);
            background = new GameObject("Galaxy background").AddComponent<GalaxyBackground>(); background.transform.SetParent(transform, false);
            background.Initialize(backdrop); background.Configure(table.Planet, (bool?)model.Settings["motion"] ?? true, (string)model.Settings["quality"] ?? "Yüksek");
            // Efekt kalitesi also sets the 3D view's pixel budget, the table's main GPU cost: resolution cap and MSAA.
            string quality = ((string)model.Settings["quality"] ?? "Yüksek").ToLowerInvariant();
            bool low = quality == "düşük" || quality == "low", medium = quality == "orta" || quality == "medium";
            viewMsaa = low ? 1 : medium ? 2 : 4; viewScaleCap = low ? 1 : medium ? 1.5f : 2.2f;
            // Düşük also drops the table's shadow pass and halves the tile trails.
            table.SetEffectQuality(low ? 0 : medium ? 1 : 2); TileView.TrailScale = low ? .5f : 1;
            background.UseTableLayout(TableTheme.Load(table.Planet).Pg);
            background.SetFocused(Application.isFocused || Application.isBatchMode);

            view3d = Layer("table-3d"); canvas.Add(view3d); ResizeViewTarget();
            var vignette = Layer("table-vignette"); canvas.Add(vignette);
            view.Decorate(vignette, "radial-gradient(ellipse 80% 70% at 50% 58%,transparent 60%,rgba(0,0,0,.42))");
            anchorLayer = Layer("table-anchors"); canvas.Add(anchorLayer);
            var shade = Layer("table-top-shade"); shade.style.bottom = StyleKeyword.Auto; shade.style.height = 120; canvas.Add(shade);
            view.Decorate(shade, "linear-gradient(rgba(3,5,14,.78) 0%,rgba(3,5,14,.5) 45%,transparent)");
            hudLayer = Layer("table-hud"); canvas.Add(hudLayer);
            toastLayer = Layer("table-toasts"); canvas.Add(toastLayer);
            InitializeWindows(persistencePath == null);
            AppVersionLabel.Attach(canvas);

            turnStarted = Time.realtimeSinceStartup;
            Render(Current);
            // Each panel update (every rendered frame), not a fixed 16 ms that beats against a 144 Hz display.
            canvas.schedule.Execute(() => { float now = Time.realtimeSinceStartup; foreach (var motion in motions) motion(now); foreach (var motion in toastMotions) motion(now); foreach (var motion in windowMotions) motion(now); }).Every(0);
            Debug.Log("GALAXY101_TABLE_HUD_READY theme=" + table.Planet + " language=" + loc.Language);
        }

        private static VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            return layer;
        }

        /// <summary>
        /// The 3D camera draws at the canvas' device-pixel size (same rule as the galaxy background), up to the effect
        /// quality's cap (Yüksek 2.2×, Orta 1.5×, Düşük 1×) and with its MSAA (4×, 2×, off).
        /// </summary>
        private void ResizeViewTarget()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / Width, Screen.height / Height), 1, viewScaleCap);
            int w = Mathf.RoundToInt(Width * scale), h = Mathf.RoundToInt(Height * scale);
            if (viewTarget != null && viewTarget.width == w && viewTarget.height == h) return;
            var previous = viewTarget;
            viewTarget = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Galaxy101 table view", antiAliasing = viewMsaa, hideFlags = HideFlags.DontSave };
            viewTarget.Create();
            table.RenderInto(viewTarget);
            view3d.style.backgroundImage = Background.FromRenderTexture(viewTarget);
            if (previous != null) { previous.Release(); Kill(previous); }
        }

        private void LateUpdate()
        {
            if (view == null) return;
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
            // An open gift drawer or player card shows the balance and what it affords: a wallet change redraws it.
            if (state.Chips != chipsBefore && (Window == "gift" || Window == "card")) RenderWindows(true);
            seenNow.Clear();
            motions.Clear(); anchors.Clear(); dynamicAnchors.Clear(); anchorLayer.Clear(); hudLayer.Clear();
            for (int k = 1; k <= 3; k++) BuildOpponent(k, state);
            bool myTurn = state.Active == 0;
            if (myTurn && state.Phase == "discard" && state.TurnHint) BuildDiscardLabel();
            if (state.ShowIndicator && state.Phase != "deal") BuildIndicatorLabel();
            BuildTopBar(state);
            BuildToast(state);
            BuildMe(state);
            BuildActions(state);
            if (state.Phase == "seat") BuildWaiting(state);
            if (state.Phase == "deal" && state.Deal != null) BuildDeal(state.Deal);
            if (state.Countdown > 0) BuildCountdown(state);
            if (state.Moment != null) BuildMoment(state.Moment);
            if (state.Result != null) BuildResult(state.Result);
            if (state.Match != null) BuildMatchEnd(state.Match);
            if (state.ExitOpen) BuildExit();
            if (state.NetAttempt > 0 || state.NetGaveUp) BuildNet(state);
            BuildPickedTile(state);
            BuildOkeys(state);
            BuildGlowTile(state);
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
            if (s.Opened[k] > 0 && s.OpenedPairs[k]) return L("Açtı · " + s.Opened[k] + " çift", "Opened · " + s.Opened[k] + " pairs");
            if (s.Opened[k] > 0) return L("Açtı · " + s.Opened[k], "Opened · " + s.Opened[k]);
            return me ? L("Açmadı · " + s.TileCounts[k] + " taş", "Not opened · " + s.TileCounts[k] + " tiles") : L(s.TileCounts[k] + " taş", s.TileCounts[k] + " tiles");
        }

        private void BuildOpponent(int k, State s)
        {
            var anchor = Anchor("a" + k);
            var column = Centered(anchor); column.style.alignItems = Align.Center;
            if (!s.Seated[k]) { BuildEmptySeat(column, k); return; }
            var avatar = Avatar(Seat(k), 62, k == s.Active && s.Phase == "opp", 4, false);
            avatar.name = "opponent-" + k; avatar.tooltip = (string)Seat(k)["name"]; Clickable(avatar, "card:" + k); column.Add(avatar);
            PenaltyBadge(avatar, k, s, false);
            if (balloons.TryGetValue(k, out var balloon)) column.Add(BalloonElement(k, balloon));
            var plate = new VisualElement { pickingMode = PickingMode.Ignore }; plate.style.marginTop = 6; plate.style.alignItems = Align.Center;
            Pad(plate, 4, 12, 5, 12); view.Decorate(plate, "rgba(5,8,22,.66)", 12); Border(plate, 1, new Color(1, 1, 1, .08f)); GalaxyFilters.BackdropBlur(plate, 6);
            var nameRow = Row(5); nameRow.Add(Text((string)Seat(k)["name"], "Montserrat-700", 14, "#F4F1E8"));
            var bot = Text(TableStrings.Get("botBadge", Lang), "Baloo2-800", 8, "var(--ic)"); MenuView.Tracking(bot, 1, 8); bot.name = "bot-badge-" + k;
            if (Seat(k)["bot"] != null && !(bool)Seat(k)["bot"]) bot.style.display = DisplayStyle.None;
            Pad(bot, 1, 5, 1, 5); view.Decorate(bot, "rgba(255,255,255,.14)", 5); nameRow.Add(bot);
            plate.Add(nameRow);
            var status = Text(Status(k, s, false), "Baloo2-800", 11, s.Opened[k] > 0 ? "#8EF0B0" : "var(--mt)"); status.style.marginTop = 1; plate.Add(status);
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
            var invite = Text(L("Davet et", "Invite"), "Baloo2-800", 12, "#F4F1E8"); invite.name = "invite-" + k; invite.style.marginTop = 6;
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
            var title = Text(L("MASA HAZIRLANIYOR", "SETTING UP THE TABLE"), "Baloo2-800", 11, "var(--a1)"); MenuView.Tracking(title, 2, 11); card.Add(title);
            var sub = Text(seated == 4 ? L("Masa doldu · oyun başlıyor", "Table full · starting") : L("Oyuncular bekleniyor · " + seated + "/4", "Waiting for players · " + seated + "/4"), "Montserrat-700", 22, "#F4F1E8");
            sub.style.marginTop = 10; card.Add(sub);
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
                holder.Add(Avatar(Seat(m.Seat), 76, false, 0, false));
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
            motions.Add(t => { float u = TableConstants.EaseOutCubic(Mathf.Clamp01((t - began) / .35f)); card.style.translate = new Translate(0, 18 * (1 - u)); card.style.scale = new Scale(Vector2.one * Mathf.Lerp(.95f, 1, u)); });
            return card;
        }

        /// <summary>
        /// Hand result v2 (T42–T44, replaces T24): CEZA column with reasons, multiplier badges and the explained hand points;
        /// an unscored pairs finish; XP only on bot tables; "Sonraki el · 15" (the last hand leads to the match end).
        /// </summary>
        private void BuildResult(ResultInfo r)
        {
            var layer = Backdrop("table-result", null);
            var card = Card(900, 30, "result"); card.name = "table-result-card"; card.style.overflow = Overflow.Hidden;
            var header = Row(18); Pad(header, 22, 30, 14, 30);
            var left = new VisualElement { pickingMode = PickingMode.Ignore }; left.style.flexGrow = 1;
            var caption = Text(r.Title + " · " + r.Sub, "Baloo2-800", 11, "var(--mt)"); MenuView.Tracking(caption, 2, 11); left.Add(caption);
            var headRow = Row(12); headRow.style.marginTop = 4;
            var head = Text(r.Head, "Montserrat-800", 36, "var(--a1)"); head.name = "table-result-head";
            head.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = view.C("var(--lS)") }; headRow.Add(head);
            if (r.Multiplier > 1) { var mul = Text("×" + r.Multiplier, "Montserrat-800", 15, "var(--aT)"); mul.name = "result-multiplier"; Pad(mul, 3, 10, 3, 10); view.Decorate(mul, "linear-gradient(var(--a1),var(--a2))", 10, 3, "var(--aS)"); headRow.Add(mul); }
            left.Add(headRow); header.Add(left);
            var right = new VisualElement { pickingMode = PickingMode.Ignore }; right.style.alignItems = Align.FlexEnd;
            var xpCap = Text("XP", "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(xpCap, 1.4f, 10); right.Add(xpCap);
            var xp = Text("+" + view.N(r.Xp) + " XP", "Montserrat-800", 28, "#6BE8B0"); xp.name = "table-result-xp"; xp.style.marginTop = 2; right.Add(xp);
            header.Add(right); card.Add(header);

            var body = new VisualElement { pickingMode = PickingMode.Ignore }; Pad(body, 0, 30, 0, 30);
            var cols = Row(10); Pad(cols, 0, 14, 0, 14);
            cols.Add(Cell(Text("", "Baloo2-800", 10, "var(--mt)"), 36, false));
            cols.Add(Cell(Text("", "Baloo2-800", 10, "var(--mt)"), 40, false));
            var player = Caption(L("OYUNCU", "PLAYER")); var pc = Cell(player, 0, false); pc.style.flexGrow = 1; cols.Add(pc);
            var penaltyCap = Caption(TableStrings.Get("heCPen", Lang)); var penCell = Cell(penaltyCap, 80, false); penCell.style.justifyContent = Justify.Center; cols.Add(penCell);
            cols.Add(Cell(Caption(L("EL PUANI", "HAND")), 180, true));
            cols.Add(Cell(Caption(L("TOPLAM", "TOTAL")), 80, true));
            body.Add(cols);
            int index = 0;
            foreach (var row in r.Rows)
            {
                var line = Row(10); line.name = "result-row-" + row.Seat; line.style.marginTop = 6; Pad(line, 9, 14, 9, 14); line.style.minHeight = 58;
                view.Decorate(line, row.Seat == 0 ? "var(--pnH)" : "var(--dp)", 16);
                if (row.Seat == r.Winner) Border(line, 2, view.C("var(--a1)"));
                line.Add(Cell(Text(index + 1 + ".", "Montserrat-700", 20, index == 0 ? "var(--a1)" : "var(--mt)"), 36, false));
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 34; dot.style.alignItems = Align.Center; dot.style.justifyContent = Justify.Center;
                view.Decorate(dot, (string)Seat(row.Seat)["color"], 17); dot.Add(Text((string)Seat(row.Seat)["ini"], "Montserrat-700", 12, "#fff")); line.Add(Cell(dot, 40, false));
                var who = new VisualElement { pickingMode = PickingMode.Ignore }; who.style.flexGrow = 1;
                var nameRow = Row(8);
                nameRow.Add(Text((string)Seat(row.Seat)["name"] + (row.Seat == 0 ? L(" (sen)", " (you)") : ""), "Montserrat-700", 16, "#F4F1E8"));
                nameRow.Add(Text(row.Note, "Baloo2-800", 11, "var(--mt)"));
                who.Add(nameRow);
                if (row.Badges.Count > 0)
                {
                    var badges = Row(6); badges.style.marginTop = 4;
                    foreach (var (text, gold) in row.Badges)
                    {
                        var badge = Text(text, "Baloo2-800", 10, gold ? "var(--aT)" : "var(--ic)"); Pad(badge, 2, 8, 2, 8);
                        view.Decorate(badge, gold ? "linear-gradient(var(--a1),var(--a2))" : "rgba(255,255,255,.12)", 8); badges.Add(badge);
                    }
                    who.Add(badges);
                }
                line.Add(who);
                var penalty = new VisualElement { pickingMode = PickingMode.Ignore }; penalty.style.width = 80; penalty.style.alignItems = Align.Center; penalty.style.flexShrink = 0;
                if (row.Penalty > 0)
                {
                    var pill = AmountPill("+" + view.N(row.Penalty), 12); pill.name = "result-penalty-" + row.Seat; penalty.Add(pill);
                    if (!string.IsNullOrEmpty(row.PenaltyWhy)) { var why = Text(row.PenaltyWhy, "Baloo2-800", 10, "#FF8A95"); why.style.marginTop = 3; penalty.Add(why); }
                }
                else penalty.Add(Text("—", "Montserrat-700", 14, "var(--mt)"));
                line.Add(penalty);
                var hand = new VisualElement { pickingMode = PickingMode.Ignore }; hand.style.width = 180; hand.style.alignItems = Align.FlexEnd; hand.style.flexShrink = 0;
                if (r.Unscored || !row.Hand.HasValue) hand.Add(Text("—", "Montserrat-700", 18, "var(--mt)"));
                else
                {
                    int v = row.Hand.Value;
                    hand.Add(Text(v > 0 ? "+" + view.N(v) : v < 0 ? "−" + view.N(-v) : "0", "Montserrat-800", 18, v < 0 ? "#6BE8B0" : v > 0 ? "#FF8A95" : "#F4F1E8"));
                    if (!string.IsNullOrEmpty(row.Explain)) { var ex = Text(row.Explain, "Baloo2-700", 10, "var(--mt)"); ex.name = "result-explain-" + row.Seat; hand.Add(ex); }
                }
                line.Add(hand);
                line.Add(Cell(Text(row.Total < 0 ? "−" + view.N(-row.Total) : view.N(row.Total), "Montserrat-800", 18, "#F4F1E8"), 80, true));
                float began = r.StartedAt + index * .09f;
                motions.Add(t => { float u = TableConstants.EaseOutCubic(Mathf.Clamp01((t - began) / .35f)); line.style.translate = new Translate(-44 * (1 - u), 0); line.style.opacity = u; });
                body.Add(line); index++;
            }
            if (r.Unscored)
            {
                // T44: dashed a1 box.
                var box = new VisualElement { name = "result-unscored", pickingMode = PickingMode.Ignore }; box.style.marginTop = 10; box.style.alignItems = Align.Center; Pad(box, 10, 14, 10, 14);
                Border(box, 2, view.C("var(--a1)")); box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 14;
                box.Add(Text(TableStrings.Get("unscored", Lang), "Montserrat-800", 15, "var(--a1)"));
                var sub = Text(TableStrings.Get("unscoredSub", Lang), "Baloo2-800", 11, "var(--mt)"); sub.style.marginTop = 2; box.Add(sub);
                body.Add(box);
            }
            card.Add(body);

            var strip = Row(12); strip.style.marginTop = 14; strip.style.marginLeft = 30; strip.style.marginRight = 30; Pad(strip, 10, 14, 10, 14); view.Decorate(strip, "var(--dp)", 16);
            var gifts = Caption(L("ALDIĞIN İKRAMLAR", "TREATS RECEIVED")); strip.Add(gifts);
            if (received.Count == 0) { var none = Text(L("Bu elde ikram gelmedi", "No treats this hand"), "Baloo2-700", 13, "var(--mt)"); none.style.flexGrow = 1; none.style.flexShrink = 1; strip.Add(none); }
            else
            {
                // spec/13 §3: the treats I received this hand, 38 px pictures.
                var got = Row(6); got.name = "result-gifts"; got.style.flexGrow = 1; got.style.flexShrink = 1; got.style.overflow = Overflow.Hidden;
                foreach (var kind in received) { var img = new VisualElement { pickingMode = PickingMode.Position, tooltip = GiftName(kind, false) }; img.style.width = img.style.height = 38; img.style.flexShrink = 0; if (GiftThumb(kind) is Texture2D thumb) img.style.backgroundImage = thumb; got.Add(img); }
                strip.Add(got);
            }
            strip.Add(Text(L("Toplam ", "Total ") + "+" + view.N(r.TotalXp) + " XP", "Baloo2-800", 13, "#6BE8B0"));
            card.Add(strip);

            var buttons = Row(12); buttons.style.justifyContent = Justify.FlexEnd; Pad(buttons, 18, 30, 24, 30);
            var leave = Text(L("Masadan kalk", "Leave table"), "Montserrat-700", 17, "#F4F1E8"); leave.name = "result-leave"; Pad(leave, 15, 24, 15, 24);
            view.Decorate(leave, "var(--pnH)", 18, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(leave, 1.2f); buttons.Add(Clickable(leave, "exit"));
            var next = new VisualElement { name = "result-next", pickingMode = PickingMode.Position }; next.style.overflow = Overflow.Hidden; Pad(next, 15, 34, 15, 34);
            view.Decorate(next, "linear-gradient(var(--a1),var(--a2))", 18, 5, "var(--aS)");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore }; bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.top = 0; bar.style.bottom = 0; bar.style.backgroundColor = new Color(1, 1, 1, .32f);
            next.Add(bar);
            bool last = r.LastHand;
            var label = Text((last ? L("Maç sonucu · ", "Match result · ") : L("Sonraki el · ", "Next hand · ")) + r.Seconds, "Montserrat-800", 18, "var(--aT)"); label.name = "result-next-label"; next.Add(label);
            float start = r.StartedAt; int total = r.Seconds;
            // barFill 15s + the countdown in the label ("Sonraki el · 11"); the last hand leads to the match end.
            motions.Add(t =>
            {
                float elapsed = Mathf.Max(0, t - start);
                bar.style.width = Length.Percent(Mathf.Clamp01(elapsed / total) * 100);
                label.text = (last ? L("Maç sonucu · ", "Match result · ") : L("Sonraki el · ", "Next hand · ")) + Mathf.Max(0, total - Mathf.FloorToInt(elapsed));
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

        /// <summary>Leave card (T25). The bot table is offline and XP-only, so the stake sentence becomes the XP one.</summary>
        private void BuildExit()
        {
            var layer = Backdrop("table-exit-card", "exit-stay");
            var card = Card(460, 28, "exit"); card.name = "table-exit-confirm"; card.style.alignItems = Align.Center; Pad(card, 28, 28, 28, 28);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 64; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, "rgba(255,94,108,.16)", 20); icon.Add(view.Icon("table-exit", 32, "#FF8A94")); card.Add(icon);
            var title = Text(L("Masadan kalkmak istiyor musun?", "Leave the table?"), "Montserrat-700", 24, "#F4F1E8"); title.style.marginTop = 12;
            title.style.whiteSpace = WhiteSpace.Normal; title.style.unityTextAlign = TextAnchor.UpperCenter; title.style.maxWidth = 404; title.style.flexShrink = 1; card.Add(title);
            var body = Text(TableStrings.Get("exitBodyBot", Lang), "Baloo2-700", 15, "var(--mt)");
            body.style.whiteSpace = WhiteSpace.Normal; body.style.maxWidth = 360; body.style.unityTextAlign = TextAnchor.UpperCenter; body.style.marginTop = 12; card.Add(body);
            var buttons = Row(10); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 20;
            var stay = Text(L("Masada kal", "Stay"), "Montserrat-700", 17, "var(--aT)"); stay.name = "exit-stay"; stay.style.flexGrow = 1; stay.style.flexBasis = 0; stay.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(stay, 14, 14, 14, 14); view.Decorate(stay, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); buttons.Add(Clickable(stay, "exit-stay"));
            var leave = Text(L("Kalk", "Leave"), "Montserrat-700", 17, "#fff"); leave.name = "exit-leave"; leave.style.flexGrow = 1; leave.style.flexBasis = 0; leave.style.unityTextAlign = TextAnchor.MiddleCenter;
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
                var room = hudLayer?.Q<VisualElement>("table-room"); if (room == null || row.childCount == 0) return;
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
            var label = Text(L("BURAYA AT", "DISCARD HERE"), "Baloo2-800", 10, "var(--aT)"); MenuView.Tracking(label, 1.4f, 10);
            Pad(label, 4, 9, 4, 9); view.Decorate(label, "var(--a1)", 8, 3, "var(--aS)");
            var holder = Anchor("disc"); var box = Centered(holder, -62, false); box.Add(label);
            // floatA 1.4s ease-in-out: 0 → −8px → 0 (support.js keyframes).
            motions.Add(t => { float f = Mathf.Repeat(t, 1.4f) / 1.4f; box.style.translate = new Translate(Length.Percent(-50), -62 - 8 * Mathf.Sin(f * Mathf.PI)); });
        }

        private void BuildIndicatorLabel()
        {
            var label = Text(L("GÖSTERGE", "INDICATOR"), "Baloo2-800", 9, "var(--ic)"); MenuView.Tracking(label, 1.4f, 9);
            Pad(label, 3, 8, 3, 8); view.Decorate(label, "rgba(5,8,22,.6)", 7);
            Centered(Anchor("ind"), 4, false).Add(label);
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

            bar.Add(Clickable(IconButton("table-exit", 22, L("Masadan çık", "Leave table"), "table-exit"), "exit"));
            var logo = Text("GALAXY 101", "Montserrat-800", 19, "var(--a1)"); MenuView.Tracking(logo, 3, 19);
            // CSS paints the logo with linear-gradient(lg1, a1 50%, a2) clipped to the glyphs; UI Toolkit text takes
            // one colour, so the mid stop carries it. drop-shadow(0 2px 0 var(--lS)) is a hard text shadow.
            logo.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 0, color = view.C("var(--lS)") };
            bar.Add(logo);

            var room = Row(8); room.name = "table-room"; Pad(room, 7, 13, 7, 13); view.Decorate(room, "var(--dp)", 15);
            if (s.RoomName != null)
            {
                // A server table (G13): its name and type, then the stake.
                room.Add(Text(s.RoomName, "Baloo2-800", 12, "var(--ic)"));
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 4; view.Decorate(dot, "var(--mt)", 2); room.Add(dot);
                room.Add(Text(L("Bahis ", "Bet ") + view.N(s.RoomStake), "Baloo2-800", 12, "var(--ic)"));
            }
            else
            {
                room.Add(Text(TableStrings.Get("botRoom", Lang), "Baloo2-800", 12, "var(--ic)"));
                var practice = Text(TableStrings.Get("botTag", Lang), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(practice, 1.2f, 9); practice.name = "bot-tag";
                Pad(practice, 2, 7, 2, 7); view.Decorate(practice, "var(--a1)", 6); room.Add(practice);
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 4; view.Decorate(dot, "var(--mt)", 2); room.Add(dot);
                room.Add(Text(TableStrings.Get("botStake", Lang), "Baloo2-800", 12, "var(--ic)"));
            }
            bar.Add(room);
            BuildHandCounter(s);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.flexGrow = 1; bar.Add(spacer);

            var deck = Row(8); Pad(deck, 5, 13, 5, 6); view.Decorate(deck, "var(--dp)", 18);
            var mini = new VisualElement { pickingMode = PickingMode.Ignore }; mini.style.width = 20; mini.style.height = 27; mini.style.overflow = Overflow.Hidden;
            view.Decorate(mini, "linear-gradient(var(--tf1),var(--tf2))", 5);
            var edge = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(edge, 0, 24, 20, 3); edge.style.backgroundColor = view.C("var(--te)"); mini.Add(edge);
            deck.Add(mini);
            deck.Add(Text(L("Taş", "Deck"), "Baloo2-800", 12, "var(--mt)"));
            var count = Text(s.DeckCount.ToString(), "Montserrat-700", 16, "#F4F1E8"); count.name = "deck-count"; count.style.minWidth = 22; deck.Add(count);
            bar.Add(deck);

            // XP pill instead of chips (T28): level disc, 100×7 bar, "6.240 / 9.000 XP".
            var xp = Row(8); xp.name = "xp-pill"; Pad(xp, 5, 14, 5, 5); view.Decorate(xp, "var(--dp)", 18);
            var levelDisc = new VisualElement { pickingMode = PickingMode.Ignore }; levelDisc.style.width = levelDisc.style.height = 30; levelDisc.style.alignItems = Align.Center; levelDisc.style.justifyContent = Justify.Center;
            view.Decorate(levelDisc, "linear-gradient(var(--a1),var(--a2))", 15); levelDisc.Add(Text(s.Level.ToString(), "Montserrat-800", 12, "var(--aT)")); xp.Add(levelDisc);
            var xpColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.width = 100; track.style.height = 7; track.style.overflow = Overflow.Hidden; view.Decorate(track, "rgba(255,255,255,.14)", 4);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.style.position = Position.Absolute; fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
            fill.style.width = Length.Percent(Mathf.Clamp01(s.XpNeed > 0 ? (float)s.XpInLevel / s.XpNeed : 0) * 100); view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 4);
            track.Add(fill); xpColumn.Add(track);
            var xpText = Text(TableStrings.Get("xpPill", Lang, view.N(s.XpInLevel), view.N(s.XpNeed)), "Baloo2-800", 10, "var(--mt)"); xpText.name = "xp-text"; xpText.style.marginTop = 2; xpColumn.Add(xpText);
            xp.Add(xpColumn);
            bar.Add(xp);

            var settings = Clickable(IconButton("settings", 24, L("Ayarlar", "Settings"), "table-settings"), "settings"); bar.Add(settings);
        }

        /// <summary>Hand counter under the room pill (T28): played hands as gold dots, then "2 / 5. el".</summary>
        private void BuildHandCounter(State s)
        {
            var counter = Row(6); counter.name = "hand-counter"; counter.style.position = Position.Absolute; counter.style.left = 232; counter.style.top = 62;
            Pad(counter, 3, 10, 3, 8); view.Decorate(counter, "var(--dp)", 12);
            var dots = Row(3);
            for (int i = 1; i <= s.MatchHands; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 6;
                view.Decorate(dot, i <= s.Hand ? "var(--a1)" : "rgba(255,255,255,.22)", 3); dots.Add(dot);
            }
            counter.Add(dots);
            counter.Add(Text(TableStrings.Get("handCounter", Lang, Mathf.Clamp(s.Hand, 1, s.MatchHands), s.MatchHands), "Baloo2-800", 10, "var(--ic)"));
            hudLayer.Add(counter);
        }

        private VisualElement IconButton(string icon, int size, string tooltip, string name)
        {
            var button = new VisualElement { name = name, tooltip = tooltip, focusable = true };
            button.style.width = button.style.height = 44; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.Center; button.style.flexShrink = 0;
            view.Decorate(button, "var(--pnH)", 14, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(button, 1.25f);
            button.Add(view.Icon(icon, size)); return button;
        }

        // ---------- toast ----------
        private void BuildToast(State s)
        {
            string text = null, bg = null, fg = null;
            if (s.DragBanner != null) { text = s.DragBanner; bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "draw" && s.Active == 0 && s.DeckOnly) { text = TableStrings.Get("drawDeckOnly", Lang); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "discard" && s.Active == 0 && s.ShowPutBack) { text = TableStrings.Get("putBackHint", Lang); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            else if (s.Phase == "deal") { text = L("Taşlar dağıtılıyor…", "Dealing tiles…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            else if (s.Phase == "draw" && s.Active == 0) { string n = FirstName(3); text = L("Sıra sende · desteden ya da " + n + "'ın taşından çek", "Your turn · draw from the deck or " + n + "'s tile"); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "discard" && s.Active == 0)
            {
                text = s.FirstTurn ? L("İlk hamle senin · ıstakadan bir taş at", "You start · discard a tile from your rack") : L("Atacağın taşı halkaya sürükle ya da çift tıkla", "Drag a tile onto the ring or double-click it");
                bg = "#3CCB6E"; fg = "#062B14";
            }
            else if (s.Active > 0) { string n = FirstName(s.Active); text = L(n + " oynuyor…", n + " is playing…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            if (!s.TurnHint && s.Phase != "deal" && s.Phase != "fin") text = null;
            // G101-Masa: the time warning overrides it on my turn; a table moment and the deal ceremony hide it.
            if (s.Warn && s.Active == 0 && s.Phase != "deal") { text = L("Süren bitiyor!", "Time is running out!"); bg = "linear-gradient(#FF7A86,#E8455A)"; fg = "#fff"; }
            if (s.Moment != null || s.Deal != null) text = null;
            if (text == null) return;

            var row = MessageRow(16); row.name = "table-toast-row";
            var toast = Row(10); Pad(toast, 9, 18, 9, 10); view.Decorate(toast, bg, 20); GalaxyFilters.Shadow(toast, 0, 8, 12, new Color(0, 0, 0, .35f));
            var bullet = new VisualElement { pickingMode = PickingMode.Ignore }; bullet.style.width = bullet.style.height = 22; bullet.style.alignItems = Align.Center; bullet.style.justifyContent = Justify.Center;
            view.Decorate(bullet, "rgba(0,0,0,.18)", 11);
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore }; diamond.style.width = diamond.style.height = 8; diamond.style.rotate = new Rotate(45); view.Decorate(diamond, fg, 2);
            bullet.Add(diamond); toast.Add(bullet);
            var label = Text(text, "Baloo2-800", 14, fg); label.name = "table-toast"; toast.Add(label);
            // toastIn .3s (ease) when the band appears; the design keeps the element while its text changes, so a new
            // message does not replay it.
            float began = Since("turn-toast");
            motions.Add(now => { float k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((now - began) / .3f)); toast.style.opacity = k; toast.style.translate = new Translate(0, -14 * (1 - k)); });
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
            var row = new VisualElement { name = "table-message-row", pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.top = 132; row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center;
            var toast = Row(10); toast.name = "table-message"; Pad(toast, 10, 18, 10, 12);
            view.Decorate(toast, bg, 999); GalaxyFilters.Shadow(toast, 0, 8, 12, new Color(0, 0, 0, .35f));
            toast.Add(view.Icon("tick", 18, fg));
            toast.Add(Text(flash.Text, "Baloo2-800", 14, fg));
            float began = flash.ShownAt;
            // toastIn .3s ease.
            toastMotions.Add(now => { float k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((now - began) / .3f)); toast.style.opacity = k; toast.style.translate = new Translate(0, -14 * (1 - k)); });
            row.Add(toast); toastLayer.Add(row);
        }

        /// <summary>Penalty pill (tehlike degradesi, Montserrat 800, hard #9E1F33 shadow).</summary>
        private VisualElement AmountPill(string text, int size)
        {
            var pill = Text(text, "Montserrat-800", size, "#fff"); Pad(pill, 2, 9, 2, 9);
            view.Decorate(pill, "linear-gradient(#FF7A86,#E8455A)", 12, 3, "#9E1F33"); return pill;
        }

        /// <summary>This hand's penalty total on the avatar (me: top centre, opponents: top right), popping when it changes.</summary>
        private void PenaltyBadge(VisualElement avatar, int seat, State s, bool me)
        {
            int amount = s.Penalties != null && seat < s.Penalties.Length ? s.Penalties[seat] : 0;
            if (amount != shownPenalty[seat]) { shownPenalty[seat] = amount; penaltyChangedAt[seat] = Time.realtimeSinceStartup; }
            if (amount <= 0) return;
            var pill = AmountPill("+" + view.N(amount), 12); pill.name = "penalty-badge-" + seat; pill.style.position = Position.Absolute;
            if (me) { pill.style.top = -14; pill.style.left = 0; pill.style.right = 0; pill.style.alignSelf = Align.Center; pill.style.unityTextAlign = TextAnchor.MiddleCenter; pill.style.marginLeft = pill.style.marginRight = 14; }
            else { pill.style.top = -8; pill.style.right = -22; }
            float began = penaltyChangedAt[seat];
            motions.Add(t => { float u = Mathf.Clamp01((t - began) / .5f); float k = u < .6f ? Mathf.Lerp(0, 1.1f, TableConstants.EaseOutCubic(u / .6f)) : Mathf.Lerp(1.1f, 1, (u - .6f) / .4f); pill.style.scale = new Scale(Vector2.one * k); });
            avatar.Add(pill);
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

        /// <summary>Tile taken from the pile (T38–T39): YERDEN over it, and the emptied ring with GERİ BIRAK.</summary>
        private void BuildPickedTile(State s)
        {
            if (s.PickedTile < 0 || TilePosition == null) return;
            int id = s.PickedTile;
            var tag = Anchor("picked", () => TilePosition(id));
            var frame = new VisualElement { name = "picked-frame", pickingMode = PickingMode.Ignore }; MenuView.Place(frame, -26, -36, 52, 72);
            Border(frame, 3, view.C("var(--a1)")); frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = 8;
            tag.Add(frame);
            var label = Text(TableStrings.Get("pileTag", Lang), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(label, 1.2f, 9); label.name = "picked-tag";
            Pad(label, 2, 7, 2, 7); view.Decorate(label, "var(--a1)", 6);
            Centered(tag, -56, false).Add(label);
            if (!s.ShowPutBack) return;
            var ring = Anchor("putback");
            var box = Centered(ring); box.style.alignItems = Align.Center;
            var put = Text(TableStrings.Get("putBack", Lang), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(put, 1.2f, 9); put.name = "putback-label";
            Pad(put, 3, 8, 3, 8); view.Decorate(put, "var(--a1)", 7, 3, "var(--aS)"); box.Add(put);
            var circle = new VisualElement { name = "putback-ring", pickingMode = PickingMode.Position }; circle.style.width = circle.style.height = 74; circle.style.marginTop = 6;
            var dashed = new CssDashedCircle(74, 2, view.C("var(--a1)")); MenuView.Place(dashed, 0, 0, 74, 74); circle.Add(dashed);
            box.Add(Clickable(circle, "putback"));
        }

        /// <summary>
        /// Okeys on the table (T33): a white pill with a gold edge at the tile's top-right showing the number it stands for,
        /// in that colour; while dragging the matching real tile the okey glows (T47); a refused drop frames it red (T49).
        /// </summary>
        private void BuildOkeys(State s)
        {
            if (TilePosition == null) return;
            foreach (var okey in s.Okeys)
            {
                int id = okey.Id;
                var anchor = Anchor("okey-" + id, () => TilePosition(id));
                if (okey.Target || okey.Invalid)
                {
                    var frame = new VisualElement { name = (okey.Invalid ? "okey-invalid-" : "okey-target-") + id, pickingMode = PickingMode.Ignore };
                    MenuView.Place(frame, -15, -19, 30, 38);
                    frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = 6;
                    Border(frame, 3, view.C(okey.Invalid ? "#FF5E6C" : "var(--a1)"));
                    if (okey.Target)
                        // 0 0 0 3px --a1, 0 0 22px --gl, pulsing over 1.1 s.
                        motions.Add(t => GalaxyFilters.Shadow(frame, 0, 0, 6 + 5 * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2 / 1.1f)), view.C("var(--gl)")));
                    anchor.Add(frame);
                }
                var pill = Text(okey.Number, "Montserrat-800", 11, okey.Color); pill.name = "okey-badge-" + id;
                pill.style.position = Position.Absolute; pill.style.left = 6; pill.style.top = -26; pill.style.minWidth = 17; pill.style.height = 17;
                pill.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(pill, 0, 3, 0, 3); view.Decorate(pill, "#FFFFFF", 9); Border(pill, 2, view.C("#E8B730"));
                anchor.Add(pill);
                if (s.OkeyTipId == id && s.OkeyTip != null)
                {
                    var tip = Text(s.OkeyTip, "Baloo2-800", 12, "#F4F1E8"); tip.name = "okey-tip";
                    Pad(tip, 6, 11, 6, 11); view.Decorate(tip, "rgba(5,8,22,.88)", 10); GalaxyFilters.Shadow(tip, 0, 6, 9, new Color(0, 0, 0, .35f));
                    Centered(anchor, -64, false).Add(tip);
                }
            }
        }

        private void BuildGlowTile(State s)
        {
            if (s.GlowTile < 0 || TilePosition == null) return;
            int id = s.GlowTile;
            var frame = new VisualElement { name = "glow-tile-" + id, pickingMode = PickingMode.Ignore }; MenuView.Place(frame, -15, -19, 30, 38);
            frame.style.borderTopLeftRadius = frame.style.borderTopRightRadius = frame.style.borderBottomLeftRadius = frame.style.borderBottomRightRadius = 6;
            Border(frame, 3, view.C("var(--a1)"));
            float began = Since("glow-" + id);
            motions.Add(t => { float u = Mathf.Clamp01((t - began) / 1.2f); GalaxyFilters.Shadow(frame, 0, 0, 9 * Mathf.Sin(Mathf.PI * u) + 2, view.C("var(--gl)")); frame.style.opacity = u > .8f ? (1 - u) / .2f : 1; });
            Anchor("glow-" + id, () => TilePosition(id)).Add(frame);
        }

        // ---------- my panel ----------
        private void BuildMe(State s)
        {
            var panel = Row(12); panel.name = "table-me"; panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.bottom = 16;
            bool active = s.Active == 0 && (s.Phase == "draw" || s.Phase == "discard");
            var me = Avatar(Seat(0), 74, active, s.TurnSeconds, s.Warn); panel.Add(me);
            if (balloons.TryGetValue(0, out var mine)) me.Add(BalloonElement(0, mine));
            PenaltyBadge(me, 0, s, true);
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.Add(Text((string)Seat(0)["name"], "Montserrat-700", 16, "#F4F1E8"));
            var status = Text(Status(0, s, true), "Baloo2-800", 12, s.Opened[0] > 0 ? "#8EF0B0" : "var(--mt)"); status.style.marginTop = 1; column.Add(status);
            var buttons = Row(8); buttons.style.marginTop = 6; buttons.pickingMode = PickingMode.Position;
            var chat = Square("table-chat-open", "linear-gradient(160deg,#7AA6FF,#3D6BF0)", "#2448B8", L("Sohbet", "Chat"));
            chat.Add(view.Icon("table-chat", 22, "#fff"));
            if (Unread > 0)
            {
                var badge = Text(Unread.ToString(), "Baloo2-800", 10, "#fff"); badge.style.position = Position.Absolute; badge.style.top = -6; badge.style.right = -6;
                badge.style.minWidth = 18; badge.style.height = 18; badge.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(badge, 0, 4, 0, 4); view.Decorate(badge, "#FF5E6C", 9);
                chat.Add(badge);
            }
            buttons.Add(Clickable(chat, "chat"));
            var gift = Square("table-gift-open", "linear-gradient(160deg,var(--a1),var(--a2))", "var(--aS)", L("Hediye gönder", "Send a gift"));
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
        private VisualElement Avatar(JObject seat, int size, bool active, int turnSeconds, bool warn)
        {
            var host = new VisualElement { pickingMode = PickingMode.Ignore }; host.style.width = host.style.height = size; host.style.flexShrink = 0;
            if (active)
            {
                var ring = new ArcRing(view.C(warn ? "#FF5E6C" : "var(--a1)"), new Color(1, 1, 1, .12f)); MenuView.Place(ring, -7, -7, size + 14, size + 14); host.Add(ring);
                float began = Current.TurnStartedAt >= 0 && size >= 74 ? Current.TurnStartedAt : turnStarted;
                motions.Add(t => ring.Fraction = (t - began) / Mathf.Max(1, turnSeconds));
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
            badgeRow.Add(badge); host.Add(badgeRow);
            return host;
        }

        // ---------- actions ----------
        private void BuildActions(State s)
        {
            var actions = Row(12); actions.name = "table-actions"; actions.style.alignItems = Align.FlexEnd;
            actions.style.position = Position.Absolute; actions.style.right = 18; actions.style.bottom = 16;
            var sorts = new VisualElement { pickingMode = PickingMode.Ignore };
            sorts.Add(Clickable(SortButton("table-sort-run", L("Seri diz", "Sort runs")), "sort-run")); var pair = Clickable(SortButton("table-sort-pair", L("Çift diz", "Sort pairs")), "sort-pair"); pair.style.marginTop = 7; sorts.Add(pair);
            actions.Add(sorts);

            var meter = Row(10); meter.name = "table-open-meter"; Pad(meter, 8, 8, 8, 10);
            view.Decorate(meter, "rgba(5,8,22,.62)", 22); Border(meter, 1, new Color(1, 1, 1, .08f)); GalaxyFilters.BackdropBlur(meter, 6);
            var gauge = new VisualElement { pickingMode = PickingMode.Ignore }; gauge.style.width = gauge.style.height = 62;
            bool opened = s.Opened[0] > 0;
            var arc = new ArcRing(view.C(s.OpenSum >= s.OpenBar || opened ? "#3CCB6E" : "#FF5E6C"), new Color(1, 1, 1, .1f)) { Fraction = Mathf.Min(100, Mathf.Round(s.OpenSum / (float)s.OpenBar * 100)) / 100f };
            MenuView.Place(arc, 0, 0, 62, 62); gauge.Add(arc);
            var dial = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(dial, 6, 6, 50, 50); dial.style.alignItems = Align.Center; dial.style.justifyContent = Justify.Center;
            view.Decorate(dial, "var(--dp)", 25);
            var sum = Text(s.OpenSum.ToString(), "Montserrat-700", 19, "#F4F1E8"); sum.name = "table.openSum"; dial.Add(sum);
            var gaugeSub = Text(opened ? L("AÇIK", "OPEN") : "/ " + s.OpenBar, "Baloo2-800", 9, "var(--mt)"); gaugeSub.name = "table.gaugeSub"; dial.Add(gaugeSub);
            gauge.Add(dial); meter.Add(gauge);

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            if (opened)
            {
                var info = Row(6);
                var led = new VisualElement { pickingMode = PickingMode.Ignore }; led.style.width = led.style.height = 7; view.Decorate(led, "#3CCB6E", 4); GalaxyFilters.Shadow(led, 0, 0, 4, view.C("#3CCB6E")); info.Add(led);
                var line = Text(s.OpenedPairs[0] ? L("AÇILDI · " + s.Opened[0] + " ÇİFT", "OPENED · " + s.Opened[0] + " PAIRS") : L("AÇILDI · SERİ " + s.Opened[0], "OPENED · RUNS " + s.Opened[0]), "Baloo2-800", 10, "#8EF0B0");
                MenuView.Tracking(line, 1.2f, 10); info.Add(line); info.style.marginBottom = 6; column.Add(info);
            }
            var buttons = Row(6);
            if (s.CanUndo)
            {
                var undo = Row(6); undo.name = "table.doUndo"; undo.focusable = true; Pad(undo, 12, 14, 12, 12);
                view.Decorate(undo, "#3D7BFF", 15, 5, "#2148B0"); GalaxyFilters.HoverBrightness(undo, 1.1f);
                undo.Add(Text("↓", "Montserrat-800", 15, "#fff")); undo.Add(Text(L("Geri topla", "Take back"), "Montserrat-800", 14, "#fff"));
                buttons.Add(Clickable(undo, "undo"));
            }
            if (s.ShowReturn)
            {
                // G101-Masa table.doReturn: Geri bırak (R-32) next to Geri topla, the same panel button.
                var back = Row(6); back.name = "table.doReturn"; back.focusable = true; Pad(back, 12, 14, 12, 12);
                view.Decorate(back, "var(--pnH)", 15, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(back, 1.2f); back.tooltip = TableStrings.Get("returnTip", Lang);
                back.Add(Text("↑", "Montserrat-800", 15, "#F4F1E8")); back.Add(Text(L("Geri bırak", "Put back"), "Montserrat-800", 14, "#F4F1E8"));
                buttons.Add(Clickable(back, "putback"));
            }
            if (s.CanFromHand)
            {
                var bomb = Row(6); bomb.name = "table.doBomb"; bomb.focusable = true; Pad(bomb, 12, 16, 12, 12);
                view.Decorate(bomb, "linear-gradient(#FF6B5E,#D9343F)", 15, 5, "#8E1A24"); GalaxyFilters.HoverBrightness(bomb, 1.1f);
                // The bomb: a dark round body with a gold fuse spark; it wobbles every 1.4 s.
                var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 18;
                view.Decorate(icon, "#2A1A1E", 9); var spark = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(spark, 12, -3, 6, 6); view.Decorate(spark, "#FFC94A", 3); icon.Add(spark);
                bomb.Add(icon); bomb.Add(Text(L("Elden bit", "Finish from hand"), "Montserrat-800", 15, "#fff"));
                motions.Add(t => icon.style.rotate = new Rotate(12 * Mathf.Sin(t * 9) * (Mathf.Repeat(t, 1.4f) < .5f ? 1 : 0)));
                buttons.Add(Clickable(bomb, "fromhand"));
            }
            if (!opened && s.Pairs >= 3)
            {
                var pairs = ActionButton("table.doPair", L("Çift aç · " + s.Pairs + "/" + s.PairsBar, "Open pairs · " + s.Pairs + "/" + s.PairsBar), s.CanPair, false, 14, "pairs");
                // warnB(): fewer than five pairs is a short opening — the same orange-red as Aç.
                if (s.ShortPairs && s.CanPair) { view.Decorate(pairs, "linear-gradient(#FF9A4D,#E8573A)", 15, 5, "#9E3418"); pairs.style.color = view.C("#fff"); pairs.EnableInClassList("short", true); }
                buttons.Add(pairs);
            }
            if (!s.OpenedPairs[0])
            {
                var open = ActionButton("table.doOpen", opened ? L("Yere aç", "Lay down") : L("Aç", "Open"), s.CanOpen, true, 17, "open");
                if (s.ShortOpen && s.CanOpen)
                {
                    // Hatalı açma: orange-red, with the warning as its tooltip.
                    view.Decorate(open, "linear-gradient(#FF9A4D,#E8573A)", 15, 5, "#9E3418"); open.style.color = view.C("#fff");
                    open.tooltip = L("Sayı yetmiyor · açarsan hatalı açma cezası alırsın", "Not enough points · opening now costs a wrong-opening penalty");
                    open.EnableInClassList("short", true);
                }
                buttons.Add(open);
            }
            if (s.ShowLayPair) buttons.Add(ActionButton("table.doLayPair", TableStrings.Get("layPair", Lang), s.CanLayPair, false, 14, "pairs"));
            column.Add(buttons);
            if (s.ShowIsle)
            {
                // table.doIsle: a full-width row under the buttons (flex 1 1 100%).
                var isle = ActionButton("table.doIsle", TableStrings.Get("isle", Lang), s.CanIsle, true, 15, "isle");
                isle.style.marginTop = 6; isle.style.unityTextAlign = TextAnchor.MiddleCenter; column.Add(isle);
            }
            meter.Add(column);
            actions.Add(meter);
            hudLayer.Add(actions);
        }

        /// <summary>btn(on, primary): gold (primary) or green when enabled, muted pnH at 75 % otherwise.</summary>
        private VisualElement ActionButton(string name, string text, bool on, bool primary, int size, string command)
        {
            var button = Text(text, "Montserrat-800", size, on ? primary ? "var(--aT)" : "#05301A" : "var(--mt)");
            button.name = name; button.focusable = true; button.pickingMode = PickingMode.Position;
            Pad(button, 12, primary ? 20 : 14, 12, primary ? 20 : 14);
            view.Decorate(button, on ? primary ? "linear-gradient(var(--a1),var(--a2))" : "linear-gradient(#5BE38A,#2BB45F)" : "var(--pnH)", 15, 5, on ? primary ? "var(--aS)" : "#18793F" : "var(--pnS)");
            button.style.opacity = on ? 1 : .75f;
            if (on)
            {
                // pulseG 1.6s: a gl ring grows and fades.
                motions.Add(t => { float f = Mathf.Repeat(t, 1.6f) / 1.6f; GalaxyFilters.Shadow(button, 0, 0, 1 + 6 * Mathf.Sin(Mathf.PI * f), view.C("var(--gl)")); });
                Clickable(button, command);
            }
            return button;
        }

        private VisualElement Clickable(VisualElement element, string command)
        {
            element.pickingMode = PickingMode.Position; GalaxyFilters.PressSink(element);
            element.RegisterCallback<ClickEvent>(_ => Press(command));
            return element;
        }

        private VisualElement SortButton(string name, string text)
        {
            var button = Text(text, "Montserrat-700", 13, "#F4F1E8"); button.name = name; button.focusable = true; button.pickingMode = PickingMode.Position;
            button.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(button, 9, 14, 9, 14);
            view.Decorate(button, "var(--pnH)", 13, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(button, 1.25f);
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
            if (table != null && table.CameraRig != null) table.RenderInto(null);
            if (viewTarget != null) { viewTarget.Release(); Kill(viewTarget); }
            FramePacing.DisplayChanged -= OnDisplayChanged;
            view?.Dispose(); loc?.Dispose();
        }
    }
}
