using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table chat (T13) and the player card (T07), spec/13 §4–§5. Bot tables are offline: bots answer from the design's
    /// phrase list, and gifts cost nothing.
    /// </summary>
    public sealed partial class TableHud
    {
        public sealed class ChatLine { public int Seat; public string Text, Gift; }

        private readonly List<ChatLine> chat = new List<ChatLine>();
        private readonly List<(float At, Action Do)> scheduled = new List<(float, Action)>();
        private readonly HashSet<int> friendSent = new HashSet<int>();
        private VisualElement chatLog, cardAnchor;
        private ScrollView chatScroll;
        private TextField chatInput;
        private string chatDraft = "";
        private float cardOpenedAt;

        public IReadOnlyList<ChatLine> Chat => chat;
        /// <summary>Messages that arrived while the chat panel was closed (red count on the chat button).</summary>
        public int Unread { get; private set; }
        /// <summary>Opponent whose player card is open (1–3), 0 = none.</summary>
        public int CardSeat { get; private set; }
        /// <summary>Chance a bot answers my message after 1.4 s (spec/13 §5: 60 %).</summary>
        public float BotReplyChance = .6f;
        public System.Random SocialRandom = new System.Random();
        /// <summary>Bot tables: treats, gifts and emoji are free (no balance row, "Ücretsiz" instead of prices).</summary>
        public bool FreeGifts = true;

        /// <summary>Run <paramref name="action"/> after <paramref name="seconds"/> of real time (cancelled with the HUD).</summary>
        public void After(float seconds, Action action) => scheduled.Add((Time.realtimeSinceStartup + seconds, action));

        private void UpdateSocial()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < scheduled.Count; i++)
            {
                if (scheduled[i].At > now) continue;
                var run = scheduled[i].Do; scheduled.RemoveAt(i--); run();
            }
            if (Window != null && cardAnchor != null && CardSeat > 0 && anchors.TryGetValue("a" + CardSeat, out var avatar))
            { cardAnchor.style.left = avatar.style.left; cardAnchor.style.top = avatar.style.top; }
        }

        private bool HandleSocial(string command)
        {
            if (command == "chat") { if (Window == "chat") CloseWindow(); else OpenChat(); return true; }
            if (command == "chat-close" || command == "card-close") { CloseWindow(); return true; }
            if (command.StartsWith("card:", StringComparison.Ordinal) && int.TryParse(command.Substring(5), out int seat))
            {
                if (Window == "card" && CardSeat == seat) CloseWindow(); else OpenCard(seat);
                return true;
            }
            return false;
        }

        public void OpenChat()
        {
            Window = "chat"; CardSeat = 0; Unread = 0;
            RenderWindows(false); Render(Current);
        }

        public void OpenCard(int seat)
        {
            if (seat < 1 || seat > 3) return;
            Window = "card"; CardSeat = seat; cardOpenedAt = Time.realtimeSinceStartup;
            RenderWindows(false);
        }

        // ---------- chat ----------
        /// <summary>A chat message: the log, a bubble next to the speaker for 2.8 s ("Sohbet balonları") and the unread count.</summary>
        public void Say(int seat, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            AddLine(new ChatLine { Seat = seat, Text = text });
            if (seat != 0 && Window != "chat") Unread++;
            if (Settings == null || Settings.Bubbles)
            {
                float now = Time.realtimeSinceStartup;
                balloons[seat] = new BalloonInfo { Text = text, Until = now + 2.8f, ShownAt = now, Duration = 2.8f };
            }
            if (view != null) Render(Current);
        }

        /// <summary>System line (gifts): the gift picture and a --a1 sentence.</summary>
        public void SystemLine(string text, string gift = null) => AddLine(new ChatLine { Seat = -1, Text = text, Gift = gift });

        private void AddLine(ChatLine line)
        {
            chat.Add(line); if (chat.Count > 40) chat.RemoveAt(0);
            if (chatLog != null)
            {
                if (chatLog.childCount >= 40) chatLog.RemoveAt(0);
                chatLog.Add(ChatRow(line)); ScrollChatToEnd();
            }
        }

        /// <summary>Send my message (80 characters at most); a bot answers 60 % of the time after 1.4 s.</summary>
        public void SendChat(string text)
        {
            text = (text ?? "").Trim(); if (text.Length == 0) return;
            if (text.Length > 80) text = text.Substring(0, 80);
            Say(0, text);
            if (SocialRandom.NextDouble() < BotReplyChance)
            {
                int seat = 1 + SocialRandom.Next(3); string reply = S("bot." + (1 + SocialRandom.Next(6)));
                After(1.4f, () => Say(seat, reply));
            }
        }

        /// <summary>An opponent's casual line after a discard (16 %), or after finishing.</summary>
        public void BotChatter(int seat, float chance)
        {
            if (seat < 1 || seat > 3 || SocialRandom.NextDouble() >= chance) return;
            Say(seat, S("bot." + (1 + SocialRandom.Next(6))));
        }

        private void ScrollChatToEnd()
        {
            var scroll = chatScroll; if (scroll == null) return;
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, Mathf.Max(0, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height))).ExecuteLater(16);
        }

        /// <summary>T13: 360 px drawer on the left (top 74, bottom 118): header, log, quick phrases, input + send.</summary>
        private void BuildChat()
        {
            var layer = Layer("table-chat-layer"); layer.pickingMode = PickingMode.Ignore; windowLayer.Add(layer);
            var dim = Layer("table-chat-dim"); dim.pickingMode = PickingMode.Position; dim.style.backgroundColor = new Color(0, 0, 0, .2f); Tap(dim, CloseWindow); layer.Add(dim);
            var panel = new VisualElement { name = "table-chat", pickingMode = PickingMode.Position };
            panel.style.position = Position.Absolute; panel.style.left = 16; panel.style.top = 74; panel.style.bottom = 118; panel.style.width = 360;
            view.Decorate(panel, "var(--pn)", 26, 8, "var(--pnS)"); Border(panel, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(panel, 0, 30, 40, new Color(0, 0, 0, .6f));
            panel.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            DrawerIn(panel, -1); layer.Add(panel);

            var head = Row(12); Pad(head, 16, 16, 10, 16); head.style.flexShrink = 0;
            var badge = Box(); badge.style.width = badge.style.height = 38; badge.style.alignItems = Align.Center; badge.style.justifyContent = Justify.Center;
            view.Decorate(badge, "linear-gradient(160deg,#7AA6FF,#3D6BF0)", 12, 3, "#2448B8"); badge.Add(view.Icon("table-chat", 20, "#fff")); head.Add(badge);
            var title = Text(S("chatTitle"), "Montserrat-700", 20, "#F4F1E8"); title.style.flexGrow = 1; title.style.flexShrink = 1; head.Add(title);
            var close = Box("chat-close"); close.style.width = close.style.height = 36; close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            view.Decorate(close, "var(--pnH)", 12, 3, "var(--pnS)"); GalaxyFilters.HoverBrightness(close, 1.25f); close.Add(view.Icon("close", 15, "var(--ic)"));
            Tap(close, CloseWindow); head.Add(close);
            panel.Add(head);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "table-chat-log", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0; Pad(scroll.contentContainer, 6, 16, 6, 16);
            panel.Add(scroll); chatScroll = scroll; chatLog = scroll.contentContainer;
            foreach (var line in chat) chatLog.Add(ChatRow(line));
            ScrollChatToEnd();

            var quick = Box("table-chat-quick"); quick.style.flexDirection = FlexDirection.Row; quick.style.flexWrap = Wrap.Wrap; quick.style.flexShrink = 0; Pad(quick, 10, 10, 4, 16);
            for (int i = 0; i < 6; i++)
            {
                string phrase = S("quick." + i);
                var chip = Text(phrase, "Baloo2-800", 12, "var(--ic)"); chip.name = "chat-quick-" + i; Pad(chip, 6, 11, 6, 11); chip.style.marginRight = 6; chip.style.marginBottom = 6;
                view.Decorate(chip, "var(--dp)", 14); GalaxyFilters.HoverBrightness(chip, 1.3f);
                Tap(chip, () => SendChat(phrase)); quick.Add(chip);
            }
            panel.Add(quick);

            var inputRow = Row(8); Pad(inputRow, 0, 16, 16, 16); inputRow.style.flexShrink = 0;
            var input = new TextField { name = "chat-input", maxLength = 80, value = chatDraft };
            input.style.flexGrow = 1; input.style.flexShrink = 1; input.style.minWidth = 0; input.style.height = 44; input.style.marginLeft = input.style.marginRight = input.style.marginTop = input.style.marginBottom = 0;
            input.textEdition.placeholder = S("chatPh"); input.textEdition.hidePlaceholderOnFocus = false;
            var field = input.Q(className: TextField.inputUssClassName);
            if (field != null)
            {
                view.Decorate(field, "var(--dp)", 14); Border(field, 0, Color.clear); Pad(field, 0, 14, 0, 14);
                field.style.unityFont = view.Font("Baloo2-700"); field.style.fontSize = 14; field.style.color = view.C("#F4F1E8");
                GalaxyFilters.Shadow(field, 0, 2, 0, new Color(0, 0, 0, .3f));
            }
            input.RegisterValueChangedCallback(evt => chatDraft = evt.newValue);
            input.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.character != '\n') return;
                evt.StopPropagation(); SubmitDraft();
            }, TrickleDown.TrickleDown);
            inputRow.Add(input); chatInput = input;
            var send = Box("chat-send"); send.style.width = send.style.height = 44; send.style.flexShrink = 0; send.style.alignItems = Align.Center; send.style.justifyContent = Justify.Center;
            view.Decorate(send, "linear-gradient(var(--a1),var(--a2))", 14, 4, "var(--aS)"); send.Add(new SendArrow(view.C("var(--aT)")));
            Tap(send, SubmitDraft); inputRow.Add(send);
            panel.Add(inputRow);
        }

        private void SubmitDraft()
        {
            string text = chatDraft; chatDraft = "";
            if (chatInput != null) chatInput.SetValueWithoutNotify("");
            SendChat(text);
            chatInput?.Focus();
        }

        /// <summary>Opponent rows on the left (coloured avatar, name, dark bubble), mine mirrored with the theme gradient, system lines in --a1.</summary>
        private VisualElement ChatRow(ChatLine line)
        {
            var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.FlexStart; row.style.marginBottom = 10;
            if (line.Seat < 0)
            {
                var sys = Row(8); sys.style.flexGrow = 1; Pad(sys, 7, 10, 7, 10); view.Decorate(sys, "var(--dp)", 11); sys.name = "chat-system";
                if (line.Gift != null && GiftThumb(line.Gift) is Texture2D thumb) { var img = Box(); img.style.width = img.style.height = 30; img.style.flexShrink = 0; img.style.backgroundImage = thumb; sys.Add(img); }
                var words = Text(line.Text, "Baloo2-800", 12, "var(--a1)"); words.style.whiteSpace = WhiteSpace.Normal; words.style.flexShrink = 1; sys.Add(words);
                row.Add(sys); ApplyGaps(row); return row;
            }
            bool mine = line.Seat == 0; var seat = Seat(line.Seat);
            row.style.flexDirection = mine ? FlexDirection.RowReverse : FlexDirection.Row;
            var avatar = Box(); avatar.style.width = avatar.style.height = 30; avatar.style.flexShrink = 0; avatar.style.alignItems = Align.Center; avatar.style.justifyContent = Justify.Center;
            view.Decorate(avatar, (string)seat["color"], 15); avatar.Add(Text((string)seat["ini"], "Montserrat-700", 11, "#fff"));
            avatar.style.marginLeft = mine ? 9 : 0; avatar.style.marginRight = mine ? 0 : 9; row.Add(avatar);
            var column = Box(); column.style.maxWidth = 240; column.style.flexShrink = 1; column.style.alignItems = mine ? Align.FlexEnd : Align.FlexStart;
            column.Add(Text(mine ? S("you") : (string)seat["name"], "Baloo2-800", 11, (string)seat["color"]));
            var bubble = Text(line.Text, "Baloo2-700", 14, mine ? "var(--aT)" : "#F4F1E8"); bubble.name = "chat-bubble"; bubble.style.whiteSpace = WhiteSpace.Normal; bubble.style.flexShrink = 1; bubble.style.marginTop = 2;
            Pad(bubble, 7, 11, 7, 11); view.Decorate(bubble, mine ? "linear-gradient(var(--a1),var(--a2))" : "var(--dp)", 13);
            column.Add(bubble); row.Add(column);
            return row;
        }

        // ---------- player card (T07) ----------
        /// <summary>290 px card beside the avatar: left of the right seat, right of the left seat, under the top seat.</summary>
        private void BuildCard()
        {
            int k = CardSeat; var seat = Seat(k);
            cardAnchor = Box("card-anchor"); cardAnchor.style.position = Position.Absolute;
            if (anchors.TryGetValue("a" + k, out var avatarAnchor)) { cardAnchor.style.left = avatarAnchor.style.left; cardAnchor.style.top = avatarAnchor.style.top; }
            windowLayer.Add(cardAnchor);
            var card = new VisualElement { name = "player-card-" + k, pickingMode = PickingMode.Position };
            card.style.position = Position.Absolute; card.style.width = 290; Pad(card, 16, 16, 16, 16);
            view.Decorate(card, "var(--pn)", 22, 7, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(card, 0, 26, 30, new Color(0, 0, 0, .55f));
            card.style.translate = k == 1 ? new Translate(new Length(-290 - 50), Length.Percent(-50)) : k == 3 ? new Translate(50, Length.Percent(-50)) : new Translate(-145, 58);
            CardIn(card, k);
            cardAnchor.Add(card);

            var head = Row(12);
            var avatar = Avatar(seat, 58, false, 0, false); avatar.pickingMode = PickingMode.Ignore; head.Add(avatar);
            var names = Box(); names.style.flexGrow = 1; names.style.flexShrink = 1; names.style.minWidth = 0;
            names.Add(Text((string)seat["name"], "Montserrat-700", 18, "#F4F1E8"));
            names.Add(Text((string)seat["rank"] + " · " + L("SV ", "LV ") + (int)seat["lv"], "Baloo2-800", 12, "var(--mt)"));
            head.Add(names);
            var close = Box("card-close"); close.style.width = close.style.height = 34; close.style.flexShrink = 0; close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            view.Decorate(close, "var(--pnH)", 11, 3, "var(--pnS)"); GalaxyFilters.HoverBrightness(close, 1.25f); close.Add(view.Icon("close", 14, "var(--ic)"));
            Tap(close, CloseWindow); head.Add(close);
            card.Add(head);

            var stats = Row(8); stats.style.marginTop = 14; stats.style.alignItems = Align.Stretch;
            foreach (var (value, key) in new[] { (((int)seat["lv"]).ToString(), "stLv"), (L("%" + (int)seat["win"], (int)seat["win"] + "%"), "stWin"), (view.N((int)seat["hands"]), "stHands") })
            {
                var cell = Box(); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.alignItems = Align.Center; Pad(cell, 8, 6, 8, 6); view.Decorate(cell, "var(--dp)", 12);
                cell.Add(Text(value, "Montserrat-700", 16, "#F4F1E8"));
                var label = Text(S(key), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(label, .5f, 10); cell.Add(label);
                stats.Add(cell);
            }
            card.Add(stats);

            var caption = Text(S("quickGift"), "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(caption, 1.4f, 10); caption.style.marginTop = 14; card.Add(caption);
            var quick = Row(6); quick.style.marginTop = 8; quick.style.alignItems = Align.Stretch;
            foreach (var kind in QuickGifts)
            {
                string gift = kind;
                var cell = Box("card-gift-" + gift); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.alignItems = Align.Center; Pad(cell, 4, 2, 6, 2); view.Decorate(cell, "var(--dp)", 12);
                var img = Box(); img.style.width = img.style.height = 46; if (GiftThumb(gift) is Texture2D thumb) img.style.backgroundImage = thumb; cell.Add(img);
                cell.Add(PriceTag(gift, 11));
                GalaxyFilters.HoverBrightness(cell, 1.2f);
                Tap(cell, () => { CloseWindow(); SendGift(gift, k.ToString()); });
                quick.Add(cell);
            }
            card.Add(quick);

            var buttons = Row(8); buttons.style.marginTop = 14; buttons.style.alignItems = Align.Stretch;
            var all = Text(S("allGifts"), "Montserrat-700", 13, "var(--aT)"); all.name = "card-all-gifts"; all.style.flexGrow = 1; all.style.flexBasis = 0; all.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(all, 10, 6, 10, 6); view.Decorate(all, "linear-gradient(var(--a1),var(--a2))", 13, 4, "var(--aS)");
            Tap(all, () => OpenGifts(k.ToString())); buttons.Add(all);
            bool sent = friendSent.Contains(k);
            var friend = Text(S(sent ? "friendSent" : "addFriend"), "Montserrat-700", 13, sent ? "#8EF0B0" : "#F4F1E8"); friend.name = "card-add-friend"; friend.style.flexGrow = 1; friend.style.flexBasis = 0; friend.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(friend, 10, 6, 10, 6); view.Decorate(friend, "var(--pnH)", 13, 4, "var(--pnS)");
            Tap(friend, () => { if (friendSent.Add(k)) RenderWindows(false); }); buttons.Add(friend);
            card.Add(buttons);
            ApplyGaps(card);
        }

        public bool FriendRequested(int seat) => friendSent.Contains(seat);

        /// <summary>Gift price, or "Ücretsiz" on bot tables: 10 px coin + 800 11 px #FFD964.</summary>
        private VisualElement PriceTag(string gift, int size)
        {
            var row = Row(3); row.style.marginTop = 2;
            if (FreeGifts) { row.Add(Text(S("free"), "Baloo2-800", size, "#8EF0B0")); return row; }
            var coin = Box(); coin.style.width = coin.style.height = size - 1; view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", size); row.Add(coin);
            row.Add(Text(view.N(GiftPrice(gift)), "Baloo2-800", size, "#FFD964"));
            return row;
        }

        /// <summary>drawerInL / drawerIn .3s: slide in 44 px from the side with the modal overshoot.</summary>
        private void DrawerIn(VisualElement panel, int side)
        {
            float began = Time.realtimeSinceStartup;
            windowMotions.Add(t =>
            {
                float u = Mathf.Clamp01((t - began) / .3f), k = 1 + 2.70158f * Mathf.Pow(u - 1, 3) + 1.70158f * Mathf.Pow(u - 1, 2);
                panel.style.translate = new Translate(44 * side * (1 - k), 0); panel.style.opacity = Mathf.Clamp01(u * 4);
            });
        }

        private void CardIn(VisualElement card, int seat)
        {
            float began = cardOpenedAt;
            windowMotions.Add(t =>
            {
                float u = Mathf.Clamp01((t - began) / .25f), k = 1 + 2.70158f * Mathf.Pow(u - 1, 3) + 1.70158f * Mathf.Pow(u - 1, 2);
                card.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.95f, 1, k)); card.style.opacity = Mathf.Clamp01(u * 4);
            });
        }

        /// <summary>The send arrow (M4 12l16-7-6 16-3-6.5z, stroke 2.4) drawn with the vector API.</summary>
        private sealed class SendArrow : VisualElement
        {
            private readonly Color color;
            public SendArrow(Color color)
            {
                this.color = color; pickingMode = PickingMode.Ignore; style.width = style.height = 20;
                generateVisualContent += Draw;
            }
            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D; float s = 20f / 24f;
                p.strokeColor = color; p.lineWidth = 2.4f * s; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(new Vector2(4, 12) * s); p.LineTo(new Vector2(20, 5) * s); p.LineTo(new Vector2(14, 21) * s); p.LineTo(new Vector2(11, 14.5f) * s); p.ClosePath(); p.Stroke();
            }
        }
    }
}
