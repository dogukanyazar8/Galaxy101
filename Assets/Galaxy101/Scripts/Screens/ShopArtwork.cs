using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    // Geometry and paints from Galaxy101_AnaMenu_1a.dc.html, lines 447–493.
    internal static class ShopArtwork
    {
        internal static VisualElement Deal(MenuView v)
        {
            var art = Canvas(v, 86, 64);
            Place(art, Coin(v, 44, "rgba(0,0,0,.25)"), 0, 18);
            Place(art, Coin(v, 48, "rgba(0,0,0,.25)"), 24, 0);
            var gem = Gem(v, 26, 6, "linear-gradient(135deg,#B8F1FF,#3D7BFF)");
            GalaxyFilters.Shadow(gem, 0, 0, 7, v.C("rgba(127,227,255,.8)"));
            Place(art, gem, 54, 24);
            return art;
        }

        internal static VisualElement Coins(MenuView v, float scale)
        {
            var art = Canvas(v, 92, 72);
            Place(art, Coin(v, 44, "#8A5208"), 0, 26);
            Place(art, Coin(v, 44, "#8A5208"), 46, 22);
            var front = Coin(v, 50, "#8A5208");
            GalaxyFilters.Shadow(front, 0, 0, 10, v.C("rgba(255,201,74,.45)"));
            Place(art, front, 22, 0);
            art.style.scale = new Scale(new Vector3(scale, scale, 1));
            return art;
        }

        internal static VisualElement Gems(MenuView v, float scale)
        {
            var art = Canvas(v, 80, 72);
            var large = Gem(v, 46, 10, "linear-gradient(135deg,#C8F6FF,#7FE3FF 40%,#3D7BFF)");
            large.style.overflow = Overflow.Hidden;
            large.Add(new InsetPaint(46, 46, 10, -4, -4, v.C("rgba(0,0,0,.15)")));
            GalaxyFilters.Shadow(large, 0, 0, 12, v.C("rgba(127,227,255,.6)"));
            Place(art, large, 14, 10);
            Place(art, Gem(v, 20, 5, "linear-gradient(135deg,#C8F6FF,#3D7BFF)"), 56, 40);
            art.style.scale = new Scale(new Vector3(scale, scale, 1));
            return art;
        }

        internal static VisualElement PriceGem(MenuView v) => Gem(v, 13, 3, "linear-gradient(135deg,#C8F6FF,#3D7BFF)");

        internal static VisualElement Tile(MenuView v, JObject offer)
        {
            var tile = Canvas(v, 60, 82);
            v.Decorate(tile, (string)offer["face"], 11, 5, (string)offer["edge2"]);
            tile.style.overflow = Overflow.Hidden;
            tile.Add(new InsetPaint(60, 82, 11, 0, -6, v.C((string)offer["edge"])));
            var glow = v.C((string)offer["glow"]);
            if (glow.a > 0) GalaxyFilters.Shadow(tile, 0, 0, 9, glow);
            tile.style.alignItems = Align.Center; tile.style.paddingTop = 8;
            var number = v.Text("7", 46, true, (string)offer["col"]); number.style.height = 46; number.style.flexShrink = 0;
            tile.Add(number);
            var dot = v.Box(); dot.style.width = dot.style.height = 10; dot.style.marginTop = 3;
            v.Decorate(dot, (string)offer["col"], 5); tile.Add(dot);
            return tile;
        }

        internal static void ProductRing(MenuView v, VisualElement card, bool popular)
        {
            // CSS inset box-shadow does not consume layout space like a border.
            card.style.borderTopWidth = card.style.borderRightWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = 0;
            var ring = v.Box(); Fill(ring);
            v.Decorate(ring, "transparent", 22);
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = popular ? 2 : 1;
            var color = v.C(popular ? "a1" : "rgba(255,255,255,.07)");
            ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = color;
            card.Add(ring);
            if (popular) GalaxyFilters.Shadow(card, 0, 0, 12, v.C("gl"));
        }

        internal static void Popular(MenuView v, Label badge)
        {
            badge.style.position = Position.Absolute; badge.style.top = 0; badge.style.left = Length.Percent(50);
            badge.style.translate = new Translate(Length.Percent(-50), 0);
            badge.style.unityFont = v.Font("Baloo2-800"); badge.style.fontSize = 10; MenuView.Tracking(badge, 1, 10);
            badge.style.whiteSpace = WhiteSpace.NoWrap; PanelUi.Pad(badge, 5, 14);
            v.Decorate(badge, "linear-gradient(var(--a1),var(--a2))");
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 12;
        }

        internal static void Shine(MenuView v, VisualElement banner)
        {
            banner.style.overflow = Overflow.Hidden;
            var shine = new ShinePaint(); shine.name = "shop-deal-shine";
            shine.style.position = Position.Absolute; shine.style.left = 22; shine.style.top = shine.style.bottom = 0; shine.style.width = 70;
            banner.Insert(0, shine);
            v.AnimateElement(time =>
            {
                var phase = time % 3.6f / 3.6f;
                var progress = phase >= .6f ? 1 : CssEase(phase / .6f);
                shine.style.translate = new Translate(Mathf.Lerp(-1.6f * 70, 3.2f * 70, progress), 0);
            });
        }

        private static float CssEase(float x)
        {
            // CSS animation-timing-function: ease = cubic-bezier(.25,.1,.25,1).
            var lo = 0f; var hi = 1f;
            for (var i = 0; i < 20; i++)
            {
                var t = (lo + hi) * .5f; var one = 1 - t;
                var px = 3 * one * one * t * .25f + 3 * one * t * t * .25f + t * t * t;
                if (px < x) lo = t; else hi = t;
            }
            var u = (lo + hi) * .5f; var inv = 1 - u;
            return 3 * inv * inv * u * .1f + 3 * inv * u * u + u * u * u;
        }

        private static VisualElement Coin(MenuView v, int size, string shadow)
        {
            var coin = new CoinPaint(size); coin.style.width = coin.style.height = size; coin.style.flexShrink = 0;
            v.Decorate(coin, "transparent", size / 2, 4, shadow);
            return coin;
        }

        private static VisualElement Gem(MenuView v, int size, int radius, string paint)
        {
            var gem = Canvas(v, size, size); gem.style.rotate = new Rotate(45); v.Decorate(gem, paint, radius);
            return gem;
        }

        private static VisualElement Canvas(MenuView v, int width, int height)
        {
            var e = v.Box(); e.style.width = width; e.style.height = height; e.style.flexShrink = 0; e.pickingMode = PickingMode.Ignore;
            return e;
        }

        private static void Place(VisualElement parent, VisualElement child, int left, int top)
        {
            child.style.position = Position.Absolute; child.style.left = left; child.style.top = top; parent.Add(child);
        }

        private static void Fill(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore; e.style.position = Position.Absolute;
            e.style.left = e.style.right = e.style.top = e.style.bottom = 0;
        }

        private sealed class ShinePaint : VisualElement
        {
            internal ShinePaint()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += context =>
                {
                    var p = context.painter2D; var height = contentRect.height;
                    var skew = Mathf.Tan(-20 * Mathf.Deg2Rad) * height / 2;
                    p.fillColor = new Color(1, 1, 1, .35f); p.BeginPath();
                    p.MoveTo(new Vector2(-skew, 0)); p.LineTo(new Vector2(70 - skew, 0));
                    p.LineTo(new Vector2(70 + skew, height)); p.LineTo(new Vector2(skew, height)); p.ClosePath(); p.Fill();
                };
            }
        }

        private sealed class CoinPaint : VisualElement
        {
            private Texture2D texture;
            internal CoinPaint(int size)
            {
                pickingMode = PickingMode.Ignore;
                RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    const int samples = 192;
                    texture = new Texture2D(samples, samples, TextureFormat.RGBA32, false) { name = "Shop CSS radial coin", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                    var pixels = new Color32[samples * samples];
                    ColorUtility.TryParseHtmlString("#FFF3C8", out var first); ColorUtility.TryParseHtmlString("#F5A623", out var mid); ColorUtility.TryParseHtmlString("#B86E0F", out var last);
                    var radius = new Vector2(.65f, .7f).magnitude; // CSS circle farthest-corner from 35% 30%.
                    for (var y = 0; y < samples; y++) for (var x = 0; x < samples; x++)
                    {
                        var t = Vector2.Distance(new Vector2((x + .5f) / samples, 1 - (y + .5f) / samples), new Vector2(.35f, .3f)) / radius;
                        pixels[y * samples + x] = t < .6f ? Color.Lerp(first, mid, t / .6f) : Color.Lerp(mid, last, (t - .6f) / .4f);
                    }
                    texture.SetPixels32(pixels); texture.Apply(false, true); style.backgroundImage = new StyleBackground(texture);
                });
                RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    style.backgroundImage = StyleKeyword.None;
                    if (texture != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                        else UnityEngine.Object.DestroyImmediate(texture);
                    }
                    texture = null;
                });
                generateVisualContent += context =>
                {
                    var p = context.painter2D; p.strokeColor = new Color(1, 243f / 255, 200f / 255); p.lineWidth = 2; p.lineCap = LineCap.Butt;
                    var radius = size / 2f - 1; var count = Mathf.RoundToInt(2 * Mathf.PI * radius / 12); var step = 360f / count;
                    for (var i = 0; i < count; i++) { p.BeginPath(); p.Arc(new Vector2(size / 2f, size / 2f), radius, new Angle(i * step), new Angle((i + .5f) * step)); p.Stroke(); }
                };
            }
        }

        private sealed class InsetPaint : VisualElement
        {
            internal InsetPaint(float width, float height, float radius, float offsetX, float offsetY, Color color)
            {
                Fill(this);
                generateVisualContent += context =>
                {
                    // The hard inset is the outer rounded path minus its translated inner hole.
                    var p = context.painter2D; p.fillColor = color;
                    p.BeginPath(); RoundedPath(p, 0, 0, width, height, radius);
                    RoundedPath(p, offsetX, offsetY, width, height, radius); p.Fill(FillRule.OddEven);
                };
                style.overflow = Overflow.Hidden;
                style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = radius;
            }

            private static void RoundedPath(Painter2D p, float x, float y, float width, float height, float radius)
            {
                p.MoveTo(new Vector2(x + radius, y)); p.LineTo(new Vector2(x + width - radius, y));
                p.ArcTo(new Vector2(x + width, y), new Vector2(x + width, y + radius), radius);
                p.LineTo(new Vector2(x + width, y + height - radius)); p.ArcTo(new Vector2(x + width, y + height), new Vector2(x + width - radius, y + height), radius);
                p.LineTo(new Vector2(x + radius, y + height)); p.ArcTo(new Vector2(x, y + height), new Vector2(x, y + height - radius), radius);
                p.LineTo(new Vector2(x, y + radius)); p.ArcTo(new Vector2(x, y), new Vector2(x + radius, y), radius); p.ClosePath();
            }
        }
    }
}
