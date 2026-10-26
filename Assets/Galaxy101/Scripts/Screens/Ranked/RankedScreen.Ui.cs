using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>The Dereceli screen's building blocks (the lobby's CSS-line helpers, emblems, framed avatars, spinners).</summary>
    public sealed partial class RankedScreen
    {
        /// <summary>
        /// A CSS text line: no label padding, a box of the font's CSS line height (<see cref="MenuView.CssLineHeight"/> for
        /// "normal", or <paramref name="lineHeight"/> em) with the glyphs centred in it, as a browser lays them out.
        /// Weight 900 of Baloo 2 is drawn with 800 (the font has no 900; Chrome falls back to the same face).
        /// </summary>
        private Label Text(string text, int size, string font, string color, float lineHeight = 0, string name = null)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font); label.style.whiteSpace = WhiteSpace.NoWrap; label.pickingMode = PickingMode.Ignore;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.height = lineHeight > 0 ? size * lineHeight : MenuView.CssLineHeight(font, size); label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.flexShrink = 0;
            if (name != null) label.name = name;
            return label;
        }

        /// <summary>
        /// CSS letter-spacing laid out as Chrome does: <see cref="MenuView.Tracking"/> sets the spacing, but UI Toolkit's text adds
        /// it after neither a space nor the last glyph, where Chrome adds it after every character (S16 run: "SEZON 3 · DERECELİ"
        /// at 1.6 px came out 6.4 px = 4 × 1.6 narrower, "4 OYUNCU" at 1 px 2 px narrower). The spaces get it back as rich-text
        /// space, the last character as right padding. Call after <see cref="Pad"/>.
        /// </summary>
        private static Label Track(Label label, float px, float size)
        {
            MenuView.Tracking(label, px, size);
            string text = label.text ?? "";
            if (text.IndexOf(' ') >= 0 && text.IndexOf('<') < 0)
            {
                label.enableRichText = true;
                label.text = text.Replace(" ", " <space=" + (px / size).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "em>");
            }
            label.style.paddingRight = label.style.paddingRight.value.value + px;
            return label;
        }

        /// <summary>white-space: nowrap; overflow: hidden; text-overflow: ellipsis.</summary>
        private static Label Clip(Label label) { label.style.flexShrink = 1; label.style.overflow = Overflow.Hidden; label.style.textOverflow = TextOverflow.Ellipsis; label.style.minWidth = 0; return label; }

        /// <summary>A wrapping paragraph of <paramref name="lineHeight"/> em lines, centred (noHist, pSub).</summary>
        private static Label Paragraph(Label label, float lineHeight)
        {
            label.style.whiteSpace = WhiteSpace.Normal; label.style.height = StyleKeyword.Auto; label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return MenuView.CssParagraph(label, label.style.fontSize.value.value * lineHeight, true);
        }

        private static Label Tabular(Label label) => MenuView.TabularDigits(label);

        private static VisualElement Box(string name = null) { var e = new VisualElement { pickingMode = PickingMode.Ignore }; if (name != null) e.name = name; return e; }
        private static VisualElement Row(string name = null) { var r = Box(name); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0; return r; }
        private static VisualElement Column(string name = null) { var c = Box(name); c.style.flexDirection = FlexDirection.Column; c.style.flexShrink = 0; return c; }
        private static VisualElement Grow() { var g = Box(); g.style.flexGrow = 1; return g; }
        private static VisualElement Fill(string name = null) { var e = Box(name); Inset(e, 0); return e; }
        private static void Inset(VisualElement e, float all) { e.style.position = Position.Absolute; e.style.left = e.style.top = e.style.right = e.style.bottom = all; }
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

        /// <summary>CSS "box-shadow: inset 0 0 0 Npx color": a ring over the box that takes no layout.</summary>
        private VisualElement InsetRing(VisualElement e, float width, string color, float radius)
        {
            var ring = Fill(); Radius(ring, radius); Border(ring, width, view.C(color)); e.Add(ring); return ring;
        }

        /// <summary>CSS "border: w dashed c" (the lobby's DashedBorder): a transparent border keeps the box, dashes over it.</summary>
        private static void DashedBorder(VisualElement e, float w, Color c)
        {
            Border(e, w, Color.clear);
            float radius = e.style.borderTopLeftRadius.value.value, size = e.style.width.value.value;
            VisualElement dashes = size > 0 && radius * 2 >= size && e.style.height.value.value == size ? new CssDashedCircle(size, w, c) : new DashedOutline(radius, w, c, 3 * w, 2 * w);
            dashes.style.left = dashes.style.top = -w; if (!(dashes is CssDashedCircle)) dashes.style.right = dashes.style.bottom = -w;
            e.Add(dashes);
        }

        private static void Tap(VisualElement e, Action action)
        {
            e.pickingMode = PickingMode.Position; e.focusable = true; GalaxyFilters.PressSink(e);
            e.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
            e.RegisterCallback<NavigationSubmitEvent>(evt => { action(); evt.StopPropagation(); });
        }

        /// <summary>The CSS "box-shadow: 0 depth 0 shade" ledge (<see cref="GalaxyFilters.HardShadow"/>).</summary>
        private void Raise(VisualElement e, string bg, int radius, int depth, string shade) { view.Decorate(e, bg, radius); if (depth > 0) GalaxyFilters.HardShadow(e, depth, view.C(shade)); }

        /// <summary>A CSS colour with its alpha replaced (gradients end in the same colour at alpha 0, as CSS interpolates premultiplied).</summary>
        private static string Fade(string css, float alpha)
        {
            var m = System.Text.RegularExpressions.Regex.Match(css ?? "", @"rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)");
            if (m.Success) return "rgba(" + m.Groups[1].Value + "," + m.Groups[2].Value + "," + m.Groups[3].Value + "," + alpha.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            if (ColorUtility.TryParseHtmlString(css, out var c)) return "rgba(" + Mathf.RoundToInt(c.r * 255) + "," + Mathf.RoundToInt(c.g * 255) + "," + Mathf.RoundToInt(c.b * 255) + "," + alpha.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            return css;
        }

        /// <summary>radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F) coin; 2 px dashed #FFF3C8 from 24 px up.</summary>
        private VisualElement Coin(int size, bool dashed)
        {
            var coin = Box(); Square(coin, size); view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", size / 2);
            if (dashed) DashedBorder(coin, 2, view.C("#FFF3C8"));
            return coin;
        }

        /// <summary>A round spinner: 3 px ring in <paramref name="color"/>, its top quarter clear, one turn per <paramref name="period"/> s (rkSpin, linear).</summary>
        private VisualElement Spinner(float size, float stroke, string color, float period)
        {
            var ring = Box(); ring.style.width = ring.style.height = size; ring.style.flexShrink = 0; Radius(ring, size / 2); Border(ring, stroke, view.C(color)); ring.style.borderTopColor = Color.clear;
            Loop(ring, t => ring.style.rotate = new Rotate(Mathf.Repeat(t / period, 1) * 360));
            return ring;
        }

        /// <summary>
        /// G101Emblem (g101-rank.js emblem()): the league medal at <paramref name="size"/> px from lane D's rasterised files;
        /// Galaksi carries its "#rank" plate (rect y 168–198 of 200, #120A34, Montserrat 800 19 px white) when a rank is given.
        /// </summary>
        private VisualElement Emblem(string key, int tier, float size, int rank = 0)
        {
            var e = Box(); e.style.width = e.style.height = size; e.style.flexShrink = 0;
            var texture = RankedLadder.Emblem(key, tier, size);
            if (texture != null) { e.style.backgroundImage = new StyleBackground(texture); e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); }
            if (key == "galaxy" && rank > 0 && size >= 40)
            {
                float k = size / 200f; string t = "#" + rank; float w = 34 + t.Length * 13;
                var plate = Box(); plate.style.position = Position.Absolute; plate.style.left = (100 - w / 2) * k; plate.style.top = 168 * k; plate.style.width = w * k; plate.style.height = 30 * k;
                plate.style.alignItems = Align.Center; plate.style.justifyContent = Justify.Center;
                view.Decorate(plate, "#120A34", Mathf.RoundToInt(15 * k)); Border(plate, 3 * k, view.C("#7FD8F0")); // stroke url(M): the metal ramp's mid tone
                var label = Text(t, Mathf.Max(1, Mathf.RoundToInt(19 * k)), "Montserrat-800", "#fff"); label.style.unityTextAlign = TextAnchor.MiddleCenter; plate.Add(label);
                e.Add(plate);
            }
            return e;
        }

        /// <summary>
        /// G101FramedAvatar (g101-rank.js): the season frame ring (rasterised from ring(key, 64), halo included) and the
        /// player's disc inset by round(size × .14) with a max(2, round(size × .02)) px ring in the metal's dark tone.
        /// </summary>
        private VisualElement FramedAvatar(string key, int size, string initials, string color)
        {
            var root = Box(); root.style.width = root.style.height = size; root.style.flexShrink = 0;
            var ring = Box(); ring.style.position = Position.Absolute; float over = size * 12.5f / 200; // the image spans -12.5 … 212.5 of the 200 box
            ring.style.left = ring.style.top = -over; ring.style.width = ring.style.height = size + 2 * over;
            var texture = Resources.Load<Texture2D>("Galaxy101/Ranked/frame_" + key + "_64@2x");
            if (texture != null) ring.style.backgroundImage = new StyleBackground(texture);
            root.Add(ring);
            var league = RankedLadder.Leagues[Mathf.Max(0, IndexOfLeague(key))];
            int inset = Mathf.RoundToInt(size * .14f), edge = Mathf.Max(2, Mathf.RoundToInt(size * .02f));
            var disc = Box(); disc.style.position = Position.Absolute; disc.style.left = disc.style.top = disc.style.right = disc.style.bottom = inset;
            disc.style.alignItems = Align.Center; disc.style.justifyContent = Justify.Center;
            view.Decorate(disc, color ?? "#3D7BFF", (size - 2 * inset) / 2);
            // box-shadow 0 0 0 Npx metal[3]: a band just outside the disc; inset 0 -6px 12px rgba(0,0,0,.25): a soft shade at the bottom.
            GalaxyFilters.SpreadRing(disc, edge)(edge, view.C(league.Edge));
            var shade = Fill(); Radius(shade, (size - 2 * inset) / 2); view.Decorate(shade, "linear-gradient(rgba(0,0,0,0) 55%,rgba(0,0,0,.25))", (size - 2 * inset) / 2); disc.Add(shade);
            var ini = Text(initials, Mathf.RoundToInt(size * .3f), "Montserrat-800", "#fff"); ini.style.unityTextAlign = TextAnchor.MiddleCenter; disc.Add(ini);
            root.Add(disc);
            return root;
        }

        private static int IndexOfLeague(string key) { var l = RankedLadder.Leagues; for (int i = 0; i < l.Count; i++) if (l[i].Key == key) return i; return -1; }

        /// <summary>A round initials disc (the seats' and invite list's avatars).</summary>
        private VisualElement Disc(float size, string color, string initials, int font, string name = null)
        {
            var d = Box(name); Square(d, size); view.Decorate(d, color ?? "#3D7BFF", Mathf.RoundToInt(size / 2));
            if (initials != null) { var t = Text(initials, font, "Montserrat-800", "#fff"); t.style.unityTextAlign = TextAnchor.MiddleCenter; d.Add(t); }
            return d;
        }

        /// <summary>The prototype's ini(): the first letters of up to two words, upper-cased the Turkish way.</summary>
        public static string Initials(string name)
        {
            var parts = (name ?? "?").Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string s = ""; foreach (var p in parts) { s += p.Substring(0, 1); if (s.Length >= 2) break; }
            return (s.Length == 0 ? "?" : s).ToUpper(new System.Globalization.CultureInfo("tr-TR"));
        }

        // ---------- animation: one scheduler for the screen's layer ----------
        private readonly List<(VisualElement Owner, Action Step)> motions = new List<(VisualElement, Action)>();
        private readonly List<(VisualElement Owner, Action Step)> stepping = new List<(VisualElement, Action)>();
        private IVisualElementScheduledItem ticker;

        private void Animate(Action step, VisualElement owner = null)
        {
            if (step != null) { motions.Add((owner, step)); step(); }
            if (ticker == null && layer != null) ticker = layer.schedule.Execute(Tick).Every(0);
        }

        /// <summary>An endless loop (rkSpin, rkFloat, rkPulse, rkRays) on the shared clock: frozen captures sit at the same phase (D-61).</summary>
        private void Loop(VisualElement owner, Action<float> step) => Animate(() => step(GameClock.Seconds), owner);

        /// <summary>A one-shot entrance from <paramref name="began"/> (realtime s) over <paramref name="seconds"/>: step(eased progress 0…1).</summary>
        private void Enter(VisualElement owner, float began, float seconds, Func<float, float> ease, Action<float> step, float delay = 0)
        {
            float now = Time.realtimeSinceStartup;
            if (now - began - delay >= seconds) { step(1); return; }
            bool done = false;
            Animate(() =>
            {
                if (done) return;
                float u = Mathf.Clamp01((Time.realtimeSinceStartup - began - delay) / seconds); done = u >= 1;
                step(ease(u));
            }, owner);
        }

        private void Tick()
        {
            if (layer == null) return;
            motions.RemoveAll(m => m.Owner != null && m.Owner.panel == null);
            stepping.Clear(); stepping.AddRange(motions);
            foreach (var m in stepping) m.Step();
            Every();
        }

        private static float Bezier(float x1, float y1, float x2, float y2, float u) => MenuView.CssBezier(x1, y1, x2, y2, u);
        private static float EaseOut(float u) => Bezier(0, 0, .58f, 1, u);
        private static float Ease(float u) => Bezier(.25f, .1f, .25f, 1, u);
        private static float Linear(float u) => u;
    }
}
