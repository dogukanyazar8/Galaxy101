using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    internal static class PanelUi
    {
        internal static VisualElement Shell(MenuView v, string name)
        {
            var template = v.Template(name);
            return template.Q<VisualElement>("body") ?? template;
        }

        internal static VisualElement Spacer(MenuView v)
        {
            var e = v.Box("g-panel-spacer");
            return e;
        }

        internal static void Grow(VisualElement e)
        {
            e.style.flexGrow = 1;
            e.style.minWidth = 0;
        }

        internal static void Gap(VisualElement row, int gap)
        {
            // Flow children only: ledges and rings are absolute overlays.
            var flow = row.Children().Where(c => c.style.position.value != Position.Absolute).ToList();
            for (var i = 0; i < flow.Count - 1; i++) flow[i].style.marginRight = gap;
        }

        internal static void Pad(VisualElement e, int vertical, int horizontal)
        {
            e.style.paddingTop = e.style.paddingBottom = vertical;
            e.style.paddingLeft = e.style.paddingRight = horizontal;
        }

        internal static Label Wrap(MenuView v, string text, int size = 14, string color = "mt", bool heading = false)
        {
            var label = v.Text(text, size, heading, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        internal static VisualElement Badge(MenuView v, string text, string background, string color = "#fff", int size = 11)
        {
            var e = v.Box();
            v.Decorate(e, background, 7);
            Pad(e, 3, 8);
            var label = v.Text(text, size, false, color);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            e.Add(label);
            e.style.alignItems = Align.Center;
            return e;
        }

        internal static VisualElement Circle(MenuView v, int size, string background, string text = null, string color = "#fff")
        {
            var e = v.Box();
            e.style.width = e.style.height = size;
            e.style.flexShrink = 0;
            e.style.alignItems = Align.Center;
            e.style.justifyContent = Justify.Center;
            v.Decorate(e, background, size / 2);
            if (text != null) e.Add(v.Text(text, size > 50 ? 18 : 14, true, color));
            return e;
        }

        internal static VisualElement Person(MenuView v, JObject person, int size)
        {
            return Circle(v, size, (string)(person["c"] ?? person["color"]) ?? "a2", (string)person["ini"] ?? "?");
        }

        internal static VisualElement Coin(MenuView v, int size = 30) => v.ChipIcon(size);

        internal static VisualElement Gem(MenuView v, int size = 22) => v.GemIcon(size);

        internal static VisualElement CoinPile(MenuView v, float scale = 1)
        {
            var pile = v.Box();
            pile.style.width = 92 * scale;
            pile.style.height = 72 * scale;
            foreach (var p in new[] { new Vector3(0, 26, 44), new Vector3(46, 22, 44), new Vector3(22, 0, 50) })
            {
                var coin = Coin(v, Mathf.RoundToInt(p.z * scale));
                coin.style.position = Position.Absolute;
                coin.style.left = p.x * scale;
                coin.style.top = p.y * scale;
                pile.Add(coin);
            }
            return pile;
        }

        internal static Button CustomButton(MenuView v, string text, System.Action action, string classes)
        {
            var tuned = System.Text.RegularExpressions.Regex.Match(classes ?? "", @"(?<![\w-])bright-\d+");
            var button = v.Button(text, action, tuned.Success ? "plain " + tuned.Value : "plain");
            button.RemoveFromClassList("plain");
            button.style.backgroundColor = string.IsNullOrEmpty(classes) ? new StyleColor(Color.clear) : new StyleColor(StyleKeyword.Null);
            button.style.backgroundImage = StyleKeyword.None;
            button.style.color = StyleKeyword.Null;
            button.style.fontSize = StyleKeyword.Null;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = StyleKeyword.Null;
            foreach (var name in classes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) button.AddToClassList(name);
            return button;
        }

        /// <summary>
        /// Button sized by its padding like the reference markup (no min-width/fixed height),
        /// e.g. "padding:12px 28px;border-radius:14px;box-shadow:0 4px 0 …;font:700 16px".
        /// </summary>
        internal static Button CssButton(MenuView v, string text, System.Action action, string kind, float padY, float padX, int fontSize, int radius = 14, int depth = 4)
        {
            var b = v.Button(text, action, kind);
            b.style.paddingTop = b.style.paddingBottom = padY; b.style.paddingLeft = b.style.paddingRight = padX;
            b.style.fontSize = fontSize; b.style.whiteSpace = WhiteSpace.NoWrap; b.style.flexShrink = 0;
            string bg = kind.Contains("danger") ? "#FF5E6C" : kind.Contains("success") ? "#3CCB6E" : kind.Contains("secondary") ? "pnH" : "linear-gradient(var(--a1),var(--a2))";
            string shade = kind.Contains("danger") ? "#C23E4A" : kind.Contains("success") ? "#22914A" : kind.Contains("secondary") ? "pnS" : "aS";
            v.Decorate(b, bg, radius, depth, shade);
            return b;
        }

        /// <summary>CSS "box-shadow: inset 4px 0 0 var(--a1)" on a rounded selected row, without the layout shift of a border.</summary>
        internal static void InsetBar(MenuView v, VisualElement row, int radius, string face = "pnH")
        {
            var bar = v.Box("g-inset-bar"); bar.pickingMode = PickingMode.Ignore; v.Decorate(bar, "a1", radius);
            var fill = v.Box("g-inset-bar-face"); fill.pickingMode = PickingMode.Ignore; v.Decorate(fill, face, radius); bar.Add(fill);
            row.Insert(0, bar);
        }

        /// <summary>CSS "box-shadow: inset 0 0 0 Npx color" as an overlay ring (no layout shift).</summary>
        internal static VisualElement Ring(MenuView v, float width, string color, float radius)
        {
            var ring = v.Box(); ring.name = "inset-ring"; ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute; ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = 0;
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = width;
            ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = v.C(color);
            ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = radius; return ring;
        }

        /// <summary>Rotated rounded square used by the reference for rank/league markers.</summary>
        internal static VisualElement Diamond(MenuView v, float size, string color, int radius = 2)
        {
            var d = v.Box(); d.style.width = d.style.height = size; d.style.flexShrink = 0; d.style.rotate = new Rotate(45); v.Decorate(d, color, radius); return d;
        }

        /// <summary>Level chip: "font:900 10px body;padding:2px 7px;border-radius:6px;background:dp;color:a1".</summary>
        internal static VisualElement LevelChip(MenuView v, string text)
        {
            var chip = v.Box(); chip.style.flexShrink = 0; chip.style.paddingTop = chip.style.paddingBottom = 2; chip.style.paddingLeft = chip.style.paddingRight = 7; v.Decorate(chip, "dp", 6);
            var label = v.Text(text, 10, false, "a1"); label.style.unityFont = v.Font("Baloo2-800"); label.style.whiteSpace = WhiteSpace.NoWrap; chip.Add(label); return chip;
        }

        /// <summary>Friend code strip: dashed 1px bd border, label 700 12-13px body, code 700 17-18px heading with 1px tracking.</summary>
        internal static VisualElement CodeStrip(MenuView v, int labelSize, int codeSize, int radius)
        {
            var label = v.Text(v.L("Kodun", "Your code"), labelSize, false, "mt");
            var code = v.Text(v.Model.FriendCode, codeSize, true, "a1"); MenuView.Tracking(code, 1, codeSize); code.style.whiteSpace = WhiteSpace.NoWrap;
            var row = v.Row(label, code); row.style.flexShrink = 0; Dashed(v, row, radius); v.Decorate(row, "transparent", radius); return row;
        }

        internal static Label Caption(MenuView v, string text, int size, float trackingPx, string color = "mt")
        {
            var label = v.Text(text, size, false, color); label.style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(label, trackingPx, size); label.style.whiteSpace = WhiteSpace.NoWrap; return label;
        }

        internal static void Dashed(MenuView v, VisualElement box, float radius, string color = "bd")
        {
            box.Add(new DashedOutline(radius, 1, v.C(color)));
        }

        internal static void Shine(MenuView v, VisualElement container, int width, float seconds)
        {
            var mask = v.Box(); mask.pickingMode = PickingMode.Ignore;
            mask.style.position = Position.Absolute; mask.style.left = mask.style.right = mask.style.top = mask.style.bottom = 0;
            mask.style.overflow = Overflow.Hidden;
            mask.style.borderTopLeftRadius = mask.style.borderTopRightRadius = mask.style.borderBottomLeftRadius = mask.style.borderBottomRightRadius = 22;
            var streak = v.Box(); streak.pickingMode = PickingMode.Ignore;
            streak.style.position = Position.Absolute; streak.style.top = -20; streak.style.bottom = -20; streak.style.width = width;
            streak.style.rotate = new Rotate(-20);
            v.Decorate(streak, "linear-gradient(90deg,transparent,rgba(255,255,255,.35),transparent)");
            mask.Add(streak); container.Insert(0, mask);
            v.AnimateElement(elapsed =>
            {
                var phase = elapsed % seconds / seconds;
                streak.style.display = phase <= .6f ? DisplayStyle.Flex : DisplayStyle.None;
                streak.style.left = -width * 1.6f + ((float.IsNaN(container.resolvedStyle.width)?0:container.resolvedStyle.width) + width * 3.2f) * Mathf.Clamp01(phase / .6f);
            });
        }

        internal static VisualElement Chest(MenuView v)
        {
            var chest = v.Box();
            chest.style.width = 64;
            chest.style.height = 46;
            var bottom = v.Box();
            bottom.style.position = Position.Absolute;
            bottom.style.left = 2; bottom.style.right = 2; bottom.style.bottom = 0; bottom.style.height = 28;
            v.Decorate(bottom, "linear-gradient(#C67B3A,#8E4E1E)", 7, 3, "#5A2E0E");
            chest.Add(bottom);
            var lid = v.Box();
            lid.style.position = Position.Absolute;
            lid.style.left = 0; lid.style.right = 0; lid.style.top = 4; lid.style.height = 18;
            v.Decorate(lid, "linear-gradient(#D98C48,#A25A24)", 10);
            chest.Add(lid);
            var stripe = v.Box();
            stripe.style.position = Position.Absolute;
            stripe.style.left = 28; stripe.style.top = 4; stripe.style.width = 8; stripe.style.height = 42;
            v.Decorate(stripe, "linear-gradient(var(--a1),var(--a2))");
            chest.Add(stripe);
            var lockBox = v.Box();
            lockBox.style.position = Position.Absolute;
            lockBox.style.left = 25; lockBox.style.top = 18; lockBox.style.width = 14; lockBox.style.height = 12;
            v.Decorate(lockBox, "a1", 3);
            chest.Add(lockBox);
            return chest;
        }

        internal static TextField Input(MenuView v, string name, string value, string placeholder = null, int maxLength = -1, int fontSize = 15, bool heading = false)
        {
            var field = new TextField { name = name, value = value ?? "", maxLength = maxLength };
            field.AddToClassList("g-panel-input");
            field.style.unityFont = v.Text("", fontSize, heading).style.unityFont;
            var input = field.Q<VisualElement>(className: "unity-text-field__input");
            if (input != null)
            {
                input.style.unityFont = field.style.unityFont; input.style.fontSize = fontSize;
            }
            v.InputLook(field, "#F4F1E8");
            if (!string.IsNullOrEmpty(placeholder)) field.textEdition.placeholder = placeholder;
            return field;
        }

        internal static Button Action(MenuView v, string text, System.Action action, string kind = "primary", int width = 0)
        {
            var b = v.Button(text, action, kind);
            b.AddToClassList("g-panel-small-button");
            if (width > 0) b.style.width = width;
            return b;
        }

        internal static VisualElement Code(MenuView v, bool copyButton = true, bool compact = false, string background = "transparent")
        {
            var row = v.Row(v.Text(v.L("Kodun", "Your code"), 12, false, "mt"), v.Text(v.Model.FriendCode, 17, true, "a1"));
            row.AddToClassList("g-panel-code");
            v.Decorate(row, background, 14);
            Gap(row, 10);
            if (copyButton)
            {
                row.Add(Spacer(v));
                Button copy = null;
                System.Action clicked = () =>
                {
                    v.Copy(v.Model.FriendCode);
                    copy.text = v.L("Kopyalandı", "Copied");
                    copy.schedule.Execute(() => copy.text = v.L("Kopyala", "Copy")).StartingIn(1500);
                };
                copy = compact ? CustomButton(v, v.L("Kopyala", "Copy"), clicked, "") : Action(v, v.L("Kopyala", "Copy"), clicked, "secondary");
                copy.style.minWidth = 70;
                copy.style.height = 34;
                copy.style.paddingLeft = copy.style.paddingRight = 10;
                if (compact) { copy.style.height = StyleKeyword.Auto; copy.style.paddingLeft = copy.style.paddingRight = copy.style.paddingTop = copy.style.paddingBottom = 0; copy.style.fontSize = 13; copy.style.color = v.C("ic"); row[1].style.fontSize = 16; }
                row.Add(copy);
            }
            return row;
        }

        internal static void Header(MenuView v, VisualElement root, string title, string subtitle = null, string icon = null, string background = null, int titleSize = 24, string shade = "pnS", int iconSize = 22)
        {
            var header = root.Q<VisualElement>("header");
            if (icon != null)
            {
                var tile = v.Box();
                tile.style.width = tile.style.height = 44;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                tile.style.marginRight = 12;
                v.Decorate(tile, background ?? "pnH", 14, 4, shade);
                tile.Add(v.Icon(icon, iconSize, "#fff"));
                header.Add(tile);
            }
            var captions = v.Column(v.Text(title, titleSize, true));
            if (subtitle != null) captions.Add(v.Text(subtitle, 13, false, "mt"));
            Grow(captions);
            header.Add(captions);
            var close = v.Button("", v.CloseTop, "secondary");
            close.name = "dialog-close";
            close.tooltip = v.L("Kapat", "Close");
            close.style.width = close.style.height = 40;
            close.style.minWidth = 40;
            close.style.paddingLeft = close.style.paddingRight = 0;
            v.Decorate(close, "pnH", 12, 4, "pnS");
            close.Add(v.Icon("close", 16));
            header.Add(close);
        }

        internal static string Money(MenuView v, decimal value)
        {
            return "₺" + value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo(v.Loc.Language == "en" ? "en-US" : "tr-TR"));
        }

        internal static void SaveFailure(MenuView v)
        {
            v.Toast(v.L("Değişiklikler kaydedilemedi. Tekrar dene.", "Could not save changes. Try again."));
        }

        internal static void Bob(MenuView v, VisualElement element)
        {
            // bob 1.6s with the default CSS ease on each half.
            v.AnimateElement(elapsed => { var f = Mathf.Repeat((float)elapsed, 1.6f) / 1.6f; element.style.translate = new Translate(0, -4 * (f < .5f ? MenuView.CssBezier(.25f, .1f, .25f, 1, f * 2) : 1 - MenuView.CssBezier(.25f, .1f, .25f, 1, f * 2 - 1)), 0); });
        }
    }
}
