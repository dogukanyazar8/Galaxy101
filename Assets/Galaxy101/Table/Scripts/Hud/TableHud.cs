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
    public sealed class TableHud : MonoBehaviour
    {
        public const float Width = 1280, Height = 720;

        /// <summary>Everything the HUD shows. <see cref="TableGame"/> fills it; the default mirrors T05 (my draw turn).</summary>
        public sealed class State
        {
            public string Phase = "draw"; // deal, draw, discard, opp, fin
            public int Active = 0, DeckCount = 24, Chips = 125400, Unread = 0, OpenSum = 88, TurnSeconds = 30, Pairs;
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
            /// <summary>T33/T47–T49: every okey on the table with what it stands for, drag targets and refusals; the
            /// tooltip under one okey; the band text while dragging a tile that can take an okey.</summary>
            public readonly List<OkeyMark> Okeys = new List<OkeyMark>();
            public int OkeyTipId = -1;
            public string OkeyTip, DragBanner;
        }

        public sealed class OkeyMark { public int Id; public string Number, Color; public bool Target, Invalid; }

        /// <summary>Message kinds (spec/17 §5, T40). Opponent events never queue: they are avatar balloons.</summary>
        public enum ToastKind { Penalty, Info, Success, Warning, Error }

        private sealed class Toast { public ToastKind Kind; public string Text, Sub; public int Amount; public float Duration, Until, ShownAt; public bool Sticky; }
        private sealed class BalloonInfo { public string Text; public bool Danger; public float Until, ShownAt; }
        private readonly List<Toast> toasts = new List<Toast>();
        private readonly Dictionary<int, BalloonInfo> balloons = new Dictionary<int, BalloonInfo>();
        private readonly int[] shownPenalty = new int[4];
        private readonly float[] penaltyChangedAt = { -10, -10, -10, -10 };
        private VisualElement toastLayer;

        /// <summary>World position of a tile view (the board's), for labels that follow a tile.</summary>
        public Func<int, Vector3?> TilePosition;

        /// <summary>Front message text (tests, diagnostics).</summary>
        public string ActiveToast => toasts.Count > 0 ? toasts[0].Text : null;

        /// <summary>
        /// Queue a message (§5): one shows at a time, the next waits smaller behind it with a "+n" count; penalties and
        /// errors jump the queue; the same text again restarts its time. Sticky messages stay until dismissed.
        /// </summary>
        public void Post(ToastKind kind, string text, string sub = null, int amount = 0, bool sticky = false)
        {
            float duration = kind == ToastKind.Penalty || kind == ToastKind.Warning ? 3.5f : kind == ToastKind.Error ? 3f : kind == ToastKind.Info ? 2.5f : 2f;
            float now = Time.realtimeSinceStartup;
            var same = toasts.Find(t => t.Text == text);
            if (same != null) { same.Until = now + same.Duration; same.ShownAt = now; RenderToasts(); return; }
            var toast = new Toast { Kind = kind, Text = text, Sub = sub, Amount = amount, Duration = duration, Sticky = sticky };
            if (kind == ToastKind.Penalty || kind == ToastKind.Error) toasts.Insert(0, toast); else toasts.Add(toast);
            if (toasts[0] == toast || toasts.Count == 1) { toast.Until = now + duration; toast.ShownAt = now; }
            RenderToasts();
        }

        public void Dismiss(string text)
        {
            int index = toasts.FindIndex(t => t.Text == text); if (index < 0) return;
            toasts.RemoveAt(index); if (index == 0 && toasts.Count > 0) { toasts[0].Until = Time.realtimeSinceStartup + toasts[0].Duration; toasts[0].ShownAt = Time.realtimeSinceStartup; }
            RenderToasts();
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
            if (toasts.Count > 0 && !toasts[0].Sticky && now > toasts[0].Until)
            {
                toasts.RemoveAt(0);
                if (toasts.Count > 0) { toasts[0].Until = now + toasts[0].Duration; toasts[0].ShownAt = now; }
                RenderToasts();
            }
            foreach (var seat in new List<int>(balloons.Keys)) if (now > balloons[seat].Until) { balloons.Remove(seat); dirty = true; }
            if (dirty) Render(Current);
        }

        /// <summary>Finish banner (spec/10 §7–§8): seat −1 = no avatar (TAŞ BİTTİ).</summary>
        public sealed class MomentInfo { public string Title, Sub; public int Seat = -1; public float StartedAt; }

        /// <summary>Hand result card (T24). Bot tables are offline: XP only, no chips.</summary>
        public sealed class ResultInfo
        {
            public string Title, Sub, Head;
            public int Xp, TotalXp, Winner, Seconds = 15;
            public float StartedAt;
            public readonly List<ResultRow> Rows = new List<ResultRow>();
        }

        public sealed class ResultRow { public int Seat, Total; public int? Hand; public string Note; }

        /// <summary>HUD buttons: exit, settings, chat, gift, sort-run, sort-pair, open, pairs.</summary>
        public event Action<string> Command;

        /// <summary>Act as if a HUD button with this command was pressed (keyboard shortcuts, tests).</summary>
        public void Press(string command) => Command?.Invoke(command);

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
        private float turnStarted;

        public State Current { get; private set; } = new State();
        public VisualElement Canvas => canvas;
        public TableScene Table => table;

        private void Start() { if (view == null) Initialize(); }

        public string L(string tr, string en) => view != null ? view.L(tr, en) : tr;
        public string SeatName(int k) => seats != null ? (string)Seat(k)["name"] : "";
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

            turnStarted = Time.realtimeSinceStartup;
            Render(Current);
            canvas.schedule.Execute(() => { float now = Time.realtimeSinceStartup; foreach (var motion in motions) motion(now); }).Every(16);
            Debug.Log("GALAXY101_TABLE_HUD_READY theme=" + table.Planet + " language=" + loc.Language);
        }

        private static VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            return layer;
        }

        /// <summary>The 3D camera draws at the canvas' device-pixel size (same rule as the galaxy background).</summary>
        private void ResizeViewTarget()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / Width, Screen.height / Height), 1, 2.2f);
            int w = Mathf.RoundToInt(Width * scale), h = Mathf.RoundToInt(Height * scale);
            if (viewTarget != null && viewTarget.width == w && viewTarget.height == h) return;
            var previous = viewTarget;
            viewTarget = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Galaxy101 table view", antiAliasing = 4, hideFlags = HideFlags.DontSave };
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
        public void Render(State state)
        {
            Current = state ?? throw new ArgumentNullException(nameof(state));
            if (view == null) return; // rendered once Initialize runs
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
            if (state.Countdown > 0) BuildCountdown(state);
            if (state.Moment != null) BuildMoment(state.Moment);
            if (state.Result != null) BuildResult(state.Result);
            if (state.ExitOpen) BuildExit();
            BuildPickedTile(state);
            BuildOkeys(state);
            ApplyGaps(hudLayer); ApplyGaps(anchorLayer);
            RenderToasts();
            LateUpdate();
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
            avatar.name = "opponent-" + k; column.Add(avatar);
            PenaltyBadge(avatar, k, s, false);
            if (balloons.TryGetValue(k, out var balloon)) column.Add(BalloonElement(k, balloon));
            var plate = new VisualElement { pickingMode = PickingMode.Ignore }; plate.style.marginTop = 6; plate.style.alignItems = Align.Center;
            Pad(plate, 4, 12, 5, 12); view.Decorate(plate, "rgba(5,8,22,.66)", 12); Border(plate, 1, new Color(1, 1, 1, .08f)); GalaxyFilters.BackdropBlur(plate, 6);
            plate.Add(Text((string)Seat(k)["name"], "Montserrat-700", 14, "#F4F1E8"));
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

        private VisualElement Card(float width, int radius)
        {
            var card = new VisualElement { pickingMode = PickingMode.Position }; card.style.width = width;
            view.Decorate(card, "var(--pn)", radius, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f));
            GalaxyFilters.Shadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            float began = Time.realtimeSinceStartup;
            // modalIn .35s: y +18 → 0, scale .95 → 1.
            motions.Add(t => { float u = TableConstants.EaseOutCubic(Mathf.Clamp01((t - began) / .35f)); card.style.translate = new Translate(0, 18 * (1 - u)); card.style.scale = new Scale(Vector2.one * Mathf.Lerp(.95f, 1, u)); });
            return card;
        }

        /// <summary>Hand result (T24): rows sorted by hand score, XP instead of chips (offline bot table), 15 s "Sonraki el".</summary>
        private void BuildResult(ResultInfo r)
        {
            var layer = Backdrop("table-result", null);
            var card = Card(780, 30); card.name = "table-result-card"; card.style.overflow = Overflow.Hidden;
            var header = Row(18); Pad(header, 26, 30, 18, 30);
            var left = new VisualElement { pickingMode = PickingMode.Ignore }; left.style.flexGrow = 1;
            var caption = Text(r.Title + " · " + r.Sub, "Baloo2-800", 11, "var(--mt)"); MenuView.Tracking(caption, 2, 11); left.Add(caption);
            var head = Text(r.Head, "Montserrat-800", 40, "var(--a1)"); head.name = "table-result-head"; head.style.marginTop = 4;
            head.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = view.C("var(--lS)") }; left.Add(head);
            header.Add(left);
            var right = new VisualElement { pickingMode = PickingMode.Ignore }; right.style.alignItems = Align.FlexEnd;
            var xpCap = Text("XP", "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(xpCap, 1.4f, 10); right.Add(xpCap);
            var xp = Text("+" + view.N(r.Xp), "Montserrat-700", 30, "#8EF0B0"); xp.name = "table-result-xp"; xp.style.marginTop = 2; right.Add(xp);
            header.Add(right); card.Add(header);

            var body = new VisualElement { pickingMode = PickingMode.Ignore }; Pad(body, 0, 30, 0, 30);
            var cols = Row(10); Pad(cols, 0, 14, 0, 14);
            cols.Add(Cell(Text("", "Baloo2-800", 10, "var(--mt)"), 40, false));
            var player = Text(L("OYUNCU", "PLAYER"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(player, 1.4f, 10); var pc = Cell(player, 0, false); pc.style.flexGrow = 1; cols.Add(pc);
            var handCap = Text(L("EL PUANI", "HAND"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(handCap, 1.4f, 10); cols.Add(Cell(handCap, 110, true));
            var totalCap = Text(L("TOPLAM", "TOTAL"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(totalCap, 1.4f, 10); cols.Add(Cell(totalCap, 110, true));
            body.Add(cols);
            int index = 0;
            foreach (var row in r.Rows)
            {
                var line = Row(10); line.name = "result-row-" + row.Seat; line.style.marginTop = 6; Pad(line, 9, 14, 9, 14);
                view.Decorate(line, row.Seat == 0 ? "var(--pnH)" : "var(--dp)", 16);
                if (row.Seat == r.Winner) Border(line, 2, view.C("var(--a1)"));
                var rank = Text(index + 1 + ".", "Montserrat-700", 20, index == 0 ? "var(--a1)" : "var(--mt)"); line.Add(Cell(rank, 40, false));
                var who = Row(10); who.style.flexGrow = 1;
                var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 36; dot.style.alignItems = Align.Center; dot.style.justifyContent = Justify.Center;
                view.Decorate(dot, (string)Seat(row.Seat)["color"], 18); dot.Add(Text((string)Seat(row.Seat)["ini"], "Montserrat-700", 13, "#fff")); who.Add(dot);
                who.Add(Text((string)Seat(row.Seat)["name"], "Montserrat-700", 16, "#F4F1E8"));
                who.Add(Text(row.Note, "Baloo2-800", 11, "var(--mt)"));
                line.Add(who);
                string hand = row.Hand.HasValue ? (row.Hand.Value > 0 ? "+" + row.Hand.Value : row.Hand.Value < 0 ? "−" + -row.Hand.Value : "0") : "–";
                string handColor = !row.Hand.HasValue ? "var(--mt)" : row.Hand.Value < 0 ? "#8EF0B0" : row.Hand.Value >= 202 ? "#FF8A94" : "#F4F1E8";
                line.Add(Cell(Text(hand, "Montserrat-700", 18, handColor), 110, true));
                line.Add(Cell(Text(row.Total < 0 ? "−" + -row.Total : row.Total.ToString(), "Montserrat-700", 18, "#F4F1E8"), 110, true));
                float began = r.StartedAt + index * .09f;
                // drawerIn .35s, 90 ms apart: rows slide in from the left.
                motions.Add(t => { float u = TableConstants.EaseOutCubic(Mathf.Clamp01((t - began) / .35f)); line.style.translate = new Translate(-44 * (1 - u), 0); line.style.opacity = u; });
                body.Add(line); index++;
            }
            card.Add(body);

            var strip = Row(12); strip.style.marginTop = 16; strip.style.marginLeft = 30; strip.style.marginRight = 30; Pad(strip, 10, 14, 10, 14); view.Decorate(strip, "var(--dp)", 16);
            var gifts = Text(L("ALDIĞIN İKRAMLAR", "TREATS RECEIVED"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(gifts, 1.4f, 10); strip.Add(gifts);
            var none = Text(L("Bu elde ikram gelmedi", "No treats this hand"), "Baloo2-700", 13, "var(--mt)"); none.style.flexGrow = 1; none.style.flexShrink = 1; strip.Add(none);
            strip.Add(Text(L("Toplam ", "Total ") + "+" + view.N(r.TotalXp) + " XP", "Baloo2-800", 13, "#8EF0B0"));
            card.Add(strip);

            var buttons = Row(12); buttons.style.justifyContent = Justify.FlexEnd; Pad(buttons, 20, 30, 26, 30);
            var leave = Text(L("Masadan kalk", "Leave table"), "Montserrat-700", 17, "#F4F1E8"); leave.name = "result-leave"; Pad(leave, 15, 24, 15, 24);
            view.Decorate(leave, "var(--pnH)", 18, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(leave, 1.2f); buttons.Add(Clickable(leave, "exit"));
            var next = new VisualElement { name = "result-next", pickingMode = PickingMode.Position }; next.style.overflow = Overflow.Hidden; Pad(next, 15, 34, 15, 34);
            view.Decorate(next, "linear-gradient(var(--a1),var(--a2))", 18, 5, "var(--aS)");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore }; bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.top = 0; bar.style.bottom = 0; bar.style.backgroundColor = new Color(1, 1, 1, .32f);
            next.Add(bar);
            var label = Text("", "Montserrat-800", 18, "var(--aT)"); label.name = "result-next-label"; next.Add(label);
            float start = r.StartedAt; int total = r.Seconds;
            // barFill 15s + the countdown in the label ("Sonraki el · 11").
            motions.Add(t =>
            {
                float elapsed = Mathf.Max(0, t - start);
                bar.style.width = Length.Percent(Mathf.Clamp01(elapsed / total) * 100);
                label.text = L("Sonraki el · ", "Next hand · ") + Mathf.Max(0, total - Mathf.FloorToInt(elapsed));
            });
            buttons.Add(Clickable(next, "next"));
            card.Add(buttons);
            layer.Add(card);
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
            var card = Card(460, 28); card.name = "table-exit-confirm"; card.style.alignItems = Align.Center; Pad(card, 28, 28, 28, 28);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 64; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, "rgba(255,94,108,.16)", 20); icon.Add(view.Icon("table-exit", 32, "#FF8A94")); card.Add(icon);
            var title = Text(L("Masadan kalkmak istiyor musun?", "Leave the table?"), "Montserrat-700", 24, "#F4F1E8"); title.style.marginTop = 12; card.Add(title);
            var body = Text(L("El bitmeden kalkarsan bu elin XP'sini alamazsın ve yerine bot oynar.", "If you leave before the hand ends you get no XP for it and a bot takes your seat."), "Baloo2-700", 15, "var(--mt)");
            body.style.whiteSpace = WhiteSpace.Normal; body.style.maxWidth = 360; body.style.unityTextAlign = TextAnchor.UpperCenter; body.style.marginTop = 12; card.Add(body);
            var buttons = Row(10); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 20;
            var stay = Text(L("Masada kal", "Stay"), "Montserrat-700", 17, "var(--aT)"); stay.name = "exit-stay"; stay.style.flexGrow = 1; stay.style.flexBasis = 0; stay.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(stay, 14, 14, 14, 14); view.Decorate(stay, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); buttons.Add(Clickable(stay, "exit-stay"));
            var leave = Text(L("Kalk", "Leave"), "Montserrat-700", 17, "#fff"); leave.name = "exit-leave"; leave.style.flexGrow = 1; leave.style.flexBasis = 0; leave.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(leave, 14, 14, 14, 14); view.Decorate(leave, "linear-gradient(#FF7A86,#E8455A)", 16, 5, "#A3283A"); buttons.Add(Clickable(leave, "exit-leave"));
            card.Add(buttons); layer.Add(card);
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

            var room = Row(8); Pad(room, 7, 13, 7, 13); view.Decorate(room, "var(--dp)", 15);
            room.Add(Text(L("Botlara karşı · 101", "vs Bots · 101"), "Baloo2-800", 12, "var(--ic)"));
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 4; view.Decorate(dot, "var(--mt)", 2); room.Add(dot);
            room.Add(Text(L("Antrenman · XP", "Practice · XP"), "Baloo2-800", 12, "var(--ic)"));
            bar.Add(room);

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.flexGrow = 1; bar.Add(spacer);

            var deck = Row(8); Pad(deck, 5, 13, 5, 6); view.Decorate(deck, "var(--dp)", 18);
            var mini = new VisualElement { pickingMode = PickingMode.Ignore }; mini.style.width = 20; mini.style.height = 27; mini.style.overflow = Overflow.Hidden;
            view.Decorate(mini, "linear-gradient(var(--tf1),var(--tf2))", 5);
            var edge = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(edge, 0, 24, 20, 3); edge.style.backgroundColor = view.C("var(--te)"); mini.Add(edge);
            deck.Add(mini);
            deck.Add(Text(L("Taş", "Deck"), "Baloo2-800", 12, "var(--mt)"));
            var count = Text(s.DeckCount.ToString(), "Montserrat-700", 16, "#F4F1E8"); count.name = "deck-count"; count.style.minWidth = 22; deck.Add(count);
            bar.Add(deck);

            var chips = Row(8); Pad(chips, 5, 14, 5, 6); view.Decorate(chips, "var(--dp)", 18);
            var coin = new VisualElement { pickingMode = PickingMode.Ignore }; coin.style.width = coin.style.height = 26; coin.style.overflow = Overflow.Hidden;
            view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 13);
            var dashed = new CssDashedCircle(26, 2, view.C("#FFF3C8")); MenuView.Place(dashed, 0, 0, 26, 26); coin.Add(dashed);
            var shine = new VisualElement { pickingMode = PickingMode.Ignore }; shine.style.position = Position.Absolute; shine.style.top = -4; shine.style.bottom = -4; shine.style.width = 9;
            view.Decorate(shine, "linear-gradient(90deg,transparent,rgba(255,255,255,.95),transparent)"); coin.Add(shine);
            // iconShine 3.2s ease-in-out: the streak crosses the coin in the first ~40 % of the cycle.
            motions.Add(t => { float f = Mathf.Repeat(t, 3.2f) / 3.2f; shine.style.left = Mathf.Lerp(-12, 30, Mathf.SmoothStep(0, 1, Mathf.Clamp01(f / .4f))); });
            chips.Add(coin);
            var amount = Text(view.N(s.Chips), "Montserrat-700", 16, "#F4F1E8"); amount.name = "chips-balance"; amount.style.minWidth = 70; chips.Add(amount);
            bar.Add(chips);

            var settings = Clickable(IconButton("settings", 24, L("Ayarlar", "Settings"), "table-settings"), "settings"); bar.Add(settings);
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
            if (toasts.Count > 0) return;
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
            // Overrides: the time warning, then one-off messages (rule hints and refusals).
            if (text == null) return;

            var row = new VisualElement { name = "table-toast-row", pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute; row.style.left = 0; row.style.right = 0; row.style.top = 16; row.style.alignItems = Align.Center;
            var toast = Row(10); Pad(toast, 9, 18, 9, 10); view.Decorate(toast, bg, 20); GalaxyFilters.Shadow(toast, 0, 8, 12, new Color(0, 0, 0, .35f));
            var bullet = new VisualElement { pickingMode = PickingMode.Ignore }; bullet.style.width = bullet.style.height = 22; bullet.style.alignItems = Align.Center; bullet.style.justifyContent = Justify.Center;
            view.Decorate(bullet, "rgba(0,0,0,.18)", 11);
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore }; diamond.style.width = diamond.style.height = 8; diamond.style.rotate = new Rotate(45); view.Decorate(diamond, fg, 2);
            bullet.Add(diamond); toast.Add(bullet);
            var label = Text(text, "Baloo2-800", 14, fg); label.name = "table-toast"; toast.Add(label);
            row.Add(toast); hudLayer.Add(row);
        }

        private string Lang => loc != null ? loc.Language : "tr";

        // ---------- messages (spec/17 §5) ----------
        private void RenderToasts()
        {
            if (toastLayer == null) return;
            toastLayer.Clear();
            if (toasts.Count > 1) toastLayer.Add(ToastElement(toasts[1], true, 0));
            if (toasts.Count > 0) toastLayer.Add(ToastElement(toasts[0], false, toasts.Count - 1));
            if (hudLayer != null && view != null) { var band = hudLayer.Q<VisualElement>("table-toast-row"); if (band != null) band.style.display = toasts.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex; }
        }

        private VisualElement ToastElement(Toast t, bool behind, int waiting)
        {
            var row = FullRow(behind ? 46 : 16); row.name = behind ? "table-message-next" : "table-message-row";
            string bg, fg, iconBg, iconFg, glyph;
            switch (t.Kind)
            {
                case ToastKind.Penalty: bg = "linear-gradient(#3A0A14,#22060B)"; fg = "#FFFFFF"; iconBg = "#FF5E6C"; iconFg = "#fff"; glyph = "!"; break;
                case ToastKind.Success: bg = "linear-gradient(#6BE8B0,#2FBF7E)"; fg = "#06301C"; iconBg = "rgba(0,0,0,.18)"; iconFg = "#06301C"; glyph = "tick"; break;
                case ToastKind.Warning: bg = "rgba(5,8,22,.82)"; fg = "#FFB13D"; iconBg = "#FFB13D"; iconFg = "#3A2604"; glyph = "!"; break;
                case ToastKind.Error: bg = "linear-gradient(#FF7A86,#E8455A)"; fg = "#FFFFFF"; iconBg = "rgba(0,0,0,.2)"; iconFg = "#fff"; glyph = "close"; break;
                default: bg = "rgba(5,8,22,.82)"; fg = "#F4F1E8"; iconBg = "var(--a1)"; iconFg = "var(--aT)"; glyph = "i"; break;
            }
            var toast = Row(10); toast.name = behind ? "table-message-behind" : "table-message"; Pad(toast, 8, t.Amount > 0 ? 8 : 18, 8, 9);
            view.Decorate(toast, bg, 24); GalaxyFilters.Shadow(toast, 0, 8, 12, new Color(0, 0, 0, .35f));
            if (t.Kind == ToastKind.Penalty) Border(toast, 2, view.C("#FF5E6C"));
            else if (t.Kind == ToastKind.Warning) Border(toast, 2, view.C("#FFB13D"));
            else if (t.Kind == ToastKind.Info) Border(toast, 1, new Color(1, 1, 1, .12f));
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 24; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, iconBg, 12);
            if (glyph == "tick" || glyph == "close") icon.Add(view.Icon(glyph, 14, iconFg)); else icon.Add(Text(glyph, "Montserrat-800", 14, iconFg));
            toast.Add(icon);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.Add(Text(t.Text, "Baloo2-800", 14, fg));
            if (!string.IsNullOrEmpty(t.Sub)) { var sub = Text(t.Sub, "Baloo2-700", 11, t.Kind == ToastKind.Penalty ? "#FF8A95" : fg); sub.style.marginTop = -2; texts.Add(sub); }
            toast.Add(texts);
            if (t.Amount > 0) toast.Add(AmountPill("+" + view.N(t.Amount), 14));
            if (!behind && waiting > 0)
            {
                var count = Text("+" + waiting, "Montserrat-800", 11, "var(--aT)"); count.name = "table-message-count"; count.style.position = Position.Absolute; count.style.top = -8; count.style.right = -8;
                Pad(count, 1, 6, 1, 6); view.Decorate(count, "var(--a1)", 9); toast.Add(count);
            }
            if (behind) { toast.style.scale = new Scale(Vector2.one * .88f); toast.style.opacity = .6f; }
            else
            {
                float began = t.ShownAt; bool shake = t.Kind == ToastKind.Error;
                // toastIn .3s (y −14 → 0); errors shake ±6 px twice.
                motions.Add(now =>
                {
                    float u = Mathf.Clamp01((now - began) / .3f);
                    float dx = shake && now - began < .4f ? Mathf.Sin((now - began) / .4f * Mathf.PI * 4) * 6 : 0;
                    toast.style.translate = new Translate(dx, -14 * (1 - TableConstants.EaseOutCubic(u))); toast.style.opacity = u;
                });
            }
            row.Add(toast); return row;
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
            view.Decorate(bubble, b.Danger ? "linear-gradient(#FF7A86,#E8455A)" : "#fff", 14); GalaxyFilters.Shadow(bubble, 0, 6, 9, new Color(0, 0, 0, .35f));
            float began = b.ShownAt;
            // bubble 2.5s: appear 12 %, hold, rise and fade over the last 15 %.
            motions.Add(t => { float u = Mathf.Clamp01((t - began) / 2.5f); bubble.style.opacity = u < .12f ? u / .12f : u > .85f ? (1 - u) / .15f : 1; bubble.style.translate = new Translate(0, u > .85f ? -10 * (u - .85f) / .15f : 0); });
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

        // ---------- my panel ----------
        private void BuildMe(State s)
        {
            var panel = Row(12); panel.name = "table-me"; panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.bottom = 16;
            bool active = s.Active == 0 && (s.Phase == "draw" || s.Phase == "discard");
            var me = Avatar(Seat(0), 74, active, s.TurnSeconds, s.Warn); panel.Add(me);
            PenaltyBadge(me, 0, s, true);
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.Add(Text((string)Seat(0)["name"], "Montserrat-700", 16, "#F4F1E8"));
            var status = Text(Status(0, s, true), "Baloo2-800", 12, s.Opened[0] > 0 ? "#8EF0B0" : "var(--mt)"); status.style.marginTop = 1; column.Add(status);
            var buttons = Row(8); buttons.style.marginTop = 6; buttons.pickingMode = PickingMode.Position;
            var chat = Square("table-chat-open", "linear-gradient(160deg,#7AA6FF,#3D6BF0)", "#2448B8", L("Sohbet", "Chat"));
            chat.Add(view.Icon("table-chat", 22, "#fff"));
            if (s.Unread > 0)
            {
                var badge = Text(s.Unread.ToString(), "Baloo2-800", 10, "#fff"); badge.style.position = Position.Absolute; badge.style.top = -6; badge.style.right = -6;
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
            var arc = new ArcRing(view.C(s.OpenSum >= 101 || opened ? "#3CCB6E" : "#FF5E6C"), new Color(1, 1, 1, .1f)) { Fraction = Mathf.Min(100, Mathf.Round(s.OpenSum / 101f * 100)) / 100f };
            MenuView.Place(arc, 0, 0, 62, 62); gauge.Add(arc);
            var dial = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(dial, 6, 6, 50, 50); dial.style.alignItems = Align.Center; dial.style.justifyContent = Justify.Center;
            view.Decorate(dial, "var(--dp)", 25);
            var sum = Text(s.OpenSum.ToString(), "Montserrat-700", 19, "#F4F1E8"); sum.name = "table-open-sum"; dial.Add(sum);
            dial.Add(Text(opened ? L("AÇIK", "OPEN") : "/ 101", "Baloo2-800", 9, "var(--mt)"));
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
            if (!opened && s.Pairs >= 3 || s.OpenedPairs[0])
                buttons.Add(ActionButton("table-pairs", L("Çift aç · " + s.Pairs + "/5", "Open pairs · " + s.Pairs + "/5"), s.CanPair, false, 14, "pairs"));
            if (!s.OpenedPairs[0]) buttons.Add(ActionButton("table-open", opened ? L("Yere aç", "Lay down") : L("Aç", "Open"), s.CanOpen, true, 17, "open"));
            column.Add(buttons); meter.Add(column);
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
            element.pickingMode = PickingMode.Position;
            element.RegisterCallback<ClickEvent>(_ => Command?.Invoke(command));
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
            view?.Dispose(); loc?.Dispose();
        }
    }
}
