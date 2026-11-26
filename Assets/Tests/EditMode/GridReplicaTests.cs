using NUnit.Framework;

namespace EachOneCoin.Tests
{
    public class GridReplicaTests
    {
        private static GridModel CreateGrid() => new GridModel(3, 3, new[] { new GridPosition(0, 0), new GridPosition(2, 2) });

        [Test]
        public void Snapshot_AppliesOwnersAndAllowsClearingOrdinaryCells()
        {
            var grid = CreateGrid();
            var snapshot = new byte[9];
            snapshot[0] = 1; snapshot[8] = 2; snapshot[3] = 2;
            Assert.That(grid.ApplyReplicaSnapshot(snapshot), Is.True);
            Assert.That(grid.OwnerAt(new GridPosition(1, 0)), Is.EqualTo(1));
            snapshot[3] = 0;
            Assert.That(grid.ApplyReplicaSnapshot(snapshot), Is.True);
            Assert.That(grid.OwnerAt(new GridPosition(1, 0)), Is.Null);
        }

        [Test]
        public void Snapshot_InvalidSpawnIsRejectedAtomically()
        {
            var grid = CreateGrid();
            var snapshot = new byte[9];
            snapshot[0] = 2; snapshot[8] = 2; snapshot[3] = 1;
            Assert.That(grid.ApplyReplicaSnapshot(snapshot), Is.False);
            Assert.That(grid.OwnerAt(new GridPosition(0, 0)), Is.EqualTo(0));
            Assert.That(grid.OwnerAt(new GridPosition(1, 0)), Is.Null);
        }

        [Test]
        public void Delta_RejectsInvalidOwnerCoordinatesAndSpawnOverwrite()
        {
            var grid = CreateGrid();
            Assert.That(grid.ApplyReplicaOwner(new GridPosition(-1, 0), 1), Is.False);
            Assert.That(grid.ApplyReplicaOwner(new GridPosition(1, 0), 3), Is.False);
            Assert.That(grid.ApplyReplicaOwner(new GridPosition(0, 0), 2), Is.False);
            Assert.That(grid.ApplyReplicaOwner(new GridPosition(1, 0), 2), Is.True);
            Assert.That(grid.OwnerAt(new GridPosition(1, 0)), Is.EqualTo(1));
        }
    }
}
