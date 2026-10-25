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
    /// Klasik lobby (spec/20, S09–S15; reference G101-Lobi): the live table list with filters, Masa aç, PIN / code / connecting /
    /// full / closed / share dialogs, the waiting room and the invite modal. Drawn over the menu canvas (the menu's main layer
    /// hides meanwhile); every table action goes to the server through <see cref="ILobbyBackend"/>.
    /// </summary>
    public sealed partial class LobbyScreen
    {
        public static readonly long[] Stakes = TableOptions.StakeChoices;
        private readonly MenuView view;
        private readonly ILobbyBackend backend;
        private VisualElement layer, body, dialogLayer, toastLayer;
        private string screen = "list";
        // Filters (S09).
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
        /// <summary>Minimum time the "Masaya bağlanılıyor…" dialog stays (spec/20: 850 ms); tests shorten it.</summary>
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
            backend.TablesChanged += OnTables; backend.RoomChanged += OnRoom; backend.ChipsChanged += OnChips;
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

        private void ShowOffline()
        {
            OpenDialog(new Dialog { Kind = "offline" });
        }

        public void Hide()
        {
            if (layer == null) return;
            backend.TablesChanged -= OnTables; backend.RoomChanged -= OnRoom; backend.ChipsChanged -= OnChips;
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

        private void OnChips() { Render(); }

        private void OnRoom(TableStateMessage state)
        {
            if (layer == null || state == null) return;
            if (state.Closed)
            {
                if (state.MatchId != null) { MatchStarted?.Invoke(state); return; }
                screen = "list"; invite = null; inviteOpen = false; seen.Clear(); animateCards = true; CloseDialog();
                switch (state.Reason)
                {
                    case "kicked": Flash(L("Oda sahibi seni masadan çıkardı", "The host removed you from the table"), "bad"); break;
                    case "insufficient_funds": Flash(L("Bakiyen yetmediği için masadan kalktın", "You left the table: not enough chips"), "bad"); break;
                    case "left": if (!leaving) Flash(L("Masa kapandı", "The table closed"), "bad"); break;
                    case "offline": Flash(L("Bağlantı koptu, masadan kalktın", "Connection lost: you left the table"), "bad"); break;
                }
                leaving = false; Render(); return;
            }
            bool entering = screen != "wait";
            if (entering) { screen = "wait"; roomShownAt = Time.realtimeSinceStartup; if (dialog?.Kind == "connect") CloseDialog(); }
            NoteSeats(state);
            countdownFrom = state.Countdown; countdownAt = Time.realtimeSinceStartup;
            Render();
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

        private void BuildList()
        {
            long chips = backend.Chips;
            // Top bar (86 px): back, 101 badge, Klasik + subtitle, balance, Kodla katıl, Masa aç.
            var bar = Row(); MenuView.Place(bar, 0, 0, 1280, 86); Pad(bar, 0, 28, 0, 28); body.Add(bar);
            var back = Box("lobby-back"); Square(back, 46); Raise(back, "var(--pnH)", 14, 4, "var(--pnS)"); back.Add(view.Icon("back", 20, "var(--ic)"));
            GalaxyFilters.HoverBrightness(back, 1.25f); Tap(back, Back); bar.Add(back);
            var badge = Box(); Square(badge, 52); badge.style.marginLeft = 14; Raise(badge, "#3D7BFF", 16, 4, "#2148B0");
            badge.Add(Text("101", 17, "Montserrat-800", "#fff")); bar.Add(badge);
            var titles = Box(); titles.style.marginLeft = 14;
            titles.Add(Text(L("Klasik", "Classic"), 26, "Montserrat-700", "#F4F1E8"));
            titles.Add(Text(L("Tekli ve eşli masalar · 101 Okey", "Singles and pairs tables · 101 Okey"), 13, "Baloo2-700", "var(--mt)")); bar.Add(titles);
            bar.Add(Grow());
            bar.Add(Balance(chips, true));
            var code = Row(); code.name = "lobby-code"; code.style.height = 46; code.style.marginLeft = 14; Pad(code, 0, 18, 0, 18); Raise(code, "var(--pnH)", 14, 4, "var(--pnS)");
            var key = view.Icon("key", 18, "var(--ic)"); key.style.marginRight = 8; code.Add(key); code.Add(Text(L("Kodla katıl", "Join by code"), 15, "Montserrat-700", "#F4F1E8"));
            GalaxyFilters.HoverBrightness(code, 1.2f); Tap(code, OpenCode); bar.Add(code);
            var open = Row(); open.name = "lobby-create"; open.style.height = 46; open.style.marginLeft = 14; Pad(open, 0, 20, 0, 20); Raise(open, "linear-gradient(var(--a1),var(--a2))", 14, 5, "var(--aS)");
            var plus = view.Icon("plus", 18, "var(--aT)"); plus.style.marginRight = 8; open.Add(plus); open.Add(Text(L("Masa aç", "Open table"), 16, "Montserrat-800", "var(--aT)"));
            GalaxyFilters.HoverBrightness(open, 1.07f); Tap(open, OpenCreate); bar.Add(open);
            Pulse(open);

            // Filters (54 px): type segment with counts, Boş yer var, Katlamalı, stake segment, search.
            var visible = backend.Tables.Where(t => !t.Private || Mine(t)).ToList();
            var filters = Row(); MenuView.Place(filters, 0, 86, 1280, 54); Pad(filters, 0, 28, 0, 28); body.Add(filters);
            var types = Segment();
            foreach (var (value, label, count) in new[] { ("all", L("Tümü", "All"), visible.Count), ("single", L("Tekli", "Singles"), visible.Count(t => t.Type == "single")), ("pairs", L("Eşli", "Pairs"), visible.Count(t => t.Type == "pairs")) })
            {
                string pick = value;
                var option = SegmentOption("lobby-type-" + value, label, typeFilter == value, () => { typeFilter = pick; animateCards = true; Render(); }, 14, 8, 14);
                var n = Tabular(Text(count.ToString(), 11, "Baloo2-800", typeFilter == value ? "var(--aT)" : "var(--mt)")); n.style.marginLeft = 7; Pad(n, 1, 6, 1, 6);
                n.style.minWidth = 14 + 12; n.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(n, "rgba(0,0,0,.2)", 7); option.Add(n);
                types.Add(option);
            }
            if (types.childCount > 0) types[types.childCount - 1].style.marginRight = 0;
            filters.Add(types);
            filters.Add(Chip("lobby-free", freeFilter, () => { freeFilter = !freeFilter; animateCards = true; Render(); }, Dot("#3CCB6E", 8), L("Boş yer var", "Has free seat")));
            var x2 = Text("×2", 12, "Montserrat-800", "#C2A4FF");
            filters.Add(Chip("lobby-doubling", doublingFilter, () => { doublingFilter = !doublingFilter; animateCards = true; Render(); }, x2, L("Katlamalı", "Doubling")));
            var stakes = Segment(); stakes.style.marginLeft = 10;
            foreach (long stake in new long[] { 0 }.Concat(Stakes))
            {
                long pick = stake;
                stakes.Add(SegmentOption("lobby-stake-" + stake, stake == 0 ? L("Tümü", "All") : Short(stake), stakeFilter == stake, () => { stakeFilter = pick; animateCards = true; Render(); }, 13, 8, 11, 3));
            }
            if (stakes.childCount > 0) stakes[stakes.childCount - 1].style.marginRight = 0;
            filters.Add(stakes);
            filters.Add(Grow());
            filters.Add(Search());

            // Cards (3 columns) or the empty state.
            var list = Filter(visible, chips);
            var scroll = view.Scroll(); scroll.name = "lobby-list"; scroll.style.position = Position.Absolute; scroll.style.left = 0; scroll.style.right = 0; scroll.style.top = 146; scroll.style.bottom = 0;
            scroll.contentContainer.style.paddingTop = 6; scroll.contentContainer.style.paddingBottom = 26; scroll.contentContainer.style.paddingLeft = 28; scroll.contentContainer.style.paddingRight = 22;
            scroll.pickingMode = PickingMode.Position; body.Add(scroll);
            if (list.Count == 0) { scroll.Add(Empty()); animateCards = false; return; }
            VisualElement row = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 3 == 0) { row = Row(); row.style.alignItems = Align.Stretch; row.style.marginBottom = 16; scroll.Add(row); }
                var card = Card(list[i], chips, i); card.style.flexGrow = 1; card.style.flexBasis = 0; if (i % 3 != 2) card.style.marginRight = 16; row.Add(card);
            }
            for (int pad = list.Count % 3; pad != 0 && pad < 3; pad++) { var spacer = Box(); spacer.style.flexGrow = 1; spacer.style.flexBasis = 0; if (pad < 2) spacer.style.marginRight = 16; row.Add(spacer); }
            animateCards = false;
        }

        private bool Mine(TableSummary t) => t.Seats.Any(s => s?.Player != null && s.Player.Id == backend.Me);

        /// <summary>Filters, then mine first, waiting with a free seat, playing with a free seat, playing, full; cheaper first.</summary>
        public List<TableSummary> Filter(IEnumerable<TableSummary> tables, long chips)
        {
            string q = query.Trim().ToLower(new System.Globalization.CultureInfo("tr-TR"));
            int Rank(TableSummary t) { bool free = Free(t); return Mine(t) ? -1 : t.Status != "playing" && free ? 0 : t.Status == "playing" && free ? 1 : t.Status == "playing" ? 2 : 3; }
            return tables.Where(t => (typeFilter == "all" || t.Type == typeFilter) && (!freeFilter || t.Seats.Any(s => s == null)) && (!doublingFilter || t.Doubling)
                    && (stakeFilter == 0 || t.Stake == stakeFilter) && (q.Length == 0 || (t.Name ?? "").ToLower(new System.Globalization.CultureInfo("tr-TR")).Contains(q)))
                .OrderBy(Rank).ThenBy(t => t.Stake).ToList();
        }

        private VisualElement Card(TableSummary t, long chips, int index)
        {
            bool free = Free(t), playing = t.Status == "playing", low = t.Stake > chips, mine = Mine(t);
            int seated = t.Seats.Count(s => s != null);
            // The 6 px under-edge is a plain box behind the face: a drop-shadow filter here would nest the buttons' own
            // filters inside a filtered card, which UI Toolkit's nested render passes do not survive in a scroll view.
            var card = Box("lobby-card-" + t.Id); card.AddToClassList("lobby-card"); card.pickingMode = PickingMode.Position;
            var under = Box(); under.style.position = Position.Absolute; under.style.left = under.style.right = 0; under.style.top = 6; under.style.bottom = -6; view.Decorate(under, "var(--pnS)", 20); card.Add(under);
            if (mine) { var glow = Box(); glow.style.position = Position.Absolute; glow.style.left = glow.style.right = glow.style.top = glow.style.bottom = -4; Radius(glow, 24); Border(glow, 4, view.C("var(--gl)")); glow.style.opacity = .6f; card.Add(glow); }
            // The ring is an inset box-shadow in CSS (no layout): the padding gives its width back.
            float ring = mine ? 2 : 1;
            var face = Box(); face.style.flexGrow = 1; Pad(face, 15 - ring, 16 - ring, 14 - ring, 16 - ring); view.Decorate(face, "var(--pn)", 20);
            Border(face, ring, mine ? view.C("var(--a1)") : new Color(1, 1, 1, .06f));
            card.Add(face);
            // Entry: lbCard .5s cubic-bezier(.22,1,.36,1), 35 ms per card (max 10); hover lifts 6 px, scales 1.012, brightens 1.08.
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

            var head = Row();
            var name = Text(t.Name, 17, "Montserrat-700", "#F4F1E8", 1.2f); name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; head.Add(name);
            if (t.Locked) { var lockBox = Box(); Square(lockBox, 22); lockBox.style.marginLeft = 8; view.Decorate(lockBox, "rgba(255,201,74,.16)", 7); lockBox.Add(view.Icon("lock", 12, "var(--a1)")); lockBox.tooltip = L("Şifreli masa", "Password protected"); head.Add(lockBox); }
            head.Add(Grow());
            string stText = playing ? L("Oyunda · El " + Math.Max(1, t.Hand) + "/" + t.Hands, "Playing · Hand " + Math.Max(1, t.Hand) + "/" + t.Hands) : free ? L("Bekliyor", "Waiting") : L("Dolu · başlıyor", "Full · starting");
            string stBg = playing ? "rgba(255,201,74,.14)" : free ? "rgba(60,203,110,.15)" : "rgba(255,94,108,.14)", stFg = playing ? "var(--a1)" : free ? "#8EF0B0" : "#FF9AA3";
            var status = Row(); Pad(status, 4, 10, 4, 10); view.Decorate(status, stBg, 999); var sd = Dot(stFg, 7, false); sd.style.marginRight = 6; status.Add(sd); status.Add(Text(stText, 12, "Baloo2-800", stFg)); head.Add(status);
            face.Add(head);

            var money = Row(); money.style.marginTop = 11;
            money.Add(Coin(24));
            var bet = Tabular(Text(view.N(t.Stake), 23, "Montserrat-700", "#F4F1E8", 1)); bet.style.marginLeft = 8; money.Add(bet);
            var meta = Text(L("· " + t.Hands + " el · " + t.TurnSeconds + " sn", "· " + t.Hands + " hands · " + t.TurnSeconds + "s"), 13, "Baloo2-700", "var(--mt)"); meta.style.marginLeft = 8; money.Add(meta);
            money.Add(Grow());
            var type = Text(t.Type == "pairs" ? L("Eşli", "Pairs") : L("Tekli", "Singles"), 12, "Montserrat-800", "#fff"); Pad(type, 4, 10, 4, 10); view.Decorate(type, t.Type == "pairs" ? "#FF6B6B" : "#3D7BFF", 9); money.Add(type);
            face.Add(money);

            var seats = Row(); seats.style.marginTop = 11;
            for (int s = 0; s < 4; s++)
            {
                var seat = s < t.Seats.Count ? t.Seats[s] : null;
                var chip = Box(); Square(chip, 36); if (s > 0) chip.style.marginLeft = 6;
                if (seat?.Player != null)
                {
                    bool me = seat.Player.Id == backend.Me;
                    view.Decorate(chip, seat.Bot ? "#5B6390" : seat.Player.Color ?? "#3D7BFF", 18); Border(chip, 2, view.C(me ? "var(--a1)" : "rgba(255,255,255,.75)"));
                    chip.Add(Text(Initials(seat.Player.Name), 12, "Montserrat-700", "#fff")); chip.tooltip = seat.Player.Name;
                }
                else { view.Decorate(chip, "rgba(0,0,0,.2)", 18); DashedBorder(chip, 2, view.C("var(--bd)")); chip.Add(Text("+", 12, "Montserrat-700", "var(--mt)")); chip.tooltip = L("Boş koltuk", "Empty seat"); }
                seats.Add(chip);
            }
            var occ = Text(seated + "/4", 13, "Baloo2-800", "var(--mt)"); occ.style.marginLeft = 10; seats.Add(occ);
            seats.Add(Grow());
            // Buttons: İzle (playing, spectators on), Masana dön (mine), Otur (waiting, free), Katıl (playing, free), Dolu.
            if (playing && t.Spectators) seats.Add(CardButton("lobby-watch-" + t.Id, L("İzle", "Watch"), "var(--pnH)", "#F4F1E8", "var(--pnS)", true, () => Watch(t)));
            if (mine) seats.Add(CardButton("lobby-return-" + t.Id, L("Odaya dön", "Back to room"), "linear-gradient(var(--a1),var(--a2))", "var(--aT)", "var(--aS)", false, () => { if (backend.Room != null) { screen = "wait"; Render(); } }));
            else if (free && !playing) seats.Add(CardButton("lobby-sit-" + t.Id, L("Otur", "Sit"), low ? "var(--dp)" : "linear-gradient(var(--a1),var(--a2))", low ? "var(--mt)" : "var(--aT)", low ? "rgba(0,0,0,.25)" : "var(--aS)", false, () => { if (low) Low(t.Stake, chips); else Sit(t); }));
            else if (free && playing) seats.Add(CardButton("lobby-join-" + t.Id, L("Katıl", "Join"), low ? "var(--dp)" : "#3CCB6E", low ? "var(--mt)" : "#062B14", low ? "rgba(0,0,0,.25)" : "#22914A", false, () => { if (low) Low(t.Stake, chips); else JoinRunning(t); }));
            else if (!(playing && t.Spectators)) seats.Add(CardButton("lobby-full-" + t.Id, L("Dolu", "Full"), "var(--dp)", "var(--mt)", "rgba(0,0,0,.25)", false, () => { }));
            face.Add(seats);

            var badges = Row(); badges.style.marginTop = 11; badges.style.height = 22; badges.style.overflow = Overflow.Hidden;
            foreach (var b in Badges(t, false)) badges.Add(b);
            face.Add(badges);
            return card;
        }

        private VisualElement CardButton(string name, string text, string bg, string fg, string shade, bool eye, Action go)
        {
            var button = Row(); button.name = name; button.style.marginLeft = 8; Pad(button, 9, 15, 9, 15); view.Decorate(button, bg, 12); Ledge(button, 4, shade);
            if (eye) { var icon = view.Icon("eye", 15, fg); icon.style.marginRight = 6; button.Add(icon); }
            button.Add(Text(text, 14, "Montserrat-800", fg)); GalaxyFilters.HoverBrightness(button, 1.1f); Tap(button, go); return button;
        }

        /// <summary>Table badges (G101-Lobi badges()): ×2 Katlamalı, (full: type, Özel, Şifreli, el, sn), Okey +101, İşlek +101, İzleyici, sohbet, hediye.</summary>
        private IEnumerable<VisualElement> Badges(TableSummary t, bool full)
        {
            const string nb = "rgba(255,255,255,.08)", nf = "var(--ic)";
            if (full) yield return Badge(t.Type == "pairs" ? L("Eşli", "Pairs") : L("Tekli", "Singles"), t.Type == "pairs" ? "#FF6B6B" : "#3D7BFF", "#fff", full);
            if (full && t.Private) yield return Badge(L("Özel", "Private"), "rgba(255,201,74,.16)", "var(--a1)", full);
            if (full && t.Locked) yield return Badge(L("Şifreli", "Password"), "rgba(255,201,74,.16)", "var(--a1)", full);
            if (t.Doubling) yield return Badge(L("×2 Katlamalı", "×2 Doubling"), "rgba(139,92,246,.28)", "#D9C8FF", full);
            if (full) { yield return Badge(L(t.Hands + " el", t.Hands + " hands"), nb, nf, full); yield return Badge(L(t.TurnSeconds + " sn", t.TurnSeconds + "s"), nb, nf, full); }
            if (t.OkeyPenalty) yield return Badge("Okey +101", "rgba(255,94,108,.14)", "#FFB3BA", full);
            if (t.Penalties) yield return Badge(L("İşlek +101", "Playable +101"), "rgba(255,94,108,.14)", "#FFB3BA", full);
            yield return t.Spectators ? Badge(L("İzleyici açık", "Spectators on"), nb, nf, full) : Badge(L("İzleyici yok", "No spectators"), nb, "var(--mt)", full);
            if (t.Chat == "quick") yield return Badge(L("Hazır sohbet", "Quick chat"), nb, nf, full);
            if (t.Chat == "off") yield return Badge(L("Sohbet kapalı", "Chat off"), nb, "var(--mt)", full);
            if (!t.Gifts) yield return Badge(L("Hediye kapalı", "Gifts off"), nb, "var(--mt)", full);
        }

        private VisualElement Badge(string text, string bg, string fg, bool large)
        {
            var badge = Text(text, large ? 12 : 11, "Baloo2-800", fg, large ? 0 : 16f / 11); badge.style.flexShrink = 0; badge.style.marginRight = large ? 6 : 5; badge.style.marginBottom = large ? 6 : 0;
            Pad(badge, large ? 4 : 3, large ? 9 : 8, large ? 4 : 3, large ? 9 : 8); view.Decorate(badge, bg, large ? 8 : 7); return badge;
        }

        private VisualElement Empty()
        {
            var empty = Box("lobby-empty"); empty.style.alignItems = Align.Center; Pad(empty, 90, 0, 90, 0);
            var icon = Box(); Square(icon, 64); Raise(icon, "var(--pn)", 20, 5, "var(--pnS)"); icon.Add(view.Icon("search", 28, "var(--mt)")); empty.Add(icon);
            var title = Text(L("Bu filtrelere uygun masa yok", "No tables match these filters"), 22, "Montserrat-700", "#F4F1E8"); title.style.marginTop = 12; empty.Add(title);
            var sub = Text(L("Filtreleri temizle ya da kendi masanı aç; açık masalar lobide hemen listelenir.", "Clear the filters or open your own table; open tables show up in the lobby right away."), 14, "Baloo2-700", "var(--mt)");
            Paragraph(sub); sub.style.maxWidth = 380; sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.marginTop = 12; empty.Add(sub);
            var buttons = Row(); buttons.style.marginTop = 18;
            var clear = Text(L("Filtreleri temizle", "Clear filters"), 15, "Montserrat-700", "#F4F1E8"); clear.name = "lobby-clear-filters"; Pad(clear, 11, 18, 11, 18); Raise(clear, "var(--pnH)", 13, 4, "var(--pnS)");
            Tap(clear, () => { typeFilter = "all"; freeFilter = doublingFilter = false; stakeFilter = 0; query = ""; animateCards = true; Render(); }); buttons.Add(clear);
            var open = Text(L("Masa aç", "Open table"), 15, "Montserrat-800", "var(--aT)"); open.name = "lobby-empty-create"; open.style.marginLeft = 10; Pad(open, 11, 20, 11, 20); Raise(open, "linear-gradient(var(--a1),var(--a2))", 13, 4, "var(--aS)");
            Tap(open, OpenCreate); buttons.Add(open); empty.Add(buttons);
            return empty;
        }

        private VisualElement Search()
        {
            var box = Row(); box.style.width = 210; box.style.height = 42; Pad(box, 0, 6, 0, 12); view.Decorate(box, "var(--dp)", 13);
            var ring = FieldRing(box, 13); ring(queryFocused ? "focus" : "rest");
            box.Add(view.Icon("search", 16, "var(--mt)"));
            var field = Input("lobby-search", query, L("Masa ara", "Search tables"), 14, "Baloo2-700", "#F4F1E8", 40);
            field.style.flexGrow = 1; field.style.marginLeft = 8;
            // The clear button shows while there is text (it follows the typing, not only redraws); hover: --pnH.
            var clear = Box("lobby-search-clear"); clear.pickingMode = PickingMode.Position; Square(clear, 28); Radius(clear, 9); clear.Add(view.Icon("close", 12, "var(--ic)"));
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

        /// <summary>Typing in the search box redraws only the cards, so the field keeps focus.</summary>
        private void RefreshCards()
        {
            var scroll = body?.Q<ScrollView>("lobby-list"); if (scroll == null) return;
            scroll.Clear(); long chips = backend.Chips;
            var list = Filter(backend.Tables.Where(t => !t.Private || Mine(t)), chips);
            if (list.Count == 0) { scroll.Add(Empty()); return; }
            VisualElement row = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 3 == 0) { row = Row(); row.style.alignItems = Align.Stretch; row.style.marginBottom = 16; scroll.Add(row); }
                var card = Card(list[i], chips, i); card.style.flexGrow = 1; card.style.flexBasis = 0; if (i % 3 != 2) card.style.marginRight = 16; row.Add(card);
            }
            for (int pad = list.Count % 3; pad != 0 && pad < 3; pad++) { var spacer = Box(); spacer.style.flexGrow = 1; spacer.style.flexBasis = 0; if (pad < 2) spacer.style.marginRight = 16; row.Add(spacer); }
        }

        // ---------- actions ----------
        private void Low(long stake, long chips) => Flash(L("Yetersiz bakiye · " + view.N(stake - chips) + " çip eksik", "Not enough chips · " + view.N(stake - chips) + " short"), "bad");

        /// <summary>"Otur" on a table card (the state router's S11 path calls it as the prototype's attempt(t, 'join') does).</summary>
        public void TrySit(TableSummary t) => Sit(t);

        private void Sit(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t);
        }

        /// <summary>A seat to take: an empty one while waiting; one the server plays (a bot's, or left) while playing.</summary>
        private static bool Free(TableSummary t) => t.Status == "playing" ? t.OpenSeats > 0 : t.Seats.Any(s => s == null);

        /// <summary>İzle: the table opens as a spectator's (the server hands it over like a starting match).</summary>
        private void Watch(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t, Watch = true }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t, watch: true);
        }

        /// <summary>Oyuna katıl: a seat the server plays; the stake is paid and the table opens mid-hand.</summary>
        private void JoinRunning(TableSummary t)
        {
            if (t.Locked) { OpenDialog(new Dialog { Kind = "pin", Table = t }); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = t.Id }, t);
        }

        /// <summary>Connecting dialog (≥ 850 ms), then the waiting room — or the full / closed / PIN / balance answer.</summary>
        private async Task JoinAsync(TableJoinMessage join, TableSummary table, bool watch = false)
        {
            // The PIN dialog stays up while its PIN is checked; a code dialog gives way to "Bağlanıyor" and comes back on an error.
            var origin = dialog?.Kind == "pin" || dialog?.Kind == "code" ? dialog : null;
            var pinDialog = origin?.Kind == "pin" ? origin : null;
            if (pinDialog == null) OpenDialog(new Dialog { Kind = "connect", Table = table });
            float began = Time.realtimeSinceStartup;
            string error = watch ? await backend.WatchAsync(join) : await backend.JoinAsync(join);
            while (Time.realtimeSinceStartup - began < ConnectSeconds && layer != null && pinDialog == null) await Task.Yield();
            if (layer == null) return;
            // A waiting room shows here; a playing table (joined or watched) is handed over through the room state.
            if (error == null) { if (backend.Room != null) { CloseDialog(); screen = "wait"; Render(); } return; }
            Fail(error, table, origin, join);
        }

        private void Fail(string error, TableSummary table, Dialog origin, TableJoinMessage join)
        {
            switch (error)
            {
                case TableErrors.WrongPin:
                    if (origin?.Kind == "pin") { origin.Error = L("Yanlış şifre. Tekrar dene.", "Wrong password. Try again."); origin.Pin = ""; origin.Shake = Time.realtimeSinceStartup; dialog = origin; RenderDialog(); }
                    else OpenDialog(new Dialog { Kind = "pin", Table = table, Code = join.Code ?? "", Error = string.IsNullOrEmpty(join.Pin) ? null : L("Yanlış şifre. Tekrar dene.", "Wrong password. Try again.") });
                    return;
                case TableErrors.Full: OpenDialog(new Dialog { Kind = "full", Table = table }); return;
                case TableErrors.NotReady: CloseDialog(); Flash(L("Bu masada henüz oyun başlamadı", "The game at this table has not started yet"), "info"); return;
                case ErrorCodes.Validation: CloseDialog(); Flash(L("Bu masa izleyiciye kapalı", "This table is closed to spectators"), "bad"); return;
                case TableErrors.Closed:
                    if (origin?.Kind == "code") { origin.Error = L("Bu koda ait açık bir masa bulunamadı.", "No open table found for this code."); origin.Shake = Time.realtimeSinceStartup; dialog = origin; RenderDialog(); return; }
                    OpenDialog(new Dialog { Kind = "closed", Table = table }); return;
                case TableErrors.Playing: CloseDialog(); Flash(L("Bu masada oyun başladı", "The game at this table has started"), "bad"); return;
                case ErrorCodes.InsufficientFunds: CloseDialog(); Flash(L("Bakiyen bu masa için yetersiz", "Not enough chips for this table"), "bad"); return;
                case ErrorCodes.Conflict: CloseDialog(); Flash(L("Zaten bir masadasın", "You are already at a table"), "bad"); return;
                case "offline": case "timeout": CloseDialog(); ShowOffline(); return;
                default: CloseDialog(); Flash(L("Masaya bağlanılamadı (" + error + ")", "Could not join the table (" + error + ")"), "bad"); return;
            }
        }

        /// <summary>Benzer masa bul: the cheapest waiting public table of the same type and stake with a free seat.</summary>
        private void Similar(TableSummary table)
        {
            var next = backend.Tables.Where(t => t.Id != table?.Id && !t.Private && t.Status != "playing" && t.Seats.Any(s => s == null) && t.Type == table?.Type && t.Stake == table?.Stake && !t.Locked).FirstOrDefault();
            if (next == null) { CloseDialog(); Flash(L("Şu an benzer boş masa yok", "No similar free table right now"), "info"); return; }
            CloseDialog(); Sit(next);
        }

        // ---------- toast (lbToast: top 18, pill) ----------
        public string LastToast { get; private set; }
        public void Flash(string text, string kind)
        {
            if (toastLayer == null) return; LastToast = text;
            toastLayer.Clear();
            // G101-Lobi flash(): ok #3CCB6E / #062B14, bad linear-gradient(#FF7A86,#E8455A) / #fff, info rgba(5,8,22,.85) / #F4F1E8.
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
        private VisualElement Balance(long chips, bool plus)
        {
            var box = Row(); box.name = "lobby-balance"; box.style.marginLeft = 14; Pad(box, 5, plus ? 5 : 14, 5, 6); view.Decorate(box, "var(--dp)", 999);
            box.Add(Coin(26));
            var amount = Tabular(Text(view.N(chips), 17, "Montserrat-700", "#F4F1E8")); amount.style.marginLeft = 8; amount.style.minWidth = plus ? 78 : 0; box.Add(amount);
            if (plus)
            {
                var add = Text("+", 20, "Montserrat-700", "#fff"); add.name = "lobby-buy"; Square(add, 28); add.style.unityTextAlign = TextAnchor.MiddleCenter; add.style.marginLeft = 8;
                Raise(add, "#3CCB6E", 14, 3, "#22914A"); add.tooltip = L("Çip satın al", "Buy chips"); GalaxyFilters.HoverBrightness(add, 1.12f); Tap(add, () => BuyRequested?.Invoke()); box.Add(add);
            }
            return box;
        }

        private VisualElement Coin(int size)
        {
            var coin = Box(); Square(coin, size); view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", size / 2);
            if (size >= 24) DashedBorder(coin, 2, view.C("#FFF3C8"));
            return coin;
        }

        private VisualElement Segment()
        {
            var segment = Row(); Pad(segment, 4, 4, 4, 4); view.Decorate(segment, "var(--dp)", 14); return segment;
        }

        private VisualElement SegmentOption(string name, string text, bool on, Action pick, int size, int padY, int padX, int gap = 4)
        {
            var option = Row(); option.name = name; Pad(option, padY, padX, padY, padX); if (gap > 0) option.style.marginRight = gap;
            Raise(option, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", 10, on ? 3 : 0, "var(--aS)");
            option.Add(Text(text, size, "Montserrat-700", on ? "var(--aT)" : "var(--mt)"));
            option.EnableInClassList("selected", on); Tap(option, pick); return option;
        }

        private VisualElement Chip(string name, bool on, Action toggle, VisualElement mark, string text)
        {
            var chip = Row(); chip.name = name; chip.style.marginLeft = 10; Pad(chip, 9, 14, 9, 14); view.Decorate(chip, on ? "var(--pnH)" : "var(--dp)", 12);
            if (on) Border(chip, 2, view.C("var(--a1)"));
            mark.style.marginRight = 7; chip.Add(mark); chip.Add(Text(text, 14, "Montserrat-700", on ? "#F4F1E8" : "var(--mt)"));
            chip.EnableInClassList("selected", on); Tap(chip, toggle); return chip;
        }

        private VisualElement Dot(string color, int size, bool glow = true)
        {
            var dot = Box(); Square(dot, size); view.Decorate(dot, color, size); if (glow) GalaxyFilters.Shadow(dot, 0, 0, size * .75f, view.C(color)); return dot;
        }

        /// <summary>
        /// The "0 Npx 0 shade" button edge as a bottom border. Inside the scrolling card list every element stays filter-free:
        /// UI Toolkit loses filter render textures there once a few dozen are on screen.
        /// </summary>
        /// <summary>The CSS "box-shadow: 0 depth 0 shade" ledge (<see cref="GalaxyFilters.HardShadow"/>: no layout).</summary>
        private void Ledge(VisualElement e, float depth, string shade) => GalaxyFilters.HardShadow(e, depth, view.C(shade));
        private void Raise(VisualElement e, string bg, int radius, int depth, string shade) { view.Decorate(e, bg, radius); if (depth > 0) Ledge(e, depth, shade); }

        private string Short(long stake) => stake >= 1000 ? stake / 1000 + "K" : stake.ToString();

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

        private void Pulse(VisualElement element)
        {
            // lbPulse 2.4 s: box-shadow 0 0 0 0 --gl → 0 0 0 12px transparent → back (ease per half): a solid ring that
            // spreads out fading, then closes in returning.
            var ring = GalaxyFilters.SpreadRing(element, 12); var glow = view.C("var(--gl)");
            float began = shownAt; // one continuous pulse across redraws
            Animate(() =>
            {
                if (element.parent == null || element.layout.width <= 0) return;
                float p = Mathf.Repeat((Time.realtimeSinceStartup - began) / 2.4f, 1);
                float f = MenuView.CssBezier(.25f, .1f, .25f, 1, p < .5f ? p * 2 : (p - .5f) * 2), out_ = p < .5f ? f : 1 - f;
                var c = glow; c.a *= 1 - out_; ring(12 * out_, c);
            }, element);
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
        private Label Text(string text, int size, string font, string color, float lineHeight = 0)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font); label.style.whiteSpace = WhiteSpace.NoWrap; label.pickingMode = PickingMode.Ignore;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.height = lineHeight > 0 ? size * lineHeight : MenuView.CssLineHeight(font, size); label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.flexShrink = 0;
            return label;
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
