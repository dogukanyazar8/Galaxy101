using System;
using System.Collections.Generic;

namespace Galaxy101.Table
{
    /// <summary>Scene-owned effect cues. No timers survive a hidden or destroyed flat table.</summary>
    public sealed class FlatEffectTimeline
    {
        private sealed class Cue { public float At; public Action Run; }
        private readonly List<Cue> cues = new List<Cue>();
        private bool cancelled;
        public float Seconds { get; private set; }
        public int PendingCount => cues.Count;
        public const float DiceDuration = 1.5f, FinishFlightDuration = 1.75f;
        public static float GiftLanding(bool rocket) => rocket ? 3.6f : 2.6f;
        public static float GiftDuration(bool rocket) => rocket ? 5.2f : 3.8f;

        public void Schedule(float delay, Action work)
        {
            if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0) throw new ArgumentOutOfRangeException(nameof(delay));
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (cancelled) return;
            var cue = new Cue { At = Seconds + delay, Run = work };
            int index = cues.FindIndex(c => c.At > cue.At);
            if (index < 0) cues.Add(cue); else cues.Insert(index, cue);
        }

        public void Advance(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (cancelled) return;
            Seconds += delta;
            while (!cancelled && cues.Count > 0 && cues[0].At <= Seconds)
            {
                var cue = cues[0]; cues.RemoveAt(0); cue.Run();
            }
        }

        public void Cancel() { cancelled = true; cues.Clear(); }
    }
}
