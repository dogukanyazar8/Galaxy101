using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>table3d.js startTween/frame in Unity space, including the reflected world rotation axes.</summary>
    public readonly struct DealTileMotion
    {
        public enum Kind { Travel, Slide, Deal, Block, Drop, Flip, Lay }

        private readonly Pose from, to;
        private readonly Kind kind;
        private readonly Vector3 c1, c2, axis;
        private readonly float arc, spin, roll;
        public float Duration { get; }
        public bool Curved => kind == Kind.Deal || kind == Kind.Flip;
        public bool Sliding => kind == Kind.Slide || kind == Kind.Drop;

        public DealTileMotion(string key, Pose from, Pose to, Kind kind, float forcedDuration = 0)
        {
            this.from = from; this.to = to; this.kind = kind;
            c1 = c2 = axis = Vector3.zero; spin = roll = 0;
            float distance = Vector3.Distance(from.position, to.position);
            Duration = forcedDuration > 0 ? forcedDuration : kind == Kind.Slide
                ? Mathf.Clamp(.18f + distance * .6f, .18f, .34f) : Mathf.Clamp(.42f + distance * .38f, .42f, .85f);
            arc = kind == Kind.Slide || kind == Kind.Drop ? 0 : Mathf.Clamp(distance * .42f, .05f, .24f);
            if (kind == Kind.Deal)
            {
                c1 = from.position + new Vector3(-(Hash(key + "x") - .5f) * .05f, .12f + Hash(key + "y") * .04f, 0);
                c2 = Vector3.Lerp(to.position, from.position, .18f); c2.y = Mathf.Max(c2.y, to.position.y) + .1f;
                Duration = .56f + Mathf.Clamp(distance * .2f, 0, .24f); arc = 0;
                spin = -(Hash(key + "s") > .5f ? 1 : -1) * 90;
            }
            else if (kind == Kind.Flip)
            {
                var outward = new Vector3(-from.position.x, 0, -from.position.z).normalized;
                c1 = from.position + outward * .1f + Vector3.up * .03f;
                c2 = to.position + outward * .05f + Vector3.up * .2f;
                Duration = forcedDuration > 0 ? forcedDuration : 1.1f; arc = 0;
            }
            else if (kind == Kind.Drop) Duration = forcedDuration > 0 ? forcedDuration : .3f;
            // mode 'lay' (rack → meld): no arc, no roll, clamp(0.3 + d·0.3, 0.3, 0.55).
            else if (kind == Kind.Lay) { arc = 0; Duration = forcedDuration > 0 ? forcedDuration : Mathf.Clamp(.3f + distance * .3f, .3f, .55f); }
            else if (kind == Kind.Block)
            {
                arc = Mathf.Clamp(.04f + distance * .06f, .04f, .09f);
                Duration = forcedDuration > 0 ? forcedDuration : Mathf.Clamp(.46f + distance * .22f, .46f, .72f);
                var direction = to.position - from.position; direction.y = 0;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    axis = Vector3.Cross(Vector3.up, direction.normalized);
                    roll = Mathf.Clamp(distance * .18f, .03f, .11f) * Mathf.Rad2Deg;
                }
            }
            else if (kind != Kind.Slide && kind != Kind.Lay)
            {
                var direction = to.position - from.position; direction.y = 0;
                if (direction.sqrMagnitude > 1e-6f) { axis = -direction.normalized; roll = (Hash(key + "roll") - .5f) * .7f * Mathf.Rad2Deg; }
            }
        }

        public Pose Sample(float elapsed)
        {
            float u = Mathf.Clamp01(elapsed / Duration);
            if (u >= 1) return to;
            Vector3 position; Quaternion rotation;
            if (Curved)
            {
                float b = Sine(u), ib = 1 - b;
                position = from.position * (ib * ib * ib) + c1 * (3 * ib * ib * b) + c2 * (3 * ib * b * b) + to.position * (b * b * b);
                rotation = Quaternion.AngleAxis((1 - Smoother(u)) * spin, Vector3.up) * Quaternion.Slerp(from.rotation, to.rotation, Smoother(u * 1.08f));
            }
            else
            {
                float k = Sliding ? EaseOutCubic(u) : kind == Kind.Block ? Sine(u) : Smoother(u);
                position = Vector3.Lerp(from.position, to.position, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * arc);
                rotation = Quaternion.Slerp(from.rotation, to.rotation, Sliding ? EaseOutCubic(u) : kind == Kind.Block ? Smoother(u) : EaseInOutCubic(Mathf.Min(1, u * 1.15f)));
                if (axis.sqrMagnitude > 0) rotation = Quaternion.AngleAxis(Mathf.Sin(Mathf.PI * u) * roll, axis) * rotation;
            }
            return new Pose(position, rotation);
        }

        public static float LandingHop(float elapsed, bool curved)
        {
            float v = elapsed / (curved ? .26f : .24f);
            return v <= 0 || v >= 1 ? 0 : Mathf.Abs(Mathf.Sin(Mathf.PI * 2 * v)) * Mathf.Pow(1 - v, 2.2f) * (curved ? .0035f : .007f);
        }

        public static float LandingScale(float elapsed)
        {
            float v = elapsed / .34f;
            return v < 0 || v >= 1 ? 1 : 1 + Mathf.Sin(Mathf.PI * v) * (1 - v) * .05f;
        }

        private static float Sine(float u) => .5f - .5f * Mathf.Cos(Mathf.PI * u);
        private static float Smoother(float u) { u = Mathf.Clamp01(u); return u * u * u * (u * (u * 6 - 15) + 10); }
    }
}
