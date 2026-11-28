using UnityEngine;

namespace EachOneCoin.Gameplay
{
    // Heights are integer levels; ramps climb one level in their configured direction.
    public static class ArenaMapTopology
    {
        public const float HeightUnit = 0.25f;
        private static readonly int[] Dx = { 0, 1, 0, -1 };
        private static readonly int[] Dy = { 1, 0, -1, 0 };

        public static bool Contains(ArenaMapDefinition map, GridPosition cell) => map != null && cell.X >= 0 && cell.Y >= 0 && cell.X < map.Width && cell.Y < map.Height;
        public static GridPosition WorldToCell(ArenaMapDefinition map, Vector3 world) => new GridPosition(Mathf.FloorToInt(world.x + map.Width * 0.5f), Mathf.FloorToInt(world.z + map.Height * 0.5f));

        public static float SurfaceHeight(ArenaMapDefinition map, Vector3 world)
        {
            var position = WorldToCell(map, world);
            if (!Contains(map, position)) return 0f;
            var cell = map.Cells[position.X * map.Height + position.Y];
            float height = cell.height * HeightUnit;
            if (cell.terrain != ArenaTerrain.Ramp) return height;
            float x = Mathf.Clamp01(world.x + map.Width * 0.5f - position.X);
            float y = Mathf.Clamp01(world.z + map.Height * 0.5f - position.Y);
            float t = cell.direction == 0 ? y : cell.direction == 1 ? x : cell.direction == 2 ? 1f - y : 1f - x;
            return height + t * HeightUnit;
        }

        public static bool CanTraverse(ArenaMapDefinition map, GridPosition from, GridPosition to, bool allowHole = false)
        {
            if (!Contains(map, from) || !Contains(map, to)) return false;
            if (from.Equals(to)) return true;
            int dx = to.X - from.X, dy = to.Y - from.Y;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) return false;
            var a = map.Cells[from.X * map.Height + from.Y];
            var b = map.Cells[to.X * map.Height + to.Y];
            if (a.terrain == ArenaTerrain.Hole || a.terrain == ArenaTerrain.Wall || b.terrain == ArenaTerrain.Wall) return false;
            if (b.terrain == ArenaTerrain.Hole) return allowHole;
            int edgeA = EdgeHeight(a, dx, dy);
            int edgeB = EdgeHeight(b, -dx, -dy);
            if (edgeA == int.MinValue || edgeB == int.MinValue) return false;
            if (edgeA == edgeB) return true;
            return a.terrain != ArenaTerrain.Ramp && b.terrain != ArenaTerrain.Ramp &&
                   (a.terrain == ArenaTerrain.Step || b.terrain == ArenaTerrain.Step) && System.Math.Abs((long)edgeA - edgeB) == 1;
        }

        private static int EdgeHeight(ArenaMapCell cell, int dx, int dy)
        {
            if (cell.terrain != ArenaTerrain.Ramp) return cell.height;
            if (cell.direction < 0 || cell.direction >= Dx.Length || cell.height == int.MaxValue) return int.MinValue;
            int along = dx * Dx[cell.direction] + dy * Dy[cell.direction];
            if (along == 0) return int.MinValue;
            return along > 0 ? cell.height + 1 : cell.height;
        }
    }
}
