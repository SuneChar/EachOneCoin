using System;
using System.Collections.Generic;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public static class ArenaMapValidator
    {
        public static bool TryValidate(ArenaMapDefinition map, out string error)
        {
            error = null;
            if (map == null) error = "地图为空";
            else if (string.IsNullOrWhiteSpace(map.MapId)) error = "地图 ID 为空";
            else if (map.Width <= 0 || map.Height <= 0 || (long)map.Width * map.Height > int.MaxValue) error = "地图尺寸无效";
            else if (map.DefaultTilePrefab == null) error = "缺少默认地块 Prefab";
            else if (map.Cells == null || map.Cells.Length != (long)map.Width * map.Height) error = "地块数量与地图尺寸不符";
            else if (map.Spawns == null || map.Spawns.Length != 4) error = "地图必须包含四个出生点";
            else if (map.Mines == null || map.Mines.Length != 4) error = "地图必须包含四个矿点";
            else if (float.IsNaN(map.HeightUnit) || float.IsInfinity(map.HeightUnit) || map.HeightUnit <= 0f) error = "每级高度必须为有限正数";
            else if (float.IsNaN(map.SlowMultiplier) || float.IsInfinity(map.SlowMultiplier) || map.SlowMultiplier <= 0f || map.SlowMultiplier > 1f) error = "减速倍率无效";
            if (error != null) return false;
            foreach (var cell in map.Cells)
            {
                if ((int)cell.terrain < (int)ArenaTerrain.Ground || (int)cell.terrain > (int)ArenaTerrain.Step || cell.direction < 0 || cell.direction > 3 || cell.height == int.MinValue || (cell.terrain == ArenaTerrain.Ramp && cell.height == int.MaxValue))
                { error = "地块类型或方向无效"; return false; }
                if (float.IsInfinity(cell.height * map.HeightUnit) || (cell.terrain == ArenaTerrain.Ramp && float.IsInfinity((cell.height + 1f) * map.HeightUnit))) { error = "地块高度超出数值范围"; return false; }
                if (cell.terrain == ArenaTerrain.Wall && (map.WallPrefab == null || map.WallPrefab.GetComponent<ArenaWall>() == null))
                { error = "墙地形需要无伤墙 Prefab"; return false; }
            }
            var rampDx = new[] { 0, 1, 0, -1 };
            var rampDy = new[] { 1, 0, -1, 0 };
            for (int x = 0; x < map.Width; x++) for (int y = 0; y < map.Height; y++)
            {
                var cell = map.Cells[x * map.Height + y];
                if (cell.terrain != ArenaTerrain.Ramp) continue;
                var position = new GridPosition(x, y);
                var low = new GridPosition(x - rampDx[cell.direction], y - rampDy[cell.direction]);
                var high = new GridPosition(x + rampDx[cell.direction], y + rampDy[cell.direction]);
                if (!ArenaMapTopology.CanTraverse(map, low, position) || !ArenaMapTopology.CanTraverse(map, position, high))
                { error = $"斜坡 ({x}, {y}) 的高低两端必须连接对应高度的地面"; return false; }
            }
            var occupied = new HashSet<Vector2Int>();
            foreach (var position in map.Spawns)
                if (!ValidPoint(map, position) || !occupied.Add(position)) { error = "出生点无效或重复"; return false; }
            foreach (var position in map.Mines)
                if (!ValidPoint(map, position) || !occupied.Add(position)) { error = "矿点无效或与其他点重叠"; return false; }
            var ids = new HashSet<string>();
            var objectCells = new HashSet<Vector2Int>();
            if (map.Objects == null) { error = "地图对象列表为空"; return false; }
            foreach (var item in map.Objects)
            {
                if (string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) || item.prefab == null || !Finite(item.position) || !Finite(item.rotation) ||
                    !Enum.IsDefined(typeof(ArenaMapObjectGroup), item.group) ||
                    (item.overridePatrolSpeed && (!Finite(item.patrolSpeed) || item.patrolSpeed < 0f)) ||
                    (item.overridePatrolEndOffset && !Finite(item.patrolEndOffset)))
                { error = "地图对象 ID、资源或坐标无效"; return false; }
                if ((item.group == ArenaMapObjectGroup.Moving && item.prefab.GetComponent<LinearPatrolObstacle>() == null) ||
                    (item.group == ArenaMapObjectGroup.Spike && item.prefab.GetComponent<TimedSpikeTrap>() == null) ||
                    (item.group == ArenaMapObjectGroup.Static && item.prefab.GetComponent<ArenaWall>() == null && item.prefab.GetComponent<LethalObstacle>() == null))
                { error = "地图对象类型与 Prefab 不匹配"; return false; }
                if ((item.overridePatrolSpeed || item.overridePatrolEndOffset) && item.group != ArenaMapObjectGroup.Moving)
                { error = "只有移动障碍可以配置巡逻参数"; return false; }
                if (item.overrideInitiallyVisible && item.group != ArenaMapObjectGroup.Spike)
                { error = "只有尖刺陷阱可以配置初始可见状态"; return false; }
                var cell = ArenaMapTopology.WorldToCell(map, item.position);
                if (!ArenaMapTopology.Contains(map, cell)) { error = "地图对象超出地图"; return false; }
                var point = new Vector2Int(cell.X, cell.Y);
                if (item.group == ArenaMapObjectGroup.Moving)
                {
                    var patrol = item.prefab.GetComponent<LinearPatrolObstacle>();
                    Vector3 offset = item.overridePatrolEndOffset ? item.patrolEndOffset : patrol.ConfiguredEndOffset;
                    Vector3 end = item.position + offset;
                    if (!Finite(offset) || !Finite(end) || !ArenaMapTopology.Contains(map, ArenaMapTopology.WorldToCell(map, end)))
                    { error = $"移动障碍 ({cell.X}, {cell.Y}) 的巡逻终点超出地图"; return false; }
                }
                var terrain = map.Cells[cell.X * map.Height + cell.Y].terrain;
                if (!objectCells.Add(point) || occupied.Contains(point) || terrain == ArenaTerrain.Hole || terrain == ArenaTerrain.Wall)
                { error = $"障碍物 ({cell.X}, {cell.Y}) 与其他对象、点位或地形冲突"; return false; }
            }
            return true;
        }

        private static bool ValidPoint(ArenaMapDefinition map, Vector2Int p)
        {
            if (p.x < 0 || p.y < 0 || p.x >= map.Width || p.y >= map.Height) return false;
            var terrain = map.Cells[p.x * map.Height + p.y].terrain;
            return terrain == ArenaTerrain.Ground || terrain == ArenaTerrain.Slow;
        }

        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
