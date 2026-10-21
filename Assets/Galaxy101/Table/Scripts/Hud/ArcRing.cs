using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    /// <summary>
    /// conic-gradient(fill p%, track 0): a disc whose first <see cref="Fraction"/> (clockwise from 12 o'clock) is
    /// <see cref="Fill"/> and the rest <see cref="Track"/>. Used by the turn timer ring and the opening meter.
    /// </summary>
    public sealed class ArcRing : VisualElement
    {
        private float fraction;
        private Color fill, track;

        public ArcRing(Color fill, Color track)
        {
            this.fill = fill; this.track = track; pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public float Fraction { get => fraction; set { value = Mathf.Clamp01(value); if (Mathf.Approximately(value, fraction)) return; fraction = value; MarkDirtyRepaint(); } }
        public Color Fill { get => fill; set { fill = value; MarkDirtyRepaint(); } }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect; if (rect.width <= 0 || rect.height <= 0) return;
            var p = context.painter2D; var center = rect.center; float radius = Mathf.Min(rect.width, rect.height) / 2;
            p.fillColor = track; p.BeginPath(); p.Arc(center, radius, 0, 360); p.Fill();
            if (fraction <= 0) return;
            p.fillColor = fill; p.BeginPath();
            if (fraction >= 1) p.Arc(center, radius, 0, 360);
            else { p.MoveTo(center); p.Arc(center, radius, -90, -90 + 360 * fraction); p.ClosePath(); }
            p.Fill();
        }
    }
}
