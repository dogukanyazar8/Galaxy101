using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// One tile on the board and its motion (table3d.js startTween / frame): rack-internal slides (0.18–0.34 s, no arc),
    /// travels (0.42–0.85 s with a sine arc), the deal's cubic Bézier swirl (0.78 s, ±360° spin), landing pop (+7 %,
    /// 0.28 s), hover/selection lift and drag following. Uses scaled time so tests can run the table fast.
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        public enum Motion { Travel, Slide, Deal }

        /// <summary>Engine tile id, or −1 for an opponent tile whose face I cannot see.</summary>
        public int Id = -1;
        public string Code;
        public int Seat = -1;
        public RackSlot OpponentSlot;
        public float LiftWant;
        public bool Dragged;
        /// <summary>Effects (spec/12 §1): sparks in flight, the landing effect of this move, under-tile bars.</summary>
        public TableFx Fx;
        public bool Valid;
        public float NewUntil;
        public GameObject Bar;
        private string landing;

        public Pose Target { get; private set; }
        public bool HasTarget { get; private set; }
        public bool Tweening => tween;
        /// <summary>Length of the current (or last) tween in seconds.</summary>
        public float Duration => duration;
        private Pose? overridePose;

        private bool tween, slide, deal;
        private Vector3? trailFrom;
        private float trailDue;
        private GameObject barOwner; private MeshRenderer barRenderer;
        /// <summary>spec/23: the trail is authored as three points per 60 Hz frame; any frame rate emits that many per second.</summary>
        private const float TrailPerSecond = 180;
        /// <summary>Efekt kalitesi: 1 as designed, ½ at Düşük.</summary>
        public static float TrailScale = 1;
        private Pose from, to;
        private Vector3 c1, c2;
        private float t0, duration, arc, spin, pop = -10, lift;
        private Pose follow;
        private bool visible = true;

        /// <summary>A dealt tile waits hidden on the deck until its turn (inactive objects would not update).</summary>
        private void SetVisible(bool value)
        {
            visible = value;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = value;
        }

        /// <summary>Re-apply the hidden state to renderers added since (tile style change).</summary>
        public void RefreshVisibility() => SetVisible(visible);

        public void Snap(Pose pose)
        {
            Target = pose; HasTarget = true; overridePose = null; tween = false;
            transform.SetPositionAndRotation(pose.position, pose.rotation); SetVisible(true);
        }

        /// <summary>Retarget; a tween starts only when the target actually changed.</summary>
        public void MoveTo(Pose pose, Motion motion, float delay = 0, float forcedDuration = 0, string fx = null)
        {
            bool changed = !HasTarget || (Target.position - pose.position).sqrMagnitude > 1e-8f || 1 - Mathf.Abs(Quaternion.Dot(Target.rotation, pose.rotation)) > 1e-6f;
            Target = pose; HasTarget = true;
            if (!changed && overridePose == null) return;
            overridePose = null;
            landing = motion == Motion.Deal ? "deal" : motion == Motion.Slide ? null : fx;
            if (Dragged) return;
            Begin(pose, motion, delay, forcedDuration);
        }

        /// <summary>Live drag preview: slide to a temporary slot (0.2 s) without changing the committed target.</summary>
        public void Preview(Pose? pose)
        {
            if (pose == null) { if (overridePose == null) return; overridePose = null; if (HasTarget) Begin(Target, Motion.Slide, 0, 0.22f); return; }
            if (overridePose.HasValue && (overridePose.Value.position - pose.Value.position).sqrMagnitude < 1e-8f) return;
            overridePose = pose; Begin(pose.Value, Motion.Slide, 0, 0.2f);
        }

        public void Follow(Pose pose) { Dragged = true; follow = pose; }

        public void Release(Motion motion = Motion.Slide)
        {
            Dragged = false; transform.localScale = Vector3.one;
            Begin(overridePose ?? Target, motion, 0, motion == Motion.Slide ? 0.22f : 0);
        }

        private void Begin(Pose pose, Motion motion, float delay, float forcedDuration)
        {
            from = new Pose(transform.position, transform.rotation); to = pose;
            float distance = Vector3.Distance(from.position, to.position);
            slide = motion == Motion.Slide; deal = motion == Motion.Deal; t0 = Time.time + delay; tween = true;
            duration = forcedDuration > 0 ? forcedDuration : slide ? Mathf.Clamp(0.18f + distance * 0.6f, 0.18f, 0.34f) : Mathf.Clamp(0.42f + distance * 0.38f, 0.42f, 0.85f);
            arc = slide ? 0 : Mathf.Clamp(distance * 0.42f, 0.05f, 0.24f);
            if (deal)
            {
                string key = name + Id;
                float a = Hash(key) * 6.283f, r = 0.16f + Hash(key + "r") * 0.08f;
                c1 = new Vector3(Mathf.Cos(a) * r, TY + 0.3f + Hash(key + "y") * 0.12f, Mathf.Sin(a) * r);
                c2 = Vector3.Lerp(to.position, new Vector3(0, to.position.y, 0), 0.3f) + Vector3.up * 0.22f;
                duration = 0.78f; arc = 0; spin = (Hash(key + "s") > 0.5f ? 1 : -1) * 360f;
                if (delay > 0) SetVisible(false);
            }
        }

        private void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            if (Dragged)
            {
                transform.position = Vector3.Lerp(transform.position, follow.position, Mathf.Min(1, dt * 22));
                transform.rotation = Quaternion.Slerp(transform.rotation, follow.rotation, Mathf.Min(1, dt * 14));
                return;
            }
            lift += (LiftWant - lift) * Mathf.Min(1, dt * 14);
            if (tween)
            {
                float u = (now - t0) / duration;
                if (u < 0) return;
                if (!visible) SetVisible(true);
                float k = Mathf.Min(1, u);
                if (deal)
                {
                    float b = EaseInOutCubic(k), ib = 1 - b;
                    transform.position = from.position * (ib * ib * ib) + c1 * (3 * ib * ib * b) + c2 * (3 * ib * b * b) + to.position * (b * b * b);
                    transform.rotation = Quaternion.AngleAxis((1 - b) * spin, Vector3.up) * Quaternion.Slerp(from.rotation, to.rotation, EaseInOutCubic(Mathf.Min(1, u * 1.1f)));
                }
                else
                {
                    float e = EaseOutCubic(k), e2 = slide ? e : EaseInOutCubic(k);
                    transform.position = Vector3.Lerp(from.position, to.position, e2) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * arc);
                    transform.rotation = Quaternion.Slerp(from.rotation, to.rotation, slide ? e : EaseInOutCubic(Mathf.Min(1, u * 1.15f)));
                }
                if (Fx != null && k < 1 && !slide && trailFrom.HasValue)
                {
                    // spec/23: a soft light trail along the path (gold; green when laid on a meld), spaced evenly since the last point.
                    var color = landing == "meld" ? TableFx.TrailGreen : TableFx.TrailGold;
                    trailDue += dt * TrailPerSecond * TrailScale;
                    int n = Mathf.Min((int)trailDue, 12); trailDue -= n;
                    for (int i = 1; i <= n; i++) Fx.Trail(Vector3.Lerp(trailFrom.Value, transform.position, i / (float)n), color);
                    if (n > 0) trailFrom = transform.position;
                }
                else trailFrom = transform.position;
                if (k >= 1) { trailFrom = null; trailDue = 0; }
                if (u >= 1)
                {
                    tween = false; if (!slide) pop = now;
                    if (landing != null && Fx != null && !slide) { if (landing == "mine") NewUntil = now + 1.6f; Fx.Landed(this, landing); }
                    landing = null;
                }
            }
            else
            {
                // A resting tile writes its transform only when its pose or lift moved (most tiles, most frames: never).
                var rest = overridePose ?? Target;
                var at = lift > 0.0002f ? rest.position + rest.rotation * Vector3.up * lift : rest.position;
                transform.GetPositionAndRotation(out var position, out var rotation);
                if (at != position || rest.rotation != rotation) transform.SetPositionAndRotation(at, rest.rotation);
            }
            if (Bar != null)
            {
                bool isNew = NewUntil > now, show = Valid || isNew;
                if (Bar.activeSelf != show) Bar.SetActive(show);
                if (show)
                {
                    if (barOwner != Bar) { barOwner = Bar; barRenderer = Bar.GetComponent<MeshRenderer>(); }
                    var material = isNew && !Valid ? TileFactory.NewBar : TileFactory.ValidBar;
                    if (barRenderer != null && barRenderer.sharedMaterial != material) barRenderer.sharedMaterial = material;
                }
            }
            float pv = (now - pop) / 0.28f, scale = pv >= 0 && pv < 1 ? 1 + Mathf.Sin(Mathf.PI * pv) * 0.07f : 1;
            if (transform.localScale.x != scale) transform.localScale = Vector3.one * scale;
        }
    }
}
