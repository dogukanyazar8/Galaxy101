using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public readonly struct TilePlacement
    {
        public readonly Transform Target;
        public readonly Vector3 Start, End;
        public TilePlacement(Transform target, Vector3 start, Vector3 end)
        { Target = target; Start = start; End = end; }
    }

    public sealed class TileMotion : MonoBehaviour
    {
        private IReadOnlyList<TilePlacement> placements = Array.Empty<TilePlacement>();
        private TaskCompletionSource<bool> completion;
        private CancellationTokenRegistration cancellation;
        private int cancelled;
        private float elapsed;
        private bool paused;
        public bool IsPlaying => completion != null;
        public bool ReducedMotion { get; set; }
        public void Prepare(IReadOnlyList<TilePlacement> value)
        {
            if (IsPlaying) throw new InvalidOperationException("A tile transition is already running.");
            placements = value ?? throw new ArgumentNullException(nameof(value));
        }
        public void SetPaused(bool value) { paused = value; }
        public Task PlayAsync(IReadOnlyList<VisibleEvent> events, bool reducedMotion, CancellationToken token)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (IsPlaying) throw new InvalidOperationException("A tile transition is already running.");
            if (token.IsCancellationRequested) { Apply(1); placements = Array.Empty<TilePlacement>(); return Task.FromCanceled(token); }
            if (reducedMotion || ReducedMotion || placements.Count == 0) { Apply(1); placements = Array.Empty<TilePlacement>(); return Task.CompletedTask; }
            elapsed = 0; cancelled = 0; completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Cancellation may arrive from a worker: only Update/OnDisable touch Unity objects.
            cancellation = token.Register(() => Interlocked.Exchange(ref cancelled, 1));
            Apply(0); return completion.Task;
        }
        private void Update()
        {
            if (!IsPlaying) return;
            if (Volatile.Read(ref cancelled) != 0) { Apply(1); Finish(true); return; }
            if (paused) return;
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / .18f); Apply(progress);
            if (progress >= 1) Finish(false);
        }
        private void Apply(float progress)
        {
            var eased = progress * progress * (3 - 2 * progress);
            foreach (var placement in placements)
                if (placement.Target != null)
                    placement.Target.position = progress >= 1 ? placement.End : Vector3.LerpUnclamped(placement.Start, placement.End, eased)
                        + Vector3.up * (.16f * Mathf.Sin(Mathf.PI * progress));
        }
        private void Finish(bool cancel)
        {
            var pending = completion; completion = null; cancellation.Dispose();
            placements = Array.Empty<TilePlacement>();
            if (cancel) pending?.TrySetCanceled(); else pending?.TrySetResult(true);
        }
        // Destruction can run before the controller's OnDestroy. Post continuations so the
        // owner invalidates its lifetime before an await resumes. Disabling alone freezes motion.
        private void OnDestroy() { Finish(true); }
    }
}
