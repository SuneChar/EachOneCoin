using System;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class MatchSystemTests
    {
        private MatchSessionModel session;
        private MatchSystem rules;
        private static readonly GridPosition[] Spawns = { new GridPosition(0, 0), new GridPosition(2, 2) };

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
        }

        [TearDown]
        public void TearDown() => TestMatchArchitecture.Interface.Deinit();

        [Test]
        public void StartMatch_TransitionsOnlyOnce()
        {
            var match = session.CreateMatch(3, 3, Spawns, 3, 100f);
            Assert.That(rules.StartMatch(), Is.True);
            Assert.That(match.Status, Is.EqualTo(MatchStatus.Playing));
            Assert.That(rules.StartMatch(), Is.False);
        }

        [Test]
        public void Rules_ForwardMatchOperationsAndDamageResult()
        {
            var match = session.CreateMatch(3, 3, Spawns, 3, 100f);
            rules.StartMatch();
            var cell = new GridPosition(1, 0);
            Assert.That(rules.TryPaint(0, cell), Is.True);
            Assert.That(rules.TryCollectCoin(0, cell), Is.True);
            Assert.That(rules.TryObstacleContact(0), Is.True);
            Assert.That(rules.TryDamage(0, 2, 3, out int dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(match.Participants[0].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(rules.TryRespawn(0), Is.True);
            Assert.That(match.Participants[0].Health, Is.EqualTo(2));
            rules.AdvanceTime(1f);
            Assert.That(match.RemainingTime, Is.EqualTo(59f));
        }

        [Test]
        public void Rules_UseNewMatchAndRejectClearedSession()
        {
            var first = session.CreateMatch(3, 3, Spawns, 3, 100f);
            rules.StartMatch();
            var next = session.CreateMatch(3, 3, Spawns, 3, 100f);
            rules.StartMatch();
            Assert.That(rules.TryCollectCoin(0, Spawns[0]), Is.True);
            Assert.That(first.Participants[0].Coins, Is.Zero);
            Assert.That(next.Participants[0].Coins, Is.EqualTo(1));
            session.ClearMatch(next);
            Assert.Throws<InvalidOperationException>(() => rules.AdvanceTime(1f));
        }

        [Test]
        public void Rules_AfterArchitectureDeinitRejectsCalls()
        {
            session.CreateMatch(3, 3, Spawns, 3, 100f);
            TestMatchArchitecture.Interface.Deinit();
            Assert.Throws<InvalidOperationException>(() => rules.TryPaint(0, Spawns[0]));
        }
    }
}
