using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>table3d.js rollDie/tickDie: four flights, felt roll, two decaying spin axes and the final face wobble.</summary>
    public readonly struct DiceRollMotion
    {
        public const float FlightDuration = 1.75f, ReturnDuration = .45f, Size = .03f;
        private readonly Vector3 start, land, axis, secondaryAxis;
        private readonly Quaternion final, initial;
        private readonly Pose rest;
        private readonly float spin, secondarySpin, wobble, pre;
        public float TotalDuration => pre + FlightDuration;
        public Pose InitialPose => new Pose(start, initial);

        public DiceRollMotion(Vector3 start, Vector3 land, Quaternion final, Vector3 axis, Vector3 secondaryAxis,
            float spin, float secondarySpin, float wobble, Pose rest, bool returning)
        {
            this.start = start; this.land = land; this.final = final; this.axis = axis; this.secondaryAxis = secondaryAxis;
            this.spin = spin; this.secondarySpin = secondarySpin; this.wobble = wobble; this.rest = rest;
            pre = returning ? ReturnDuration : 0;
            initial = Turn(spin, axis) * Turn(secondarySpin, secondaryAxis) * final;
        }

        public Pose Sample(float elapsed)
        {
            elapsed = Mathf.Max(0, elapsed);
            if (elapsed >= TotalDuration) return new Pose(land, final);
            if (elapsed < pre)
            {
                float t = elapsed / pre, k = t * t * t * (t * (t * 6 - 15) + 10);
                return new Pose(Vector3.Lerp(rest.position, start, k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * .06f), Quaternion.Slerp(rest.rotation, initial, k));
            }
            float u = Mathf.Clamp01((elapsed - pre) / FlightDuration);
            if (u >= 1) return new Pose(land, final);
            float f = 1, height = 0;
            if (u < .32f)
            {
                float v = u / .32f, h = start.y - land.y;
                f = .64f * v; height = h + .5f * h * v - 1.5f * h * v * v;
            }
            else if (u < .51f) Flight(u, .32f, .51f, .64f, .82f, .05f, out f, out height);
            else if (u < .64f) Flight(u, .51f, .64f, .82f, .91f, .019f, out f, out height);
            else if (u < .73f) Flight(u, .64f, .73f, .91f, .955f, .007f, out f, out height);
            else if (u < .955f) f = .955f + .045f * (1 - Mathf.Pow(1 - (u - .73f) / (.955f - .73f), 2.4f));

            float r = spin * Mathf.Pow(1 - Mathf.Min(1, u / .955f), 2.6f);
            float r2 = secondarySpin * Mathf.Pow(1 - Mathf.Min(1, u / .63f), 2);
            if (u >= .955f)
            {
                float w = (u - .955f) / (1 - .955f);
                r = wobble * Mathf.Exp(-4.5f * w) * Mathf.Cos(w * 9.5f) * (1 - w);
            }
            var position = Vector3.Lerp(start, land, f);
            position.y = land.y + height + Size / 2 * .82f * (Mathf.Abs(Mathf.Cos(r)) + Mathf.Abs(Mathf.Sin(r)) - 1);
            return new Pose(position, Turn(r, axis) * Turn(r2, secondaryAxis) * final);
        }

        public int ImpactsAt(float elapsed)
        {
            float u = (elapsed - pre) / FlightDuration;
            return u >= .63f ? 3 : u >= .5f ? 2 : u >= .3f ? 1 : 0;
        }

        private static Quaternion Turn(float radians, Vector3 axis) => Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis);
        private static void Flight(float u, float a, float b, float from, float to, float peak, out float fraction, out float height)
        {
            float v = (u - a) / (b - a);
            fraction = Mathf.LerpUnclamped(from, to, v); height = peak * 4 * v * (1 - v);
        }
    }
}
