using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Klasik lobby (v4 S09–S15; G101-Lobi.dc.html): the live table list with filters, Masa aç, the PIN / code /
    /// connecting / full / closed / share dialogs, the waiting room and the invite panel. Drawn over the menu canvas (the
    /// menu's main layer hides meanwhile); every table action goes to the server through <see cref="ILobbyBackend"/>.
    /// Element names are the prototype's data-g101-id (D-62): a looped element keeps the prototype's index, so the same
    /// name repeats across cards exactly as in the reference dump; containers the prototype leaves unnamed get a
    /// "lobby-…" name of their own for tests.
    /// </summary>
    public sealed partial class LobbyScreen
    {
        public static readonly long[] Stakes = TableOptions.StakeChoices;
        private readonly MenuView view;
        private readonly ILobbyBackend backend;
        private VisualElement layer, body, dialogLayer, toastLayer;
        private string screen = "list";
        // filt {type, free, kat, bet} and q of the prototype's state (S09).
        private string typeFilter = "all", query = "";
        private bool freeFilter, doublingFilter, queryFocused;
        private long stakeFilter;
        private bool animateCards = true;
        private float shownAt;

        /// <summary>Back to the main menu.</summary>
        public event Action Closed;
        /// <summary>"+" next to the balance or Çip al: the shop.</summary>
        public event Action BuyRequested;
        /// <summary>The table became a match (TableStateMessage.MatchId, YourSeat, Table).</summary>
        public event Action<TableStateMessage> MatchStarted;
        public bool Visible => layer != null;
        public string Screen => screen;
        public string DialogKind => dialog?.Kind;
        public VisualElement Layer => layer;
        /// <summary>connect(): the "Masaya bağlanılıyor…" dialog holds 850 ms (setTimeout 850); tests shorten it.</summary>
        public float ConnectSeconds = .85f;

        public LobbyScreen(MenuView view, ILobbyBackend backend)
        {
            this.view = view; this.backend = backend;
        }

        private string L(string tr, string en) => view.L(tr, en);

        public async void Show()
        {
            if (layer != null) return;
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            var main = canvas.Q<VisualElement>("main-layer"); if (main != null) main.style.display = DisplayStyle.None;
            layer = Fill("lobby"); layer.pickingMode = PickingMode.Position;
            var modal = canvas.Q<VisualElement>("modal-layer");
            if (modal?.parent != null) modal.parent.Insert(modal.parent.IndexOf(modal), layer); else canvas.Add(layer);
            view.Decorate(layer, "linear-gradient(180deg,rgba(5,8,22,.15),rgba(5,8,22,.5))");
            body = Fill("lobby-body"); body.pickingMode = PickingMode.Ignore; layer.Add(body);
            AppVersionLabel.Attach(layer);
            cardsSeen.Clear();
            dialogLayer = Fill("lobby-dialogs"); dialogLayer.pickingMode = PickingMode.Ignore; layer.Add(dialogLayer);
            toastLayer = Fill("lobby-toasts"); toastLayer.pickingMode = PickingMode.Ignore; layer.Add(toastLayer);
            backend.TablesChanged += OnTables; backend.RoomChanged += OnRoom; backend.ChipsChanged += OnChips; backend.InviteAnswered += OnInviteAnswered;
            shownAt = Time.realtimeSinceStartup;
            screen = backend.Room != null ? "wait" : "list"; animateCards = true; Render();
            if (!backend.Online && !(await Connect())) return;
            string error = await backend.SubscribeAsync();
            if (layer == null) return;
            if (error != null) ShowOffline();
        }

        private async Task<bool> Connect()
        {
            string error = await backend.SubscribeAsync();
            if (layer == null) return false;
            if (error != null) { ShowOffline(); return false; }
            return true;
        }

        private void ShowOffline() => OpenDialog(new Dialog { Kind = "offline" });

        public void Hide()
        {
            if (layer == null) return;
            backend.TablesChanged -= OnTables; backend.RoomChanged -= OnRoom; backend.ChipsChanged -= OnChips; backend.InviteAnswered -= OnInviteAnswered;
            backend.Unsubscribe();
            ticker?.Pause(); ticker = null; motions.Clear(); toastStep = null;
            var canvas = layer.parent; layer.RemoveFromHierarchy(); layer = null;
            var main = canvas?.Q<VisualElement>("main-layer"); if (main != null) main.style.display = DisplayStyle.Flex;
        }

        private void Back() { Hide(); Closed?.Invoke(); }

        // ---------- server events ----------
        private void OnTables()
        {
            if (screen != "list") return;
            // Keep focus in the search box and the open form: only the cards are redrawn then.
            if (queryFocused || create != null || dialog != null) RefreshCards(); else Render();
        }

        private void OnChips() => Render();

        private void OnRoom(TableStateMessage state)
        {
            if (layer == null || state == null) return;
            if (state.Closed)
            {
                if (state.MatchId != null) { MatchStarted?.Invoke(state); return; }
                var table = state.Table ?? backend.Room?.Table;
                screen = "list"; invite = null; inviteOpen = false; seen.Clear(); animateCards = true; CloseDialog();
                // leftTable{reason} / kicked / tableClosed{reason: "ownerLeft"} (protocol.md s2c); the current server's reasons alongside.
                switch (state.Reason)
                {
                    case "kicked": Flash(L("Oda sahibi seni masadan çıkardı", "The host removed you from the table"), "bad"); break;
                    case "insufficient_funds": Flash(ErrorText("E_NO_CHIPS"), "bad"); break;
                    case "ownerLeft": case "closed": Render(); OpenDialog(new Dialog { Kind = "closed", Table = table }); leaving = false; return;
                    case "left": case "leave": if (!leaving) Flash(S("closedT"), "bad"); break;
                    case "offline": case "disconnect": Flash(L("Bağlantı koptu, masadan kalktın", "Connection lost: you left the table"), "bad"); break;
                }
                leaving = false; Render(); return;
            }
            // While "Masaya bağlanılıyor…" holds its 850 ms the room waits behind it (JoinAsync opens it).
            if (joining) return;
            bool entering = screen != "wait";
            if (entering) EnterRoom();
            NoteSeats(state);
            countdownFrom = state.Countdown; countdownAt = Time.realtimeSinceStartup;
            Render();
        }

        private void EnterRoom()
        {
            screen = "wait"; roomShownAt = Time.realtimeSinceStartup; create = null; seatPops.Clear();
            if (dialog?.Kind == "connect") dialog = null;
        }

        // ---------- layout ----------
        /// <summary>Redraw after an outside change (the state router sets the invite tab).</summary>
        public void Refresh() => Render();

        private void Render()
        {
            if (body == null) return;
            // A redraw is invisible: scroll positions and the focused text field carry over (no jump back to the top,
            // no lost caret), entrance animations only play for things that newly appear.
            var scrolls = new Dictionary<string, Vector2>();
            body.Query<ScrollView>().ForEach(s => { if (!string.IsNullOrEmpty(s.name)) scrolls[s.name] = s.scrollOffset; });
            var focused = body.panel?.focusController?.focusedElement as VisualElement;
            string focusName = (focused as TextField)?.name ?? focused?.GetFirstAncestorOfType<TextField>()?.name;
            body.Clear(); motions.Clear(); hovers.Clear();
            if (screen == "wait" && backend.Room != null) BuildRoom(); else { if (screen != "list") cardsSeen.Clear(); screen = "list"; BuildList(); }
            if (create != null) BuildCreate();
            RenderDialog();
            foreach (var pair in scrolls)
                if (body.Q<ScrollView>(pair.Key) is ScrollView again && pair.Value != Vector2.zero) Restore(again, pair.Value);
            if (focusName != null && body.Q<TextField>(focusName) is TextField field)
                field.schedule.Execute(() => { field.Focus(); field.SelectRange(field.value.Length, field.value.Length); });
        }

        /// <summary>Put a rebuilt scroll view back where it was as soon as its content has a size (same frame).</summary>
        private static void Restore(ScrollView scroll, Vector2 offset)
        {
            scroll.scrollOffset = offset;
            EventCallback<GeometryChangedEvent> once = null;
            once = _ => { scroll.scrollOffset = offset; scroll.contentContainer.UnregisterCallback(once); };
            scroll.contentContainer.RegisterCallback(once);
        }

        /// <summary>When each listed table first appeared (its lbCard entrance runs once, not on every redraw).</summary>
        private readonly Dictionary<Guid, float> cardsSeen = new Dictionary<Guid, float>();

        /// <summary>The listed tables: private ones never show (renderVals: vis = tables with vis !== 'private').</summary>
        private IEnumerable<TableSummary> Listed => backend.Tables.Where(t => !t.Private);

        private void BuildList()
        {
            long chips = backend.Chips;
            // Top bar: 86 px, padding 0 28, gap 14 — back, 101 badge, Klasik + subtitle, balance, Kodla katıl, Masa aç.
            var bar = Row(); MenuView.Place(bar, 0, 0, 1280, 86); Pad(bar, 0, 28, 0, 28); body.Add(bar);
            var back = Box("lobby.back"); Square(back, 46); Raise(back, "var(--pnH)", 14, 4, "var(--pnS)"); back.Add(view.Icon(IconBack, 20, "var(--ic)")); back.tooltip = S("back");
            GalaxyFilters.HoverBrightness(back, 1.25f); Tap(back, Back); bar.Add(back);
            var badge = Box(); Square(badge, 52); badge.style.marginLeft = 14; Raise(badge, "#3D7BFF", 16, 4, "#2148B0"); TopSheen(badge, 16, .35f);
            badge.Add(Text("101", 17, "Montserrat-800", "#fff")); bar.Add(badge);
            var titles = Box(); titles.style.marginLeft = 14; titles.style.flexShrink = 1;
            titles.Add(Text(S("classic"), 26, "Montserrat-700", "#F4F1E8", 1.1f, "lobby.text.classic"));
            var sub = Text(S("classicSub"), 13, "Baloo2-700", "var(--mt)", 0, "lobby.text.classicSub"); sub.style.marginTop = 2; titles.Add(sub); bar.Add(titles);
            bar.Add(Grow());
            bar.Add(Balance(chips, true));
            var code = Row(); code.name = "lobby.openCode"; code.style.height = 46; code.style.marginLeft = 14; Pad(code, 0, 18, 0, 18); Raise(code, "var(--pnH)", 14, 4, "var(--pnS)");
            var key = view.Icon(IconKey, 18, "var(--ic)"); key.style.marginRight = 8; code.Add(key); code.Add(Text(S("joinCode"), 15, "Montserrat-700", "#F4F1E8"));
            GalaxyFilters.HoverBrightness(code, 1.2f); Tap(code, OpenCode); bar.Add(code);
            var open = Row(); open.name = "lobby.openCreate"; open.style.height = 46; open.style.marginLeft = 14; Pad(open, 0, 20, 0, 20); Raise(open, "linear-gradient(var(--a1),var(--a2))", 14, 5, "var(--aS)");
            var plus = view.Icon(IconPlus, 18, "var(--aT)"); plus.style.marginRight = 8; open.Add(plus); open.Add(Text(S("create"), 16, "Montserrat-800", "var(--aT)"));
            GalaxyFilters.HoverBrightness(open, 1.07f); Tap(open, OpenCreate); bar.Add(open);
            Pulse(open, 2.4f);

            // Filters: 54 px, padding 0 28, gap 10 — type tabs with counts, Boş yer var, Katlamalı, stake tabs, search.
            var visible = Listed.ToList();
            var filters = Row(); MenuView.Place(filters, 0, 86, 1280, 54); Pad(filters, 0, 28, 0, 28); body.Add(filters);
            var types = Segment(4); types.name = "lobby-types";
            var typeTabs = new[] { ("all", S("all"), visible.Count), ("single", S("tekli"), visible.Count(t => t.Type == "single")), ("pairs", S("esli"), visible.Count(t => t.Type == "pairs")) };
            for (int i = 0; i < typeTabs.Length; i++)
            {
                var (value, label, count) = typeTabs[i]; string pick = value; bool on = typeFilter == value;
                var option = SegmentOption("lobby.o.pick[" + i + "]", label, on, () => { typeFilter = pick; animateCards = true; Render(); }, 14, 8, 14, i < typeTabs.Length - 1 ? 4 : 0);
                // min-width 14px + padding 1px 6px (content box), rgba(0,0,0,.2), the tab's colour.
                var n = Tabular(Text(count.ToString(), 11, "Baloo2-800", on ? "var(--aT)" : "var(--mt)", 0, "lobby.o.n[" + i + "]")); n.style.marginLeft = 7; Pad(n, 1, 6, 1, 6);
                n.style.minWidth = 14 + 12; n.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(n, "rgba(0,0,0,.2)", 7); option.Add(n);
                types.Add(option);
            }
            filters.Add(types);
            filters.Add(Chip("lobby.chFree.toggle", freeFilter, () => { freeFilter = !freeFilter; animateCards = true; Render(); }, Dot("#3CCB6E", 8, 6), S("fFree")));
            filters.Add(Chip("lobby.chKat.toggle", doublingFilter, () => { doublingFilter = !doublingFilter; animateCards = true; Render(); }, Text("101↑", 12, "Montserrat-800", "#C2A4FF"), S("fKat")));
            var stakes = Segment(4); stakes.name = "lobby-stakes"; stakes.style.marginLeft = 10;
            var stakeTabs = new long[] { 0 }.Concat(Stakes).ToArray();
            for (int i = 0; i < stakeTabs.Length; i++)
            {
                long pick = stakeTabs[i];
                stakes.Add(SegmentOption("lobby.o.pick[" + i + "]", pick == 0 ? S("allBets") : Short(pick), stakeFilter == pick, () => { stakeFilter = pick; animateCards = true; Render(); }, 13, 8, 11, i < stakeTabs.Length - 1 ? 3 : 0));
            }
            filters.Add(stakes);
            filters.Add(Grow());
            filters.Add(Search());

            // Cards (grid of 3, gap 16) or the empty state, in a scroller from 146 px (padding 6 22 26 28, stable gutter).
            var list = Filter(visible, chips);
            var scroll = view.Scroll(); scroll.name = "lobby-list"; scroll.style.position = Position.Absolute; scroll.style.left = 0; scroll.style.right = 0; scroll.style.top = 146; scroll.style.bottom = 0;
            scroll.contentContainer.style.paddingTop = 6; scroll.contentContainer.style.paddingBottom = 26; scroll.contentContainer.style.paddingLeft = 28; scroll.contentContainer.style.paddingRight = 22;
            scroll.pickingMode = PickingMode.Position; body.Add(scroll);
            FillCards(scroll, list, chips);
            animateCards = false;
        }

        private void FillCards(ScrollView scroll, List<TableSummary> list, long chips)
        {
            if (list.Count == 0) { scroll.Add(Empty()); return; }
            VisualElement row = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 3 == 0) { row = Row(); row.style.alignItems = Align.Stretch; row.style.marginBottom = 16; scroll.Add(row); }
                var card = Card(list[i], chips, i); card.style.flexGrow = 1; card.style.flexBasis = 0; if (i % 3 != 2) card.style.marginRight = 16; row.Add(card);
            }
            for (int pad = list.Count % 3; pad != 0 && pad < 3; pad++) { var spacer = Box(); spacer.style.flexGrow = 1; spacer.style.flexBasis = 0; if (pad < 2) spacer.style.marginRight = 16; row.Add(spacer); }
            row.style.marginBottom = 0;
        }

        private bool Mine(TableSummary t) => t.Seats.Any(s => s?.Player != null && s.Player.Id == backend.Me);

        /// <summary>A seat to take: an empty one while waiting; while playing an empty one or one the server plays (a bot's, or left, R-65).</summary>
        private static bool Free(TableSummary t) => t.Seats.Any(s => s == null) || t.Status == "playing" && t.OpenSeats > 0;

        /// <summary>renderVals(): the filters, then mine first, waiting with a free seat, playing with a free seat, playing, full; cheaper first.</summary>
        public List<TableSummary> Filter(IEnumerable<TableSummary> tables, long chips)
        {
            var tr = new System.Globalization.CultureInfo("tr-TR"); string q = query.Trim().ToLower(tr);
            int Rank(TableSummary t) { bool free = Free(t); return Mine(t) ? -1 : t.Status != "playing" && free ? 0 : t.Status == "playing" && free ? 1 : t.Status == "playing" ? 2 : 3; }
            return tables.Where(t => (typeFilter == "all" || t.Type == typeFilter) && (!freeFilter || Free(t)) && (!doublingFilter || t.Doubling)
                    && (stakeFilter == 0 || t.Stake == stakeFilter) && (q.Length == 0 || (t.Name ?? "").ToLower(tr).Contains(q)))
                .OrderBy(Rank).ThenBy(t => t.Stake).ToList();
        }

        private VisualElement Card(TableSummary t, long chips, int index)
        {
            bool free = Free(t), playing = t.Status == "playing", low = t.Stake > chips, mine = Mine(t);
            int seated = t.Seats.Count(s => s != null);
            // 0 6px 0 --pnS under-edge as a plain box behind the face: a drop-shadow filter here would nest the buttons'
            // own filters inside a filtered card, which UI Toolkit's nested render passes do not survive in a scroll view.
            var card = Box("lobby-card-" + t.Id); card.AddToClassList("lobby-card"); card.pickingMode = PickingMode.Position;
            var under = Box(); under.style.position = Position.Absolute; under.style.left = under.style.right = 0; under.style.top = 6; under.style.bottom = -6; view.Decorate(under, "var(--pnS)", 20); card.Add(under);
            // t.ring: mine → inset 0 0 0 2px --a1, 0 0 24px --gl; others → inset 0 0 0 1px rgba(255,255,255,.06).
            if (mine) { var glow = Box(); glow.style.position = Position.Absolute; glow.style.left = glow.style.right = glow.style.top = glow.style.bottom = -4; Radius(glow, 24); Border(glow, 4, view.C("var(--gl)")); glow.style.opacity = .6f; card.Add(glow); }
            // The ring is an inset box-shadow in CSS (no layout): the padding gives its width back.
            float ring = mine ? 2 : 1;
            var face = Box(); face.style.flexGrow = 1; Pad(face, 15 - ring, 16 - ring, 14 - ring, 16 - ring); view.Decorate(face, "var(--pn)", 20);
            Border(face, ring, mine ? view.C("var(--a1)") : new Color(1, 1, 1, .06f));
            card.Add(face);
            // lbCard .5s cubic-bezier(.22,1,.36,1), delay 35 ms per card (max 10); hover: translate 0 −6px, scale 1.012, brightness 1.08.
            if (!cardsSeen.TryGetValue(t.Id, out float appeared)) cardsSeen[t.Id] = appeared = Time.realtimeSinceStartup + Mathf.Min(index, 10) * .035f;
            if (Time.realtimeSinceStartup - appeared < .5f)
            {
                float began = appeared; card.style.opacity = 0;
                bool done = false;
                Animate(() =>
                {
                    if (done) return;
                    float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .5f), k = Ease(u); done = u >= 1;
                    card.style.opacity = k; card.style.translate = new Translate(0, 14 * (1 - k) + hoverLift(card));
                }, card);
            }
            Hover(card);

            // Head (gap 8): name (700 17px/1.2), lock, status pill.
            var head = Row();
            var name = Text(t.Name, 17, "Montserrat-700", "#F4F1E8", 1.2f, "lobby.t.name[" + index + "]"); name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; head.Add(name);
            if (t.Locked) { var lockBox = Box(); Square(lockBox, 22); lockBox.style.marginLeft = 8; view.Decorate(lockBox, "rgba(255,201,74,.16)", 7); lockBox.Add(view.Icon(IconLock, 12, "var(--a1)")); lockBox.tooltip = S("locked"); head.Add(lockBox); }
            head.Add(Grow());
            string stText = playing ? S("sPlay", Math.Max(1, t.Hand), t.Hands) : free ? S("sWait") : S("sFull");
            string stBg = playing ? "rgba(255,201,74,.14)" : free ? "rgba(60,203,110,.15)" : "rgba(255,94,108,.14)", stFg = playing ? "var(--a1)" : free ? "#8EF0B0" : "#FF9AA3";
            var status = Row(); status.style.marginLeft = 8; Pad(status, 4, 10, 4, 10); view.Decorate(status, stBg, 999); var sd = Dot(stFg, 7, 0); sd.style.marginRight = 6; status.Add(sd); status.Add(Text(stText, 12, "Baloo2-800", stFg)); head.Add(status);
            face.Add(head);

            // Money (gap 8): coin, bet (700 23px/1), meta, type label.
            var money = Row(); money.style.marginTop = 11;
            money.Add(Coin(24));
            var bet = Tabular(Text(view.N(t.Stake), 23, "Montserrat-700", "#F4F1E8", 1, "lobby.t.bet[" + index + "]")); bet.style.marginLeft = 8; money.Add(bet);
            var meta = Text(S("meta", t.Hands, t.TurnSeconds), 13, "Baloo2-700", "var(--mt)", 0, "lobby.t.meta[" + index + "]"); meta.style.marginLeft = 8; money.Add(meta);
            money.Add(Grow());
            var type = Text(t.Type == "pairs" ? S("esli") : S("tekli"), 12, "Montserrat-800", "#fff", 0, "lobby.t.typeLabel[" + index + "]"); type.style.marginLeft = 8; Pad(type, 4, 10, 4, 10); view.Decorate(type, t.Type == "pairs" ? "#FF6B6B" : "#3D7BFF", 9); money.Add(type);
            face.Add(money);

            // Seats (gap 6): four 36 px chips, n/4, buttons.
            var seats = Row(); seats.style.marginTop = 11;
            for (int s = 0; s < 4; s++)
            {
                var seat = s < t.Seats.Count ? t.Seats[s] : null;
                var chip = SeatChip(seat, 36, 12, "lobby.z.ini[" + s + "]", false); if (s > 0) chip.style.marginLeft = 6;
                seats.Add(chip);
            }
            var occ = Tabular(Text(seated + "/4", 13, "Baloo2-800", "var(--mt)", 0, "lobby.t.occ[" + index + "]")); occ.style.marginLeft = 6 + 4; seats.Add(occ);
            seats.Add(Grow());
            // btns: İzle (playing, spectators on), then Odaya dön (mine) | Otur (waiting, free) | Katıl (playing, free) | Dolu.
            var buttons = new List<VisualElement>();
            if (playing && t.Spectators) buttons.Add(CardButton(S("watch"), "var(--pnH)", "#F4F1E8", "var(--pnS)", true, () => Watch(t)));
            if (mine) buttons.Add(CardButton(S("back2"), "linear-gradient(var(--a1),var(--a2))", "var(--aT)", "var(--aS)", false, () => { if (backend.Room != null) { EnterRoom(); Render(); } }));
            else if (free && !playing) buttons.Add(CardButton(S("sit"), low ? "var(--dp)" : "linear-gradient(var(--a1),var(--a2))", low ? "var(--mt)" : "var(--aT)", low ? "rgba(0,0,0,.25)" : "var(--aS)", false, () => { if (low) Low(t.Stake, chips); else Sit(t); }));
            else if (free && playing) buttons.Add(CardButton(S("joinRun"), low ? "var(--dp)" : "#3CCB6E", low ? "var(--mt)" : "#062B14", low ? "rgba(0,0,0,.25)" : "#22914A", false, () => { if (low) Low(t.Stake, chips); else JoinRunning(t); }));
            else if (buttons.Count == 0) buttons.Add(CardButton(S("full"), "var(--dp)", "var(--mt)", "rgba(0,0,0,.25)", false, () => { }));
            for (int b = 0; b < buttons.Count; b++) { buttons[b].name = "lobby.b.go[" + b + "]"; buttons[b].style.marginLeft = 6; seats.Add(buttons[b]); }
            face.Add(seats);

            var badges = Row(); badges.style.marginTop = 11; badges.style.height = 22; badges.style.overflow = Overflow.Hidden;
            int g = 0; foreach (var b in Badges(t, false)) { b.name = "lobby.g.label[" + g++ + "]"; badges.Add(b); }
            face.Add(badges);
            return card;
        }

        /// <summary>A seat chip (card z, invite iv.seats): the player's colour with a solid ring, or a dashed "+" when empty.</summary>
        private VisualElement SeatChip(TableSeat seat, float size, int font, string name, bool chosen)
        {
            var chip = Box(name); Square(chip, size);
            if (seat?.Player != null)
            {
                bool me = seat.Player.Id == backend.Me;
                view.Decorate(chip, seat.Bot ? "#5B6390" : seat.Player.Color ?? "#3D7BFF", Mathf.RoundToInt(size / 2)); Border(chip, 2, view.C(me ? "var(--a1)" : "rgba(255,255,255,.75)"));
                chip.Add(Text(Initials(seat.Player.Name), font, "Montserrat-700", "#fff")); chip.tooltip = seat.Player.Name;
            }
            else
            {
                view.Decorate(chip, chosen ? "rgba(255,201,74,.16)" : "rgba(0,0,0,.2)", Mathf.RoundToInt(size / 2)); DashedBorder(chip, 2, view.C(chosen ? "var(--a1)" : "var(--bd)"));
                chip.Add(Text("+", font, "Montserrat-700", "var(--mt)")); chip.tooltip = S("emptySeat");
            }
            return chip;
        }

        /// <summary>A card button (b): gap 6, padding 9 15, radius 12, 0 4px 0 shade, 800 14px; İzle carries the eye.</summary>
        private VisualElement CardButton(string text, string bg, string fg, string shade, bool eye, Action go)
        {
            var button = Row(); Pad(button, 9, 15, 9, 15); view.Decorate(button, bg, 12); Ledge(button, 4, shade);
            if (eye) { var icon = view.Icon(IconEye, 15, fg); icon.style.marginRight = 6; button.Add(icon); }
            button.Add(Text(text, 14, "Montserrat-800", fg)); GalaxyFilters.HoverBrightness(button, 1.1f); Tap(button, go); return button;
        }

        /// <summary>badges(t, full) of G101-Lobi: Katlamalı (+ partner rule on pairs), (full: type, Özel, Şifreli, el, sn), Okey +101, İşlek +101, İzleyici, sohbet, hediye.</summary>
        private IEnumerable<VisualElement> Badges(TableSummary t, bool full)
        {
            const string nb = "rgba(255,255,255,.08)", nf = "var(--ic)";
            if (full) yield return Badge(t.Type == "pairs" ? S("esli") : S("tekli"), t.Type == "pairs" ? "#FF6B6B" : "#3D7BFF", "#fff", full);
            if (full && t.Private) yield return Badge(S("bPriv"), "rgba(255,201,74,.16)", "var(--a1)", full);
            if (full && t.Locked) yield return Badge(S("bPass"), "rgba(255,201,74,.16)", "var(--a1)", full);
            if (t.Doubling) yield return Badge(S("bKat"), "rgba(139,92,246,.28)", "#D9C8FF", full);
            // R-64: Eşli + Katlamalı shows whose openings raise the bar (katP === false → Yalnız rakip yükseltir).
            if (t.Doubling && t.Type == "pairs") yield return Badge(t.PartnerRaises ? S("bKatP") : S("bKatNP"), "rgba(139,92,246,.16)", "#D9C8FF", full);
            if (full) { yield return Badge(S("bHands", t.Hands), nb, nf, full); yield return Badge(S("bTurn", t.TurnSeconds), nb, nf, full); }
            if (t.OkeyPenalty) yield return Badge(S("bOkey"), "rgba(255,94,108,.14)", "#FFB3BA", full);
            if (t.Penalties) yield return Badge(S("bIsle"), "rgba(255,94,108,.14)", "#FFB3BA", full);
            yield return t.Spectators ? Badge(S("bSpec"), nb, nf, full) : Badge(S("bNoSpec"), nb, "var(--mt)", full);
            if (t.Chat == "quick") yield return Badge(S("bQuick"), nb, nf, full);
            if (t.Chat == "off") yield return Badge(S("bChatOff"), nb, "var(--mt)", full);
            if (!t.Gifts) yield return Badge(S("bNoGift"), nb, "var(--mt)", full);
        }

        /// <summary>
        /// A badge (g): padding 3px 8px, radius 7, 800 11px Baloo. Card badges set line-height 16px (22 px tall); the
        /// summary ids keep the font's own line (24 px).
        /// </summary>
        private VisualElement Badge(string text, string bg, string fg, bool summary)
        {
            var badge = Text(text, 11, "Baloo2-800", fg, summary ? 0 : 16f / 11); badge.style.flexShrink = 0; badge.style.marginRight = 5;
            Pad(badge, 3, 8, 3, 8); view.Decorate(badge, bg, 7); return badge;
        }

        private VisualElement Empty()
        {
            // noCards: column, gap 12, padding 90px 0, lbFade .3s.
            var empty = Box("lobby-empty"); empty.style.alignItems = Align.Center; Pad(empty, 90, 0, 90, 0);
            var icon = Box(); Square(icon, 64); Raise(icon, "var(--pn)", 20, 5, "var(--pnS)"); icon.Add(view.Icon(IconSearch, 28, "var(--mt)")); empty.Add(icon);
            var title = Text(S("emptyT"), 22, "Montserrat-700", "#F4F1E8", 0, "lobby.text.emptyT"); title.style.marginTop = 12; empty.Add(title);
            var sub = Text(S("emptyB"), 14, "Baloo2-700", "var(--mt)", 0, "lobby.text.emptyB");
            Paragraph(sub); sub.style.maxWidth = 380; sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.marginTop = 12; empty.Add(sub);
            var buttons = Row(); buttons.style.marginTop = 12 + 6;
            var clear = Text(S("clearF"), 15, "Montserrat-700", "#F4F1E8", 0, "lobby.resetFilters"); Pad(clear, 11, 18, 11, 18); Raise(clear, "var(--pnH)", 13, 4, "var(--pnS)");
            Tap(clear, () => { typeFilter = "all"; freeFilter = doublingFilter = false; stakeFilter = 0; query = ""; animateCards = true; Render(); }); buttons.Add(clear);
            var open = Text(S("create"), 15, "Montserrat-800", "var(--aT)", 0, "lobby.openCreate#2"); open.style.marginLeft = 10; Pad(open, 11, 20, 11, 20); Raise(open, "linear-gradient(var(--a1),var(--a2))", 13, 4, "var(--aS)");
            Tap(open, OpenCreate); buttons.Add(open); empty.Add(buttons);
            FadeIn(empty, Time.realtimeSinceStartup, .3f);
            return empty;
        }

        private VisualElement Search()
        {
            // 210 × 42, padding 0 6 0 12, gap 8, radius 13, --dp; qRing: focus inset 0 0 0 2px --a1, else inset 0 2px 0 rgba(0,0,0,.3).
            var box = Row(); box.name = "lobby-search"; box.style.width = 210; box.style.height = 42; Pad(box, 0, 6, 0, 12); view.Decorate(box, "var(--dp)", 13);
            var ring = FieldRing(box, 13); ring(queryFocused ? "focus" : "rest");
            box.Add(view.Icon(IconSearch, 16, "var(--mt)"));
            var field = Input("lobby.onQ", query, S("search"), 14, "Baloo2-700", "#F4F1E8", 40);
            field.style.flexGrow = 1; field.style.marginLeft = 8;
            // hasQ: the clear button shows while there is text (it follows the typing, not only redraws); hover --pnH.
            var clear = Box("lobby.clearQ"); clear.pickingMode = PickingMode.Position; Square(clear, 28); Radius(clear, 9); clear.Add(view.Icon(IconClose, 12, "var(--ic)"));
            clear.style.marginLeft = 8;
            clear.RegisterCallback<PointerEnterEvent>(_ => clear.style.backgroundColor = view.C("var(--pnH)")); clear.RegisterCallback<PointerLeaveEvent>(_ => clear.style.backgroundColor = Color.clear);
            clear.style.display = query.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Tap(clear, () => field.value = "");
            field.RegisterValueChangedCallback(e => { query = e.newValue ?? ""; clear.style.display = query.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None; RefreshCards(); });
            field.RegisterCallback<FocusInEvent>(_ => { queryFocused = true; ring("focus"); });
            field.RegisterCallback<FocusOutEvent>(_ => { queryFocused = false; ring("rest"); });
            box.Add(field); box.Add(clear);
            return box;
        }

        /// <summary>A field box's inset ring (<see cref="MenuView.InsetRing"/>).</summary>
        private Action<string> FieldRing(VisualElement box, float radius) => view.InsetRing(box, radius);

        /// <summary>Typing in the search box (or a live list change under an open form) redraws only the cards.</summary>
        private void RefreshCards()
        {
            var scroll = body?.Q<ScrollView>("lobby-list"); if (scroll == null) return;
            scroll.Clear();
            FillCards(scroll, Filter(Listed, backend.Chips), backend.Chips);
        }

        // ---------- actions ----------
        /// <summary>A stake above the balance: lowB(bet, missing) as a bad toast.</summary>
        private void Low(long stake, long chips) => Flash(S("lowB", view.N(stake), view.N(stake - chips)), "bad");

        /// <summary>"Otur" on a table card (the state router's S11 path calls it as the prototype's attempt(t, 'join') does).</summary>
        public void TrySit(TableSummary t) => Sit(t);

        /// <summary>attempt(t, act): a password-protected table asks for its PIN first.</summary>
        private void Sit(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t);
        }

        /// <summary>İzle: the table opens as a spectator's (the server hands it over like a starting match).</summary>
        private void Watch(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t, Watch = true }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t, watch: true);
        }

        /// <summary>Oyuna katıl (R-65): a seat the server plays; the stake is paid then and the table opens mid-hand.</summary>
        private void JoinRunning(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t);
        }

        private bool joining;

        /// <summary>
        /// connect(t, act): "Masaya bağlanılıyor…" for at least 850 ms, then the waiting room — or the full / closed /
        /// PIN / balance answer. The prototype checks a PIN or a code before connecting; here the server does, so the PIN
        /// or code dialog stays up (and shakes) until it answers, and only what it accepts goes on to "bağlanılıyor".
        /// </summary>
        private async Task JoinAsync(TableJoinMessage join, TableSummary table, bool watch = false)
        {
            var origin = dialog?.Kind == "pin" || dialog?.Kind == "code" ? dialog : null;
            if (origin == null) OpenDialog(new Dialog { Kind = "connect", Table = table });
            joining = true;
            string error;
            try { error = watch ? await backend.WatchAsync(join) : await backend.JoinAsync(join); }
            finally { joining = false; }
            if (layer == null) return;
            if (watch && error == ErrorCodes.Validation) error = "E_SPECTATE_OFF";
            string code = error == null ? null : V4Code(error);
            if (origin != null && (code == "E_PIN_WRONG" || origin.Kind == "code" && (code == "E_CODE_UNKNOWN" || code == "E_CODE_FORMAT" || code == "E_TABLE_CLOSED"))) { Fail(error, table, origin, join); return; }
            if (origin != null) OpenDialog(new Dialog { Kind = "connect", Table = table });
            float began = Time.realtimeSinceStartup;
            joining = error == null;
            while (Time.realtimeSinceStartup - began < ConnectSeconds && layer != null) await Task.Yield();
            joining = false;
            if (layer == null) return;
            // A waiting room shows here; a playing table (joined or watched) is handed over through the room state.
            if (error == null)
            {
                if (backend.Room != null) { EnterRoom(); NoteSeats(backend.Room); countdownFrom = backend.Room.Countdown; countdownAt = Time.realtimeSinceStartup; Render(); }
                else if (dialog?.Kind == "connect") CloseDialog();
                return;
            }
            Fail(error, table, origin, join);
        }

        private void Fail(string error, TableSummary table, Dialog origin, TableJoinMessage join)
        {
            switch (V4Code(error))
            {
                case "E_PIN_WRONG":
                    // d.err: the boxes turn red, the PIN clears, lbShake; a code join that needs a PIN asks for it.
                    if (origin?.Kind == "pin") { origin.Error = true; origin.Pin = ""; origin.Shake = Time.realtimeSinceStartup; dialog = origin; RenderDialog(); }
                    else OpenDialog(new Dialog { Kind = "pin", Table = table, Code = join?.Code ?? "", Error = !string.IsNullOrEmpty(join?.Pin) });
                    return;
                case "E_TABLE_FULL": OpenDialog(new Dialog { Kind = "full", Table = table }); return;
                case "E_TABLE_CLOSED": case "E_CODE_UNKNOWN":
                    if (origin?.Kind == "code") { origin.CodeError = S("codeErr"); origin.Shake = Time.realtimeSinceStartup; dialog = origin; RenderDialog(); return; }
                    OpenDialog(new Dialog { Kind = "closed", Table = table }); return;
                case "E_CODE_FORMAT":
                    if (origin?.Kind == "code") { origin.CodeError = S("codeShort"); origin.Shake = Time.realtimeSinceStartup; dialog = origin; RenderDialog(); return; }
                    break;
                // The current server answers İzle on a table that has not started with not_ready (no package code).
                case "E_TABLE_NOT_FULL": CloseDialog(); Flash(L("Bu masada henüz oyun başlamadı", "The game at this table has not started yet"), "info"); return;
                case "E_NO_CHIPS": CloseDialog(); if (table != null) Low(table.Stake, backend.Chips); else Flash(ErrorText(error), "bad"); return;
                case "offline": case "timeout": CloseDialog(); ShowOffline(); return;
            }
            CloseDialog();
            Flash(ErrorText(error) ?? L("Masaya bağlanılamadı (" + error + ")", "Could not join the table (" + error + ")"), "bad");
        }

        /// <summary>similar(t): the closest-stake waiting public table of the same type with a free seat you can afford.</summary>
        private void Similar(TableSummary table)
        {
            long chips = backend.Chips;
            var next = backend.Tables.Where(t => t.Id != table?.Id && !t.Private && t.Type == table?.Type && t.Status != "playing" && t.Seats.Any(s => s == null) && !t.Locked && t.Stake <= chips)
                .OrderBy(t => Math.Abs(t.Stake - (table?.Stake ?? 0))).FirstOrDefault();
            if (next == null) { CloseDialog(); Flash(S("noSimilar"), "info"); return; }
            CloseDialog(); _ = JoinAsync(new TableJoinMessage { TableId = next.Id }, next);
        }

        // ---------- toast (lbToast: top 18, pill) ----------
        public string LastToast { get; private set; }
        public void Flash(string text, string kind)
        {
            if (toastLayer == null) return; LastToast = text;
            toastLayer.Clear();
            // flash(): ok #3CCB6E / #062B14, bad linear-gradient(#FF7A86,#E8455A) / #fff, info rgba(5,8,22,.85) / #F4F1E8.
            bool ok = kind == "good" || kind == "ok";
            string bg = kind == "bad" ? "linear-gradient(#FF7A86,#E8455A)" : ok ? "#3CCB6E" : "rgba(5,8,22,.85)", fg = kind == "bad" ? "#fff" : ok ? "#062B14" : "#F4F1E8";
            var toast = Row(); toast.name = "lobby-toast"; toast.style.position = Position.Absolute; toast.style.top = 18; toast.style.left = 640; Pad(toast, 9, 18, 9, 10);
            view.Decorate(toast, bg, 999); GalaxyFilters.Shadow(toast, 0, 10, 15, new Color(0, 0, 0, .4f));
            var mark = Box(); Square(mark, 24); view.Decorate(mark, "rgba(0,0,0,.18)", 12); var diamond = Box(); diamond.style.width = diamond.style.height = 8; diamond.style.rotate = new Rotate(45); view.Decorate(diamond, fg, 2); mark.Add(diamond); toast.Add(mark);
            var label = Text(text, 14, "Baloo2-800", fg); label.style.marginLeft = 10; toast.Add(label);
            toastLayer.Add(toast);
            float began = Time.realtimeSinceStartup;
            toastStep = () =>
            {
                float t = Time.realtimeSinceStartup - began, k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01(t / .3f)); // animation-timing-function: ease
                // lbToast .3s, then held until the 2.8 s timeout unmounts it (no fade-out).
                toast.style.translate = new Translate(Length.Percent(-50), -12 * (1 - k)); toast.style.opacity = k;
                if (t > 2.8f) { toast.RemoveFromHierarchy(); toastStep = null; }
            };
            Animate(null);
        }

        // ---------- shared pieces ----------
        // Icons by the prototype's SVG (Resources/Galaxy101/Icons/v4, lane D; ids are named after their first use there).
        private const string IconBack = "v4/form-back", IconKey = "v4/lobby-open-code", IconPlus = "v4/lobby-open-create", IconSearch = "v4/lobby-o-pick",
            IconClose = "v4/menu-close-settings", IconEye = "v4/form-field-eye", IconLock = "lock", IconChevron = "v4/menu-sec-cur", IconLeave = "v4/lobby-w-leave",
            IconInvite = "v4/lobby-w-open-inv", IconSeatInvite = "v4/lobby-z-invite", IconInviteHead = "v4/lobby-iv-close", IconCrown = "v4/lobby-z-ini",
            IconBot = "v4/lobby-z-add-bot", IconTrophy = "v4/wait-summary-prize", IconInfo = "v4/lobby-w-share", IconShare = "v4/menu-open-share",
            IconSliders = "v4/lobby-cf-toggle-rules", IconFull = "v4/icon-47", IconClosed = "v4/icon-48", IconCopy = "v4/lobby-d-copy", IconCheck = "v4/form-field-eye-7";

        /// <summary>The chips pill: padding 5px 5px 5px 6px (no "+": 5px 14px 5px 6px), gap 8, --dp, inset 0 2px 0 rgba(0,0,0,.3).</summary>
        private VisualElement Balance(long chips, bool plus)
        {
            var box = Row(); box.name = "lobby-balance"; box.style.marginLeft = 14; Pad(box, 5, plus ? 5 : 14, 5, 6); view.Decorate(box, "var(--dp)", 999);
            view.InsetRing(box, 19)("rest");
            box.Add(Coin(26));
            var amount = Tabular(Text(view.N(chips), 17, "Montserrat-700", "#F4F1E8", 0, plus ? "lobby.chipsLabel" : "lobby.chipsLabel#2")); amount.style.marginLeft = 8; amount.style.minWidth = plus ? 78 : 0; box.Add(amount);
            if (plus)
            {
                var add = Text("+", 20, "Montserrat-700", "#fff", 0, "lobby.buy"); Square(add, 28); add.style.unityTextAlign = TextAnchor.MiddleCenter; add.style.marginLeft = 8;
                Raise(add, "#3CCB6E", 14, 3, "#22914A"); add.tooltip = S("buy"); GalaxyFilters.HoverBrightness(add, 1.12f); Tap(add, () => BuyRequested?.Invoke()); box.Add(add);
            }
            return box;
        }

        private VisualElement Coin(int size, bool dashed = true)
        {
            var coin = Box(); Square(coin, size); view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", size / 2);
            if (dashed && size >= 24) DashedBorder(coin, 2, view.C("#FFF3C8"));
            return coin;
        }

        /// <summary>A segmented control: padding 4, radius 14, --dp (the options' gap is their margin).</summary>
        private VisualElement Segment(int padding)
        {
            var segment = Row(); Pad(segment, padding, padding, padding, padding); view.Decorate(segment, "var(--dp)", 14); return segment;
        }

        /// <summary>seg(): the picked option is gold with a 0 3px 0 --aS ledge, the rest transparent in --mt.</summary>
        private VisualElement SegmentOption(string name, string text, bool on, Action pick, int size, int padY, int padX, int gap, int radius = 10, string font = "Montserrat-700")
        {
            var option = Row(); option.name = name; Pad(option, padY, padX, padY, padX); if (gap > 0) option.style.marginRight = gap;
            Raise(option, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", radius, on ? 3 : 0, "var(--aS)");
            option.Add(Text(text, size, font, on ? "var(--aT)" : "var(--mt)"));
            option.EnableInClassList("selected", on); Tap(option, pick); return option;
        }

        /// <summary>chip(on, key): gap 7, padding 9 14, radius 12; on → --pnH with an inset 2px --a1 ring (no layout), #F4F1E8.</summary>
        private VisualElement Chip(string name, bool on, Action toggle, VisualElement mark, string text)
        {
            float ring = on ? 2 : 0;
            var chip = Row(); chip.name = name; chip.style.marginLeft = 10; Pad(chip, 9 - ring, 14 - ring, 9 - ring, 14 - ring); view.Decorate(chip, on ? "var(--pnH)" : "var(--dp)", 12);
            if (on) Border(chip, 2, view.C("var(--a1)"));
            mark.style.marginRight = 7; chip.Add(mark); chip.Add(Text(text, 14, "Montserrat-700", on ? "#F4F1E8" : "var(--mt)"));
            chip.EnableInClassList("selected", on); Tap(chip, toggle); return chip;
        }

        /// <summary>A round dot; <paramref name="glow"/> is the CSS box-shadow blur (0 0 Npx colour).</summary>
        private VisualElement Dot(string color, int size, float glow)
        {
            var dot = Box(); Square(dot, size); view.Decorate(dot, color, size); if (glow > 0) GalaxyFilters.Shadow(dot, 0, 0, glow / 2, view.C(color)); return dot;
        }

        /// <summary>An "inset 0 1px 0 rgba(255,255,255,a)" top highlight inside a rounded box.</summary>
        private static void TopSheen(VisualElement box, float radius, float alpha)
        {
            var sheen = Box(); sheen.style.position = Position.Absolute; sheen.style.left = sheen.style.top = sheen.style.right = sheen.style.bottom = 0; Radius(sheen, radius);
            sheen.style.borderTopWidth = 1; sheen.style.borderTopColor = new Color(1, 1, 1, alpha); box.Add(sheen);
        }

        /// <summary>An icon drawn inside a box of its own size that clips like the SVG viewport (a path may leave the viewBox).</summary>
        private VisualElement ClippedIcon(string icon, int size, string color)
        {
            var clip = Box(); clip.style.width = clip.style.height = size; clip.style.flexShrink = 0; clip.style.overflow = Overflow.Hidden; clip.Add(view.Icon(icon, size, color)); return clip;
        }

        /// <summary>The CSS "box-shadow: 0 depth 0 shade" ledge (<see cref="GalaxyFilters.HardShadow"/>: no layout).</summary>
        private void Ledge(VisualElement e, float depth, string shade) => GalaxyFilters.HardShadow(e, depth, view.C(shade));
        private void Raise(VisualElement e, string bg, int radius, int depth, string shade) { view.Decorate(e, bg, radius); if (depth > 0) Ledge(e, depth, shade); }

        private string Short(long stake) => stake >= 1000 ? stake / 1000 + "K" : stake.ToString();

        /// <summary>ini(n): the first letters of the first two words (Bot · prefix dropped), or the first two letters.</summary>
        public static string Initials(string name)
        {
            var parts = (name ?? "").Replace("Bot · ", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string first = parts[0].Substring(0, 1), second = parts.Length > 1 ? parts[1].Substring(0, 1) : parts[0].Length > 1 ? parts[0].Substring(1, 1) : "";
            return (first + second).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        }

        private TextField Input(string name, string value, string placeholder, int size, string font, string color, int maxLength)
        {
            var field = new TextField { name = name, value = value, maxLength = maxLength };
            field.style.marginLeft = field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            field.textEdition.placeholder = placeholder; field.textEdition.hidePlaceholderOnFocus = false;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Color.clear; Border(input, 0, Color.clear); Pad(input, 0, 0, 0, 0);
                input.style.unityFont = view.Font(font); input.style.fontSize = size;
            }
            view.InputLook(field, color);
            return field;
        }

        /// <summary>
        /// lbPulse (2.4 s on Masa aç, 2 s on Başlat): box-shadow 0 0 0 0 --gl → 0 0 0 12px transparent → back, ease per half —
        /// a solid ring that spreads out fading, then closes in returning.
        /// </summary>
        private void Pulse(VisualElement element, float period)
        {
            var ring = GalaxyFilters.SpreadRing(element, 12); var glow = view.C("var(--gl)");
            float began = shownAt; // one continuous pulse across redraws
            Animate(() =>
            {
                if (element.parent == null || element.layout.width <= 0) return;
                float p = Mathf.Repeat((Time.realtimeSinceStartup - began) / period, 1);
                float f = MenuView.CssBezier(.25f, .1f, .25f, 1, p < .5f ? p * 2 : (p - .5f) * 2), out_ = p < .5f ? f : 1 - f;
                var c = glow; c.a *= 1 - out_; ring(12 * out_, c);
            }, element);
        }

        /// <summary>lbFade: opacity 0 → 1 over <paramref name="seconds"/> from <paramref name="began"/>.</summary>
        private void FadeIn(VisualElement element, float began, float seconds)
        {
            if (Time.realtimeSinceStartup - began >= seconds) return;
            element.style.opacity = 0;
            Animate(() => element.style.opacity = Mathf.Clamp01((Time.realtimeSinceStartup - began) / seconds), element);
        }

        private readonly Dictionary<VisualElement, float> hovers = new Dictionary<VisualElement, float>();
        private float hoverLift(VisualElement e) => hovers.TryGetValue(e, out var v) ? -6 * v : 0;

        /// <summary>Card hover: translate 0 −6 px, scale 1.012 (.55 s cubic-bezier(.22,1,.36,1)), brightness 1.08 (.45 s).</summary>
        private void Hover(VisualElement card)
        {
            bool over = false; float from = 0, began = 0;
            // brightness(1.08) as a faint white sheen over the face (a filter would nest the inner buttons' filters).
            var sheen = Box(); sheen.style.position = Position.Absolute; sheen.style.left = sheen.style.right = sheen.style.top = sheen.style.bottom = 0; Radius(sheen, 20);
            sheen.style.backgroundColor = new Color(1, 1, 1, 0); card.Add(sheen);
            card.RegisterCallback<PointerEnterEvent>(_ => { from = hovers.TryGetValue(card, out var v) ? v : 0; over = true; began = Time.realtimeSinceStartup; });
            card.RegisterCallback<PointerLeaveEvent>(_ => { from = hovers.TryGetValue(card, out var v) ? v : 0; over = false; began = Time.realtimeSinceStartup; });
            float last = -1;
            Animate(() =>
            {
                if (card.parent == null || began == 0) return;
                float k = Ease(Mathf.Clamp01((Time.realtimeSinceStartup - began) / .55f)), v = Mathf.Lerp(from, over ? 1 : 0, k);
                if (Mathf.Abs(v - last) < .001f) return;
                last = v; hovers[card] = v;
                if (!animateCards || card.style.opacity.value >= 1) card.style.translate = new Translate(0, -6 * v);
                card.style.scale = new Scale(Vector2.one * (1 + .012f * v));
                sheen.style.backgroundColor = new Color(1, 1, 1, .045f * v);
            }, card);
        }

        private static float Ease(float u) => 1 - Mathf.Pow(1 - u, 3.2f);

        // ---------- animation: one scheduler for the lobby's layer ----------
        // Element motions are dropped on every redraw; the fade-in and the toast outlive redraws. A motion owned by an
        // element (a card) also goes when that element leaves the panel — a search or live list update redraws only the
        // cards, and their motions and hover states must not pile up behind them.
        private readonly List<(VisualElement Owner, Action Step)> motions = new List<(VisualElement, Action)>();
        private readonly List<(VisualElement Owner, Action Step)> stepping = new List<(VisualElement, Action)>();
        private Action toastStep;
        private IVisualElementScheduledItem ticker;
        private void Animate(Action step, VisualElement owner = null)
        {
            if (step != null) motions.Add((owner, step));
            if (ticker == null && layer != null) ticker = layer.schedule.Execute(Tick).Every(0); // each panel update (frame)
        }
        private void Tick()
        {
            if (layer == null) return;
            layer.style.opacity = Mathf.Clamp01((Time.realtimeSinceStartup - shownAt) / .3f);
            if (motions.RemoveAll(m => m.Owner != null && m.Owner.panel == null) > 0)
                foreach (var gone in hovers.Keys.Where(e => e.panel == null).ToList()) hovers.Remove(gone);
            // A step may redraw (and clear the list): run this tick's snapshot.
            stepping.Clear(); stepping.AddRange(motions);
            foreach (var m in stepping) m.Step();
            toastStep?.Invoke();
        }

        /// <summary>
        /// A CSS text line: no label padding, a box of the font's CSS line height (<see cref="MenuView.CssLineHeight"/> for
        /// "normal", or <paramref name="lineHeight"/> em when the design sets one) with the glyphs centred in it, as a browser
        /// lays them out. <see cref="Paragraph"/> turns it into a free-height paragraph.
        /// </summary>
        private Label Text(string text, int size, string font, string color, float lineHeight = 0, string name = null)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font); label.style.whiteSpace = WhiteSpace.NoWrap; label.pickingMode = PickingMode.Ignore;
            if (name != null) label.name = name;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.height = lineHeight > 0 ? size * lineHeight : MenuView.CssLineHeight(font, size); label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.flexShrink = 0;
            return label;
        }

        /// <summary>A CSS text line at a fractional size (12.5px descriptions).</summary>
        private Label Text(string text, float size, string font, string color, string name)
        {
            var label = Text(text, Mathf.RoundToInt(size), font, color, 0, name); label.style.fontSize = size; label.style.height = MenuView.CssLineHeight(font, size); return label;
        }

        /// <summary>
        /// A wrapping paragraph: as many CSS lines as the text needs at its width, each <paramref name="lineHeight"/> em
        /// or the font's "normal" (<see cref="MenuView.CssLineHeight"/>).
        /// </summary>
        private static void Paragraph(Label label, float lineHeight = 0)
        {
            // A redraw lays the paragraph out at its last height at once: no frame at the single-line height first.
            string key = label.text + "|" + label.style.fontSize.value.value + "|" + lineHeight;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (paragraphHeights.TryGetValue(key, out float known)) label.style.height = known; else label.style.height = StyleKeyword.Auto;
            label.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                float width = label.layout.width; if (float.IsNaN(width) || width <= 0 || string.IsNullOrEmpty(label.text)) return;
                var font = label.resolvedStyle.unityFont; float size = label.resolvedStyle.fontSize;
                float one = label.MeasureTextSize("Ag", 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).y;
                float all = label.MeasureTextSize(label.text, width - label.resolvedStyle.paddingLeft - label.resolvedStyle.paddingRight, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined).y;
                if (one <= 0) return;
                float line = lineHeight > 0 ? size * lineHeight : MenuView.CssLineHeight(font != null ? font.name : null, size);
                float target = Mathf.Max(1, Mathf.Round(all / one)) * line + label.resolvedStyle.paddingTop + label.resolvedStyle.paddingBottom;
                paragraphHeights[key] = target;
                var h = label.style.height;
                if (h.keyword != StyleKeyword.Undefined || Mathf.Abs(h.value.value - target) > .01f) label.style.height = target;
            });
        }
        private static readonly Dictionary<string, float> paragraphHeights = new Dictionary<string, float>();

        /// <summary>font-variant-numeric: tabular-nums — every digit on the font's tabular advance (Montserrat 0.69 em, Baloo 2 0.59 em).</summary>
        private static Label Tabular(Label label)
        {
            string em = label.style.unityFont.value != null && label.style.unityFont.value.name.StartsWith("Baloo") ? "0.59em" : "0.69em";
            label.enableRichText = true;
            label.text = System.Text.RegularExpressions.Regex.Replace(label.text, "[0-9]+", m => "<mspace=" + em + ">" + m.Value + "</mspace>");
            return label;
        }

        private static VisualElement Box(string name = null) { var e = new VisualElement { pickingMode = PickingMode.Ignore }; if (name != null) e.name = name; return e; }
        private static VisualElement Row() { var r = Box(); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0; return r; }
        private static VisualElement Grow() { var g = Box(); g.style.flexGrow = 1; return g; }
        private static VisualElement Fill(string name) { var e = Box(name); e.style.position = Position.Absolute; e.style.left = e.style.top = e.style.right = e.style.bottom = 0; return e; }
        private static void Square(VisualElement e, float size) { e.style.width = e.style.height = size; e.style.flexShrink = 0; e.style.alignItems = Align.Center; e.style.justifyContent = Justify.Center; }
        private static void Pad(VisualElement e, float t, float r, float b, float l)
        {
            // UI Toolkit boxes are border-box: a CSS text line (fixed height from Text) grows by its padding, as in CSS.
            var h = e.style.height;
            if (e is Label && h.keyword == StyleKeyword.Undefined && h.value.unit == LengthUnit.Pixel && h.value.value > 0)
                e.style.height = h.value.value - (e.style.paddingTop.value.value + e.style.paddingBottom.value.value) + t + b;
            e.style.paddingTop = t; e.style.paddingRight = r; e.style.paddingBottom = b; e.style.paddingLeft = l;
        }
        private static void Radius(VisualElement e, float r) => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        private static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = w;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = c;
        }
        /// <summary>
        /// CSS "border: w dashed c" (UI Toolkit has none): a transparent border keeps the box model, dashes painted over it
        /// (Chrome's 3w dash / 2w gap) — round boxes as a dashed circle, others along their rounded outline.
        /// </summary>
        private static void DashedBorder(VisualElement e, float w, Color c)
        {
            Border(e, w, Color.clear);
            float radius = e.style.borderTopLeftRadius.value.value, size = e.style.width.value.value;
            VisualElement dashes = size > 0 && radius * 2 >= size && e.style.height.value.value == size ? new CssDashedCircle(size, w, c) : new DashedOutline(radius, w, c, 3 * w, 2 * w);
            // An absolute child sits in the padding box: reach back over the border.
            dashes.style.left = dashes.style.top = -w; if (!(dashes is CssDashedCircle)) dashes.style.right = dashes.style.bottom = -w;
            e.Add(dashes);
        }
        private static void Tap(VisualElement e, Action action)
        {
            e.pickingMode = PickingMode.Position; e.focusable = true; GalaxyFilters.PressSink(e);
            e.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
        }
    }
}
