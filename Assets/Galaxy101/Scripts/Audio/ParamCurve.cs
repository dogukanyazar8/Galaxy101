using System;
using System.Collections.Generic;

namespace Galaxy101.Audio
{
    /// <summary>
    /// The part of a WebAudio AudioParam the prototype's music uses (g101-sound.js setMusic/duck): setValueAtTime,
    /// linearRampToValueAtTime, setTargetAtTime and cancelScheduledValues, evaluated at any time. Unity sets the
    /// AudioSource volume from it each frame.
    /// </summary>
    public sealed class ParamCurve
    {
        private enum Kind { Set, Linear, Target }

        private struct Point
        {
            public Kind Kind;
            public double Time;
            public float Value;
            public float TimeConstant;
        }

        private readonly List<Point> points = new List<Point>();
        private readonly float initial;

        public ParamCurve(float initial) { this.initial = initial; }

        /// <summary>setValueAtTime: the value jumps to <paramref name="value"/> at <paramref name="time"/>.</summary>
        public void Set(float value, double time) => Insert(new Point { Kind = Kind.Set, Time = time, Value = value });

        /// <summary>linearRampToValueAtTime: a straight line from the previous point to (time, value).</summary>
        public void LinearTo(float value, double time) => Insert(new Point { Kind = Kind.Linear, Time = time, Value = value });

        /// <summary>setTargetAtTime: from <paramref name="time"/> the value approaches <paramref name="target"/> exponentially.</summary>
        public void TargetAt(float target, double time, float timeConstant)
        {
            if (timeConstant <= 0) throw new ArgumentOutOfRangeException(nameof(timeConstant));
            Insert(new Point { Kind = Kind.Target, Time = time, Value = target, TimeConstant = timeConstant });
        }

        /// <summary>cancelScheduledValues: drops every point at or after <paramref name="time"/>.</summary>
        public void CancelFrom(double time) => points.RemoveAll(p => p.Time >= time);

        /// <summary>The prototype's "cancel, then setValueAtTime(param.value, now)": freezes the value at <paramref name="time"/>.</summary>
        public void HoldAt(double time)
        {
            float value = Evaluate(time);
            CancelFrom(time);
            Set(value, time);
        }

        /// <summary>True once no point lies after <paramref name="time"/> (the value no longer changes, unless a target is still approaching).</summary>
        public bool SettledAt(double time)
        {
            foreach (var p in points)
                if (p.Time > time || p.Kind == Kind.Target) return false;
            return true;
        }

        public float Evaluate(double time)
        {
            float value = initial;
            double at = double.NegativeInfinity;
            bool approaching = false;
            float target = 0, timeConstant = 1;
            foreach (var p in points)
            {
                if (p.Time > time)
                {
                    // Inside a linear ramp: from the previous point (its value at its time) to this one.
                    if (p.Kind == Kind.Linear && !double.IsNegativeInfinity(at) && p.Time > at)
                        return Lerp(value, p.Value, (float)((time - at) / (p.Time - at)));
                    break;
                }
                float before = approaching ? Approach(value, target, timeConstant, p.Time - at) : value;
                approaching = false;
                switch (p.Kind)
                {
                    case Kind.Set:
                    case Kind.Linear:
                        value = p.Value;
                        break;
                    case Kind.Target:
                        value = before; target = p.Value; timeConstant = p.TimeConstant; approaching = true;
                        break;
                }
                at = p.Time;
            }
            return approaching ? Approach(value, target, timeConstant, time - at) : value;
        }

        private void Insert(Point point)
        {
            // Sorted by time; a point at the same time as an earlier one goes after it (WebAudio keeps call order).
            int i = points.Count;
            while (i > 0 && points[i - 1].Time > point.Time) i--;
            points.Insert(i, point);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * (t < 0 ? 0 : t > 1 ? 1 : t);

        private static float Approach(float from, float target, float timeConstant, double elapsed) =>
            target + (from - target) * (float)Math.Exp(-elapsed / timeConstant);
    }
}
