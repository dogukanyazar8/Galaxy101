using System;

namespace Galaxy101
{
    /// <summary>Elapsed animation time; paused intervals never enter the simulation.</summary>
    public sealed class BackgroundTimeState
    {
        public double Seconds { get; private set; }
        public void Advance(double deltaSeconds, bool running)
        {
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (running) Seconds += deltaSeconds;
        }
    }
}
