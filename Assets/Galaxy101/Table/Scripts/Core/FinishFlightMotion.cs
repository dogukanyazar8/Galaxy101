using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>The exact table3d.js finish tween, sampled without scene state or random-number consumption.</summary>
    public readonly struct FinishFlightMotion
    {
        public readonly struct Frame
        {
            public readonly Pose Pose;
            public readonly float Scale;
            public Frame(Pose pose, float scale) { Pose = pose; Scale = scale; }
        }

        public static readonly Pose Target = new Pose(ThreeSpace.ToUnity(0, TY + TD / 2 + .002f, .02f),
            ThreeSpace.ToUnity(ThreeSpace.AxisAngle(Vector3.up, .3f) * ThreeSpace.Euler(-Mathf.PI / 2, 0, 0)));
        public static string Style(string style) => style == "blackhole" || style == "supernova" || style == "meteor" ? style : "nuke";
        private readonly Pose from;
        private readonly Vector3 apex;
        private readonly bool meteor;
        public float Duration => meteor ? 1.8f : 1.75f;

        public FinishFlightMotion(Pose from, string style)
        {
            this.from = from; meteor = Style(style) == "meteor";
            apex = meteor ? from.position + Vector3.up * 1.7f : new Vector3(from.position.x * .6f, TY + .58f, from.position.z * .6f);
        }

        /// <param name="jitter">The source's two independent random values minus 0.5, for the charging x/y tremble.</param>
        public Frame Sample(float elapsed, Vector2 jitter)
        {
            float u = Mathf.Clamp01(elapsed / Duration), scale;
            if (u >= 1) return new Frame(Target, 1);
            Vector3 p;
            if (meteor)
            {
                if (u < .32f) { p = Vector3.Lerp(from.position, apex, Mathf.Pow(u / .32f, 2.2f)); scale = 1; }
                else if (u < .5f) { p = apex; scale = .001f; }
                else
                {
                    float v = (u - .5f) / .5f, e = v * v;
                    p = Vector3.Lerp(Target.position + ThreeSpace.ToUnity(-.75f, 1.5f, -.55f), Target.position, e); scale = 1.35f - e * .35f;
                }
            }
            else if (u < .46f)
            {
                float e = 1 - Mathf.Pow(1 - u / .46f, 3);
                p = Vector3.Lerp(from.position, apex, e); scale = 1 + e * .15f;
            }
            else if (u < .62f)
            {
                float v = (u - .46f) / .16f;
                p = apex + ThreeSpace.ToUnity(jitter.x * .004f * v, Mathf.Sin(v * Mathf.PI) * .015f + jitter.y * .004f * v, 0);
                scale = 1.15f + v * .25f;
            }
            else
            {
                float v = (u - .62f) / .38f, e = v * v * v;
                p = Vector3.Lerp(apex, Target.position, e); scale = 1.4f - e * .4f;
            }
            var rotation = Quaternion.Slerp(from.rotation, Target.rotation, EaseInOutCubic(Mathf.Min(1, u * (meteor ? 1.2f : 1.3f))));
            rotation = Quaternion.AngleAxis(-(1 - EaseInOutCubic(u)) * Mathf.PI * (meteor ? 6 : 5) * Mathf.Rad2Deg, Vector3.up) * rotation;
            return new Frame(new Pose(p, rotation), scale);
        }
    }
}
