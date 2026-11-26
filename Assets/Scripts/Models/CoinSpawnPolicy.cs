using System;

namespace EachOneCoin
{
    // Pure selection rules so mine rotation and coin dispersion stay testable without a running match.
    public static class CoinSpawnPolicy
    {
        // A roll in [0, CandidateCount) picks a mine while never repeating the previous index.
        public static int CandidateCount(int mineCount, int previous)
        {
            if (mineCount <= 0) throw new ArgumentOutOfRangeException(nameof(mineCount));
            return mineCount == 1 || previous < 0 || previous >= mineCount ? mineCount : mineCount - 1;
        }

        public static int SelectMine(int mineCount, int previous, int roll)
        {
            int candidates = CandidateCount(mineCount, previous);
            if (roll < 0 || roll >= candidates) throw new ArgumentOutOfRangeException(nameof(roll));
            return candidates == mineCount || roll < previous ? roll : roll + 1;
        }

        // Preferred spread: separated cells that also sit in different quadrants around the mine.
        public static bool AreDispersed(GridPosition origin, GridPosition first, GridPosition second, int minimumDistance)
            => IsApart(first, second, minimumDistance) && Quadrant(origin, first) != Quadrant(origin, second);

        // Fallback spread: distinct cells that are not direct neighbours.
        public static bool IsApart(GridPosition first, GridPosition second, int minimumDistance)
            => !first.Equals(second) && Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y) >= minimumDistance;

        private static int Quadrant(GridPosition origin, GridPosition cell)
            => ((Math.Sign(cell.X - origin.X) + 1) * 3) + Math.Sign(cell.Y - origin.Y) + 1;
    }
}
