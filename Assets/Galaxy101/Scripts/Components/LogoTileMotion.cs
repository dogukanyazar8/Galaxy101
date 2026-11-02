using System;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>The HTML logo's individual translate/rotate plus its animated transform, before the shared perspective.</summary>
    public static class LogoTileMotion
    {
        public const float Perspective = 900, EntryEnd = 1.53f;
        public static readonly Vector2 PerspectiveOrigin = new Vector2(640, 233.6f);
        private static readonly Vector2[] Positions = { new Vector2(506, 191.6f), new Vector2(596, 177.6f), new Vector2(696, 191.6f) };
        private static readonly Vector2[] Sizes = { new Vector2(78, 108), new Vector2(88, 122), new Vector2(78, 108) };
        private static readonly float[] Angles = { -10, 2, 10 };

        public readonly struct Pose
        {
            public readonly float Scale, Opacity;
            public readonly Vector2 Translation;
            public readonly Quaternion Rotation;
            public Pose(float scale, float opacity, Vector2 translation, Quaternion rotation)
            { Scale = scale; Opacity = opacity; Translation = translation; Rotation = rotation; }
        }

        public static Vector2 Position(int index) { Validate(index); return Positions[index]; }
        // Yoga rounds layout to its pixel grid. Transforms retain the source's fractional CSS placement.
        public static Vector2 SubpixelOffset(int index)
        { var position = Position(index); return position - new Vector2(Mathf.Floor(position.x), Mathf.Floor(position.y)); }
        public static Vector2 Size(int index) { Validate(index); return Sizes[index]; }
        public static float Angle(int index) { Validate(index); return Angles[index]; }
        public static Pose Sample(int index, float time)
        {
            Validate(index);
            float f = Mathf.Clamp01((time - .15f - .14f * index) / 1.1f);
            float k = MenuView.CssBezier(.16f, 1, .3f, 1, f);
            float opacity = f >= .55f ? 1 : MenuView.CssBezier(.16f, 1, .3f, 1, f / .55f);
            float phase = Mathf.Repeat(time + 2 * index, 6) / 6;
            float bob = -7 * (phase < .5f ? MenuView.CssBezier(.45f, 0, .55f, 1, phase * 2) : 1 - MenuView.CssBezier(.45f, 0, .55f, 1, phase * 2 - 1));
            float lift = 46 * (1 - k), angle = Angles[index] * Mathf.Deg2Rad;
            // Individual rotate precedes the transform list, so it rotates the intro's translation too.
            return new Pose(.82f + .18f * k, opacity, new Vector2(-Mathf.Sin(angle) * lift, bob + Mathf.Cos(angle) * lift),
                Quaternion.AngleAxis(Angles[index], Vector3.forward) * Quaternion.AngleAxis(-70 * (1 - k), Vector3.right));
        }

        /// <summary>Projects a tile-local point into the authored 1280×720 canvas; z is its CSS depth.</summary>
        public static Vector3 Project(int index, float time, Vector2 localPoint)
        {
            var pose = Sample(index, time); Vector2 center = Sizes[index] * .5f;
            Vector3 point = pose.Rotation * ((Vector3)(localPoint - center) * pose.Scale);
            point += (Vector3)(Positions[index] + center + pose.Translation);
            float divisor = 1 - point.z / Perspective;
            point.x = PerspectiveOrigin.x + (point.x - PerspectiveOrigin.x) / divisor;
            point.y = PerspectiveOrigin.y + (point.y - PerspectiveOrigin.y) / divisor;
            return point;
        }

        private static void Validate(int index)
        { if ((uint)index >= Positions.Length) throw new ArgumentOutOfRangeException(nameof(index)); }
    }
}
