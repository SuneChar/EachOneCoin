using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class GameFlowTests
    {
        private static MatchModel CreateMatch()
        {
            return TestMatchArchitecture.Interface.GetModel<MatchSessionModel>().CreateMatch(3, 3, new[] { new GridPosition(0, 0) }, 2, 100f, 2);
        }

        [SetUp]
        public void SetUp() => TestMatchArchitecture.Interface.Deinit();

        [TearDown]
        public void TearDown() => TestMatchArchitecture.Interface.Deinit();

        [Test]
        public void NewMatch_AfterPreviousLossStartsWithFreshState()
        {
            var previous = CreateMatch();
            var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            rules.StartMatch();
            Assert.That(rules.TryCollectCoin(0, new GridPosition(0, 0)), Is.True);
            Assert.That(rules.TryObstacleContact(0), Is.True);
            Assert.That(rules.TryObstacleContact(0), Is.True);
            Assert.That(previous.Status, Is.EqualTo(MatchStatus.Finished));

            var next = CreateMatch();
            Assert.That(next.Status, Is.EqualTo(MatchStatus.Waiting));
            Assert.That(next.Participants[0].Coins, Is.Zero);
            Assert.That(next.Participants[0].Health, Is.EqualTo(2));
            Assert.That(next.Participants[0].MaxHealth, Is.EqualTo(2));
        }
    }
}
