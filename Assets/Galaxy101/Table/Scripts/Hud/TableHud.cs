using System;
using System.Collections.Generic;
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
            public bool TurnHint = true, ShowIndicator = true, Warn, FirstTurn, CanOpen, CanPair, FlashBad;
            /// <summary>Time.realtimeSinceStartup when the active turn began (the ring counts down from it); -1 = HUD build time.</summary>
            public float TurnStartedAt = -1;
            public string Flash, ResultLine;
        }

        /// <summary>HUD buttons: exit, settings, chat, gift, sort-run, sort-pair, open, pairs.</summary>
        public event Action<string> Command;

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
                var vp = cam.WorldToViewportPoint(table.AnchorWorld(pair.Key));
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
            motions.Clear(); anchors.Clear(); anchorLayer.Clear(); hudLayer.Clear();
            for (int k = 1; k <= 3; k++) BuildOpponent(k, state);
            bool myTurn = state.Active == 0;
            if (myTurn && state.Phase == "discard" && state.TurnHint) BuildDiscardLabel();
            if (state.ShowIndicator && state.Phase != "deal") BuildIndicatorLabel();
            BuildTopBar(state);
            BuildToast(state);
            BuildMe(state);
            BuildActions(state);
            ApplyGaps(hudLayer); ApplyGaps(anchorLayer);
            LateUpdate();
        }

        private VisualElement Anchor(string key)
        {
            var anchor = new VisualElement { name = "anchor-" + key, pickingMode = PickingMode.Ignore };
            MenuView.Place(anchor, 0, 0, 0, 0); anchorLayer.Add(anchor); anchors[key] = anchor; return anchor;
        }

        private string Status(int k, State s, bool me)
        {
            if (s.Opened[k] > 0 && s.OpenedPairs[k]) return L("Açtı · " + s.Opened[k] + " çift", "Opened · " + s.Opened[k] + " pairs");
            if (s.Opened[k] > 0) return L("Açtı · " + s.Opened[k], "Opened · " + s.Opened[k]);
            return me ? L("Açmadı · " + s.TileCounts[k] + " taş", "Not opened · " + s.TileCounts[k] + " tiles") : L(s.TileCounts[k] + " taş", s.TileCounts[k] + " tiles");
        }

        private void BuildOpponent(int k, State s)
        {
            var anchor = Anchor("a" + k);
            var column = Centered(anchor); column.style.alignItems = Align.Center;
            var avatar = Avatar(Seat(k), 62, k == s.Active && s.Phase == "opp", 4, false);
            avatar.name = "opponent-" + k; column.Add(avatar);
            var plate = new VisualElement { pickingMode = PickingMode.Ignore }; plate.style.marginTop = 6; plate.style.alignItems = Align.Center;
            Pad(plate, 4, 12, 5, 12); view.Decorate(plate, "rgba(5,8,22,.66)", 12); Border(plate, 1, new Color(1, 1, 1, .08f)); GalaxyFilters.BackdropBlur(plate, 6);
            plate.Add(Text((string)Seat(k)["name"], "Montserrat-700", 14, "#F4F1E8"));
            var status = Text(Status(k, s, false), "Baloo2-800", 11, s.Opened[k] > 0 ? "#8EF0B0" : "var(--mt)"); status.style.marginTop = 1; plate.Add(status);
            column.Add(plate);
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
            room.Add(Text(L("Tekli · 101", "Solo · 101"), "Baloo2-800", 12, "var(--ic)"));
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.style.width = dot.style.height = 4; view.Decorate(dot, "var(--mt)", 2); room.Add(dot);
            room.Add(Text(L("Bahis " + view.N(5000), "Stake " + view.N(5000)), "Baloo2-800", 12, "var(--ic)"));
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
            if (s.Phase == "deal") { text = L("Taşlar dağıtılıyor…", "Dealing tiles…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            else if (s.Phase == "fin" && s.ResultLine != null) { text = s.ResultLine; bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "draw" && s.Active == 0) { string n = FirstName(3); text = L("Sıra sende · desteden ya da " + n + "'ın taşından çek", "Your turn · draw from the deck or " + n + "'s tile"); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; }
            else if (s.Phase == "discard" && s.Active == 0)
            {
                text = s.FirstTurn ? L("İlk hamle senin · ıstakadan bir taş at", "You start · discard a tile from your rack") : L("Atacağın taşı halkaya sürükle ya da çift tıkla", "Drag a tile onto the ring or double-click it");
                bg = "#3CCB6E"; fg = "#062B14";
            }
            else if (s.Active > 0) { string n = FirstName(s.Active); text = L(n + " oynuyor…", n + " is playing…"); bg = "rgba(5,8,22,.75)"; fg = "var(--ic)"; }
            if (!s.TurnHint && s.Phase != "deal" && s.Phase != "fin") text = null;
            // Overrides: the time warning, then one-off messages (rule hints and refusals).
            if (s.Warn && s.Active == 0 && s.Phase != "fin") { text = L("Süren bitiyor!", "Time is running out!"); bg = "linear-gradient(#FF7A86,#E8455A)"; fg = "#fff"; }
            if (s.Flash != null) { text = s.Flash; bg = s.FlashBad ? "linear-gradient(#FF7A86,#E8455A)" : "#3CCB6E"; fg = s.FlashBad ? "#fff" : "#062B14"; }
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

        // ---------- my panel ----------
        private void BuildMe(State s)
        {
            var panel = Row(12); panel.name = "table-me"; panel.style.position = Position.Absolute; panel.style.left = 18; panel.style.bottom = 16;
            bool active = s.Active == 0 && (s.Phase == "draw" || s.Phase == "discard");
            panel.Add(Avatar(Seat(0), 74, active, s.TurnSeconds, s.Warn));
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
            var arc = new ArcRing(view.C(s.OpenSum >= 101 ? "#3CCB6E" : "var(--a1)"), new Color(1, 1, 1, .1f)) { Fraction = Mathf.Min(100, Mathf.Round(s.OpenSum / 101f * 100)) / 100f };
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
