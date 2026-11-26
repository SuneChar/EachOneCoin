using System;
using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class MultiplayerRulesTests
    {
        private MatchSystem rules;

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            rules = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
        }

        [TearDown]
        public void TearDown() => TestMatchArchitecture.Interface.Deinit();

        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static MatchModel CreateMatch(int health = 3, int target = 10, float duration = 60f)
        {
            return TestMatchArchitecture.Interface.GetModel<MatchSessionModel>().CreateMatch(5, 3, new[] { P(0, 0), P(4, 0), P(0, 2), P(4, 2) }, health, 100f, target, duration);
        }

        [Test]
        public void Grid_CutAndRepair_UsesFourNeighboursAndProtectedSpawns()
        {
            var grid = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>().CreateMatch(5, 3, new[] { P(0, 0), P(4, 0) }, 3, 100f).Grid;
            rules.StartMatch();
            Assert.That(rules.TryPaint(1, P(0, 0)), Is.False);
            Assert.That(grid.OwnerAt(P(0, 0)), Is.EqualTo(0));
            Assert.That(rules.TryPaint(0, P(1, 0)), Is.True);
            Assert.That(rules.TryPaint(0, P(1, 0)), Is.False);
            Assert.That(rules.TryPaint(0, P(2, 0)), Is.True);
            Assert.That(grid.IsConnected(0, P(2, 0)), Is.True);
            Assert.That(rules.TryPaint(1, P(1, 0)), Is.True);
            Assert.That(grid.IsConnected(0, P(2, 0)), Is.False);
            Assert.That(rules.TryPaint(0, P(1, 0)), Is.True);
            Assert.That(grid.IsConnected(0, P(2, 0)), Is.True);
            rules.TryPaint(0, P(3, 1));
            Assert.That(grid.IsConnected(0, P(3, 1)), Is.False, "Diagonal contact is not a connection.");
        }

        [Test]
        public void Grid_AlternativePathRemainsConnectedUntilAllRoutesAreCut()
        {
            var match = CreateMatch();
            rules.StartMatch();
            foreach (var cell in new[] { P(1, 0), P(2, 0), P(2, 1), P(0, 1), P(1, 1) })
                Assert.That(rules.TryPaint(0, cell), Is.True);
            Assert.That(match.Grid.IsConnected(0, P(2, 1)), Is.True);
            rules.TryPaint(1, P(1, 0));
            Assert.That(match.Grid.IsConnected(0, P(2, 1)), Is.True);
            rules.TryPaint(1, P(1, 1));
            Assert.That(match.Grid.IsConnected(0, P(2, 1)), Is.False);
            rules.TryPaint(0, P(1, 0));
            Assert.That(match.Grid.IsConnected(0, P(2, 1)), Is.True);
        }

        [Test]
        public void FixedMine_BlocksPaintingAndConnectivityWithoutDeletingExistingOwner()
        {
            var match = CreateMatch();
            rules.StartMatch();
            rules.TryPaint(0, P(1, 0));
            rules.TryPaint(0, P(2, 0));
            Assert.That(match.Grid.IsConnected(0, P(2, 0)), Is.True);
            rules.ConfigureMines(new[] { P(1, 0) });
            Assert.That(match.Grid.OwnerAt(P(1, 0)), Is.EqualTo(0));
            Assert.That(match.Grid.IsBlocked(P(1, 0)), Is.True);
            Assert.That(match.Grid.IsConnected(0, P(2, 0)), Is.False);
            Assert.That(rules.TryPaint(1, P(1, 0)), Is.False);
        }

        [Test]
        public void Grid_CopyConnectedTo_TracksCutAndReconnect()
        {
            var grid = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>().CreateMatch(4, 2, new[] { P(0, 0), P(3, 1) }, 3, 100f).Grid;
            rules.StartMatch();
            var result = new bool[4, 2];
            rules.TryPaint(0, P(1, 0));
            rules.TryPaint(0, P(2, 0));
            grid.CopyConnectedTo(0, result);
            Assert.That(result[2, 0], Is.True);
            rules.TryPaint(1, P(1, 0));
            grid.CopyConnectedTo(0, result);
            Assert.That(result[2, 0], Is.False);
            Assert.That(grid.OwnerAt(P(2, 0)), Is.EqualTo(0));
            rules.TryPaint(0, P(1, 0));
            grid.CopyConnectedTo(0, result);
            Assert.That(result[2, 0], Is.True);
            Assert.Throws<ArgumentException>(() => grid.CopyConnectedTo(0, new bool[1, 1]));
        }

        [Test]
        public void Grid_RejectsInvalidSpawnsAndOutOfBoundsAccess()
        {
            Assert.Throws<ArgumentException>(() => new GridModel(2, 2, new[] { P(0, 0), P(0, 0) }));
            Assert.Throws<ArgumentException>(() => new GridModel(2, 2, new[] { P(2, 0) }));
            var grid = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>().CreateMatch(2, 2, new[] { P(0, 0) }, 3, 100f).Grid;
            rules.StartMatch();
            Assert.That(rules.TryPaint(0, P(-1, 0)), Is.False);
            Assert.That(rules.TryPaint(4, P(1, 0)), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.OwnerAt(P(-1, 0)));
        }

        [Test]
        public void Collect_RequiresConnectedCellAndKeepsPlayersIndependent()
        {
            var match = CreateMatch();
            Assert.That(rules.TryCollectCoin(0, P(0, 0)), Is.False);
            rules.StartMatch();
            rules.TryPaint(0, P(2, 0));
            Assert.That(rules.TryCollectCoin(0, P(2, 0)), Is.False);
            rules.TryPaint(0, P(1, 0));
            Assert.That(rules.TryCollectCoin(0, P(2, 0)), Is.True);
            Assert.That(match.Participants[0].Coins, Is.EqualTo(1));
            Assert.That(match.Participants[1].Coins, Is.Zero);
            Assert.That(rules.TryCollectCoin(1, P(2, 0)), Is.False);
        }

        [Test]
        public void Victory_IsIndividualUntilEveryoneFinishes()
        {
            var match = CreateMatch(target: 1);
            rules.StartMatch();
            Assert.That(rules.TryCollectCoin(0, P(0, 0)), Is.True);
            Assert.That(match.Participants[0].Status, Is.EqualTo(ParticipantStatus.Won));
            Assert.That(match.Status, Is.EqualTo(MatchStatus.Playing));
            Assert.That(rules.TryPaint(0, P(1, 0)), Is.False);
            Assert.That(rules.TryCollectCoin(1, P(4, 0)), Is.True);
            Assert.That(rules.TryCollectCoin(2, P(0, 2)), Is.True);
            Assert.That(rules.TryCollectCoin(3, P(4, 2)), Is.True);
            Assert.That(match.Status, Is.EqualTo(MatchStatus.Finished));
        }

        [Test]
        public void Damage_DropsAtMostHeldCoinsAndRespawnsAtReducedMaximum()
        {
            var match = CreateMatch();
            rules.StartMatch();
            rules.TryPaint(0, P(1, 0));
            rules.TryCollectCoin(0, P(1, 0));
            rules.TryCollectCoin(0, P(1, 0));
            rules.TryObstacleContact(0);
            Assert.That(match.Participants[0].MaxHealth, Is.EqualTo(2));
            Assert.That(match.Participants[0].Health, Is.EqualTo(2));
            Assert.That(rules.TryDamage(0, 2, 1, out int dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(1));
            Assert.That(match.Participants[0].Coins, Is.EqualTo(1));
            Assert.That(match.Participants[0].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(rules.TryCollectCoin(0, P(1, 0)), Is.False);
            Assert.That(rules.TryRespawn(0), Is.True);
            Assert.That(match.Participants[0].Health, Is.EqualTo(2));
            Assert.That(match.Grid.OwnerAt(P(1, 0)), Is.EqualTo(0));
            Assert.That(match.Participants[1].Health, Is.EqualTo(3));
        }

        [Test]
        public void Obstacles_EliminateAtZeroAndLeavePaintOverwritable()
        {
            var match = CreateMatch(health: 2);
            rules.StartMatch();
            rules.TryPaint(0, P(1, 0));
            rules.TryObstacleContact(0);
            rules.TryObstacleContact(0);
            Assert.That(match.Participants[0].Status, Is.EqualTo(ParticipantStatus.Lost));
            Assert.That(rules.TryRespawn(0), Is.False);
            Assert.That(match.Grid.OwnerAt(P(1, 0)), Is.EqualTo(0));
            Assert.That(rules.TryPaint(1, P(1, 0)), Is.True);
            Assert.That(match.Grid.OwnerAt(P(1, 0)), Is.EqualTo(1));
        }

        [Test]
        public void Timeout_LosesActiveAndRespawningPlayersButPreservesWinner()
        {
            var match = CreateMatch(target: 1, duration: 60);
            rules.StartMatch();
            rules.TryCollectCoin(0, P(0, 0));
            rules.TryDamage(1, 3, 0, out _);
            rules.AdvanceTime(60);
            Assert.That(match.Status, Is.EqualTo(MatchStatus.Finished));
            Assert.That(match.RemainingTime, Is.Zero);
            Assert.That(match.Participants[0].Status, Is.EqualTo(ParticipantStatus.Won));
            Assert.That(match.Participants[1].Status, Is.EqualTo(ParticipantStatus.Lost));
            Assert.That(match.Participants[2].Status, Is.EqualTo(ParticipantStatus.Lost));
            Assert.That(rules.TryRespawn(1), Is.False);
            Assert.That(rules.TryCollectCoin(2, P(0, 2)), Is.False);
        }

        [Test]
        public void Match_RejectsInvalidTimeAndNonpositiveDamage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateMatch(duration: float.NaN));
            var match = CreateMatch();
            rules.StartMatch();
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.AdvanceTime(float.NaN));
            Assert.That(rules.TryDamage(0, 0, 1, out int dropped), Is.False);
            Assert.That(dropped, Is.Zero);
            Assert.That(match.Participants[0].Health, Is.EqualTo(3));
        }
    }
}
