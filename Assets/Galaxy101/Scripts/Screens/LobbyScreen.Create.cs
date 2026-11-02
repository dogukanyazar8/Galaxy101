using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        /// <summary>Masa aç (S10): the prototype's create state (openCreate defaults; R0 rules; katP on, R-64).</summary>
        public sealed class CreateDraft
        {
            public string Type = "single", Name = "", Visibility = "public", Pin = "", Chat = "on";
            public bool PinOn, Doubling, PartnerRaises = true, Spectators = true, Gifts = true, OkeyPenalty = true, Penalties = true, RulesOpen, Tried, Busy, NameFocused;
            public int Hands = 5, Turn = 30;
            public long Stake = 5000, Warn;
            public float WarnAt;
            /// <summary>Rules that differ from R0 (okey, isle, spec, chat, gifts).</summary>
            public int Changed => (Spectators ? 0 : 1) + (Gifts ? 0 : 1) + (OkeyPenalty ? 0 : 1) + (Penalties ? 0 : 1) + (Chat == "on" ? 0 : 1);
        }

        private CreateDraft create;
        private float createShownAt;
        public CreateDraft Draft => create;
        /// <summary>The PIN of the table you opened (the share dialog shows it; tables carry only "Locked").</summary>
        private string roomPin;

        /// <summary>openCreate(): type from the type filter, "‹first name›’in masası", 5K if affordable (else the highest affordable, else 1K with the warning).</summary>
        public void OpenCreate()
        {
            long chips = backend.Chips;
            string first = (GalaxyNameOrYou().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
            var affordable = Stakes.Where(s => s <= chips).ToList();
            create = new CreateDraft
            {
                Type = typeFilter == "pairs" ? "pairs" : "single",
                Name = Clip(En ? first + "'s table" : Genitive(first) + " masası", 24),
                Stake = affordable.Contains(5000) ? 5000 : affordable.Count > 0 ? affordable.Last() : Stakes[0],
            };
            if (affordable.Count == 0) { create.Warn = Stakes[0]; create.WarnAt = Time.realtimeSinceStartup; }
            createShownAt = Time.realtimeSinceStartup;
            Render();
        }

        private string GalaxyNameOrYou() => (string)view.Model.Profile["name"] ?? L("Oyuncu", "Player");
        private static string Clip(string text, int max) => text.Length > max ? text.Substring(0, max) : text;

        private void CloseCreate() { create = null; Render(); }

        private void BuildCreate()
        {
            var c = create; long chips = backend.Chips;
            // createOpen: z 20; the backdrop lobby.closeCreate (rgba(0,0,0,.55) + blur(6px), lbFade .2s) is the persistent veil (Veil()).
            // 1060 × 650 inside a 1 px border, grid minmax(0,1fr) 330px, radius 28, 0 10px 0 --pnS, 0 40px 90px rgba(0,0,0,.6).
            var modal = Row(); modal.name = "lobby-create"; modal.pickingMode = PickingMode.Position; modal.style.alignItems = Align.Stretch;
            MenuView.Place(modal, 109, 34, 1062, 652); modal.style.overflow = Overflow.Hidden;
            Border(modal, 1, new Color(1, 1, 1, .1f)); Raise(modal, "var(--pn)", 28, 10, "var(--pnS)"); GalaxyFilters.BoxShadow(modal, 0, 40, 45, new Color(0, 0, 0, .6f));
            modal.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Pop(modal, createShownAt, .3f, true);
            modalLayer.Add(modal);

            // Left: header (padding 22 28 10, gap 14) and the scrolling rows (padding 0 22 18 28).
            var left = Box(); left.style.flexGrow = 1; left.style.flexBasis = 0; modal.Add(left);
            var head = Row(); Pad(head, 22, 28, 10, 28);
            var icon = Box(); Square(icon, 50); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 16, 4, "var(--aS)"); icon.Add(view.Icon(IconPlus, 24, "var(--aT)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 14;
            words.Add(Text(S("createT"), 26, "Montserrat-700", "#F4F1E8", 1.1f, "lobby.text.createT"));
            var sub = Text(S("createS"), 13, "Baloo2-700", "var(--mt)", 0, "lobby.text.createS"); sub.style.marginTop = 2; words.Add(sub);
            head.Add(words); left.Add(head);
            // Source overflow-y:auto / scrollbar-width:thin reserves the existing 10px scroller when the form overflows.
            var scroll = view.Scroll(false); scroll.name = "lobby-create-scroll"; scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft = 28; scroll.contentContainer.style.paddingRight = 22; scroll.contentContainer.style.paddingBottom = 18;
            left.Add(scroll);

            bool nameBad = c.Tried && c.Name.Trim().Length == 0, pinBad = c.PinOn && c.Tried && !Regex.IsMatch(c.Pin, "^\\d{4}$");

            // Oyun modu: the Klasik pill (padding 8 12, radius 12, --sd, gap 6) and Tekli / Eşli.
            var mode = FormRow(scroll, S("rMode"), "lobby.text.rMode", S("rModeD"), "lobby.text.rModeD", null);
            var klasik = Row(); Pad(klasik, 8, 12, 8, 12); klasik.style.marginRight = 16; view.Decorate(klasik, "var(--sd)", 12);
            var mini = Text("101", 8, "Montserrat-800", "#fff"); Square(mini, 22); mini.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(mini, "#3D7BFF", 7); mini.style.marginRight = 6; klasik.Add(mini);
            klasik.Add(Text(S("classic"), 14, "Montserrat-700", "#F4F1E8")); mode.Add(klasik);
            mode.Add(Options("lobby-create-type", new[] { ("single", S("tekli")), ("pairs", S("esli")) }, c.Type, v => { c.Type = v; Render(); }, 16));

            // Masa adı: 320 × 46, padding 0 14, gap 10, radius 14, --dp; nameRing bad / focus / rest.
            var nameRow = FormRow(scroll, S("rName"), "lobby.text.rName", nameBad ? S("rNameErr") : S("rNameD"), "lobby.cf.nameD", nameBad ? "#FF8A94" : null);
            var nameBox = Row(); nameBox.name = "lobby-create-name"; nameBox.style.width = 320; nameBox.style.height = 46; Pad(nameBox, 0, 14, 0, 14); view.Decorate(nameBox, "var(--dp)", 14);
            var nameRing = FieldRing(nameBox, 14); nameRing(nameBad ? "bad" : c.NameFocused ? "focus" : "rest");
            var nameField = Input("lobby.cf.onName", c.Name, "", 16, "Baloo2-700", "#F4F1E8", 24); nameField.style.flexGrow = 1; nameBox.Add(nameField);
            var count = Tabular(Text(c.Name.Length + "/24", 12, "Baloo2-800", "var(--mt)", 0, "lobby.cf.nameCount")); count.style.marginLeft = 10; nameBox.Add(count);
            nameField.RegisterValueChangedCallback(e => { c.Name = Clip(e.newValue ?? "", 24); count.text = c.Name.Length + "/24"; Tabular(count); UpdateSummary(); });
            nameField.RegisterCallback<FocusInEvent>(_ => { c.NameFocused = true; if (!nameBad) nameRing("focus"); });
            nameField.RegisterCallback<FocusOutEvent>(_ => { c.NameFocused = false; if (!nameBad) nameRing("rest"); });
            nameRow.Add(nameBox);

            // Görünürlük.
            var vis = FormRow(scroll, S("rVis"), "lobby.text.rVis", c.Visibility == "private" ? S("visPrivD") : S("visOpenD"), "lobby.cf.visD", null);
            vis.Add(Options("lobby-create-vis", new[] { ("public", S("visOpen")), ("private", S("visPriv")) }, c.Visibility, v => { c.Visibility = v; Render(); }, 16));

            // Şifre: 150 × 44 field (padding 0 12, gap 8, radius 13, passRing bad / inset 2px --bd) and the switch.
            var pass = FormRow(scroll, S("rPass"), "lobby.text.rPass", pinBad ? S("passErr") : c.PinOn ? S("passOnD") : S("passOffD"), "lobby.cf.passD", pinBad ? "#FF8A94" : null);
            // The PIN field's place is kept while the switch is off, so turning it on changes nothing else in the form
            // (the row's text keeps its width and wrapping, the rows below stay put).
            if (!c.PinOn)
            {
                var slot = Box("lobby-create-pin-slot"); slot.style.width = 150; slot.style.height = 44; slot.style.marginRight = 16; slot.style.flexShrink = 0; slot.style.visibility = Visibility.Hidden; pass.Add(slot);
            }
            else
            {
                var pinBox = Row(); pinBox.name = "lobby-create-pin"; pinBox.style.width = 150; pinBox.style.height = 44; pinBox.style.marginRight = 16; pinBox.style.flexShrink = 0; Pad(pinBox, 0, 12, 0, 12); view.Decorate(pinBox, "var(--dp)", 13);
                FieldRing(pinBox, 13)(pinBad ? "bad" : "line");
                var lockIcon = view.Icon(IconLock, 15, "var(--a1)"); lockIcon.style.marginRight = 8; pinBox.Add(lockIcon);
                var pin = Input("lobby.cf.onPass", c.Pin, "••••", 18, "Montserrat-700", "#F4F1E8", 4); pin.style.flexGrow = 1; MenuView.Tracking(pin, 6, 18);
                pin.RegisterValueChangedCallback(e => { string digits = Regex.Replace(e.newValue ?? "", "\\D", ""); if (digits.Length > 4) digits = digits.Substring(0, 4); if (digits != e.newValue) pin.SetValueWithoutNotify(digits); c.Pin = digits; UpdateSummary(); });
                pinBox.Add(pin); pass.Add(pinBox);
            }
            pass.Add(Switch("lobby.cf.passT.flip", c.PinOn, () => { c.PinOn = !c.PinOn; Render(); }));

            // El sayısı 3–11: the option keeps the source's min-width 44px + padding 8 12, in a swipeable window of three.
            var hands = FormRow(scroll, S("rHands"), "lobby.text.rHands", S("rHandsD"), "lobby.text.rHandsD", null);
            hands.Add(Carousel("lobby-create-hands", TableOptions.HandChoices.Select(h => (h.ToString(), h.ToString())).ToArray(), c.Hands.ToString(), v => { c.Hands = int.Parse(v); Render(); }, 68));

            // Bahis: column, gap 10, padding 12 0; the head aligns on the baseline; five tiers, then the warning.
            var betSection = Box(); Pad(betSection, 12, 0, 12, 0); Divider(betSection, .07f); scroll.Add(betSection);
            var betHead = Row(); betHead.style.alignItems = Align.FlexStart;
            betHead.Add(Text(S("rBet"), 16, "Montserrat-700", "#F4F1E8", 1.25f, "lobby.text.rBet"));
            // align-items: baseline puts the 12.5px line 2 px below the 16px one (the head is 22 tall).
            var bal = Text(S("bal", view.N(chips)), 12.5f, "Baloo2-700", "var(--mt)", "lobby.cf.balLine"); bal.style.marginLeft = 10; bal.style.marginTop = 2; betHead.Add(bal); betSection.Add(betHead);
            var tiers = Row(); tiers.name = "lobby-create-tiers"; tiers.style.marginTop = 10; tiers.style.alignItems = Align.Stretch;
            for (int i = 0; i < Stakes.Length; i++)
            {
                long stake = Stakes[i]; bool locked = stake > chips, on = stake == c.Stake && !locked;
                // The ring is an inset box-shadow in CSS (on: 2px --a1 + 0 0 18px --gl; off: 1px rgba(255,255,255,.06)): the padding gives it back.
                float ring = on ? 2 : 1;
                var tier = Box("lobby.b.pick[" + i + "]"); tier.style.flexGrow = 1; tier.style.flexBasis = 0; tier.style.alignItems = Align.Center; Pad(tier, 11 - ring, 6 - ring, 10 - ring, 6 - ring); if (i < Stakes.Length - 1) tier.style.marginRight = 8;
                view.Decorate(tier, on ? "var(--pnH)" : "var(--sd)", 16); tier.style.opacity = locked ? .45f : 1;
                Border(tier, ring, on ? view.C("var(--a1)") : new Color(1, 1, 1, .06f));
                if (on) GalaxyFilters.Shadow(tier, 0, 0, 9, view.C("var(--gl)"));
                else { var t = tier; tier.RegisterCallback<PointerEnterEvent>(_ => t.style.backgroundColor = view.C("var(--pnH)")); tier.RegisterCallback<PointerLeaveEvent>(_ => t.style.backgroundColor = view.C("var(--sd)")); }
                var top = Row(); top.Add(Coin(16, false));
                var amount = Tabular(Text(Short(stake), 18, "Montserrat-700", "#F4F1E8", 0, "lobby.b.label[" + i + "]")); amount.style.marginLeft = 6; top.Add(amount); tier.Add(top);
                var under = Row(); under.style.marginTop = 4;
                if (locked) { var l = view.Icon(IconLock, 11, "#FF9AA3"); l.style.marginRight = 4; under.Add(l); }
                under.Add(Text(locked ? S("tierLocked") : view.N(stake * 4), 11, "Baloo2-800", locked ? "#FF9AA3" : "var(--mt)")); tier.Add(under);
                Tap(tier, () => { if (locked) { c.Warn = stake; c.WarnAt = Time.realtimeSinceStartup; } else { c.Stake = stake; c.Warn = 0; } Render(); });
                tiers.Add(tier);
            }
            betSection.Add(tiers);
            bool lowAll = chips < Stakes[0];
            if (c.Warn > 0 || lowAll)
            {
                // padding 12 12 12 14, gap 12, radius 14, rgba(255,94,108,.12), inset 0 0 0 1px rgba(255,94,108,.35), lbShake .4s.
                var warn = Row(); warn.name = "lobby-create-warn"; warn.style.marginTop = 10; Pad(warn, 11, 11, 11, 13); view.Decorate(warn, "rgba(255,94,108,.12)", 14); Border(warn, 1, new Color(1, .37f, .42f, .35f));
                var bang = Text("!", 15, "Montserrat-900", "#fff"); Square(bang, 30); bang.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(bang, "#FF5E6C", 10); warn.Add(bang);
                var texts = Box(); texts.style.flexGrow = 1; texts.style.flexShrink = 1; texts.style.marginLeft = 12;
                texts.Add(Text(S("lowT"), 14, "Montserrat-700", "#FFC2C8", 0, "lobby.text.lowT"));
                var why = Text(lowAll ? S("lowAll", view.N(Stakes[0] - chips)) : S("lowB", view.N(c.Warn), view.N(c.Warn - chips)), 13, "Baloo2-700", "#FFC2C8", 0, "lobby.cf.warnText");
                why.style.opacity = .85f; texts.Add(why); warn.Add(texts);
                var buy = Text(S("buyChips"), 14, "Montserrat-800", "#fff", 0, "lobby.buy#2"); buy.style.marginLeft = 12; Pad(buy, 9, 14, 9, 14); Raise(buy, "#3CCB6E", 12, 3, "#22914A"); Tap(buy, () => BuyRequested?.Invoke()); warn.Add(buy);
                betSection.Add(warn);
                if (c.WarnAt > 0) Shake(warn, c.WarnAt);
            }

            // Ödül: the pool pill (padding 8 14, gap 10, radius 14, --sd).
            var prize = FormRow(scroll, S("rPrize"), "lobby.text.rPrize", c.Type == "pairs" ? S("prizeE") : S("prizeT"), "lobby.cf.prizeD", null);
            var pool = Row(); Pad(pool, 8, 14, 8, 14); view.Decorate(pool, "var(--sd)", 14);
            pool.Add(Text(S("pool"), 12, "Baloo2-800", "var(--mt)", 0, "lobby.text.pool#2"));
            var pc = Coin(18, false); pc.style.marginLeft = 10; pool.Add(pc);
            var pv = Tabular(Text(view.N(c.Stake * 4), 20, "Montserrat-700", "var(--a1)", 0, "lobby.cf.pool")); pv.style.marginLeft = 10; pool.Add(pv); prize.Add(pool);

            // Katlamalı (R-64) and, on an Eşli table, whether the partner's opening raises the bar (padding-left 18).
            var kat = FormRow(scroll, S("rKat"), "lobby.text.rKat", S("rKatD"), "lobby.text.rKatD", null);
            kat.Add(Switch("lobby.cf.katT.flip", c.Doubling, () => { c.Doubling = !c.Doubling; Render(); }));
            if (c.Type == "pairs" && c.Doubling)
            {
                var partner = FormRow(scroll, S("rKatP"), "lobby.text.rKatP", S("rKatPD"), "lobby.text.rKatPD", null, 15);
                partner.name = "lobby.create.katlamaPartner"; partner.style.paddingLeft = 18;
                partner.Add(Switch("lobby.cf.katPT.flip", c.PartnerRaises, () => { c.PartnerRaises = !c.PartnerRaises; Render(); }));
            }
            var turn = FormRow(scroll, S("rTurn"), "lobby.text.rTurn", S("rTurnD"), "lobby.text.rTurnD", null);
            // Tur süresi 15–60 sn, swiped like El sayısı.
            turn.Add(Carousel("lobby-create-turn", TableOptions.TurnChoices.Select(t => (t.ToString(), t + L(" sn", " s"))).ToArray(), c.Turn.ToString(), v => { c.Turn = int.Parse(v); Render(); }, 68));

            // Kural özelleştirme: padding 14 0, gap 14 — sliders badge, title + rulesSum, the chevron (rotate 180° open).
            var rules = Row(); rules.name = "lobby.cf.toggleRules"; Pad(rules, 14, 0, 14, 0);
            var ri = Box(); Square(ri, 36); view.Decorate(ri, "var(--sd)", 11); ri.Add(view.Icon(IconSliders, 18, "var(--ic)")); rules.Add(ri);
            var rw = Box(); rw.style.flexGrow = 1; rw.style.flexShrink = 1; rw.style.marginLeft = 14;
            rw.Add(Text(S("rRules"), 16, "Montserrat-700", "#F4F1E8", 1.25f, "lobby.text.rRules"));
            rw.Add(Text(c.Changed > 0 ? S("rulesCh", c.Changed) : S("rulesDef"), 12.5f, "Baloo2-700", "var(--mt)", "lobby.cf.rulesSum")); rules.Add(rw);
            var chev = Box(); Square(chev, 30); chev.style.marginLeft = 14; view.Decorate(chev, "var(--pnH)", 10); chev.Add(view.Icon(IconChevron, 14, "var(--ic)")); chev.style.rotate = new Rotate(c.RulesOpen ? 180 : 0); rules.Add(chev);
            Tap(rules, () => { c.RulesOpen = !c.RulesOpen; if (c.RulesOpen) rulesOpenedAt = Time.realtimeSinceStartup; Render(); }); scroll.Add(rules);
            if (c.RulesOpen)
            {
                // padding 2 16, margin-bottom 6, radius 18, --sd, lbFade .2s.
                var box = Box("lobby-create-rules"); Pad(box, 2, 16, 2, 16); box.style.marginBottom = 6; view.Decorate(box, "var(--sd)", 18);
                RuleSwitch(box, 0, S("rOkey"), S("rOkeyD"), c.OkeyPenalty, () => c.OkeyPenalty = !c.OkeyPenalty);
                RuleSwitch(box, 1, S("rIsle"), S("rIsleD"), c.Penalties, () => c.Penalties = !c.Penalties);
                RuleOptions(box, 0, "spec", S("rSpec"), S("rSpecD"), new[] { ("true", S("allow")), ("false", S("deny")) }, c.Spectators ? "true" : "false", v => c.Spectators = v == "true");
                RuleOptions(box, 1, "chat", S("rChat"), S("rChatD"), new[] { ("on", S("chOn")), ("quick", S("chQuick")), ("off", S("chOff")) }, c.Chat, v => c.Chat = v);
                RuleOptions(box, 2, "gifts", S("rGift"), S("rGiftD"), new[] { ("true", S("on")), ("false", S("off")) }, c.Gifts ? "true" : "false", v => c.Gifts = v == "true");
                var reset = Text(S("resetRules"), 13, "Baloo2-800", "var(--mt)", 0, "lobby.cf.resetRules"); reset.style.alignSelf = Align.FlexStart; Pad(reset, 11, 0, 11, 0);
                reset.RegisterCallback<PointerEnterEvent>(_ => reset.style.color = view.C("#F4F1E8")); reset.RegisterCallback<PointerLeaveEvent>(_ => reset.style.color = view.C("var(--mt)"));
                Tap(reset, () => { c.Spectators = c.Gifts = c.OkeyPenalty = c.Penalties = true; c.Chat = "on"; Render(); }); box.Add(reset);
                FadeIn(box, rulesOpenedAt, .2f);
                scroll.Add(box);
            }

            MatchCreateNormalText(left);

            // Right: the summary (330 wide, padding 22, gap 14, --sd, border-left 1px rgba(255,255,255,.06)).
            var right = Box("lobby-create-summary"); right.style.width = 330; Pad(right, 22, 22, 22, 22); view.Decorate(right, "var(--sd)", 0); right.style.borderLeftWidth = 1; right.style.borderLeftColor = new Color(1, 1, 1, .06f);
            modal.Add(right);
            var rh = Row(); rh.style.justifyContent = Justify.SpaceBetween;
            var cap = Text(S("summary"), 11, "Baloo2-800", "var(--mt)", 0, "lobby.text.summary#2"); MenuView.Tracking(cap, 1.8f, 11); rh.Add(cap);
            var close = Box("lobby.closeCreate#2"); Square(close, 38); Raise(close, "var(--pnH)", 12, 4, "var(--pnS)"); close.Add(view.Icon(IconClose, 16, "var(--ic)")); close.tooltip = S("close");
            GalaxyFilters.HoverBrightness(close, 1.25f); Tap(close, CloseCreate); rh.Add(close); right.Add(rh);
            summary = Box(); summary.style.flexGrow = 1; right.Add(summary);
            UpdateSummary();
        }

        private float rulesOpenedAt;
        private VisualElement summary;

        /// <summary>cs: name and mode line, ids, the four stats, MASA KURALLARI, balance after, the CTA and Vazgeç.</summary>
        private void UpdateSummary()
        {
            if (summary == null || create == null) return;
            var c = create; long chips = backend.Chips; summary.Clear();
            var t = Preview(c);
            var names = Box(); names.style.marginTop = 14;
            var name = Text(c.Name.Trim().Length > 0 ? c.Name : "—", 22, "Montserrat-700", "#F4F1E8", 1.15f, "lobby.cs.name"); name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; names.Add(name);
            var line = Row(); line.style.marginTop = 4;
            if (c.Visibility == "private") { var l = view.Icon(IconLock, 13, "var(--a1)"); l.style.marginRight = 6; line.Add(l); }
            line.Add(Text(S("modeLine", c.Type == "pairs" ? S("esli") : S("tekli"), c.Visibility == "private" ? S("priv") : S("open")), 13, "Baloo2-800", "var(--mt)"));
            names.Add(line); summary.Add(names);
            summary.Add(Ids(t, 14));
            var stats = Stats(t, "var(--pn)", "create.summary.stats"); stats.style.marginTop = 14; summary.Add(stats);
            var rules = RulesList(t, "var(--pn)", "create.summary.rules", "lobby.text.kRules#2"); rules.style.marginTop = 14; summary.Add(rules);
            bool afford = c.Stake <= chips, can = afford && chips >= Stakes[0];
            var after = Row(); after.style.justifyContent = Justify.SpaceBetween; after.style.marginTop = 14;
            after.Add(Text(S("balance"), 13, "Baloo2-800", "var(--mt)", 0, "lobby.text.balance"));
            after.Add(Tabular(Text(afford ? view.N(chips) + " → " + view.N(chips - c.Stake) : view.N(chips), 13, "Baloo2-800", afford ? "#F4F1E8" : "#FF8A94", 0, "lobby.cs.bal"))); summary.Add(after);
            var button = Text(can ? S("ctaCreate", view.N(c.Stake)) : S("ctaLow"), 19, "Montserrat-800", can ? "var(--aT)" : "var(--mt)", 0, "lobby.cf.create"); button.style.height = 62; button.style.marginTop = 14; button.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(button, can ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 18, 5, can ? "var(--aS)" : "var(--pnS)"); Tap(button, () => _ = SubmitCreate()); summary.Add(button);
            var cancel = Text(S("cancel"), 14, "Baloo2-800", "var(--mt)", 0, "lobby.closeCreate#3"); cancel.style.alignSelf = Align.Center; cancel.style.marginTop = 14;
            cancel.RegisterCallback<PointerEnterEvent>(_ => cancel.style.color = view.C("#F4F1E8")); cancel.RegisterCallback<PointerLeaveEvent>(_ => cancel.style.color = view.C("var(--mt)"));
            Tap(cancel, CloseCreate); summary.Add(cancel);
        }

        /// <summary>The draft as a table (summary(t)): a PIN switched on counts as Şifreli even before its digits.</summary>
        private TableSummary Preview(CreateDraft c) => LobbyRulePresentation.WithOpeningTilePenalty(new TableSummary
        {
            Name = c.Name.Trim(), Type = c.Type, Private = c.Visibility == "private", Locked = c.PinOn, Doubling = c.Doubling, PartnerRaises = c.PartnerRaises, Hands = c.Hands, TurnSeconds = c.Turn,
            Stake = c.Stake, Penalties = c.Penalties, OkeyPenalty = c.OkeyPenalty, Processing = true, Spectators = c.Spectators, Chat = c.Chat, Gifts = c.Gifts,
        }, c.Penalties);

        public TableOptions Options(CreateDraft c) => new TableOptions
        {
            Type = c.Type, Name = c.Name.Trim(), Visibility = c.Visibility, Pin = c.PinOn ? c.Pin : null, Hands = c.Hands, Stake = c.Stake,
            // katlamaPartner: pairs + katlamalı only (protocol tableCreate); otherwise the default.
            Doubling = c.Doubling, PartnerRaises = c.Type == "pairs" && c.Doubling ? c.PartnerRaises : true,
            TurnSeconds = c.Turn, Spectators = c.Spectators, Chat = c.Chat, Gifts = c.Gifts, Processing = true, Penalties = c.Penalties, OkeyPenalty = c.OkeyPenalty,
        };

        /// <summary>doCreate(): an unaffordable stake shakes the warning; an empty name or a short PIN marks the fields; then tableCreate.</summary>
        public async System.Threading.Tasks.Task SubmitCreate()
        {
            var c = create; if (c == null || c.Busy) return;
            long chips = backend.Chips;
            if (c.Stake > chips) { c.Warn = c.Stake; c.WarnAt = Time.realtimeSinceStartup; Render(); return; }
            if (c.Name.Trim().Length == 0 || c.PinOn && !Regex.IsMatch(c.Pin, "^\\d{4}$")) { c.Tried = true; Render(); return; }
            c.Busy = true;
            string error = await backend.CreateAsync(Options(c));
            if (layer == null) return;
            c.Busy = false;
            if (error != null)
            {
                switch (V4Code(error))
                {
                    case "E_NO_CHIPS": c.Warn = c.Stake; c.WarnAt = Time.realtimeSinceStartup; Render(); return;
                    case "E_TABLE_NAME": case "E_PIN_FORMAT": c.Tried = true; Render(); return;
                    case "E_TABLE_OPTIONS": return;
                    case "offline": case "timeout": ShowOffline(); return;
                    default: Flash(ErrorText(error) ?? L("Masa açılamadı (" + error + ")", "Could not open the table (" + error + ")"), "bad"); return;
                }
            }
            // The room state may arrive just after the acknowledgement.
            float waitFrom = Time.realtimeSinceStartup;
            while (backend.Room == null && Time.realtimeSinceStartup - waitFrom < 5 && layer != null) await System.Threading.Tasks.Task.Yield();
            if (layer == null) return;
            bool share = c.Visibility == "private"; roomPin = c.PinOn ? c.Pin : null;
            EnterRoom(); if (backend.Room != null) NoteSeats(backend.Room);
            Render();
            // doCreate → enterWait(t, true); a private table opens the share dialog first.
            if (share) OpenDialog(new Dialog { Kind = "share", SharePin = roomPin, First = true });
        }

        // ---------- form pieces ----------
        private static float CreateNormalLineHeight(string font, float size, float density)
        {
            density = Mathf.Max(.01f, density);
            bool baloo = font != null && font.StartsWith("Baloo", StringComparison.Ordinal);
            float ascent = baloo ? 1.078f : .968f, descent = baloo ? .524f : .251f;
            return (Mathf.Floor(ascent * size * density + .5f) + Mathf.Floor(descent * size * density + .5f)) / density;
        }

        private static void MatchCreateNormalText(VisualElement root)
        {
            root.Query<Label>().ForEach(label =>
            {
                var font = label.style.unityFont.value; float size = label.style.fontSize.value.value;
                if (font == null || size <= 0 || label.style.whiteSpace.value == WhiteSpace.Normal ||
                    label.style.height.keyword != StyleKeyword.Undefined || Mathf.Abs(label.style.height.value.value - MenuView.CssLineHeight(font.name, size)) > .01f) return;
                void Sync()
                {
                    float height = CreateNormalLineHeight(font.name, size, label.scaledPixelsPerPoint);
                    if (Mathf.Abs(label.style.height.value.value - height) > .01f) label.style.height = height;
                }
                label.RegisterCallback<AttachToPanelEvent>(_ => Sync());
                label.RegisterCallback<GeometryChangedEvent>(_ => Sync());
                if (label.panel != null) Sync();
            });
        }

        /// <summary>A form row: padding 12 0, border-bottom 1px rgba(255,255,255,.07), gap 16 — title (700 16px/1.25) and its 12.5px line.</summary>
        private VisualElement FormRow(VisualElement scroll, string title, string titleName, string description, string descriptionName, string descriptionColor, int titleSize = 16)
        {
            var row = Row(); Pad(row, 12, 0, 12, 0); Divider(row, .07f);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginRight = 16;
            words.Add(Text(title, titleSize, "Montserrat-700", "#F4F1E8", 1.25f, titleName));
            var d = Text(description, 12.5f, "Baloo2-700", descriptionColor ?? "var(--mt)", descriptionName); Paragraph(d, createPixelMetrics: true); words.Add(d);
            row.Add(words); scroll.Add(row); return row;
        }

        private static void Divider(VisualElement row, float alpha) { row.style.borderBottomWidth = 1; row.style.borderBottomColor = new Color(1, 1, 1, alpha); }

        /// <summary>A form segment (padding 4, gap 4, radius 14): options padding 8 <paramref name="padX"/>, 700 14px; names lobby.o.pick[i].</summary>
        private VisualElement Options(string name, (string Value, string Label)[] options, string current, Action<string> pick, int padX, int minWidth = 0)
        {
            var segment = Segment(4); segment.name = name;
            for (int i = 0; i < options.Length; i++)
            {
                var (value, label) = options[i]; string chosen = value;
                var option = SegmentOption("lobby.o.pick[" + i + "]", label, value == current, () => pick(chosen), 14, 8, padX, i < options.Length - 1 ? 4 : 0);
                // CSS min-width is the content box: the padding comes on top.
                if (minWidth > 0) { option.style.minWidth = minWidth + 2 * padX; option.style.justifyContent = Justify.Center; }
                segment.Add(option);
            }
            return segment;
        }

        /// <summary>
        /// A swipeable segment for the longer choice lists (El sayısı 3–11, Tur süresi 15–60 sn): the same --dp segment and
        /// gold option, a window of <paramref name="visible"/> fixed-width options that slides left and right (drag, horizontal
        /// wheel or the ‹ › steps) and settles on the nearest option. The choice always sits in the middle (the ends leave an
        /// empty slot, like a picker wheel), so every option has its own resting place. Its size never changes with the choice.
        /// </summary>
        private VisualElement Carousel(string name, (string Value, string Label)[] options, string current, Action<string> pick, float optionWidth, int visible = 3)
        {
            const float gap = 4, arrowWidth = 26;
            int index = Mathf.Max(0, Array.FindIndex(options, o => o.Value == current));
            float step = optionWidth + gap, windowWidth = visible * optionWidth + (visible - 1) * gap, trackWidth = options.Length * optionWidth + (options.Length - 1) * gap;
            float Offset(int i) => windowWidth / 2 - (i * step + optionWidth / 2);
            int Nearest(float x) => Mathf.Clamp(Mathf.RoundToInt((windowWidth / 2 - optionWidth / 2 - x) / step), 0, options.Length - 1);

            var segment = Segment(4); segment.name = name; segment.style.flexShrink = 0;
            // The window clips the sliding strip but not the picked option's 3px ledge (or its 3px press): it reaches 2px into
            // the segment's bottom padding, so the segment keeps the height of the other form segments.
            var window = Box(); window.pickingMode = PickingMode.Position; window.style.width = windowWidth; window.style.overflow = Overflow.Hidden; window.style.flexShrink = 0;
            window.style.paddingBottom = 6; window.style.marginBottom = -2;
            var track = Row(); track.style.width = trackWidth; track.style.flexShrink = 0;
            for (int i = 0; i < options.Length; i++)
            {
                var (value, label) = options[i]; string chosen = value;
                var option = SegmentOption("lobby.o.pick[" + i + "]", label, i == index, () => pick(chosen), 14, 8, 0, i < options.Length - 1 ? (int)gap : 0);
                option.style.width = optionWidth; option.style.flexShrink = 0; option.style.justifyContent = Justify.Center;
                track.Add(option);
            }
            window.Add(track);
            float offset = Offset(index);
            void Place(float x) { offset = x; track.style.translate = new Translate(x, 0); }
            // The redraw after a pick starts the strip where it was and eases it onto the new choice (.2s ease-out).
            if (carouselFrom.TryGetValue(name, out var from) && Time.realtimeSinceStartup - from.At < .2f)
            {
                float target = offset, began = from.At, start = from.Offset; Place(start);
                Animate(() => { float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .2f); Place(Mathf.Lerp(start, target, 1 - (1 - u) * (1 - u))); }, track);
            }
            else Place(offset);
            carouselFrom.Remove(name);
            void Choose(int i)
            {
                i = Mathf.Clamp(i, 0, options.Length - 1);
                if (i == index) { Place(Offset(index)); return; }
                carouselFrom[name] = (Time.realtimeSinceStartup, offset); pick(options[i].Value);
            }

            // Drag: the strip follows the pointer, then settles; a drag never also counts as a click on an option.
            bool dragging = false, moved = false; float startX = 0, startOffset = 0; int pointer = -1;
            window.RegisterCallback<PointerDownEvent>(e => { if (e.button != 0) return; dragging = true; moved = false; pointer = e.pointerId; startX = e.position.x; startOffset = offset; }, TrickleDown.TrickleDown);
            window.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!dragging || e.pointerId != pointer) return;
                float dx = e.position.x - startX;
                if (!moved && Mathf.Abs(dx) > 4) { moved = true; window.CapturePointer(pointer); }
                if (moved) Place(Mathf.Clamp(startOffset + dx, Offset(options.Length - 1) - 12, Offset(0) + 12));
            }, TrickleDown.TrickleDown);
            void Release(bool settle) { if (!dragging) return; dragging = false; if (window.HasPointerCapture(pointer)) window.ReleasePointer(pointer); if (moved && settle) Choose(Nearest(offset)); else if (moved) Place(Offset(index)); }
            window.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointer) Release(true); }, TrickleDown.TrickleDown);
            window.RegisterCallback<PointerCaptureOutEvent>(_ => Release(true));
            window.RegisterCallback<ClickEvent>(e => { if (moved) { moved = false; e.StopImmediatePropagation(); } }, TrickleDown.TrickleDown);
            window.RegisterCallback<WheelEvent>(e => { if (Mathf.Abs(e.delta.x) > Mathf.Abs(e.delta.y)) { Choose(index + (e.delta.x > 0 ? 1 : -1)); e.StopPropagation(); } });

            VisualElement Arrow(int direction)
            {
                bool enabled = direction < 0 ? index > 0 : index < options.Length - 1;
                var arrow = Box(name + (direction < 0 ? "-prev" : "-next")); arrow.style.width = arrowWidth; arrow.style.alignSelf = Align.Stretch; arrow.style.flexShrink = 0;
                arrow.style.alignItems = Align.Center; arrow.style.justifyContent = Justify.Center; Radius(arrow, 10);
                arrow.style.marginLeft = direction > 0 ? gap : 0; arrow.style.marginRight = direction < 0 ? gap : 0;
                var glyph = view.Icon(IconChevron, 14, "var(--mt)"); glyph.style.rotate = new Rotate(direction < 0 ? 90 : -90); arrow.Add(glyph);
                arrow.style.opacity = enabled ? 1 : .35f;
                if (enabled)
                {
                    arrow.RegisterCallback<PointerEnterEvent>(_ => arrow.style.backgroundColor = view.C("var(--pnH)"));
                    arrow.RegisterCallback<PointerLeaveEvent>(_ => arrow.style.backgroundColor = Color.clear);
                    Tap(arrow, () => Choose(index + direction));
                }
                return arrow;
            }
            segment.Add(Arrow(-1)); segment.Add(window); segment.Add(Arrow(1));
            return segment;
        }
        private readonly Dictionary<string, (float At, float Offset)> carouselFrom = new Dictionary<string, (float, float)>();

        /// <summary>tog(on, flip, small): 56 × 32 (small 52 × 30), on #3CCB6E / off --dp, inset 0 2px 0 rgba(0,0,0,.25); white knob, left .22s cubic-bezier(.3,1.5,.5,1).</summary>
        private VisualElement Switch(string name, bool on, Action flip, int width = 56, int height = 32)
        {
            var track = Box(name); track.style.width = width; track.style.height = height; track.style.flexShrink = 0;
            view.Decorate(track, on ? "#3CCB6E" : "var(--dp)", height / 2);
            var inset = Box(); inset.style.position = Position.Absolute; inset.style.left = inset.style.top = inset.style.right = inset.style.bottom = 0; Radius(inset, height / 2);
            inset.style.borderTopWidth = 2; inset.style.borderTopColor = new Color(0, 0, 0, .25f); track.Add(inset);
            int knob = height - 6; float x = on ? width - knob - 3 : 3;
            var dot = Box(); MenuView.Place(dot, x, 3, knob, knob); Raise(dot, "#fff", knob / 2, 2, "rgba(0,0,0,.25)"); track.Add(dot);
            // The knob slides from where it was (the redraw after the flip starts it at the other end).
            if (switchFlip.TryGetValue(name, out var flipped) && Time.realtimeSinceStartup - flipped.At < .22f && flipped.On == on)
            {
                float from = on ? 3 : width - knob - 3, began = flipped.At;
                dot.style.left = from;
                Animate(() => { float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .22f); dot.style.left = Mathf.LerpUnclamped(from, x, MenuView.CssBezier(.3f, 1.5f, .5f, 1, u)); }, dot);
            }
            track.EnableInClassList("on", on); Tap(track, () => { switchFlip[name] = (Time.realtimeSinceStartup, !on); flip(); }); return track;
        }
        private readonly Dictionary<string, (float At, bool On)> switchFlip = new Dictionary<string, (float, bool)>();

        /// <summary>A rule switch row (r): padding 11 0, border-bottom rgba(255,255,255,.06), gap 16 — 700 15px/1.25 name, 700 12px description, a small switch.</summary>
        private void RuleSwitch(VisualElement box, int index, string title, string description, bool on, Action flip)
        {
            var row = RuleRow(box, title, "lobby.r.name[" + index + "]", description, "lobby.r.desc[" + index + "]");
            row.Add(Switch("lobby.r.flip[" + index + "]", on, () => { flip(); Render(); }, 52, 30));
        }

        /// <summary>A rule option row (g): the same row with a segment (padding 4, gap 4, radius 13; options padding 7 12, radius 9, 700 13px).</summary>
        private void RuleOptions(VisualElement box, int index, string key, string title, string description, (string, string)[] options, string current, Action<string> pick)
        {
            var row = RuleRow(box, title, "lobby.g.name[" + index + "]", description, "lobby.g.desc[" + index + "]");
            var segment = Segment(4); segment.name = "lobby-create-rule-" + key; Radius(segment, 13);
            for (int i = 0; i < options.Length; i++)
            {
                var (value, label) = options[i]; string chosen = value;
                segment.Add(SegmentOption("lobby.o.pick[" + i + "]", label, value == current, () => { pick(chosen); Render(); }, 13, 7, 12, i < options.Length - 1 ? 4 : 0, 9));
            }
            row.Add(segment);
        }

        private VisualElement RuleRow(VisualElement box, string title, string titleName, string description, string descriptionName)
        {
            var row = Row(); Pad(row, 11, 0, 11, 0); Divider(row, .06f);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginRight = 16;
            words.Add(Text(title, 15, "Montserrat-700", "#F4F1E8", 1.25f, titleName)); var d = Text(description, 12, "Baloo2-700", "var(--mt)", 0, descriptionName); Paragraph(d, createPixelMetrics: true); words.Add(d); row.Add(words);
            box.Add(row); return row;
        }

        // ---------- the summary pieces the form and the waiting room share ----------
        /// <summary>sumIds(t): the type, Özel, Şifreli badges (flex, wrap, gap 5).</summary>
        private VisualElement Ids(TableSummary t, float marginTop)
        {
            var ids = Row(); ids.style.flexWrap = Wrap.Wrap; ids.style.marginTop = marginTop; ids.style.flexShrink = 1;
            var list = new List<VisualElement> { Badge(t.Type == "pairs" ? S("esli") : S("tekli"), t.Type == "pairs" ? "#FF6B6B" : "#3D7BFF", "#fff", true) };
            if (t.Private) list.Add(Badge(S("bPriv"), "rgba(255,201,74,.16)", "var(--a1)", true));
            if (t.Locked) list.Add(Badge(S("bPass"), "rgba(255,201,74,.16)", "var(--a1)", true));
            for (int i = 0; i < list.Count; i++) { list[i].name = "lobby.g.label[" + i + "]"; if (i == list.Count - 1) list[i].style.marginRight = 0; ids.Add(list[i]); }
            return ids;
        }

        /// <summary>summary(t).rows: BAHİS / HAVUZ / EL / TUR SÜRESİ in four equal cells (radius 14, overflow hidden).</summary>
        private VisualElement Stats(TableSummary t, string bg, string name)
        {
            var grid = Row(); grid.name = name; grid.style.alignItems = Align.Stretch; view.Decorate(grid, bg, 14); grid.style.overflow = Overflow.Hidden;
            var rows = new[] { (S("kBet"), view.N(t.Stake)), (S("kPool"), view.N(t.Stake * 4)), (S("kHands"), t.Hands.ToString()), (S("kTurn"), S("bTurn", t.TurnSeconds)) };
            for (int i = 0; i < rows.Length; i++)
            {
                // A cell: column, centred, gap 4, padding 11 4 10, inset -1px 0 0 rgba(255,255,255,.05) (a line on its right edge).
                var cell = Box(); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.alignItems = Align.Center; Pad(cell, 11, 4, 10, 4);
                cell.style.borderRightWidth = 1; cell.style.borderRightColor = new Color(1, 1, 1, .05f); cell.style.paddingRight = 3;
                var k = Text(rows[i].Item1, 9, "Baloo2-800", "var(--mt)", 0, "lobby.r.k[" + i + "]"); MenuView.Tracking(k, 1, 9); cell.Add(k);
                var v = Tabular(Text(rows[i].Item2, 15, "Montserrat-700", "#F4F1E8", 1, "lobby.r.v[" + i + "]")); v.style.marginTop = 4; cell.Add(v);
                grid.Add(cell);
            }
            return grid;
        }

        /// <summary>
        /// sumRules(t) — "MASA KURALLARI": one line per option, the name left, the value right; on = a green glowing dot
        /// and #8EF0B0, off = a faint dot and --mt. Katlamalı first, the partner rule on Eşli + Katlamalı (R-64).
        /// </summary>
        private VisualElement RulesList(TableSummary t, string bg, string name, string captionName)
        {
            // flex 1, min-height 0, radius 14, padding 4 14 6, scrolling without a scrollbar.
            var box = view.Scroll(false); box.name = name; box.verticalScrollerVisibility = ScrollerVisibility.Hidden; box.style.flexGrow = 1; box.style.flexShrink = 1; box.style.minHeight = 0;
            view.Decorate(box, bg, 14); Pad(box, 4, 14, 6, 14);
            var cap = Text(S("kRules"), 10, "Baloo2-800", "var(--mt)", 0, captionName); MenuView.Tracking(cap, 1.4f, 10); Pad(cap, 6, 0, 4, 0); box.Add(cap);
            var rows = new List<(string K, bool On, string V)> { (S("rKat"), t.Doubling, t.Doubling ? S("sKatOn") : S("off")) };
            if (t.Doubling && t.Type == "pairs") rows.Add((S("sKatP"), t.PartnerRaises, t.PartnerRaises ? S("sYes") : S("sNo")));
            rows.Add((S("sOkey"), t.OkeyPenalty, t.OkeyPenalty ? "+101" : S("off")));
            rows.Add((S("sIsle"), t.Penalties, t.Penalties ? "+101" : S("off")));
            bool opener = LobbyRulePresentation.OpeningTilePenalty(t);
            rows.Add((S("sOpen"), opener, opener ? "×10" : S("off")));
            rows.Add((S("rSpec"), t.Spectators, t.Spectators ? S("on") : S("off")));
            rows.Add((S("rChat"), t.Chat != "off", t.Chat == "quick" ? S("sQuick") : t.Chat == "off" ? S("off") : S("on")));
            rows.Add((S("rGift"), t.Gifts, t.Gifts ? S("on") : S("off")));
            for (int i = 0; i < rows.Count; i++)
            {
                var (k, on, v) = rows[i];
                // min-height 29, gap 10, inset 0 1px 0 rgba(255,255,255,.05) (a line along the top).
                var row = Row(); row.style.minHeight = 29;
                var rule = Box(); rule.style.position = Position.Absolute; rule.style.left = rule.style.right = rule.style.top = 0; rule.style.height = 1; rule.style.backgroundColor = new Color(1, 1, 1, .05f); row.Add(rule);
                var dot = Dot(on ? "#3CCB6E" : "rgba(255,255,255,.18)", 8, 0); if (on) GalaxyFilters.Shadow(dot, 0, 0, 3.5f, new Color(.235f, .8f, .43f, .7f)); row.Add(dot);
                var key = Text(k, 13, "Baloo2-700", on ? "#F4F1E8" : "var(--mt)", 1.25f, "lobby.q.k[" + i + "]"); key.style.marginLeft = 10; key.style.flexGrow = 1; key.style.flexShrink = 1; Paragraph(key, 1.25f); row.Add(key);
                var value = Text(v, 13, "Baloo2-800", on ? "#8EF0B0" : "var(--mt)", 0, "lobby.q.v[" + i + "]"); value.style.marginLeft = 10; row.Add(value);
                box.Add(row);
            }
            return box;
        }
    }
}
