using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// One tile on the board and its motion (table3d.js startTween / frame): rack-internal slides (0.18–0.34 s, no arc),
    /// travels, cubic Bézier dealing/indicator flips, block transport and settling, hover/selection lift and dragging.
    /// Uses the same source motion sampler as the ceremony and scaled time for the table clock.
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        public enum Motion { Travel, Slide, Deal, Block, Drop, Flip, Lay }

        /// <summary>Engine tile id, or −1 for an opponent tile whose face I cannot see.</summary>
        public int Id = -1;
        public string Code;
        public int Seat = -1;
        public RackSlot OpponentSlot;
        public float LiftWant;
        public bool Dragged;
        /// <summary>Effects (spec/12 §1): sparks in flight, the landing effect of this move, under-tile bars.</summary>
        public TableFx Fx;
        public float NewUntil;
        public GameObject Bar;
        /// <summary>Stable presentation identity for source jitter; independent of a hidden tile's engine identity or face.</summary>
        public string MotionKey;
        private string landing;

        public Pose Target { get; private set; }
        public bool HasTarget { get; private set; }
        public bool Tweening => tween;
        /// <summary>Length of the current (or last) tween in seconds.</summary>
        public float Duration => duration;
        private Pose? overridePose;

        private bool tween, slide, parked;
        private float parkUntil;
        private Vector3? trailFrom;
        private float trailDue;
        private GameObject barOwner; private MeshRenderer barRenderer;
        /// <summary>spec/23: the trail is authored as three points per 60 Hz frame; any frame rate emits that many per second.</summary>
        private const float TrailPerSecond = 180;
        /// <summary>Efekt kalitesi: 1 as designed, ½ at Düşük.</summary>
        public static float TrailScale = 1;
        private DealTileMotion flight;
        private float t0, duration, pop = -10, hop = -10, lift;
        private bool curvedHop;
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
            Target = pose; HasTarget = true; overridePose = null; tween = false; parked = false; hop = pop = -10;
            transform.SetPositionAndRotation(pose.position, pose.rotation); SetVisible(true);
        }

        /// <summary>
        /// The state router's freeze (g101-clock.js: every tween sits at the canonical 100000 ms): a launched move — delayed
        /// or under way — has arrived, its hop and pop played out. A dragged tile stays under the pointer.
        /// </summary>
        public void Settle()
        {
            if (Dragged) return;
            if (tween) { var pose = overridePose ?? Target; transform.SetPositionAndRotation(pose.position, pose.rotation); tween = false; landing = null; trailFrom = null; trailDue = 0; SetVisible(true); }
            hop = pop = -10; NewUntil = 0;   // the new-tile bar (1.6 s) has gone out too
        }

        /// <summary>Retarget; a tween starts only when the target actually changed.</summary>
        public void MoveTo(Pose pose, Motion motion, float delay = 0, float forcedDuration = 0, string fx = null)
        {
            bool fresh = !HasTarget;
            bool changed = !HasTarget || (Target.position - pose.position).sqrMagnitude > 1e-8f || 1 - Mathf.Abs(Quaternion.Dot(Target.rotation, pose.rotation)) > 1e-6f;
            Target = pose; HasTarget = true;
            if (!changed && (overridePose == null || parked)) return;
            parked = false;
            overridePose = null;
            landing = motion == Motion.Slide || motion == Motion.Drop ? null : fx;
            if (Dragged) return;
            Begin(pose, motion, delay, forcedDuration);
            if (fresh && delay > 0) SetVisible(false);
        }

        /// <summary>
        /// Let go where it is: the drop played a move (discard, put back, lay off, draw) whose sync carries the tile on from
        /// here, as table3d.js leaves a released tile in its drag pose until the next sync. Refused (no new target in 0.6 s)
        /// it slides home (0.22 s).
        /// </summary>
        public void Park()
        {
            Dragged = false; transform.localScale = Vector3.one;
            overridePose = new Pose(transform.position, transform.rotation); tween = false; parked = true; parkUntil = Time.time + .6f;
        }

        /// <summary>Live drag preview: slide to a temporary slot (0.2 s) without changing the committed target.</summary>
        public void Preview(Pose? pose)
        {
            if (parked) return;
            if (pose == null) { if (overridePose == null) return; overridePose = null; if (HasTarget) Begin(Target, Motion.Slide, 0, 0.22f); return; }
            if (overridePose.HasValue && (overridePose.Value.position - pose.Value.position).sqrMagnitude < 1e-8f) return;
            overridePose = pose; Begin(pose.Value, Motion.Slide, 0, 0.2f);
        }

        public void Follow(Pose pose) { Dragged = true; follow = pose; }

        public void Release(Motion motion = Motion.Slide)
        {
            Dragged = false; parked = false; GroupBar = false; transform.localScale = Vector3.one;
            Begin(overridePose ?? Target, motion, 0, motion == Motion.Slide ? 0.22f : 0);
        }

        private void Begin(Pose pose, Motion motion, float delay, float forcedDuration)
        {
            if (MotionKey == null) MotionKey = name.StartsWith("tas_") ? name.Substring(4) : name + Id;
            flight = new DealTileMotion(MotionKey, new Pose(transform.position, transform.rotation), pose, (DealTileMotion.Kind)motion, forcedDuration);
            slide = flight.Sliding; t0 = Time.time + delay; tween = true; duration = flight.Duration;
            trailFrom = null; trailDue = 0; hop = -10;
            if (!visible) RefreshVisibility();
        }

        /// <summary>A member of a set moved as a whole: table3d.js shows its bar in M.ok green (#3CCB6E) while it is carried.</summary>
        public bool GroupBar;
        private static Material okBar;

        private void UpdateBar(float now)
        {
            if (Bar == null) return;
            bool show = GroupBar || NewUntil > now;
            if (Bar.activeSelf != show) Bar.SetActive(show);
            if (!show) return;
            if (barOwner != Bar) { barOwner = Bar; barRenderer = Bar.GetComponent<MeshRenderer>(); }
            if (GroupBar && okBar == null)
            {
                okBar = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "gecerli", hideFlags = HideFlags.DontSave };
                okBar.SetColor("_BaseColor", new Color32(0x3C, 0xCB, 0x6E, 255));
            }
            var want = GroupBar ? okBar : TileFactory.NewBar;
            if (barRenderer != null && barRenderer.sharedMaterial != want) barRenderer.sharedMaterial = want;
        }

        private void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            if (Dragged)
            {
                UpdateBar(now);
                transform.position = Vector3.Lerp(transform.position, follow.position, Mathf.Min(1, dt * 22));
                transform.rotation = Quaternion.Slerp(transform.rotation, follow.rotation, Mathf.Min(1, dt * 14));
                return;
            }
            lift += (LiftWant - lift) * Mathf.Min(1, dt * 14);
            if (parked && now >= parkUntil) { parked = false; overridePose = null; if (HasTarget) Begin(Target, Motion.Slide, 0, .22f); }
            if (tween)
            {
                float u = (now - t0) / duration;
                if (u < 0) return;
                if (!visible) SetVisible(true);
                float k = Mathf.Min(1, u);
                var pose = flight.Sample(now - t0);
                transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (Fx != null && k < .94f && landing != null && landing != "reveal" && !slide && trailFrom.HasValue)
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
                    tween = false;
                    if (flight.Curved || landing == "disc" || landing == "travel") { hop = now; curvedHop = flight.Curved; }
                    if (landing != null && !slide) pop = now;
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
                float hopHeight = DealTileMotion.LandingHop(now - hop, curvedHop);
                if (hopHeight > 0) transform.position += Vector3.up * hopHeight;
            }
            UpdateBar(now);
            float scale = DealTileMotion.LandingScale(now - pop);
            if (transform.localScale.x != scale) transform.localScale = Vector3.one * scale;
        }
    }
}
