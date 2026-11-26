using System;

namespace EachOneCoin
{
    // Fixed mine-coin timetable: two coins per event, sixteen in the first forty seconds and ten in the last twenty.
    public static class CoinSpawnSchedule
    {
        public const int CoinsPerEvent = 2;
        public const double WarningSeconds = 1.5d;
        private static readonly double[] eventTimes = { 5d, 10d, 15d, 20d, 25d, 30d, 35d, 40d, 42d, 46d, 50d, 54d, 58d };

        public static int EventCount => eventTimes.Length;
        public static int TotalCoins => eventTimes.Length * CoinsPerEvent;

        public static double EventTime(int index)
        {
            if (index < 0 || index >= eventTimes.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return eventTimes[index];
        }

        // The warning never starts before the match does.
        public static double WarningStart(int index) => Math.Max(0d, EventTime(index) - WarningSeconds);

        // Coins the timetable has delivered at this match time, ignoring events that could not find a cell.
        public static int CoinsSpawnedAt(double elapsed)
        {
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed)) throw new ArgumentOutOfRangeException(nameof(elapsed));
            int count = 0;
            for (int i = 0; i < eventTimes.Length; i++)
                if (elapsed >= eventTimes[i]) count += CoinsPerEvent;
            return count;
        }
    }
}
