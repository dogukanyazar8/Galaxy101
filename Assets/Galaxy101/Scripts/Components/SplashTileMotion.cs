using UnityEngine;

namespace Galaxy101
{
    /// <summary>Source splash: inner floating/Z rotation, then the bottom-origin tileIn wrapper, then 900px perspective.</summary>
    public static class SplashTileMotion
    {
        public const float Perspective = 900, EntryEnd = 1.65f;
        public static readonly Vector2 RowOrigin = new Vector2(506, 203.9f), PerspectiveOrigin = new Vector2(640, 271.9f);
        public static Vector2 Position(int index) { var size = LogoTileMotion.Size(index); return new Vector2(index == 0 ? 506 : index == 1 ? 596 : 696, 339.9f - size.y); }

        public readonly struct Pose
        {
            public readonly float Scale, Opacity, Lift, Tilt, Bob;
            public Pose(float scale, float opacity, float lift, float tilt, float bob) { Scale = scale; Opacity = opacity; Lift = lift; Tilt = tilt; Bob = bob; }
        }

        public static Pose Sample(int index, float time)
        {
            LogoTileMotion.Size(index); // The shared source dimensions also validate the index.
            float u = Mathf.Clamp01((time - .1f - .2f * index) / 1.15f), k = MenuView.CssBezier(.16f, 1, .3f, 1, u);
            float opacity = u >= .45f ? 1 : MenuView.CssBezier(.16f, 1, .3f, 1, u / .45f);
            float phase = Mathf.Repeat(time + 2 * index, 6) / 6;
            float bob = -7 * (phase < .5f ? MenuView.CssBezier(.45f, 0, .55f, 1, phase * 2) : 1 - MenuView.CssBezier(.45f, 0, .55f, 1, phase * 2 - 1));
            return new Pose(.78f + .22f * k, opacity, 80 * (1 - k), -80 * (1 - k), bob);
        }

        public static Vector3 Project(int index, float time, Vector2 localPoint)
        {
            var size = LogoTileMotion.Size(index); var pose = Sample(index, time);
            Vector3 point = Quaternion.AngleAxis(LogoTileMotion.Angle(index), Vector3.forward) * (Vector3)(localPoint - size * .5f);
            point += new Vector3(size.x * .5f, size.y * .5f + pose.Bob, 0);
            var bottom = new Vector3(size.x * .5f, size.y, 0);
            point = Quaternion.AngleAxis(pose.Tilt, Vector3.right) * ((point - bottom) * pose.Scale) + bottom + (Vector3)Position(index) + Vector3.up * pose.Lift;
            float divisor = 1 - point.z / Perspective;
            point.x = PerspectiveOrigin.x + (point.x - PerspectiveOrigin.x) / divisor;
            point.y = PerspectiveOrigin.y + (point.y - PerspectiveOrigin.y) / divisor;
            return point;
        }
    }
}
