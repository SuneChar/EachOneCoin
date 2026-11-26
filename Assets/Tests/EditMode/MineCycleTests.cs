using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class MineCycleTests
    {
        [Test]
        public void ContinuousConnection_CompletesOnceBeforeBoundary()
        {
            var mine = new MineCycleModel(2);
            mine.BeginCycle(new GridPosition(4, 4));
            Assert.That(mine.Advance(4.9, new[] { true, false }), Is.False);
            Assert.That(mine.Advance(0.2, new[] { true, false }), Is.True);
            Assert.That(mine.Scattered, Is.True);
            Assert.That(mine.Advance(1, new[] { true, true }), Is.False);
        }

        [Test]
        public void CutConnection_ResetsOnlyThatPlayersProgress()
        {
            var mine = new MineCycleModel(2);
            mine.BeginCycle(new GridPosition(1, 1));
            mine.Advance(3, new[] { true, true });
            mine.Advance(1, new[] { false, true });
            Assert.That(mine.Progress(0), Is.Zero);
            Assert.That(mine.Progress(1), Is.EqualTo(4));
            Assert.That(mine.Advance(1, new[] { true, true }), Is.True);
            Assert.That(mine.Progress(0), Is.EqualTo(1));
        }

        [Test]
        public void CompletionExactlyAtRefresh_DoesNotScatter()
        {
            var mine = new MineCycleModel(1);
            mine.BeginCycle(new GridPosition(1, 1));
            mine.Advance(5, new[] { false });
            Assert.That(mine.Advance(5, new[] { true }), Is.False);
            Assert.That(mine.Scattered, Is.False);
            mine.BeginCycle(new GridPosition(2, 2));
            Assert.That(mine.Progress(0), Is.Zero);
            Assert.That(mine.Elapsed, Is.Zero);
        }

        [Test]
        public void SeparateMines_AdvanceAndResetIndependently()
        {
            var first = new MineCycleModel(1);
            var second = new MineCycleModel(1);
            first.BeginCycle(new GridPosition(1, 1));
            second.BeginCycle(new GridPosition(2, 2));
            Assert.That(first.Advance(5, new[] { true }), Is.True);
            Assert.That(second.Advance(5, new[] { false }), Is.False);
            Assert.That(first.Scattered, Is.True);
            Assert.That(second.Scattered, Is.False);
            first.BeginCycle(first.Position);
            Assert.That(first.Scattered, Is.False);
            Assert.That(second.Elapsed, Is.EqualTo(5d));
        }

        [Test]
        public void SimultaneousCompletion_OnlyScattersOnce()
        {
            var mine = new MineCycleModel(2);
            mine.BeginCycle(new GridPosition(1, 1));
            Assert.That(mine.Advance(5, new[] { true, true }), Is.True);
            Assert.That(mine.Advance(1, new[] { true, true }), Is.False);
            Assert.That(mine.Progress(0), Is.EqualTo(5));
            Assert.That(mine.Progress(1), Is.EqualTo(5));
        }
    }
}
