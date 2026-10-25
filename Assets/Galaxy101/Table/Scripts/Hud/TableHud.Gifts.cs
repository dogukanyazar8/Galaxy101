using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    /// <summary>
    /// Treats, gifts and emoji (spec/13 §1–§2, T08–T10): the catalogue from table_content.json, the right-hand gift drawer
    /// and the send rules (system line in the chat, the receiver's thanks). On bot tables everything is free. The 3D
    /// flights listen to <see cref="GiftSent"/>.
    /// </summary>
    public sealed partial class TableHud
    {
        public sealed class GiftInfo { public string Key, Tr, En, TrShort, EnShort; public int Tab, Price; }

        private List<GiftInfo> giftCatalog;
        private string[] quickGifts, emojis;
        private readonly Dictionary<string, Texture2D> giftThumbs = new Dictionary<string, Texture2D>();

        /// <summary>Recipient in the drawer: "1"–"3" or "all"; the gift button reopens it with the last one (default Ayşe).</summary>
        public string GiftTarget { get; private set; } = "2";
        public int GiftTab { get; private set; }
        /// <summary>Selected item: a gift key or "e:" + emoji.</summary>
        public string GiftSelection { get; private set; }

        /// <summary>A gift or emoji leaves <c>from</c> for <c>to</c> (seat); the 3D flight and the received card hang on it.</summary>
        public event Action<string, int, int> GiftSent;
        /// <summary>
        /// A server table: treats and emoji go to the server, which shows them at every seat — mine included — through
        /// <see cref="BotGift"/> / <see cref="ShowEmote"/>; nothing is simulated (no thanks, no replies). Null at a bot table.
        /// </summary>
        public Func<string, int?, System.Threading.Tasks.Task<string>> SendGiftOnline;
        public Func<string, System.Threading.Tasks.Task<string>> SendEmoteOnline;
        /// <summary>A server table's chat (text, or a ready phrase's index) and friend request (local seat).</summary>
        public Func<string, int?, System.Threading.Tasks.Task<string>> SendChatOnline;
        public Func<int, System.Threading.Tasks.Task<string>> SendFriendOnline;
        private bool OnlineTable => SendGiftOnline != null;

        public IReadOnlyList<GiftInfo> Gifts { get { LoadGifts(); return giftCatalog; } }
        private IEnumerable<string> QuickGifts { get { LoadGifts(); return quickGifts; } }
        public IReadOnlyList<string> Emojis { get { LoadGifts(); return emojis; } }

        private void LoadGifts()
        {
            if (giftCatalog != null) return;
            var content = JObject.Parse(Resources.Load<UnityEngine.TextAsset>("Galaxy101/Table/table_content").text);
            giftCatalog = content["gifts"].Select(g => new GiftInfo { Key = (string)g["k"], Tab = (int)g["tab"], Tr = (string)g["tr"], En = (string)g["en"], TrShort = (string)g["trS"], EnShort = (string)g["enS"], Price = (int)g["price"] }).ToList();
            quickGifts = content["quickGifts"].Select(x => (string)x).ToArray();
            emojis = content["emoji"].Select(x => (string)x).ToArray();
        }

        public GiftInfo Gift(string key) => Gifts.First(g => g.Key == key);
        private int GiftPrice(string key) => Gift(key).Price;
        private string GiftName(string key, bool shortForm) { var g = Gift(key); return Lang == "en" ? (shortForm ? g.EnShort : g.En) : (shortForm ? g.TrShort : g.Tr); }

        private Texture2D GiftThumb(string key)
        {
            if (!giftThumbs.TryGetValue(key, out var t)) giftThumbs[key] = t = Resources.Load<Texture2D>("Galaxy101/Table/GiftThumbs/" + key);
            return t;
        }

        private bool HandleGifts(string command)
        {
            if (command == "gift") { if (Window == "gift") CloseWindow(); else OpenGifts(GiftTarget); return true; }
            if (command == "gift-close") { CloseWindow(); return true; }
            if (command == "gift-send") { SendSelection(); return true; }
            if (command.StartsWith("gift-to:", StringComparison.Ordinal)) { GiftTarget = command.Substring(8); RenderWindows(true); return true; }
            if (command.StartsWith("gift-tab:", StringComparison.Ordinal)) { GiftTab = int.Parse(command.Substring(9)); GiftSelection = null; RenderWindows(false); return true; }
            if (command.StartsWith("gift-pick:", StringComparison.Ordinal)) { GiftSelection = command.Substring(10); RenderWindows(true); return true; }
            return false;
        }

        public void OpenGifts(string target)
        {
            GiftTarget = target; CardSeat = 0; Window = "gift";
            RenderWindows(false);
        }

        private int TargetCount => GiftTarget == "all" ? 3 : 1;
        private int[] TargetSeats => GiftTarget == "all" ? new[] { 1, 2, 3 } : new[] { int.Parse(GiftTarget) };
        private int SelectionCost => GiftSelection == null || GiftSelection.StartsWith("e:", StringComparison.Ordinal) || FreeGifts ? 0 : GiftPrice(GiftSelection) * TargetCount;
        private bool CanSend => GiftSelection != null && (FreeGifts || Current.Chips >= SelectionCost);

        private void SendSelection()
        {
            if (!CanSend) return;
            string pick = GiftSelection, target = GiftTarget;
            GiftSelection = null; CloseWindow();
            After(.12f, () => Treat(pick, target));
        }

        /// <summary>Every way to treat (the gift drawer, the player card): through the server at a server table.</summary>
        private void Treat(string pick, string target)
        {
            if (OnlineTable) { _ = SendOnlineAsync(pick, target); return; }
            if (pick.StartsWith("e:", StringComparison.Ordinal)) SendEmoji(pick.Substring(2), target); else SendGift(pick, target);
        }

        private async System.Threading.Tasks.Task SendOnlineAsync(string pick, string target)
        {
            bool emoji = pick.StartsWith("e:", StringComparison.Ordinal);
            var error = emoji ? await SendEmoteOnline(pick.Substring(2)) : await SendGiftOnline(pick, target == "all" ? (int?)null : int.Parse(target));
            if (error != null) PostSendError(error);
        }

        private void PostSendError(string error) => Post(ToastKind.Error,
            error == "insufficient_funds" ? L("Çipin yetmedi", "Not enough chips") : error == "rate_limited" ? L("Biraz bekle", "Wait a moment")
            : error == "validation" ? L("Bu masada izin verilmiyor", "Not allowed at this table") : error == "offline" ? L("Bağlantı yok", "No connection")
            : L("Gönderilemedi", "Could not send"));

        /// <summary>A server table's emoji from <paramref name="from"/>: it flies to every other seat.</summary>
        public void ShowEmote(int from, string emoji)
        {
            for (int to = 0, i = 0; to < 4; to++) if (to != from) { int seat = to; After(i++ * .14f, () => FlyEmoji(from, seat, emoji)); }
        }

        /// <summary>
        /// I send a treat or gift: one flight per recipient 180 ms apart, a system line ("Sen, Zeynep için roket
        /// gönderdin"), and a thank-you from one recipient 1.9 s later (rocket 6.3 s).
        /// </summary>
        public void SendGift(string kind, string target)
        {
            var seats = target == "all" ? new[] { 1, 2, 3 } : new[] { int.Parse(target) };
            for (int i = 0; i < seats.Length; i++) { int to = seats[i]; After(i * .18f, () => GiftSent?.Invoke(kind, 0, to)); }
            SystemLine(GiftSentence(0, target == "all" ? -1 : seats[0], kind), kind);
            int thanker = seats[SocialRandom.Next(seats.Length)];
            string thanks = S("botThanks." + SocialRandom.Next(3));
            After(kind == "rocket" ? 6.3f : 1.9f, () => Say(thanker, thanks));
        }

        /// <summary>A bot treats someone (or everyone, seat −1) with an optional line first; spec/13 demo: Ayşe's çay for all.</summary>
        public void BotGift(int from, string kind, int to, string message = null)
        {
            if (message != null) Say(from, message);
            var seats = to < 0 ? Enumerable.Range(0, 4).Where(k => k != from).ToArray() : new[] { to };
            for (int i = 0; i < seats.Length; i++) { int seat = seats[i]; After(i * .18f, () => GiftSent?.Invoke(kind, from, seat)); }
            SystemLine(GiftSentence(from, to, kind), kind);
        }

        public void SendEmoji(string emoji, string target)
        {
            var seats = target == "all" ? new[] { 1, 2, 3 } : new[] { int.Parse(target) };
            for (int i = 0; i < seats.Length; i++) { int to = seats[i]; After(i * .14f, () => { FlyEmoji(0, to, emoji); GiftSent?.Invoke("e:" + emoji, 0, to); }); }
        }

        // ---------- received (T06) ----------
        private readonly List<string> received = new List<string>();
        private VisualElement socialLayer, notice;
        private (string Kind, int From)? noticeInfo;
        private float noticeUntil;

        /// <summary>Treats that reached me this hand (listed on the hand result).</summary>
        public IReadOnlyList<string> Received => received;
        /// <summary>The open "SANA İKRAM VAR" card, if any: gift and sender.</summary>
        public (string Kind, int From)? Notice => noticeInfo;
        /// <summary>Emoji flights in the air (2D layer).</summary>
        public int EmojiFlights { get; private set; }

        /// <summary>New hand: gifts were cleared from the table, the received list starts again.</summary>
        public void NewHand() { received.Clear(); CloseNotice(); }

        /// <summary>A gift landed; mine are listed and, with "İkram bildirimleri" on, shown on the card for 5.2 s.</summary>
        public void GiftLanded(string kind, int from, int to)
        {
            if (to != 0) return;
            received.Add(kind);
            if (Settings != null && !Settings.GiftNotices) return;
            noticeInfo = (kind, from); noticeUntil = Time.realtimeSinceStartup + 5.2f;
            BuildNotice();
        }

        /// <summary>"Teşekkür et": my bubble, then a 🙏 flies to the sender (at a server table both go through the server).</summary>
        public void Thank()
        {
            if (noticeInfo == null) return;
            int from = noticeInfo.Value.From; CloseNotice();
            if (OnlineTable) { _ = ThankOnlineAsync(); return; }
            Say(0, S("thanksMsg"));
            After(.3f, () => FlyEmoji(0, from, "🙏"));
        }

        private async System.Threading.Tasks.Task ThankOnlineAsync()
        {
            var error = await SendEmoteOnline("🙏");
            if (error != null) { PostSendError(error); return; }
            // A quick-phrase or chat-off room refuses the words; the 🙏 has already said it.
            await SendChatOnline(S("thanksMsg"), null);
        }

        private void CloseNotice() { noticeInfo = null; notice?.RemoveFromHierarchy(); notice = null; }

        private void UpdateNotice() { if (noticeInfo != null && Time.realtimeSinceStartup > noticeUntil) CloseNotice(); }

        /// <summary>T06: right 18, top 78 — picture, SANA İKRAM VAR, "Ayşe sana çay ısmarladı", Teşekkür et.</summary>
        private void BuildNotice()
        {
            if (socialLayer == null || noticeInfo == null) return;
            notice?.RemoveFromHierarchy();
            var (kind, from) = noticeInfo.Value;
            var card = Row(12); card.name = "gift-notice"; card.pickingMode = PickingMode.Position;
            card.style.position = Position.Absolute; card.style.right = 18; card.style.top = 78; Pad(card, 8, 10, 8, 8);
            view.Decorate(card, "var(--pn)", 20, 6, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(card, 0, 18, 20, new Color(0, 0, 0, .5f));
            var well = Box(); well.style.width = well.style.height = 56; well.style.alignItems = Align.Center; well.style.justifyContent = Justify.Center; view.Decorate(well, "var(--dp)", 14);
            var img = Box(); img.style.width = img.style.height = 52; if (GiftThumb(kind) is Texture2D thumb) img.style.backgroundImage = thumb; well.Add(img); card.Add(well);
            var words = Box(); words.style.maxWidth = 200; words.style.flexShrink = 1;
            var cap = Text(S("recvTitle"), "Baloo2-800", 10, "var(--a1)"); MenuView.Tracking(cap, 1.4f, 10); words.Add(cap);
            var line = Text(GiftSentence(from, 0, kind), "Montserrat-700", 14, "#F4F1E8"); line.name = "gift-notice-text"; line.style.whiteSpace = WhiteSpace.Normal; line.style.flexShrink = 1; line.style.marginTop = 2; words.Add(line);
            card.Add(words);
            var thanks = Text(S("thanks"), "Montserrat-700", 13, "var(--aT)"); thanks.name = "gift-notice-thanks"; Pad(thanks, 10, 14, 10, 14);
            view.Decorate(thanks, "linear-gradient(var(--a1),var(--a2))", 13, 4, "var(--aS)"); Tap(thanks, Thank); card.Add(thanks);
            ApplyGaps(card);
            float began = Time.realtimeSinceStartup;
            motions2.Add(t =>
            {
                float u = Mathf.Clamp01((t - began) / .35f), k = 1 + 2.70158f * Mathf.Pow(u - 1, 3) + 1.70158f * Mathf.Pow(u - 1, 2);
                card.style.translate = new Translate(44 * (1 - k), 0); card.style.opacity = Mathf.Clamp01(u * 4);
                return u < 1;
            });
            notice = card; socialLayer.Add(card);
        }

        // ---------- emoji flights (spec/12 §5) ----------
        /// <summary>Animations on the social layer (notice, emoji); each returns false when finished.</summary>
        private readonly List<Func<float, bool>> motions2 = new List<Func<float, bool>>();

        /// <summary>Where a seat's avatar is on the 1280×720 canvas (the flight's start and end).</summary>
        private Vector2 AvatarCanvasPoint(int seat)
        {
            VisualElement e = seat == 0 ? hudLayer.Q<VisualElement>("table-me")?.Children().FirstOrDefault() : anchorLayer.Q<VisualElement>("opponent-" + seat);
            if (e == null) return new Vector2(Width / 2, Height / 2);
            return canvas.WorldToLocal(e.worldBound.center);
        }

        /// <summary>1.7 s curved flight: over both avatars (−120 px), 0.4 → 1.3 (−10°) → 1.7 (+6°) → 1.35 → rises and fades.</summary>
        public void FlyEmoji(int from, int to, string emoji)
        {
            if (socialLayer == null || Settings != null && !Settings.EmojiEffects) return;
            Vector2 a = AvatarCanvasPoint(from), b = AvatarCanvasPoint(to), m = new Vector2((a.x + b.x) / 2, Mathf.Min(a.y, b.y) - 120);
            var holder = Box("emoji-flight"); holder.style.position = Position.Absolute; holder.style.left = 0; holder.style.top = 0;
            var label = EmojiLabel(emoji, 44); label.style.position = Position.Absolute; label.style.left = -28; label.style.top = -28; label.style.width = label.style.height = 56;
            GalaxyFilters.Shadow(label, 0, 6, 5, new Color(0, 0, 0, .45f));
            holder.Add(label); socialLayer.Add(holder); EmojiFlights++;
            var keys = new[] { (0f, a, .4f, 0f, 0f), (.42f, m, 1.3f, -10f, 1f), (.7f, new Vector2(b.x, b.y - 18), 1.7f, 6f, 1f), (.82f, new Vector2(b.x, b.y - 26), 1.35f, 0f, 1f), (1f, new Vector2(b.x, b.y - 70), 1.1f, 0f, 0f) };
            float began = Time.realtimeSinceStartup;
            motions2.Add(t =>
            {
                float u = CubicBezier(.33f, .8f, .4f, 1, Mathf.Clamp01((t - began) / 1.7f));
                int i = 0; while (i < keys.Length - 2 && u > keys[i + 1].Item1) i++;
                var k0 = keys[i]; var k1 = keys[i + 1]; float f = Mathf.InverseLerp(k0.Item1, k1.Item1, u);
                var p = Vector2.Lerp(k0.Item2, k1.Item2, f);
                holder.style.translate = new Translate(p.x, p.y); holder.style.scale = new Scale(Vector2.one * Mathf.Lerp(k0.Item3, k1.Item3, f));
                holder.style.rotate = new Rotate(Mathf.Lerp(k0.Item4, k1.Item4, f)); holder.style.opacity = Mathf.Lerp(k0.Item5, k1.Item5, f);
                if (t - began < 1.7f) return true;
                holder.RemoveFromHierarchy(); EmojiFlights--; return false;
            });
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at time <paramref name="x"/> (Newton on x, then y).</summary>
        private static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float cx = 3 * (1 - t) * (1 - t) * t * x1 + 3 * (1 - t) * t * t * x2 + t * t * t - x;
                float dx = 3 * (1 - t) * (1 - t) * x1 + 6 * (1 - t) * t * (x2 - x1) + 3 * t * t * (1 - x2);
                if (Mathf.Abs(cx) < 1e-4f || Mathf.Abs(dx) < 1e-5f) break;
                t = Mathf.Clamp01(t - cx / dx);
            }
            return 3 * (1 - t) * (1 - t) * t * y1 + 3 * (1 - t) * t * t * y2 + t * t * t;
        }

        /// <summary>
        /// Verb rule (spec/13 §3): treats "ısmarladı / ısmarladın", gifts "gönderdi / gönderdin"; "{A}, {B} için {x} …",
        /// "{A} herkese {x} …", "{A} sana {x} …". English: "{A} sent {B} {x}".
        /// </summary>
        public string GiftSentence(int from, int to, string kind)
        {
            bool gift = Gift(kind).Tab == 1, me = from == 0;
            string a = me ? S("you") : SeatFirstName(from), item = GiftName(kind, true);
            if (Lang == "en")
            {
                if (to < 0) return a + " sent everyone " + item;
                return a + " sent " + (to == 0 ? "you" : SeatFirstName(to)) + " " + item;
            }
            string verb = gift ? (me ? "gönderdin" : "gönderdi") : (me ? "ısmarladın" : "ısmarladı");
            if (to < 0) return a + " herkese " + item + " " + verb;
            if (to == 0) return a + " sana " + item + " " + verb;
            return a + ", " + SeatFirstName(to) + " için " + item + " " + verb;
        }

        // ---------- drawer (T08–T10) ----------
        private void BuildGifts()
        {
            var layer = Layer("table-gift-layer"); windowLayer.Add(layer);
            var dim = Layer("table-gift-dim"); dim.pickingMode = PickingMode.Position; dim.style.backgroundColor = new Color(0, 0, 0, .28f); Tap(dim, CloseWindow); layer.Add(dim);
            var panel = new VisualElement { name = "table-gifts", pickingMode = PickingMode.Position };
            panel.style.position = Position.Absolute; panel.style.right = 16; panel.style.top = 74; panel.style.bottom = 16; panel.style.width = 396; panel.style.overflow = Overflow.Hidden;
            view.Decorate(panel, "var(--pn)", 26, 8, "var(--pnS)"); Border(panel, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(panel, 0, 30, 40, new Color(0, 0, 0, .6f));
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            DrawerIn(panel, 1); layer.Add(panel);

            var head = Row(12); Pad(head, 18, 18, 12, 18); head.style.flexShrink = 0;
            var badge = Box(); badge.style.width = badge.style.height = 40; badge.style.alignItems = Align.Center; badge.style.justifyContent = Justify.Center;
            view.Decorate(badge, "linear-gradient(160deg,var(--a1),var(--a2))", 13, 3, "var(--aS)"); badge.Add(view.Icon("table-gift", 22, "var(--aT)")); head.Add(badge);
            var title = Text(S("giftTitle"), "Montserrat-700", 22, "#F4F1E8"); title.style.flexGrow = 1; title.style.flexShrink = 1; head.Add(title);
            var close = Box("gift-close"); close.style.width = close.style.height = 38; close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            view.Decorate(close, "var(--pnH)", 12, 3, "var(--pnS)"); GalaxyFilters.HoverBrightness(close, 1.25f); close.Add(view.Icon("close", 16, "var(--ic)"));
            Tap(close, CloseWindow); head.Add(close);
            panel.Add(head);

            // KİME: three opponents and "Herkes ×3".
            var to = Box(); Pad(to, 0, 18, 0, 18); to.style.flexShrink = 0;
            var toCaption = Text(S("to"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(toCaption, 1.6f, 10); to.Add(toCaption);
            var recipients = Row(6); recipients.style.marginTop = 8; recipients.style.alignItems = Align.Stretch;
            foreach (var key in new[] { "1", "2", "3", "all" })
            {
                bool on = GiftTarget == key, all = key == "all"; string pick = key;
                var cell = Box("gift-to-" + key); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0; cell.style.alignItems = Align.Center; Pad(cell, 8, 4, 8, 4);
                view.Decorate(cell, on ? "var(--pnH)" : "var(--dp)", 14); Border(cell, 2, on ? view.C("var(--a1)") : Color.clear); cell.EnableInClassList("selected", on);
                var disc = Box(); disc.style.width = disc.style.height = 36; disc.style.alignItems = Align.Center; disc.style.justifyContent = Justify.Center;
                view.Decorate(disc, all ? "linear-gradient(160deg,var(--a1),var(--a2))" : (string)Seat(int.Parse(key))["color"], 18); Border(disc, 2, new Color(1, 1, 1, .25f));
                disc.Add(Text(all ? "★" : (string)Seat(int.Parse(key))["ini"], "Montserrat-700", 13, "#fff")); cell.Add(disc);
                var name = Text(all ? S("all") + " ×3" : SeatFirstName(int.Parse(key)), "Baloo2-800", 11, on ? "#F4F1E8" : "var(--mt)"); name.style.marginTop = 5; cell.Add(name);
                if (!on) GalaxyFilters.HoverBrightness(cell, 1.15f);
                Tap(cell, () => { GiftTarget = pick; RenderWindows(true); }); recipients.Add(cell);
            }
            to.Add(recipients); panel.Add(to);

            // İkram · Hediye · Emoji.
            var tabs = Row(4); tabs.name = "gift-tabs"; Pad(tabs, 4, 4, 4, 4); tabs.style.marginTop = 14; tabs.style.marginLeft = tabs.style.marginRight = 18; tabs.style.flexShrink = 0; view.Decorate(tabs, "var(--dp)", 14);
            for (int i = 0; i < 3; i++)
            {
                bool on = GiftTab == i; int index = i;
                var tab = Text(S("tabs." + i), "Montserrat-700", 14, on ? "var(--aT)" : "var(--mt)"); tab.name = "gift-tab-" + i; tab.style.flexGrow = 1; tab.style.flexBasis = 0; tab.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(tab, 9, 6, 9, 6);
                view.Decorate(tab, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", 11, on ? 3 : 0, on ? "var(--aS)" : null);
                Tap(tab, () => { GiftTab = index; GiftSelection = null; RenderWindows(false); }); tabs.Add(tab);
            }
            panel.Add(tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "gift-items", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0; Pad(scroll.contentContainer, 14, 18, 14, 18);
            panel.Add(scroll);
            var grid = CardGrid(3);
            var items = GiftTab < 2 ? Gifts.Where(g => g.Tab == GiftTab).Select(g => g.Key) : Emojis.Select(e => "e:" + e);
            foreach (var item in items)
            {
                bool on = GiftSelection == item, emoji = item.StartsWith("e:", StringComparison.Ordinal); string pick = item;
                var cell = Box("gift-item-" + (emoji ? "emoji-" + Array.IndexOf(emojis, item.Substring(2)) : item)); cell.style.alignItems = Align.Center; Pad(cell, 8, 6, 10, 6);
                view.Decorate(cell, on ? "var(--pnH)" : "var(--dp)", 16); Border(cell, 2, on ? view.C("var(--a1)") : Color.clear); cell.EnableInClassList("selected", on);
                if (on) GalaxyFilters.Shadow(cell, 0, 0, 9, view.C("var(--gl)")); else GalaxyFilters.HoverBrightness(cell, 1.15f);
                var art = Box(); art.style.width = 82; art.style.height = 72; art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center;
                if (emoji) art.Add(EmojiLabel(item.Substring(2), 44));
                else { var img = Box(); img.style.width = img.style.height = 80; img.style.marginTop = -6; if (GiftThumb(item) is Texture2D thumb) img.style.backgroundImage = thumb; art.Add(img); }
                cell.Add(art);
                if (!emoji) { var name = Text(GiftName(item, false), "Montserrat-700", 13, "#F4F1E8"); name.style.marginTop = 4; cell.Add(name); }
                var price = emoji ? Row(4) : PriceTag(item, 12); price.style.marginTop = 4;
                if (emoji) price.Add(Text(S("free"), "Baloo2-800", 12, "#8EF0B0"));
                cell.Add(price);
                Tap(cell, () => { GiftSelection = pick; RenderWindows(true); });
                AddCell(grid, cell);
            }
            scroll.Add(grid);
            if (GiftSelection != null) { float y = giftScrollY; scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, y)); }
            scroll.verticalScroller.valueChanged += v => giftScrollY = v;

            // Footer: balance (hidden on bot tables) and Gönder.
            var footer = Row(12); footer.style.flexShrink = 0; Pad(footer, 14, 18, 18, 18); footer.style.borderTopWidth = 1; footer.style.borderTopColor = new Color(1, 1, 1, .07f);
            footer.style.backgroundColor = view.C("var(--sd)");
            var left = Box(); left.style.flexGrow = 1; left.style.flexShrink = 1; left.style.minWidth = 0;
            if (!FreeGifts)
            {
                var cap = Text(S("balance"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(cap, 1.4f, 10); left.Add(cap);
                var chips = Row(6); var coin = Box(); coin.style.width = coin.style.height = 16; view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 8); chips.Add(coin);
                chips.Add(Text(view.N(Current.Chips), "Montserrat-700", 17, "#F4F1E8")); left.Add(chips);
            }
            footer.Add(left);
            bool ok = CanSend;
            string label = GiftSelection == null ? S("pick") : !ok ? S("notEnough") : SelectionCost > 0 ? S("send") + " · " + view.N(SelectionCost) : S("send");
            var send = Text(label, "Montserrat-800", 16, ok ? "#05301A" : "var(--mt)"); send.name = "gift-send"; Pad(send, 14, 20, 14, 20);
            view.Decorate(send, ok ? "linear-gradient(#5BE38A,#2BB45F)" : "var(--pnH)", 16, 5, ok ? "#18793F" : "var(--pnS)");
            if (ok) Tap(send, SendSelection);
            footer.Add(send);
            panel.Add(footer);
        }

        private float giftScrollY;

        // ---------- emoji ----------
        private static FontAsset emojiFont;
        private static bool emojiFontTried;

        /// <summary>
        /// Colour emoji from the platform's own emoji font (Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji), loaded at
        /// run time and never shipped. Without one the emoji is drawn with the UI font.
        /// </summary>
        private Label EmojiLabel(string emoji, int size)
        {
            var label = new Label(emoji) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = size; label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.color = Color.white;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            var font = EmojiFont();
            if (font != null) label.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            return label;
        }

        private static FontAsset EmojiFont()
        {
            if (emojiFontTried) return emojiFont;
            emojiFontTried = true;
            try
            {
                string path = Font.GetPathsToOSFonts().FirstOrDefault(p =>
                {
                    string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    return n == "seguiemj" || n.Contains("coloremoji") || n == "apple color emoji" || n.Contains("notocoloremoji");
                });
                if (path == null) return null;
                var font = new Font(path);
                emojiFont = FontAsset.CreateFontAsset(font, 90, 6, GlyphRenderMode.COLOR, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (emojiFont != null) emojiFont.name = "Emoji (" + System.IO.Path.GetFileName(path) + ")";
            }
            catch (Exception ex) { Debug.LogWarning("Colour emoji font unavailable: " + ex.Message); emojiFont = null; }
            return emojiFont;
        }
    }
}
