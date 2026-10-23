using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Design comparison aid: every visible text on screen with its box in 1280×720 canvas pixels, font size, weight
    /// (font asset name) and colour — the same shape the reference page's JS dump produces, so the two can be matched
    /// by text (tools/compare_ui.py). Written only when GALAXY_UI_DUMP_DIR is set.
    /// </summary>
    public static class UiDump
    {
        public static string Dir => Environment.GetEnvironmentVariable("GALAXY_UI_DUMP_DIR");

        /// <summary>
        /// Dump with the panel laid out on a 1280×720 target: a batch-mode screen is smaller, and the scaled panel would
        /// snap every box to its coarser pixel grid. Yields nothing without GALAXY_UI_DUMP_DIR.
        /// </summary>
        public static System.Collections.IEnumerator Capture(VisualElement root, string name)
        {
            if (string.IsNullOrEmpty(Dir) || root == null) yield break;
            var document = UnityEngine.Object.FindObjectsByType<UIDocument>().FirstOrDefault(d => d.rootVisualElement == root || d.rootVisualElement?.panel == root.panel);
            if (document == null) { Write(root, name); yield break; }
            yield return DisplaySettingsTests.Shot(document, 1280, 720, Path.Combine(Dir, name + ".unity.png"), r => Write(r, name));
        }

        public static void Write(VisualElement root, string name)
        {
            if (string.IsNullOrEmpty(Dir) || root == null) return;
            var canvas = root.Q<VisualElement>("galaxy-canvas") ?? root.Q<VisualElement>("table-canvas") ?? root;
            var frame = canvas.worldBound; float k = 1280f / Mathf.Max(1, frame.width);
            var sb = new StringBuilder("[\n"); bool first = true;
            foreach (var label in root.Query<TextElement>().ToList())
            {
                string text = System.Text.RegularExpressions.Regex.Replace(label.text ?? "", "<[^>]+>", "");
                if (string.IsNullOrWhiteSpace(text) || !Visible(label)) continue;
                var b = label.worldBound;
                if (b.width < 1 || b.height < 1) continue;
                // The glyph block inside the label (padding excluded), like a DOM text range rect.
                var p = label.resolvedStyle;
                float x = b.x + p.paddingLeft + p.borderLeftWidth, y = b.y + p.paddingTop + p.borderTopWidth;
                float w = b.width - p.paddingLeft - p.paddingRight - p.borderLeftWidth - p.borderRightWidth, h = b.height - p.paddingTop - p.paddingBottom - p.borderTopWidth - p.borderBottomWidth;
                var size = label.MeasureTextSize(label.text, w, VisualElement.MeasureMode.Undefined, h, VisualElement.MeasureMode.Undefined);
                float textW = Mathf.Min(w, size.x);
                float textX = p.unityTextAlign == TextAnchor.MiddleCenter || p.unityTextAlign == TextAnchor.UpperCenter || p.unityTextAlign == TextAnchor.LowerCenter ? x + (w - textW) / 2
                    : p.unityTextAlign == TextAnchor.MiddleRight || p.unityTextAlign == TextAnchor.UpperRight || p.unityTextAlign == TextAnchor.LowerRight ? x + w - textW : x;
                float textY = p.unityTextAlign == TextAnchor.MiddleLeft || p.unityTextAlign == TextAnchor.MiddleCenter || p.unityTextAlign == TextAnchor.MiddleRight ? y + (h - size.y) / 2 : y;
                if (!first) sb.Append(",\n"); first = false;
                var c = p.color;
                sb.Append("[").Append(Json(text.Length > 40 ? text.Substring(0, 40) : text)).Append(',')
                    .Append(F((textX - frame.x) * k)).Append(',').Append(F((textY - frame.y) * k)).Append(',').Append(F(textW * k)).Append(',').Append(F(size.y * k)).Append(',')
                    .Append(F(p.fontSize * k)).Append(',').Append(Json(p.unityFont != null ? p.unityFont.name : (p.unityFontDefinition.fontAsset != null ? p.unityFontDefinition.fontAsset.name : "")))
                    .Append(',').Append(Json(ColorUtility.ToHtmlStringRGBA(c))).Append(']');
            }
            sb.Append("\n]\n");
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, name + ".unity.json"), sb.ToString());
        }

        private static bool Visible(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
            {
                var s = v.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity <= 0.01f) return false;
            }
            return true;
        }

        private static string F(float v) => Mathf.Round(v).ToString(CultureInfo.InvariantCulture);
        private static string Json(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }
}
