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

        public bool TryPaint(int participantId, GridPosition position)
        {
            if (!spawns.ContainsKey(participantId) || !Contains(position)) return false;
            foreach (var spawn in spawns)
                if (spawn.Value.Equals(position) && spawn.Key != participantId) return false;
            if (owners[position.X, position.Y] == participantId) return false;
            owners[position.X, position.Y] = participantId;
            return true;
        }

        public bool IsConnected(int participantId, GridPosition target)
        {
            if (!spawns.TryGetValue(participantId, out var spawn) || !Contains(target) || owners[target.X, target.Y] != participantId)
                return false;
            Array.Clear(connected, 0, connected.Length);
            queue.Clear();
            connected[spawn.X, spawn.Y] = true;
            queue.Enqueue(spawn);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Equals(target)) return true;
                for (int i = 0; i < Dx.Length; i++)
                {
                    var next = new GridPosition(current.X + Dx[i], current.Y + Dy[i]);
                    if (!Contains(next) || connected[next.X, next.Y] || owners[next.X, next.Y] != participantId) continue;
                    connected[next.X, next.Y] = true;
                    queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
