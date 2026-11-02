using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        private sealed class FlatTween
        {
            public VisualElement Element; public float Start, Duration; public Action<float> Apply; public bool Keep;
        }
        private readonly struct FlatKey
        {
            public readonly float At, Scale, Rotation, Opacity; public readonly Vector2 Position;
            public FlatKey(float at, Vector2 position, float scale = 1, float rotation = 0, float opacity = 1)
            { At = at; Position = position; Scale = scale; Rotation = rotation; Opacity = opacity; }
        }
        private readonly List<FlatTween> flatTweens = new List<FlatTween>();
        private FlatEffectTimeline flatEffectTime = new FlatEffectTimeline();
        private readonly System.Random flatEffectRandom = new System.Random(42);
        private VisualElement flatEffectLayer, flatDie, flatFinishGhost;
        private bool flatEffectsFocused = true, flatEffectSkipFrame;
        private int flatEffectHand = -1, flatFinishingId = -1, flatDieRollCount;
        private float flatFinishLandedAt;
        private float flatLastMoment = -1;
        private string flatLastDealStep;

        private void EnsureFlatEffectLayer()
        {
            if (flatEffectLayer != null) return;
            flatEffectLayer = Layer("table-flat-effects"); flatRoot.Add(flatEffectLayer);
        }

        private float FlatRandom() => (float)flatEffectRandom.NextDouble();
        private static Vector2 FlatSeatPoint(int seat) => seat == 0 ? new Vector2(55, 650) : seat == 1 ? new Vector2(1188, 292) : seat == 2 ? new Vector2(640, 116) : new Vector2(92, 292);

        private VisualElement FlatEffectBox(string name, float x, float y, float width, float height, string paint, int radius)
        {
            EnsureFlatEffectLayer();
            var item = FlatBox(name, new Table2DLayout.Box(x, y, width, height), paint, radius); flatEffectLayer.Add(item); return item;
        }

        private void FlatAnimate(VisualElement element, float duration, Action<float> apply, float delay = 0, bool keep = false)
        {
            apply(0); flatTweens.Add(new FlatTween { Element = element, Start = flatEffectTime.Seconds + delay, Duration = duration, Apply = apply, Keep = keep });
        }

        private static void FlatKeyPose(VisualElement element, FlatKey[] keys, float progress)
        {
            int i = 0; while (i < keys.Length - 2 && progress > keys[i + 1].At) i++;
            var a = keys[i]; var b = keys[i + 1]; float f = Mathf.InverseLerp(a.At, b.At, progress);
            var p = Vector2.LerpUnclamped(a.Position, b.Position, f);
            element.style.translate = new Translate(p.x, p.y); element.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(a.Scale, b.Scale, f));
            element.style.rotate = new Rotate(Mathf.LerpUnclamped(a.Rotation, b.Rotation, f)); element.style.opacity = Mathf.LerpUnclamped(a.Opacity, b.Opacity, f);
        }

        private void UpdateFlatEffects()
        {
            if (!IsTwoDimensional || flatRoot == null || !flatEffectsFocused || GameClock.Frozen) return;
            if (flatEffectSkipFrame) { flatEffectSkipFrame = false; return; }
            flatEffectTime.Advance(Time.unscaledDeltaTime);
            for (int i = flatTweens.Count - 1; i >= 0; i--)
            {
                var tween = flatTweens[i]; float elapsed = flatEffectTime.Seconds - tween.Start;
                if (elapsed < 0) continue;
                tween.Apply(Mathf.Clamp01(elapsed / tween.Duration));
                if (elapsed < tween.Duration) continue;
                flatTweens.RemoveAt(i); if (!tween.Keep) tween.Element.RemoveFromHierarchy();
            }
        }

        private void ClearFlatEffects()
        {
            flatEffectTime.Cancel(); flatEffectTime = new FlatEffectTimeline(); flatTweens.Clear(); flatEffectLayer?.Clear();
            flatDie = flatFinishGhost = null; flatFinishingId = -1; flatDieRollCount = 0; flatLastMoment = -1; flatLastDealStep = null;
            if (flatRoot != null) flatRoot.style.translate = StyleKeyword.Null;
            foreach (var tile in flatTiles.Values) { tile.Element.style.visibility = Visibility.Visible; tile.Element.style.opacity = 1; }
        }

        private void CancelFlatTileMotion(VisualElement element) => flatTweens.RemoveAll(t => t.Element == element);

        private void PlaceFlatTile(FlatTile tile, Table2DLayout.Box target, bool instant)
        {
            bool sameTarget = tile.Positioned && tile.Placed.X == target.X && tile.Placed.Y == target.Y && tile.Placed.Width == target.Width && tile.Placed.Height == target.Height;
            if (!instant && !GameClock.Frozen && sameTarget && flatTweens.Exists(t => t.Element == tile.Element)) return;
            var element = tile.Element;
            if (!instant && !GameClock.Frozen && sameTarget && element.style.left.value.value == target.X && element.style.top.value.value == target.Y && element.style.width.value.value == target.Width && element.style.height.value.value == target.Height) return;
            bool fresh = !tile.Positioned; tile.Positioned = true; tile.Placed = target;
            CancelFlatTileMotion(element);
            if (instant || GameClock.Frozen)
            { MenuView.Place(element, target.X, target.Y, target.Width, target.Height); element.style.opacity = 1; return; }
            float x = fresh ? 617 : element.style.left.value.value, y = fresh ? 258 : element.style.top.value.value;
            float width = fresh ? target.Width : element.style.width.value.value, height = fresh ? target.Height : element.style.height.value.value;
            float duration = fresh ? .5f : .34f;
            FlatAnimate(element, duration, f =>
            {
                float position = MenuView.CssBezier(.22f, 1, .36f, 1, f);
                float size = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01(f * duration / .25f));
                MenuView.Place(element, Mathf.Lerp(x, target.X, position), Mathf.Lerp(y, target.Y, position), Mathf.Lerp(width, target.Width, size), Mathf.Lerp(height, target.Height, size));
                element.style.opacity = fresh ? MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01(f * duration / .3f)) : 1;
            }, fresh ? .016f : 0, keep: true);
        }

        private void ObserveFlatEffects()
        {
            if (!IsTwoDimensional) return;
            int hand = flatGame?.Model?.HandNo ?? -1;
            if (hand != flatEffectHand) { ClearFlatEffects(); flatEffectHand = hand; }
            var deal = Current.Deal;
            var announced = flatGame?.Model?.Deal;
            if (deal?.Step == "rolling" && flatDie == null && announced?.Dice.Count > 0)
                FlatRollDie(deal.Dealer, announced.Dice[announced.Dice.Count - 1].Value);
            if (deal?.Step != flatLastDealStep)
            {
                if (deal?.Step == "start" || deal?.Step == "centre") FlatDealFx();
                if (deal == null || deal.Step != "roll" && deal.Step != "rolling") ClearFlatDice();
                flatLastDealStep = deal?.Step;
            }
            var moment = Current.Moment;
            if (moment != null && moment.StartedAt != flatLastMoment)
            {
                flatLastMoment = moment.StartedAt;
                if (moment.Seat >= 0) FlatCelebrate(moment.Seat); else FlatDealFx();
            }
            if (flatFinishingId >= 0 && flatGame?.LastFinishedTile == flatFinishingId && flatEffectTime.Seconds >= flatFinishLandedAt)
            { flatFinishGhost?.RemoveFromHierarchy(); flatFinishGhost = null; }
        }

        private void FlatRing(float x, float y, string color, float duration, float maximum, float delay = 0)
        {
            var ring = FlatEffectBox("flat-fx-ring", x - 40, y - 40, 80, 80, "transparent", 40); Border(ring, 4, view.C(color)); GalaxyFilters.Shadow(ring, 0, 0, 12, view.C(color));
            FlatAnimate(ring, duration, f => { float k = MenuView.CssBezier(.16f, 1, .3f, 1, f); ring.style.scale = new Scale(Vector2.one * Mathf.Lerp(.1f, maximum, k)); ring.style.opacity = 1 - k; }, delay);
        }

        private void FlatSparks(float x, float y, string[] colors, int count, float speed, float delay = 0)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = FlatRandom() * Mathf.PI * 2, radius = speed * (.4f + FlatRandom()), duration = .7f + FlatRandom() * .5f;
                var end = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + 30);
                string color = colors[i % colors.Length]; var spark = FlatEffectBox("flat-fx-spark", x - 3, y - 3, 6, 6, color, 3); GalaxyFilters.Shadow(spark, 0, 0, 4, view.C(color));
                FlatAnimate(spark, duration, f => { float k = MenuView.CssBezier(.2f, .8f, .4f, 1, f); spark.style.translate = new Translate(end.x * k, end.y * k); spark.style.scale = new Scale(Vector2.one * Mathf.Lerp(1, .3f, k)); spark.style.opacity = 1 - k; }, delay);
            }
        }

        private void FlatGlow(float x, float y, string color, float size, float duration, float delay = 0)
        {
            var glow = FlatEffectBox("flat-fx-glow", x - size / 2, y - size / 2, size, size, "radial-gradient(circle," + color + ",transparent 68%)", (int)(size / 2));
            FlatAnimate(glow, duration, f =>
            {
                float k = MenuView.CssBezier(.22f, 1, .36f, 1, f);
                glow.style.scale = new Scale(Vector2.one * (k < .25f ? Mathf.Lerp(.2f, 1, k / .25f) : Mathf.Lerp(1, 1.3f, (k - .25f) / .75f)));
                glow.style.opacity = k < .25f ? k / .25f : 1 - (k - .25f) / .75f;
            }, delay);
        }

        private void FlatShake(float duration)
        {
            var keys = new[] { new FlatKey(0, Vector2.zero), new FlatKey(.2f, new Vector2(-6, 3)), new FlatKey(.4f, new Vector2(5, -4)), new FlatKey(.6f, new Vector2(-4, -2)), new FlatKey(.8f, new Vector2(3, 4)), new FlatKey(1, Vector2.zero) };
            FlatAnimate(flatRoot, duration, f => FlatKeyPose(flatRoot, keys, f), keep: true);
        }

        public void FlatRollDie(int seat, int value)
        {
            if (!IsTwoDimensional || value < 1 || value > 6) return;
            int count = flatGame?.Model?.Deal?.Dice.Count ?? 0;
            if (count > 0 && flatDie != null && flatDieRollCount == count) return;
            ClearFlatDice();
            flatDieRollCount = count;
            var start = seat == 0 ? new Vector2(640, 620) : FlatSeatPoint(seat); var end = new Vector2(600 + FlatRandom() * 80, 300 + FlatRandom() * 60);
            var die = FlatEffectBox("flat-fx-die", -20, -20, 40, 40, "linear-gradient(145deg,#FFFDF7,#E7DDC6)", 10); flatDie = die;
            int[][] pips = { new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 } };
            foreach (int pip in pips[value - 1]) die.Add(FlatBox("die-pip", new Table2DLayout.Box(6 + pip % 3 * 10, 6 + pip / 3 * 10, 8, 8), value == 1 ? "#C81E33" : "#141733", 4));
            var middle = new Vector2((start.x + end.x) / 2, Mathf.Min(start.y, end.y) - 120);
            var keys = new[] { new FlatKey(0, start, .6f), new FlatKey(.35f, middle, 1.15f, 380), new FlatKey(.55f, end + new Vector2(-30, 0), 1, 620), new FlatKey(.68f, end + new Vector2(-12, -22), 1, 700), new FlatKey(.8f, end + new Vector2(-4, 0), 1, 750), new FlatKey(.9f, end + new Vector2(0, -6), 1, 712), new FlatKey(1, end, 1, 720) };
            FlatAnimate(die, FlatEffectTimeline.DiceDuration, f => FlatKeyPose(die, keys, MenuView.CssBezier(.3f, .6f, .4f, 1, f)), keep: true);
            flatEffectTime.Schedule(1.45f, () => FlatRing(end.x, end.y, "var(--a1)", .7f, 1.4f));
        }

        private void ClearFlatDice()
        {
            if (flatDie == null) return; var die = flatDie; flatDie = null;
            flatTweens.RemoveAll(t => t.Element == die);
            FlatAnimate(die, .3f, f => die.style.opacity = 1 - f);
        }

        public void FlatCelebrate(int seat)
        {
            if (!IsTwoDimensional) return;
            var point = FlatSeatPoint(seat);
            for (int i = 0; i < 6; i++)
            {
                float x = point.x + (FlatRandom() - .5f) * 80;
                flatEffectTime.Schedule(i * .22f, () => FlatSparks(x, point.y - 20, new[] { "#FFD964", "#FF6A9A", "#7FE3FF", "#8EF0B0", "#B79CFF" }, 26, 180));
            }
            FlatRing(point.x, point.y, "var(--a1)", .9f, 3);
        }

        public void FlatDealFx()
        {
            if (!IsTwoDimensional) return;
            FlatRing(640, 330, "var(--a1)", .9f, 5); FlatRing(640, 330, "#fff", 1.1f, 4, .16f);
        }

        public void FlatGift(string kind, int from, int to)
        {
            if (!IsTwoDimensional || from < 0 || from > 3 || to < 0 || to > 3) return;
            var start = FlatSeatPoint(from); var end = FlatSeatPoint(to); bool rocket = kind == "rocket";
            var gift = FlatEffectBox("flat-fx-gift", -32, -32, 64, 64, "transparent", 0); var texture = GiftThumb(kind);
            if (texture != null) { var picture = new Image { image = texture, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit }; picture.style.width = picture.style.height = 64; gift.Add(picture); }
            else gift.Add(view.Icon("table-gift", 40, "var(--a1)"));
            GalaxyFilters.Shadow(gift, 0, 8, 6, new Color(0, 0, 0, .45f));
            var middle = new Vector2((start.x + end.x) / 2, Mathf.Min(start.y, end.y) - 140);
            var keys = new[] { new FlatKey(0, start, .3f, 0, 0), new FlatKey(.45f, middle, rocket ? 1.5f : 1.2f, rocket ? -40 : -12), new FlatKey(.7f, end + new Vector2(0, -60)), new FlatKey(.93f, end + new Vector2(0, -60)), new FlatKey(1, end + new Vector2(0, -80), .8f, 0, 0) };
            FlatAnimate(gift, FlatEffectTimeline.GiftDuration(rocket), f => FlatKeyPose(gift, keys, MenuView.CssBezier(.33f, .8f, .4f, 1, f)));
            flatEffectTime.Schedule(FlatEffectTimeline.GiftLanding(rocket), () => { FlatSparks(end.x, end.y - 60, new[] { "#FFE08A", "#fff" }, 14, 80); GiftLanded(kind, from, to); });
        }

        public float FlatFinish(int id, string style = null)
        {
            if (!IsTwoDimensional || flatFinishingId == id) return 0;
            flatFinishingId = id;
            flatFinishLandedAt = flatEffectTime.Seconds + FlatEffectTimeline.FinishFlightDuration;
            style = style ?? Settings.Text("bombFx") ?? "nuke";
            var source = flatPositions.TryGetValue(id, out var pose) ? new Vector2(pose.X, pose.Y) : new Vector2(604, 240);
            var tile = FlatEffectBox("flat-fx-finish-tile", 0, 0, 72, 100, "linear-gradient(var(--tf1),var(--tf2))", 12); flatFinishGhost = tile;
            if (flatGame?.Model?.IsOkey(TileIds.ToV4(id)) == true)
            {
                tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center;
                tile.Add(Text("★", "Montserrat-900", 42, "#E0A021")); tile.Add(Text("OKEY", "Baloo2-800", 13, "#127A4A"));
            }
            else
            {
                int cell = TileFactory.CellOf(TileCodes.Code(id)); var image = new Image { image = flatAtlas, uv = new Rect(cell % 8 / 8f, 1 - (cell / 8 + 1) / 7f, 1 / 8f, 1 / 7f), pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill }; image.style.width = 72; image.style.height = 100; tile.Add(image);
            }
            if (flatTiles.TryGetValue(id, out var original)) original.Element.style.visibility = Visibility.Hidden;
            Vector2 end = new Vector2(604, 240);
            var keys = style == "meteor" ? new[] { new FlatKey(0, source), new FlatKey(.32f, source + new Vector2(0, -700), 1.2f), new FlatKey(.5f, end + new Vector2(420, -600), 1.4f, -30), new FlatKey(1, end) }
                : new[] { new FlatKey(0, source), new FlatKey(.45f, end + new Vector2(0, -150), 1.6f, -8), new FlatKey(.62f, end + new Vector2(0, -165), 1.75f, 6), new FlatKey(1, end) };
            FlatAnimate(tile, FlatEffectTimeline.FinishFlightDuration, f => FlatKeyPose(tile, keys, MenuView.CssBezier(.5f, 0, .6f, 1, f)), keep: true);
            flatEffectTime.Schedule(FlatEffectTimeline.FinishFlightDuration, () =>
            {
                FlatFinishImpact(style);
                if (flatGame?.LastFinishedTile == id)
                {
                    flatFinishGhost?.RemoveFromHierarchy(); flatFinishGhost = null;
                    if (flatTiles.TryGetValue(id, out var closed)) closed.Element.style.visibility = Visibility.Visible;
                }
            });
            return FlatEffectTimeline.FinishFlightDuration;
        }

        private void FlatFinishImpact(string style)
        {
            const float x = 640, y = 290;
            if (style == "blackhole")
            {
                var core = FlatEffectBox("flat-fx-blackhole", x - 80, y - 80, 160, 160, "radial-gradient(circle,#030108 0 38%,#E6DDFF 44%,#B79CFF 52%,rgba(58,42,144,.6) 62%,transparent 72%)", 80);
                var keys = new[] { new FlatKey(0, Vector2.zero, 0), new FlatKey(.35f, Vector2.zero, 1, 200), new FlatKey(.75f, Vector2.zero, 1.05f, 520), new FlatKey(1, Vector2.zero, 0, 720) };
                FlatAnimate(core, 1.5f, f => FlatKeyPose(core, keys, MenuView.CssBezier(.22f, 1, .36f, 1, f)));
                FlatSparks(x, y, new[] { "#B79CFF", "#7FE3FF" }, 40, -180);
                flatEffectTime.Schedule(1.15f, () => { FlatGlow(x, y, "#EDE6FF", 460, 1.1f); FlatRing(x, y, "#fff", .7f, 7); FlatRing(x, y, "#B79CFF", 1, 6, .12f); FlatRing(x, y, "#7FE3FF", 1.2f, 5, .24f); FlatSparks(x, y, new[] { "#B79CFF", "#7FE3FF", "#fff" }, 60, 260); FlatShake(.9f); });
            }
            else if (style == "supernova")
            {
                FlatGlow(x, y, "#fff", 620, 1.5f);
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * 22.5f; var ray = FlatEffectBox("flat-fx-ray", x, y - 2, 300, 4, "linear-gradient(90deg," + (i % 2 == 1 ? "#9FD8FF" : "#fff") + ",transparent)", 2);
                    ray.style.transformOrigin = new TransformOrigin(0, Length.Percent(50)); ray.style.rotate = new Rotate(angle);
                    FlatAnimate(ray, 1.1f, f => { float k = MenuView.CssBezier(.22f, 1, .36f, 1, f); ray.style.scale = new Scale(new Vector2(k, 1)); ray.style.opacity = 1 - k; }, i * .012f);
                }
                FlatRing(x, y, "#fff", .9f, 8); FlatRing(x, y, "#9FD8FF", 1.2f, 6.5f, .12f); FlatRing(x, y, "#FFD27A", 1.5f, 5, .26f); FlatSparks(x, y, new[] { "#fff", "#9FD8FF", "#FFD27A" }, 70, 300); FlatShake(1);
            }
            else if (style == "meteor")
            {
                FlatGlow(x, y, "#FFF1C0", 480, 1.2f); FlatRing(x, y, "#FFD27A", .9f, 6); FlatRing(x, y, "#FF7A3D", 1.2f, 5, .14f); FlatSparks(x, y, new[] { "#FFB347", "#FF6A2E", "#FFE08A" }, 70, 240);
                for (int i = 0; i < 8; i++)
                {
                    float ex = x + (FlatRandom() - .5f) * 520, ey = y + (FlatRandom() - .5f) * 300, delay = .15f + i * .11f;
                    var meteor = FlatEffectBox("flat-fx-meteor", ex - 6, ey - 6, 12, 12, "#FFE08A", 6); GalaxyFilters.Shadow(meteor, 0, 0, 7, view.C("#FF7A3D"));
                    FlatAnimate(meteor, .42f, f => { float k = MenuView.CssBezier(.5f, 0, 1, .6f, f); meteor.style.translate = new Translate(160 * (1 - k), -260 * (1 - k)); }, delay);
                    flatEffectTime.Schedule(delay + .42f, () => { FlatRing(ex, ey, "#FF7A3D", .6f, 2.5f); FlatSparks(ex, ey, new[] { "#FFB347", "#FF6A2E" }, 12, 70); });
                }
                FlatShake(1.1f);
            }
            else
            {
                FlatGlow(x, y, "#FFF4D6", 520, 1.3f); FlatRing(x, y, "#fff", .8f, 7); FlatRing(x, y, "#FFD964", 1, 6, .11f); FlatRing(x, y, "#FF6A2E", 1.3f, 5, .24f); FlatSparks(x, y, new[] { "#FFE08A", "#FF7A3D", "#fff" }, 60, 260); FlatShake(1);
            }
        }

        private void BuildFlatFinishChoices(VisualElement row)
        {
            var choices = Row(4); Pad(choices, 4, 4, 4, 4); view.Decorate(choices, "rgba(255,106,46,.14)", 12);
            string[] keys = { "nuke", "blackhole", "supernova", "meteor" }, tr = { "Nükleer", "Kara delik", "Süpernova", "Meteor" }, en = { "Nuke", "Black hole", "Supernova", "Meteor" };
            string[] paint = { "radial-gradient(circle at 50% 45%,#FFF6D8 0 18%,#FFB347 42%,#E2361F 64%,transparent 70%)", "radial-gradient(circle,#05030c 0 36%,#E6DDFF 42%,#B79CFF 50%,#3a2a90 64%,transparent 71%)", "radial-gradient(circle,#FFFFFF 0 22%,#CFEBFF 36%,#6FB8FF 56%,transparent 72%)", "radial-gradient(circle at 64% 36%,#FFF1C0 0 16%,#FF9A4A 38%,#B33A12 60%,transparent 70%)" };
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i]; bool on = (Settings.Text("bombFx") ?? "nuke") == key;
                var button = view.Button("", () => Settings.Set("bombFx", key), "plain"); button.name = "table.t2.bombFx[" + i + "]"; button.tooltip = L(tr[i], en[i]); button.style.width = button.style.height = 32;
                view.Decorate(button, on ? "rgba(255,255,255,.16)" : "rgba(0,0,0,.22)", 9); if (on) Border(button, 2, view.C("#FFB48A"));
                var orb = new VisualElement { pickingMode = PickingMode.Ignore }; orb.style.width = orb.style.height = 20; view.Decorate(orb, paint[i], 10); button.Add(orb); choices.Add(button);
            }
            row.Add(choices);
        }
    }
}
