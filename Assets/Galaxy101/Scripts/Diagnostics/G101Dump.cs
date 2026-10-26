#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Diagnostics
{
    /// <summary>
    /// The Unity side of the v4 layout dump (g101-state.js dump(): data-g101-id elements with box and computed style) and
    /// of the text dump tools/compare_ui.py reads. Boxes are in 1280×720 design units of each document's canvas.
    /// </summary>
    public static class G101Dump
    {
        private static IEnumerable<UIDocument> Documents() =>
            Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude).Where(d => d.isActiveAndEnabled && d.rootVisualElement?.panel != null).OrderBy(d => d.sortingOrder);

        private static (Rect frame, float k) Canvas(VisualElement root)
        {
            var canvas = root.Q<VisualElement>("galaxy-canvas") ?? root.Q<VisualElement>("table-canvas") ?? root;
            var frame = canvas.worldBound; return (frame, 1280f / Mathf.Max(1, frame.width));
        }

        public static bool Visible(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
            {
                var s = x.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity < .02f) return false;
            }
            return true;
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
        private static double R(float v) => System.Math.Round(v, 2);

        /// <summary>Every named element (UI Toolkit name = data-g101-id, D-62) with box, text and the styles the reference dump carries.</summary>
        public static JObject Layout(string state, uint seed, double t)
        {
            var items = new JArray();
            foreach (var doc in Documents())
            {
                var root = doc.rootVisualElement; var (frame, k) = Canvas(root);
                root.Query<VisualElement>().ForEach(e =>
                {
                    if (string.IsNullOrEmpty(e.name)) return;
                    var b = e.worldBound; var s = e.resolvedStyle;
                    var text = e is TextElement te && e.childCount == 0 ? Regex.Replace(te.text ?? "", "<[^>]+>", "").Trim() : null;
                    items.Add(new JObject
                    {
                        ["id"] = e.name, ["doc"] = doc.name,
                        ["box"] = new JObject { ["x"] = R((b.x - frame.x) * k), ["y"] = R((b.y - frame.y) * k), ["w"] = R(b.width * k), ["h"] = R(b.height * k) },
                        ["visible"] = b.width > 0 && b.height > 0 && Visible(e), ["text"] = text,
                        ["style"] = new JObject
                        {
                            ["color"] = Hex(s.color), ["backgroundColor"] = Hex(s.backgroundColor), ["fontSize"] = R(s.fontSize * k),
                            ["font"] = s.unityFontDefinition.fontAsset != null ? s.unityFontDefinition.fontAsset.name : s.unityFont != null ? s.unityFont.name : null,
                            ["letterSpacing"] = R(s.letterSpacing), ["borderRadius"] = R(s.borderTopLeftRadius * k), ["borderTopWidth"] = R(s.borderTopWidth * k),
                            ["borderTopColor"] = Hex(s.borderTopColor), ["opacity"] = R(s.opacity), ["unityTextAlign"] = s.unityTextAlign.ToString()
                        }
                    });
                });
            }
            return new JObject { ["version"] = "0.4.0", ["state"] = state, ["seed"] = seed, ["t"] = t, ["unit"] = "1280x720 tasarım birimi", ["count"] = items.Count, ["items"] = items };
        }

        /// <summary>Every visible text: [text, x, y, w, h, fontSize, font, color, name] like tools/ref_capture.py TEXT_DUMP.</summary>
        public static JArray Texts()
        {
            var output = new JArray();
            foreach (var doc in Documents())
            {
                var root = doc.rootVisualElement; var (frame, k) = Canvas(root);
                foreach (var label in root.Query<TextElement>().ToList())
                {
                    if (label is GalaxyButton) continue;   // its "button-caption" child draws the text
                    string text = Regex.Replace(label.text ?? "", "<[^>]+>", "");
                    if (string.IsNullOrWhiteSpace(text) || !Visible(label) || label.resolvedStyle.color.a < .02f) continue;
                    var b = label.worldBound; if (b.width < 1 || b.height < 1) continue;
                    // The glyph block is laid out in the label's own (local) space and then transformed, so a rotated or
                    // scaled label reports the bounds of its transformed text box — what a DOM text range rect gives.
                    var p = label.resolvedStyle; var local = label.contentRect;
                    var size = label.MeasureTextSize(label.text, local.width, VisualElement.MeasureMode.Undefined, local.height, VisualElement.MeasureMode.Undefined);
                    float textW = Mathf.Min(local.width, size.x), textH = size.y;
                    var a = p.unityTextAlign;
                    bool center = a == TextAnchor.MiddleCenter || a == TextAnchor.UpperCenter || a == TextAnchor.LowerCenter, right = a == TextAnchor.MiddleRight || a == TextAnchor.UpperRight || a == TextAnchor.LowerRight;
                    bool middle = a == TextAnchor.MiddleLeft || a == TextAnchor.MiddleCenter || a == TextAnchor.MiddleRight, lower = a == TextAnchor.LowerLeft || a == TextAnchor.LowerCenter || a == TextAnchor.LowerRight;
                    float lx = local.x + (center ? (local.width - textW) / 2 : right ? local.width - textW : 0), ly = local.y + (middle ? (local.height - textH) / 2 : lower ? local.height - textH : 0);
                    var c0 = label.LocalToWorld(new Vector2(lx, ly)); var c1 = label.LocalToWorld(new Vector2(lx + textW, ly));
                    var c2 = label.LocalToWorld(new Vector2(lx, ly + textH)); var c3 = label.LocalToWorld(new Vector2(lx + textW, ly + textH));
                    float minX = Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x)), maxX = Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x));
                    float minY = Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y)), maxY = Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y));
                    float textX = minX, textY = minY; textW = maxX - minX; float boxH = maxY - minY;
                    output.Add(new JArray(text.Trim().Length > 80 ? text.Trim().Substring(0, 80) : text.Trim(), Mathf.RoundToInt((textX - frame.x) * k), Mathf.RoundToInt((textY - frame.y) * k),
                        Mathf.RoundToInt(textW * k), Mathf.RoundToInt(boxH * k), (p.fontSize * k).ToString("0.##", CultureInfo.InvariantCulture) + "px",
                        p.unityFontDefinition.fontAsset != null ? p.unityFontDefinition.fontAsset.name : p.unityFont != null ? p.unityFont.name : "", Hex(p.color), label.name));
                }
            }
            return output;
        }
    }
}
#endif
