using System;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class MatchSessionTests
    {
        private MatchSessionModel session;
        private static readonly GridPosition[] Spawns = { new GridPosition(0, 0) };

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
        }

        [TearDown]
        public void TearDown()
        {
            TestMatchArchitecture.Interface.Deinit();
        }

        [Test]
        public void CreateAndClearMatch_ProvidesFreshState()
        {
            var first = session.CreateMatch(2, 2, Spawns, 3, 100f);
            var rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            rules.StartMatch();
            rules.TryCollectCoin(0, Spawns[0]);
            rules.TryPaint(0, new GridPosition(1, 0));
            rules.TickStamina(1f, true, true, true, 25f, 20f);
            session.ClearMatch(first);
            Assert.That(session.Match, Is.Null);
            Assert.That(session.Stamina, Is.Null);

            var next = session.CreateMatch(2, 2, Spawns, 3, 100f);
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(session.Match, Is.SameAs(next));
            Assert.That(next.Status, Is.EqualTo(MatchStatus.Waiting));
            Assert.That(next.Participants[0].Coins, Is.Zero);
            Assert.That(next.Grid.OwnerAt(new GridPosition(1, 0)), Is.Null);
            Assert.That(session.Stamina.Current, Is.EqualTo(100f));
            Assert.That(session.Stamina.IsSprinting, Is.False);
        }

        [Test]
        public void ClearOldMatch_DoesNotClearNewMatch()
        {
            var first = session.CreateMatch(2, 2, Spawns, 3, 100f);
            var next = session.CreateMatch(2, 2, Spawns, 3, 50f);
            var stamina = session.Stamina;
            session.ClearMatch(first);
            Assert.That(session.Match, Is.SameAs(next));
            Assert.That(session.Stamina, Is.SameAs(stamina));
        }

        [Test]
        public void FailedCreation_PreservesExistingSession()
        {
            var match = session.CreateMatch(2, 2, Spawns, 3, 100f);
            var stamina = session.Stamina;
            Assert.Throws<ArgumentOutOfRangeException>(() => session.CreateMatch(2, 2, Spawns, 3, 0f));
            Assert.That(session.Match, Is.SameAs(match));
            Assert.That(session.Stamina, Is.SameAs(stamina));
        }

        [Test]
        public void Deinit_ClearsSessionAndCreatesNewArchitecture()
        {
            session.CreateMatch(2, 2, Spawns, 3, 100f);
            TestMatchArchitecture.Interface.Deinit();
            Assert.That(session.Match, Is.Null);
            Assert.That(session.Stamina, Is.Null);
            var next = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            Assert.That(next, Is.Not.SameAs(session));
            Assert.That(next.Match, Is.Null);
        }
    }
}
