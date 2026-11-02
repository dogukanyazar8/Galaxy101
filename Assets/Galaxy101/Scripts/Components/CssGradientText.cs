using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>CSS vertical three-stop text gradients on Unity 6.6's native SDF glyphs, preserving the original Label's layout.</summary>
    public static class CssGradientText
    {
        private static readonly ConditionalWeakTable<Label, Binding> bindings = new ConditionalWeakTable<Label, Binding>();

        /// <summary>
        /// Splits native glyph quads at the middle stop, interpolating both positions and atlas UVs.
        /// The original Label remains the measured and named element; an absolute, unnamed sibling paints the lower half.
        /// Detaching the original Label disposes the binding. Call Attach again if that Label is subsequently reattached.
        /// </summary>
        public static IDisposable Attach(Label label, Color top, Color middle, Color bottom, float middleStop = .5f)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));
            if (float.IsNaN(middleStop) || float.IsInfinity(middleStop) || middleStop <= 0 || middleStop >= 1)
                throw new ArgumentOutOfRangeException(nameof(middleStop), "The middle gradient stop must be strictly between zero and one.");
            if (!bindings.TryGetValue(label, out var binding))
            {
                binding = new Binding(label); bindings.Add(label, binding);
            }
            binding.Configure(top, middle, bottom, middleStop);
            return binding;
        }

        private sealed class Binding : IDisposable
        {
            private readonly Label label, lower;
            private readonly Action<TextElement.GlyphsEnumerable> previous;
            private readonly IVisualElementScheduledItem synchronization;
            private Color top, middle, bottom;
            private float stop, height, opacity;
            private bool disposed;

            public Binding(Label label)
            {
                this.label = label; previous = label.PostProcessTextVertices;
                lower = new Label { pickingMode = PickingMode.Ignore, focusable = false };
                lower.style.position = Position.Absolute;
                // G101Dump.Texts omits color.a == 0. The glyph callback supplies its actual rendering tint.
                lower.style.color = new Color(1, 1, 1, 0);
                lower.PostProcessTextVertices = PaintLower;
                label.PostProcessTextVertices = PaintUpper;
                label.RegisterCallback<AttachToPanelEvent>(OnAttach);
                label.RegisterCallback<DetachFromPanelEvent>(OnDetach);
                label.RegisterCallback<GeometryChangedEvent>(OnGeometry);
                label.RegisterCallback<ChangeEvent<string>>(OnTextChanged);
                synchronization = label.schedule.Execute(Synchronize).Every(0);
            }

            public void Configure(Color top, Color middle, Color bottom, float stop)
            {
                this.top = top; this.middle = middle; this.bottom = bottom; this.stop = stop;
                Synchronize(); label.MarkDirtyRepaint(); lower.MarkDirtyRepaint();
            }

            private void OnAttach(AttachToPanelEvent _) => Synchronize();
            private void OnDetach(DetachFromPanelEvent _) => Dispose();
            private void OnGeometry(GeometryChangedEvent _) => Synchronize();
            private void OnTextChanged(ChangeEvent<string> _) => Synchronize();

            private void Synchronize()
            {
                if (disposed || label.panel == null || label.parent == null) return;
                var parent = label.parent;
                int index = parent.IndexOf(label);
                if (lower.parent != parent || parent.IndexOf(lower) != index + 1) parent.Insert(index + 1, lower);
                var style = label.resolvedStyle;
                var rect = label.layout;
                height = rect.height; opacity = style.color.a;
                lower.text = label.text; lower.enableRichText = label.enableRichText; lower.parseEscapeSequences = label.parseEscapeSequences;
                lower.style.left = rect.x; lower.style.top = rect.y; lower.style.width = rect.width; lower.style.height = rect.height;
                lower.style.marginLeft = lower.style.marginRight = lower.style.marginTop = lower.style.marginBottom = 0;
                lower.style.paddingLeft = style.paddingLeft; lower.style.paddingRight = style.paddingRight;
                lower.style.paddingTop = style.paddingTop; lower.style.paddingBottom = style.paddingBottom;
                lower.style.borderLeftWidth = style.borderLeftWidth; lower.style.borderRightWidth = style.borderRightWidth;
                lower.style.borderTopWidth = style.borderTopWidth; lower.style.borderBottomWidth = style.borderBottomWidth;
                lower.style.unityFont = style.unityFont; lower.style.unityFontDefinition = style.unityFontDefinition;
                lower.style.fontSize = style.fontSize; lower.style.unityFontStyleAndWeight = style.unityFontStyleAndWeight;
                lower.style.letterSpacing = style.letterSpacing; lower.style.wordSpacing = style.wordSpacing;
                lower.style.unityParagraphSpacing = style.unityParagraphSpacing;
                lower.style.unityTextAlign = style.unityTextAlign; lower.style.whiteSpace = style.whiteSpace;
                lower.style.textOverflow = style.textOverflow; lower.style.unityTextOverflowPosition = style.unityTextOverflowPosition;
                lower.style.unityTextGenerator = style.unityTextGenerator;
                lower.style.textShadow = style.textShadow; lower.style.unityTextOutlineWidth = style.unityTextOutlineWidth;
                lower.style.unityTextOutlineColor = style.unityTextOutlineColor;
                lower.style.opacity = style.opacity; lower.style.visibility = style.visibility; lower.style.display = style.display;
                lower.style.translate = label.style.translate; lower.style.scale = label.style.scale;
                lower.style.rotate = label.style.rotate; lower.style.transformOrigin = label.style.transformOrigin;
            }

            private void PaintUpper(TextElement.GlyphsEnumerable glyphs)
            {
                previous?.Invoke(glyphs);
                Paint(glyphs, 0, height * stop, true);
            }

            private void PaintLower(TextElement.GlyphsEnumerable glyphs)
            {
                previous?.Invoke(glyphs);
                Paint(glyphs, height * stop, height, false);
            }

            private void Paint(TextElement.GlyphsEnumerable glyphs, float minimum, float maximum, bool upper)
            {
                if (disposed || height <= 0 || !float.IsFinite(height)) return;
                foreach (var glyph in glyphs)
                {
                    var vertices = glyph.vertices;
                    if (vertices.Length != 4) continue;
                    var bl = vertices[0]; var tl = vertices[1]; var tr = vertices[2]; var br = vertices[3];
                    float y0 = Mathf.Min(tl.position.y, tr.position.y), y1 = Mathf.Max(bl.position.y, br.position.y);
                    if (y1 <= minimum || y0 >= maximum || y1 <= y0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var vertex = vertices[i]; vertex.position = tl.position; vertex.tint = Color.clear; vertices[i] = vertex;
                        }
                        continue;
                    }
                    float clippedTop = Mathf.Max(y0, minimum), clippedBottom = Mathf.Min(y1, maximum);
                    vertices[0] = Interpolate(tl, bl, (clippedBottom - y0) / (y1 - y0));
                    vertices[1] = Interpolate(tl, bl, (clippedTop - y0) / (y1 - y0));
                    vertices[2] = Interpolate(tr, br, (clippedTop - y0) / (y1 - y0));
                    vertices[3] = Interpolate(tr, br, (clippedBottom - y0) / (y1 - y0));
                    for (int i = 0; i < 4; i++)
                    {
                        var vertex = vertices[i];
                        float position = vertex.position.y / height;
                        Color color = upper ? Color.Lerp(top, middle, position / stop) : Color.Lerp(middle, bottom, (position - stop) / (1 - stop));
                        color.a *= opacity; vertex.tint = color; vertices[i] = vertex;
                    }
                }
            }

            private static Vertex Interpolate(Vertex top, Vertex bottom, float fraction)
            {
                var result = top;
                result.position = Vector3.LerpUnclamped(top.position, bottom.position, fraction);
                result.uv = Vector2.LerpUnclamped(top.uv, bottom.uv, fraction);
                return result;
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true; synchronization.Pause();
                label.UnregisterCallback<AttachToPanelEvent>(OnAttach); label.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                label.UnregisterCallback<GeometryChangedEvent>(OnGeometry); label.UnregisterCallback<ChangeEvent<string>>(OnTextChanged);
                if (label.PostProcessTextVertices == (Action<TextElement.GlyphsEnumerable>)PaintUpper) label.PostProcessTextVertices = previous;
                lower.PostProcessTextVertices = null; lower.RemoveFromHierarchy(); label.MarkDirtyRepaint(); bindings.Remove(label);
            }
        }
    }
}
