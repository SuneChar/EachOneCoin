using System;

namespace EachOneCoin
{
    // One mine per cycle; each player accumulates continuous connectivity independently.
    public sealed class MineCycleModel
    {
        public const double CycleSeconds = 10d;
        public const double ActivationSeconds = 5d;
        private readonly double[] progress;
        public GridPosition Position { get; private set; }
        public double Elapsed { get; private set; }
        public bool Scattered { get; private set; }

        public MineCycleModel(int participantCount)
        {
            if (participantCount <= 0) throw new ArgumentOutOfRangeException(nameof(participantCount));
            progress = new double[participantCount];
        }

        public double Progress(int participantId) => progress[participantId];
        public void ResetProgress(int participantId) => progress[participantId] = 0d;

        public void BeginCycle(GridPosition position)
        {
            Position = position;
            Elapsed = 0d;
            Scattered = false;
            Array.Clear(progress, 0, progress.Length);
        }

        // The caller splits elapsed time at cycle boundaries and supplies the current connectivity.
        public bool Advance(double delta, bool[] connected)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0d) throw new ArgumentOutOfRangeException(nameof(delta));
            if (connected == null || connected.Length != progress.Length) throw new ArgumentException("Connectivity must match participant count.", nameof(connected));
            double step = Math.Min(delta, Math.Max(0d, CycleSeconds - Elapsed));
            bool completed = false;
            for (int i = 0; i < progress.Length; i++)
            {
                if (!connected[i]) { progress[i] = 0d; continue; }
                double remaining = ActivationSeconds - progress[i];
                if (!Scattered && remaining <= step && Elapsed + Math.Max(0d, remaining) < CycleSeconds) completed = true;
                progress[i] = Math.Min(ActivationSeconds, progress[i] + step);
            }
            Elapsed += step;
            if (completed) Scattered = true;
            return completed;
        }
    }
}
