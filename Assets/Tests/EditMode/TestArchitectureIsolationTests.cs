using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class TestArchitectureIsolationTests
    {
        [Test]
        public void IndependentArchitecture_DoesNotReplaceOrClearGameSession()
        {
            var gameSession = GameArchitecture.Interface.GetModel<MatchSessionModel>();
            if (gameSession.Match != null) Assert.Ignore("An active game session must not be replaced by the isolation test.");
            MatchModel sentinel = null;
            try
            {
                sentinel = gameSession.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var testSession = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
                testSession.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
                var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
                rules.StartMatch();
                Assert.That(rules.TryCollectCoin(0, new GridPosition(0, 0)), Is.True);
                TestMatchArchitecture.Interface.Deinit();
                Assert.That(gameSession.Match, Is.SameAs(sentinel));
                Assert.That(sentinel.Status, Is.EqualTo(MatchStatus.Waiting));
                Assert.That(sentinel.Participants[0].Coins, Is.Zero);
            }
            finally
            {
                TestMatchArchitecture.Interface.Deinit();
                gameSession.ClearMatch(sentinel);
            }
        }
    }
}
