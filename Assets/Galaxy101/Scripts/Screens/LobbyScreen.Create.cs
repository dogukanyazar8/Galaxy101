using System;
using System.Linq;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        /// <summary>Masa aç (S10) draft; defaults per spec/20.</summary>
        public sealed class CreateDraft
        {
            public string Type = "single", Name = "", Visibility = "public", Pin = "", Chat = "on";
            public bool PinOn, Doubling, Spectators = true, Gifts = true, OkeyPenalty = true, Penalties = true, RulesOpen, Tried, Busy;
            public int Hands = 5, Turn = 30;
            public long Stake = 1000, Warn;
            public float WarnAt;
            public int Changed => (Spectators ? 0 : 1) + (Gifts ? 0 : 1) + (OkeyPenalty ? 0 : 1) + (Penalties ? 0 : 1) + (Chat == "on" ? 0 : 1);
        }

        private CreateDraft create;
        private float createShownAt;
        public CreateDraft Draft => create;

        public void OpenCreate()
        {
            long chips = backend.Chips;
            string me = GalaxyNameOrYou();
            create = new CreateDraft { Name = Clip(L(me + "’in masası", me + "’s table"), 24) };
            // Highest stake the balance allows, capped at 5K so a first table stays modest.
            create.Stake = Stakes.Where(s => s <= chips && s <= 5000).DefaultIfEmpty(Stakes[0]).Max();
            createShownAt = Time.realtimeSinceStartup;
            Render();
        }

        private string GalaxyNameOrYou() => (string)view.Model.Profile["name"] ?? L("Oyuncu", "Player");
        private static string Clip(string text, int max) => text.Length > max ? text.Substring(0, max) : text;

        private void CloseCreate() { create = null; Render(); }

        private void BuildCreate()
        {
            var c = create; long chips = backend.Chips;
            var shade = Fill("create-backdrop"); shade.pickingMode = PickingMode.Position; view.Decorate(shade, "rgba(0,0,0,.55)"); Tap(shade, CloseCreate); body.Add(shade);
            var modal = Row(); modal.name = "create-modal"; modal.pickingMode = PickingMode.Position; modal.style.alignItems = Align.Stretch;
            // CSS: a 1060×650 content box inside a 1 px border, centred (the ledge is a box-shadow below it).
            MenuView.Place(modal, 109, 34, 1062, 652); modal.style.overflow = Overflow.Hidden;
            Border(modal, 1, new Color(1, 1, 1, .1f)); Raise(modal, "var(--pn)", 28, 10, "var(--pnS)"); GalaxyFilters.Shadow(modal, 0, 40, 45, new Color(0, 0, 0, .6f));
            modal.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Pop(modal, createShownAt, .3f);
            body.Add(modal);

            // Left: header + scrolling rows.
            var left = Box(); left.style.flexGrow = 1; left.style.flexBasis = 0; modal.Add(left);
            var head = Row(); Pad(head, 22, 28, 10, 28);
            var icon = Box(); Square(icon, 50); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 16, 4, "var(--aS)"); icon.Add(view.Icon("plus", 24, "var(--aT)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 14;
            words.Add(Text(L("Masa aç", "Open a table"), 26, "Montserrat-700", "#F4F1E8", 1.1f));
            var sub = Text(L("Klasik · 101 · masayı açınca otomatik oturursun", "Classic · 101 · you sit down automatically"), 13, "Baloo2-700", "var(--mt)"); sub.style.marginTop = 2; words.Add(sub);
            head.Add(words); left.Add(head);
            var scroll = view.Scroll(false); scroll.name = "create-scroll"; scroll.style.flexGrow = 1; scroll.contentContainer.style.paddingLeft = 28; scroll.contentContainer.style.paddingRight = 22; scroll.contentContainer.style.paddingBottom = 18;
            left.Add(scroll);

            bool nameBad = c.Tried && c.Name.Trim().Length == 0, pinBad = c.PinOn && c.Tried && !Regex.IsMatch(c.Pin, "^\\d{4}$");

            // Oyun modu: Klasik badge + Tekli / Eşli.
            var mode = FormRow(scroll, L("Oyun modu", "Game mode"), L("Girdiğin moda göre; tip seçebilirsin", "Set by the mode you entered; pick a type"), null);
            var klasik = Row(); Pad(klasik, 8, 12, 8, 12); klasik.style.marginRight = 16; view.Decorate(klasik, "var(--sd)", 12);
            var mini = Text("101", 8, "Montserrat-800", "#fff"); Square(mini, 22); mini.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(mini, "#3D7BFF", 7); mini.style.marginRight = 6; klasik.Add(mini);
            klasik.Add(Text(L("Klasik", "Classic"), 14, "Montserrat-700", "#F4F1E8")); mode.Add(klasik);
            mode.Add(Options("create-type-", new[] { ("single", L("Tekli", "Singles")), ("pairs", L("Eşli", "Pairs")) }, c.Type, v => { c.Type = v; Render(); }, 16));

            // Masa adı.
            var nameRow = FormRow(scroll, L("Masa adı", "Table name"), nameBad ? L("Masa adı boş olamaz", "Table name can’t be empty") : L("Lobide ve davette görünür", "Shown in the lobby and invites"), nameBad ? "#FF8A94" : null);
            var nameBox = Row(); nameBox.style.width = 320; nameBox.style.height = 46; Pad(nameBox, 0, 14, 0, 14); view.Decorate(nameBox, "var(--dp)", 14);
            if (nameBad) Border(nameBox, 2, view.C("#FF5E6C"));
            var nameField = Input("create-name", c.Name, "", 16, "Baloo2-700", "#F4F1E8", 24); nameField.style.flexGrow = 1; nameBox.Add(nameField);
            var count = Text(c.Name.Length + "/24", 12, "Baloo2-800", "var(--mt)"); count.style.marginLeft = 10; nameBox.Add(count);
            nameField.RegisterValueChangedCallback(e => { c.Name = Clip(e.newValue ?? "", 24); count.text = c.Name.Length + "/24"; UpdateSummary(); });
            nameField.RegisterCallback<FocusInEvent>(_ => { if (!nameBad) Border(nameBox, 2, view.C("var(--a1)")); });
            nameField.RegisterCallback<FocusOutEvent>(_ => { if (!nameBad) Border(nameBox, 0, Color.clear); });
            nameRow.Add(nameBox);

            // Görünürlük.
            var vis = FormRow(scroll, L("Görünürlük", "Visibility"), c.Visibility == "private" ? L("Lobide görünmez, davet koduyla girilir", "Hidden from the lobby, join by invite code") : L("Lobide listelenir, herkes oturabilir", "Listed in the lobby, anyone can sit"), null);
            vis.Add(Options("create-vis-", new[] { ("public", L("Açık", "Open")), ("private", L("Özel", "Private")) }, c.Visibility, v => { c.Visibility = v; Render(); }, 16));

            // Şifre: toggle + 4 digits.
            var pass = FormRow(scroll, L("Şifre", "Password"), pinBad ? L("4 rakam gir", "Enter 4 digits") : c.PinOn ? L("Girenler bu şifreyi yazacak", "Players must enter this password") : L("İsteğe bağlı · 4 haneli", "Optional · 4 digits"), pinBad ? "#FF8A94" : null);
            if (c.PinOn)
            {
                var pinBox = Row(); pinBox.style.width = 150; pinBox.style.height = 44; pinBox.style.marginRight = 12; Pad(pinBox, 0, 12, 0, 12); view.Decorate(pinBox, "var(--dp)", 13);
                Border(pinBox, 2, view.C(pinBad ? "#FF5E6C" : "var(--bd)"));
                var lockIcon = view.Icon("lock", 15, "var(--a1)"); lockIcon.style.marginRight = 8; pinBox.Add(lockIcon);
                var pin = Input("create-pin", c.Pin, "••••", 18, "Montserrat-700", "#F4F1E8", 4); pin.style.flexGrow = 1; MenuView.Tracking(pin, 6, 18);
                pin.RegisterValueChangedCallback(e => { string digits = Regex.Replace(e.newValue ?? "", "\\D", ""); if (digits.Length > 4) digits = digits.Substring(0, 4); if (digits != e.newValue) pin.SetValueWithoutNotify(digits); c.Pin = digits; UpdateSummary(); });
                pinBox.Add(pin); pass.Add(pinBox);
            }
            pass.Add(Switch("create-pin-toggle", c.PinOn, () => { c.PinOn = !c.PinOn; Render(); }));

            // El sayısı.
            var hands = FormRow(scroll, L("El sayısı", "Hands"), L("Maç bu kadar el sürer, en düşük toplam kazanır", "The match lasts this many hands; lowest total wins"), null);
            hands.Add(Options("create-hands-", TableOptions.HandChoices.Select(h => (h.ToString(), h.ToString())).ToArray(), c.Hands.ToString(), v => { c.Hands = int.Parse(v); Render(); }, 12, 44));

            // Bahis: five tiers (locked above the balance) + warning.
            var betSection = Box(); Pad(betSection, 12, 0, 12, 0); Divider(betSection); scroll.Add(betSection);
            var betHead = Row(); betHead.style.alignItems = Align.FlexEnd;
            betHead.Add(Text(L("Bahis", "Bet"), 16, "Montserrat-700", "#F4F1E8", 1.25f));
            var bal = Text(L("Bakiyen " + view.N(chips), "Balance " + view.N(chips)), 12, "Baloo2-700", "var(--mt)"); bal.style.fontSize = 12.5f; bal.style.height = MenuView.CssLineHeight("Baloo2", 12.5f); bal.style.marginLeft = 10; betHead.Add(bal); betSection.Add(betHead);
            var tiers = Row(); tiers.style.marginTop = 10; tiers.style.alignItems = Align.Stretch;
            for (int i = 0; i < Stakes.Length; i++)
            {
                long stake = Stakes[i]; bool locked = stake > chips, on = stake == c.Stake && !locked;
                float ring = on ? 2 : 1; // an inset box-shadow in CSS: the padding gives the ring's width back
                var tier = Box("create-stake-" + stake); tier.style.flexGrow = 1; tier.style.flexBasis = 0; tier.style.alignItems = Align.Center; Pad(tier, 11 - ring, 6 - ring, 10 - ring, 6 - ring); if (i < Stakes.Length - 1) tier.style.marginRight = 8;
                view.Decorate(tier, on ? "var(--pnH)" : "var(--sd)", 16); tier.style.opacity = locked ? .45f : 1;
                Border(tier, ring, on ? view.C("var(--a1)") : new Color(1, 1, 1, .06f));
                var top = Row(); var coin = Box(); Square(coin, 16); view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 8); top.Add(coin);
                var amount = Tabular(Text(Short(stake), 18, "Montserrat-700", "#F4F1E8")); amount.style.marginLeft = 6; top.Add(amount); tier.Add(top);
                var under = Row(); under.style.marginTop = 4;
                if (locked) { var l = view.Icon("lock", 11, "#FF9AA3"); l.style.marginRight = 4; under.Add(l); }
                under.Add(Text(locked ? L("Yetersiz", "Too high") : view.N(stake * 4), 11, "Baloo2-800", locked ? "#FF9AA3" : "var(--mt)")); tier.Add(under);
                Tap(tier, () => { if (locked) { c.Warn = stake; c.WarnAt = Time.realtimeSinceStartup; } else { c.Stake = stake; c.Warn = 0; } Render(); });
                tiers.Add(tier);
            }
            betSection.Add(tiers);
            bool lowAll = chips < Stakes[0];
            if (c.Warn > 0 || lowAll)
            {
                var warn = Row(); warn.name = "create-warn"; warn.style.marginTop = 10; Pad(warn, 12, 12, 12, 14); view.Decorate(warn, "rgba(255,94,108,.12)", 14); Border(warn, 1, new Color(1, .37f, .42f, .35f));
                var bang = Text("!", 15, "Montserrat-800", "#fff"); Square(bang, 30); bang.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(bang, "#FF5E6C", 10); warn.Add(bang);
                var texts = Box(); texts.style.flexGrow = 1; texts.style.marginLeft = 12;
                texts.Add(Text(L("Bakiyen yetersiz", "Not enough chips"), 14, "Montserrat-700", "#FFC2C8"));
                long need = lowAll ? Stakes[0] - chips : c.Warn - chips;
                var why = Text(lowAll ? L("En düşük bahis için " + view.N(need) + " çip daha gerekiyor.", "You need " + view.N(need) + " more chips for the lowest bet.") : L(view.N(c.Warn) + " bahis için " + view.N(need) + " çip daha gerekiyor.", "You need " + view.N(need) + " more chips for a " + view.N(c.Warn) + " bet."), 13, "Baloo2-700", "#FFC2C8");
                why.style.opacity = .85f; texts.Add(why); warn.Add(texts);
                var buy = Text(L("Çip al", "Get chips"), 14, "Montserrat-800", "#fff"); buy.name = "create-buy"; Pad(buy, 9, 14, 9, 14); Raise(buy, "#3CCB6E", 12, 3, "#22914A"); Tap(buy, () => BuyRequested?.Invoke()); warn.Add(buy);
                betSection.Add(warn);
                float at = c.WarnAt;
                if (at > 0) Animate(() => { float t = (Time.realtimeSinceStartup - at) / .4f; warn.style.translate = new Translate(t >= 1 ? 0 : 8 * Mathf.Sin(t * Mathf.PI * 4) * (1 - t), 0); });
            }

            // Ödül.
            var prize = FormRow(scroll, L("Ödül", "Prize"), c.Type == "pairs" ? L("Kazanan takım alır · kişi başı yarısı", "Winning team takes it · half each") : L("Kazanan alır · masadaki tüm bahisler", "Winner takes all bets at the table"), null);
            var pool = Row(); Pad(pool, 8, 14, 8, 14); view.Decorate(pool, "var(--sd)", 14);
            pool.Add(Text(L("Toplam havuz", "Total pool"), 12, "Baloo2-800", "var(--mt)"));
            var pc = Box(); Square(pc, 18); pc.style.marginLeft = 10; view.Decorate(pc, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 9); pool.Add(pc);
            var pv = Tabular(Text(view.N(c.Stake * 4), 20, "Montserrat-700", "var(--a1)")); pv.name = "create-pool"; pv.style.marginLeft = 10; pool.Add(pv); prize.Add(pool);

            // Katlamalı, tur süresi.
            var kat = FormRow(scroll, L("Katlamalı", "Doubling"), L("Okeyle ya da çiftle bitişte puanlar katlanır", "Scores double when finishing with okey or pairs"), null);
            kat.Add(Switch("create-doubling", c.Doubling, () => { c.Doubling = !c.Doubling; Render(); }));
            var turn = FormRow(scroll, L("Tur süresi", "Turn time"), L("Süre dolunca otomatik oynanır", "Auto-plays when time runs out"), null);
            turn.Add(Options("create-turn-", TableOptions.TurnChoices.Select(t => (t.ToString(), t == 45 ? L("45 sn", "45s") : t.ToString())).ToArray(), c.Turn.ToString(), v => { c.Turn = int.Parse(v); Render(); }, 12));

            // Kural özelleştirme (collapsible).
            var rules = Row(); rules.name = "create-rules"; Pad(rules, 14, 0, 14, 0);
            var ri = Box(); Square(ri, 36); view.Decorate(ri, "var(--sd)", 11); ri.Add(view.Icon("sliders", 18, "var(--ic)")); rules.Add(ri);
            var rw = Box(); rw.style.flexGrow = 1; rw.style.marginLeft = 14;
            rw.Add(Text(L("Kural özelleştirme", "Customize rules"), 16, "Montserrat-700", "#F4F1E8", 1.25f));
            var rs = Text(c.Changed > 0 ? L(c.Changed + " kural değişti", c.Changed + " rules changed") : L("Varsayılan kurallar", "Default rules"), 12, "Baloo2-700", "var(--mt)"); rs.style.fontSize = 12.5f; rs.style.height = MenuView.CssLineHeight("Baloo2", 12.5f); rw.Add(rs); rules.Add(rw);
            var chev = Box(); Square(chev, 30); view.Decorate(chev, "var(--pnH)", 10); chev.Add(view.Icon("chevron-down", 14, "var(--ic)")); chev.style.rotate = new Rotate(c.RulesOpen ? 180 : 0); rules.Add(chev);
            Tap(rules, () => { c.RulesOpen = !c.RulesOpen; Render(); }); scroll.Add(rules);
            if (c.RulesOpen)
            {
                var box = Box("create-rules-box"); Pad(box, 2, 16, 2, 16); box.style.marginBottom = 6; view.Decorate(box, "var(--sd)", 18);
                RuleSwitch(box, "create-rule-okey", L("Okey atma cezası (+101)", "Okey discard penalty (+101)"), L("Okeyi yere atan +101 yazar", "Discarding the okey costs +101"), c.OkeyPenalty, () => c.OkeyPenalty = !c.OkeyPenalty);
                RuleSwitch(box, "create-rule-penalties", L("İşlek taş atma cezası (+101)", "Playable tile penalty (+101)"), L("Masadaki pere işlenebilen taşı atan +101 yazar", "Discarding a tile that fits a set on the table costs +101"), c.Penalties, () => c.Penalties = !c.Penalties);
                RuleOptions(box, "create-rule-spec-", L("İzleyici", "Spectators"), L("Oyun sürerken başkaları izleyebilsin", "Others can watch while you play"), new[] { ("true", L("İzin ver", "Allow")), ("false", L("Verme", "Don’t allow")) }, c.Spectators ? "true" : "false", v => c.Spectators = v == "true");
                RuleOptions(box, "create-rule-chat-", L("Sohbet", "Chat"), L("Masa içi yazışma", "Table messages"), new[] { ("on", L("Açık", "On")), ("quick", L("Yalnız hazır cümleler", "Quick phrases only")), ("off", L("Kapalı", "Off")) }, c.Chat, v => c.Chat = v);
                RuleOptions(box, "create-rule-gifts-", L("İkram ve hediye", "Treats and gifts"), L("Oyuncular birbirine ikram gönderebilsin", "Players can send each other treats"), new[] { ("true", L("Açık", "On")), ("false", L("Kapalı", "Off")) }, c.Gifts ? "true" : "false", v => c.Gifts = v == "true");
                var reset = Text(L("Varsayılana dön", "Reset to default"), 13, "Baloo2-800", "var(--mt)"); reset.name = "create-rules-reset"; Pad(reset, 11, 0, 11, 0);
                Tap(reset, () => { c.Spectators = c.Gifts = c.OkeyPenalty = c.Penalties = true; c.Chat = "on"; Render(); }); box.Add(reset);
                scroll.Add(box);
            }

            // Right: summary.
            var right = Box("create-summary"); right.style.width = 330; Pad(right, 22, 22, 22, 22); view.Decorate(right, "var(--sd)", 0); right.style.borderLeftWidth = 1; right.style.borderLeftColor = new Color(1, 1, 1, .06f);
            modal.Add(right);
            var rh = Row(); rh.style.justifyContent = Justify.SpaceBetween;
            var cap = Text(L("ÖZET", "SUMMARY"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cap, 1.8f, 11); rh.Add(cap);
            var close = Box("create-close"); Square(close, 38); Raise(close, "var(--pnH)", 12, 4, "var(--pnS)"); close.Add(view.Icon("close", 16, "var(--ic)")); close.tooltip = L("Kapat", "Close");
            GalaxyFilters.HoverBrightness(close, 1.25f); Tap(close, CloseCreate); rh.Add(close); right.Add(rh);
            summary = Box(); summary.style.flexGrow = 1; right.Add(summary);
            UpdateSummary();
        }

        private VisualElement summary;

        /// <summary>The right column: name, mode line, BAHİS / HAVUZ / EL / TUR SÜRESİ, badges, balance after, CTA.</summary>
        private void UpdateSummary()
        {
            if (summary == null || create == null) return;
            var c = create; long chips = backend.Chips; summary.Clear();
            var name = Text(c.Name.Trim().Length > 0 ? c.Name : L("Masa adı", "Table name"), 22, "Montserrat-700", "#F4F1E8", 1.15f); name.style.marginTop = 14; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; summary.Add(name);
            var line = Row(); line.style.marginTop = 4;
            if (c.Visibility == "private" || c.PinOn) { var l = view.Icon("lock", 13, "var(--a1)"); l.style.marginRight = 6; line.Add(l); }
            line.Add(Text(L("Klasik · " + (c.Type == "pairs" ? "Eşli" : "Tekli") + " · " + (c.Visibility == "private" ? "Özel" : "Açık"), "Classic · " + (c.Type == "pairs" ? "Pairs" : "Singles") + " · " + (c.Visibility == "private" ? "Private" : "Open")), 13, "Baloo2-800", "var(--mt)"));
            summary.Add(line);
            summary.Add(Facts("var(--pn)", (L("BAHİS", "BET"), view.N(c.Stake)), (L("HAVUZ", "POOL"), view.N(c.Stake * 4)), (L("EL", "HANDS"), c.Hands.ToString()), (L("TUR SÜRESİ", "TURN TIME"), L(c.Turn + " sn", c.Turn + "s"))));
            var badges = Row(); badges.style.flexWrap = Wrap.Wrap; badges.style.marginTop = 14; badges.style.flexShrink = 1;
            foreach (var b in Badges(Preview(c), true)) badges.Add(b);
            summary.Add(badges);
            summary.Add(Grow());
            bool afford = c.Stake <= chips && chips >= Stakes[0];
            var after = Row(); after.style.justifyContent = Justify.SpaceBetween; after.style.marginBottom = 14;
            after.Add(Text(L("Bakiyen", "Balance"), 13, "Baloo2-800", "var(--mt)"));
            after.Add(Text(view.N(chips) + " → " + view.N(Math.Max(0, chips - c.Stake)), 13, "Baloo2-800", afford ? "var(--ic)" : "#FF9AA3")); summary.Add(after);
            bool invalid = c.Tried && (c.Name.Trim().Length == 0 || c.PinOn && !Regex.IsMatch(c.Pin, "^\\d{4}$"));
            string cta = !afford ? L("Yetersiz bakiye", "Not enough chips") : invalid ? L("Eksikleri tamamla", "Fix the highlighted fields") : c.Busy ? "…" : L("Masayı oluştur · " + view.N(c.Stake), "Create table · " + view.N(c.Stake));
            var button = Text(cta, 19, "Montserrat-800", afford ? "var(--aT)" : "var(--mt)"); button.name = "create-submit"; button.style.height = 62; button.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(button, afford ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 18, 5, afford ? "var(--aS)" : "var(--pnS)"); Tap(button, () => _ = SubmitCreate()); summary.Add(button);
            var cancel = Text(L("Vazgeç", "Cancel"), 14, "Baloo2-800", "var(--mt)"); cancel.name = "create-cancel"; cancel.style.alignSelf = Align.Center; cancel.style.marginTop = 14; Tap(cancel, CloseCreate); summary.Add(cancel);
        }

        private TableSummary Preview(CreateDraft c) => new TableSummary
        {
            Type = c.Type, Private = c.Visibility == "private", Locked = c.PinOn, Doubling = c.Doubling, Hands = c.Hands, TurnSeconds = c.Turn,
            Penalties = c.Penalties, OkeyPenalty = c.OkeyPenalty, Processing = true, Spectators = c.Spectators, Chat = c.Chat, Gifts = c.Gifts,
        };

        public TableOptions Options(CreateDraft c) => new TableOptions
        {
            Type = c.Type, Name = c.Name.Trim(), Visibility = c.Visibility, Pin = c.PinOn ? c.Pin : null, Hands = c.Hands, Stake = c.Stake,
            Doubling = c.Doubling, TurnSeconds = c.Turn, Spectators = c.Spectators, Chat = c.Chat, Gifts = c.Gifts, Processing = true, Penalties = c.Penalties, OkeyPenalty = c.OkeyPenalty,
        };

        public async System.Threading.Tasks.Task SubmitCreate()
        {
            var c = create; if (c == null || c.Busy) return;
            long chips = backend.Chips;
            if (c.Stake > chips || chips < Stakes[0]) { c.Warn = c.Stake; c.WarnAt = Time.realtimeSinceStartup; Render(); return; }
            c.Tried = true;
            if (c.Name.Trim().Length == 0 || c.PinOn && !Regex.IsMatch(c.Pin, "^\\d{4}$")) { Render(); return; }
            c.Busy = true; UpdateSummary();
            string error = await backend.CreateAsync(Options(c));
            if (layer == null) return;
            c.Busy = false;
            if (error != null)
            {
                switch (error)
                {
                    case ErrorCodes.InsufficientFunds: c.Warn = c.Stake; c.WarnAt = Time.realtimeSinceStartup; Render(); return;
                    case ErrorCodes.Conflict: Flash(L("Zaten bir masadasın", "You are already at a table"), "bad"); break;
                    case "offline": case "timeout": ShowOffline(); break;
                    default: Flash(L("Masa açılamadı (" + error + ")", "Could not open the table (" + error + ")"), "bad"); break;
                }
                UpdateSummary(); return;
            }
            // The room state may arrive just after the acknowledgement.
            float waitFrom = Time.realtimeSinceStartup;
            while (backend.Room == null && Time.realtimeSinceStartup - waitFrom < 5 && layer != null) await System.Threading.Tasks.Task.Yield();
            if (layer == null) return;
            bool share = c.Visibility == "private"; string pin = c.PinOn ? c.Pin : null;
            create = null; screen = "wait"; roomShownAt = Time.realtimeSinceStartup;
            Render();
            Flash(L("Masan açıldı", "Your table is open"), "good");
            if (share) OpenDialog(new Dialog { Kind = "share", Pin = pin, First = true });
        }

        // ---------- form pieces ----------
        private VisualElement FormRow(VisualElement scroll, string title, string description, string descriptionColor)
        {
            var row = Row(); Pad(row, 12, 0, 12, 0); Divider(row);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginRight = 16;
            words.Add(Text(title, 16, "Montserrat-700", "#F4F1E8", 1.25f));
            var d = Text(description, 12, "Baloo2-700", descriptionColor ?? "var(--mt)"); d.style.fontSize = 12.5f; Paragraph(d); words.Add(d);
            row.Add(words); scroll.Add(row); return row;
        }

        private static void Divider(VisualElement row) { row.style.borderBottomWidth = 1; row.style.borderBottomColor = new Color(1, 1, 1, .07f); }

        private VisualElement Options(string prefix, (string Value, string Label)[] options, string current, Action<string> pick, int padX, int minWidth = 0)
        {
            var segment = Segment();
            for (int i = 0; i < options.Length; i++)
            {
                var (value, label) = options[i]; string chosen = value;
                var option = SegmentOption(prefix + value, label, value == current, () => pick(chosen), 14, 8, padX, i < options.Length - 1 ? 4 : 0);
                // CSS min-width is the content box: the padding comes on top.
                if (minWidth > 0) { option.style.minWidth = minWidth + 2 * padX; option.style.justifyContent = Justify.Center; }
                segment.Add(option);
            }
            return segment;
        }

        private VisualElement Switch(string name, bool on, Action flip, int width = 56, int height = 32)
        {
            var track = Box(name); track.style.width = width; track.style.height = height; track.style.flexShrink = 0;
            view.Decorate(track, on ? "#3CCB6E" : "rgba(255,255,255,.14)", height / 2);
            int knob = height - 6; var dot = Box(); MenuView.Place(dot, on ? width - knob - 3 : 3, 3, knob, knob); Raise(dot, "#fff", knob / 2, 2, "rgba(0,0,0,.25)"); track.Add(dot);
            track.EnableInClassList("on", on); Tap(track, flip); return track;
        }

        private void RuleSwitch(VisualElement box, string name, string title, string description, bool on, Action flip)
        {
            var row = Row(); Pad(row, 11, 0, 11, 0); row.style.borderBottomWidth = 1; row.style.borderBottomColor = new Color(1, 1, 1, .06f);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginRight = 16;
            words.Add(Text(title, 15, "Montserrat-700", "#F4F1E8", 1.25f)); var d = Text(description, 12, "Baloo2-700", "var(--mt)"); Paragraph(d); words.Add(d); row.Add(words);
            row.Add(Switch(name, on, () => { flip(); Render(); }, 52, 30)); box.Add(row);
        }

        private void RuleOptions(VisualElement box, string prefix, string title, string description, (string, string)[] options, string current, Action<string> pick)
        {
            var row = Row(); Pad(row, 11, 0, 11, 0); row.style.borderBottomWidth = 1; row.style.borderBottomColor = new Color(1, 1, 1, .06f);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginRight = 16;
            words.Add(Text(title, 15, "Montserrat-700", "#F4F1E8", 1.25f)); var d = Text(description, 12, "Baloo2-700", "var(--mt)"); Paragraph(d); words.Add(d); row.Add(words);
            var segment = Segment(); Radius(segment, 13);
            for (int i = 0; i < options.Length; i++)
            {
                var (value, label) = options[i]; string chosen = value;
                segment.Add(SegmentOption(prefix + value, label, value == current, () => { pick(chosen); Render(); }, 13, 7, 12, i < options.Length - 1 ? 4 : 0));
            }
            row.Add(segment); box.Add(row);
        }

        /// <summary>Two-column fact tiles (BAHİS / HAVUZ / EL / TUR SÜRESİ).</summary>
        private VisualElement Facts(string bg, params (string Key, string Value)[] facts)
        {
            var grid = Box(); grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; grid.style.marginTop = 14; grid.style.marginBottom = -8; grid.style.justifyContent = Justify.SpaceBetween;
            foreach (var (key, value) in facts)
            {
                var tile = Box(); tile.style.width = Length.Percent(48.5f); tile.style.marginBottom = 8; Pad(tile, 10, 12, 10, 12); view.Decorate(tile, bg, 14);
                var k = Text(key, 10, "Baloo2-800", "var(--mt)"); MenuView.Tracking(k, 1.2f, 10); tile.Add(k);
                var v = Text(value, 16, "Montserrat-700", "#F4F1E8", 1.2f); v.style.marginTop = 2; tile.Add(v); grid.Add(tile);
            }
            return grid;
        }

        /// <summary>lbModal: from translateY(16) scale(.96) and transparent, with a slight overshoot.</summary>
        private void Pop(VisualElement modal, float began, float seconds)
        {
            if (Time.realtimeSinceStartup - began >= seconds) return;
            modal.style.opacity = 0;
            Animate(() =>
            {
                float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / seconds); if (u >= 1 && modal.style.opacity.value >= 1) return;
                float k = 1 + 2.2f * Mathf.Pow(u - 1, 3) + 1.2f * Mathf.Pow(u - 1, 2);
                modal.style.opacity = Mathf.Clamp01(u * 2); modal.style.translate = new Translate(0, 16 * (1 - k)); modal.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.96f, 1, k));
            });
        }
    }
}
