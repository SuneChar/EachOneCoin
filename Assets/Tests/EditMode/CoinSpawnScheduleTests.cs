using System.Collections.Generic;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class CoinSpawnScheduleTests
    {
        [Test]
        public void Schedule_DeliversSixteenCoinsBeforeFortyAndTenAfter()
        {
            Assert.That(CoinSpawnSchedule.EventCount, Is.EqualTo(13));
            Assert.That(CoinSpawnSchedule.TotalCoins, Is.EqualTo(26));
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(39.999), Is.EqualTo(14), "The eighth event lands exactly on forty seconds.");
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(40), Is.EqualTo(16));
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(60), Is.EqualTo(26));
        }

        [Test]
        public void Schedule_ProducesNothingBeforeTheFirstEvent()
        {
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(0), Is.Zero);
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(4.999), Is.Zero);
            Assert.That(CoinSpawnSchedule.CoinsSpawnedAt(5), Is.EqualTo(CoinSpawnSchedule.CoinsPerEvent));
        }

        [Test]
        public void Schedule_KeepsFiveSecondStepsUntilFortyThenFourSecondsAfter()
        {
            var expected = new[] { 5d, 10d, 15d, 20d, 25d, 30d, 35d, 40d, 42d, 46d, 50d, 54d, 58d };
            Assert.That(expected.Length, Is.EqualTo(CoinSpawnSchedule.EventCount));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(CoinSpawnSchedule.EventTime(i), Is.EqualTo(expected[i]).Within(0.0001), $"Event {i + 1} moved.");
            Assert.That(expected[expected.Length - 1], Is.EqualTo(58d), "The last delivery must leave time to collect.");
        }

        [Test]
        public void Schedule_WarnsBeforeEveryEventAndStaysInsideTheMatch()
        {
            for (int i = 0; i < CoinSpawnSchedule.EventCount; i++)
            {
                Assert.That(CoinSpawnSchedule.WarningStart(i), Is.LessThan(CoinSpawnSchedule.EventTime(i)));
                Assert.That(CoinSpawnSchedule.WarningStart(i), Is.GreaterThanOrEqualTo(0d));
                Assert.That(CoinSpawnSchedule.EventTime(i) - CoinSpawnSchedule.WarningStart(i),
                    Is.EqualTo(CoinSpawnSchedule.WarningSeconds).Within(0.0001));
            }
        }

        [Test]
        public void SelectMine_NeverRepeatsThePreviousMineAndStaysUniform()
        {
            const int mineCount = 4;
            for (int previous = -1; previous < mineCount; previous++)
            {
                int candidates = CoinSpawnPolicy.CandidateCount(mineCount, previous);
                Assert.That(candidates, Is.EqualTo(previous < 0 ? mineCount : mineCount - 1));
                var picks = new List<int>();
                for (int roll = 0; roll < candidates; roll++) picks.Add(CoinSpawnPolicy.SelectMine(mineCount, previous, roll));
                Assert.That(picks, Is.Unique, $"Rolls duplicated with previous {previous}.");
                if (previous >= 0) Assert.That(picks, Has.No.Member(previous), $"Previous mine repeated with previous {previous}.");
                else Assert.That(picks.Count, Is.EqualTo(mineCount));
            }
            Assert.That(CoinSpawnPolicy.CandidateCount(1, 0), Is.EqualTo(1));
            Assert.That(CoinSpawnPolicy.SelectMine(1, 0, 0), Is.Zero);
        }

        [Test]
        public void SelectMine_RejectsInvalidArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CoinSpawnPolicy.CandidateCount(0, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CoinSpawnPolicy.SelectMine(4, -1, 4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CoinSpawnPolicy.SelectMine(4, -1, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CoinSpawnPolicy.SelectMine(4, 2, 3), "Only three mines remain after excluding the previous one.");
        }

        [Test]
        public void Dispersion_RequiresDistanceAndDifferentQuadrants()
        {
            var origin = new GridPosition(10, 10);
            var north = new GridPosition(10, 15);
            var south = new GridPosition(10, 4);
            var east = new GridPosition(14, 10);
            Assert.That(CoinSpawnPolicy.AreDispersed(origin, north, south, 2), Is.True);
            Assert.That(CoinSpawnPolicy.AreDispersed(origin, north, east, 2), Is.True);
            Assert.That(CoinSpawnPolicy.AreDispersed(origin, north, north, 2), Is.False, "A cell cannot host both coins.");
            Assert.That(CoinSpawnPolicy.AreDispersed(origin, new GridPosition(10, 12), new GridPosition(10, 13), 2), Is.False,
                "Neighbouring cells in the same quadrant are not dispersed.");
            Assert.That(CoinSpawnPolicy.IsApart(new GridPosition(10, 12), new GridPosition(10, 13), 2), Is.False);
            Assert.That(CoinSpawnPolicy.IsApart(new GridPosition(10, 12), new GridPosition(12, 12), 2), Is.True);
        }
    }
}
