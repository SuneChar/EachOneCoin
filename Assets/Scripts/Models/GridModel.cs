using System;
using System.Collections.Generic;

namespace EachOneCoin
{
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }
        public GridPosition(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
    }

    public sealed class GridModel
    {
        private readonly int?[,] owners;
        private readonly Dictionary<int, GridPosition> spawns = new Dictionary<int, GridPosition>();
        private readonly bool[,] connected;
        private readonly Queue<GridPosition> queue = new Queue<GridPosition>();
        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public int Width { get; }
        public int Height { get; }

        public GridModel(int width, int height, IReadOnlyList<GridPosition> spawnPositions)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (spawnPositions == null) throw new ArgumentNullException(nameof(spawnPositions));
            if (spawnPositions.Count < 1 || spawnPositions.Count > 4) throw new ArgumentOutOfRangeException(nameof(spawnPositions));
            Width = width;
            Height = height;
            owners = new int?[width, height];
            connected = new bool[width, height];
            for (int i = 0; i < spawnPositions.Count; i++)
            {
                var position = spawnPositions[i];
                if (!Contains(position) || owners[position.X, position.Y].HasValue)
                    throw new ArgumentException("Spawn positions must be unique and inside the grid.", nameof(spawnPositions));
                spawns.Add(i, position);
                owners[position.X, position.Y] = i;
            }
        }

        public bool Contains(GridPosition position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

        public int? OwnerAt(GridPosition position)
        {
            if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return owners[position.X, position.Y];
        }

        internal void SetOwner(GridPosition position, int participantId) => owners[position.X, position.Y] = participantId;

        public bool IsConnected(int participantId, GridPosition target)
        {
            if (!Contains(target) || owners[target.X, target.Y] != participantId) return false;
            BuildConnected(participantId);
            return connected[target.X, target.Y];
        }

        // Copies the entire connected region so renderers need only one search per participant.
        public void CopyConnectedTo(int participantId, bool[,] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.GetLength(0) != Width || destination.GetLength(1) != Height)
                throw new ArgumentException("Destination must match grid dimensions.", nameof(destination));
            BuildConnected(participantId);
            Array.Copy(connected, destination, connected.Length);
        }

        private void BuildConnected(int participantId)
        {
            Array.Clear(connected, 0, connected.Length);
            queue.Clear();
            if (!spawns.TryGetValue(participantId, out var spawn)) return;
            connected[spawn.X, spawn.Y] = true;
            queue.Enqueue(spawn);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < Dx.Length; i++)
                {
                    var next = new GridPosition(current.X + Dx[i], current.Y + Dy[i]);
                    if (!Contains(next) || connected[next.X, next.Y] || owners[next.X, next.Y] != participantId) continue;
                    connected[next.X, next.Y] = true;
                    queue.Enqueue(next);
                }
            }
        }
    }
}
