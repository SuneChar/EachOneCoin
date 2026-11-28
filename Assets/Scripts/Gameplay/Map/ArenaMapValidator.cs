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
            else if (float.IsNaN(map.SlowMultiplier) || float.IsInfinity(map.SlowMultiplier) || map.SlowMultiplier <= 0f || map.SlowMultiplier > 1f) error = "减速倍率无效";
            if (error != null) return false;
            foreach (var cell in map.Cells)
            {
                if (!Enum.IsDefined(typeof(ArenaTerrain), cell.terrain) || cell.direction < 0 || cell.direction > 3)
                { error = "地块类型或方向无效"; return false; }
                if (cell.terrain == ArenaTerrain.Wall && (map.WallPrefab == null || map.WallPrefab.GetComponent<ArenaWall>() == null))
                { error = "墙地形需要无伤墙 Prefab"; return false; }
            }
            var occupied = new HashSet<Vector2Int>();
            foreach (var position in map.Spawns)
                if (!ValidPoint(map, position) || !occupied.Add(position)) { error = "出生点无效或重复"; return false; }
            foreach (var position in map.Mines)
                if (!ValidPoint(map, position) || !occupied.Add(position)) { error = "矿点无效或与其他点重叠"; return false; }
            var ids = new HashSet<string>();
            if (map.Objects == null) { error = "地图对象列表为空"; return false; }
            foreach (var item in map.Objects)
                if (string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) || item.prefab == null || !Finite(item.position) || !Finite(item.rotation) ||
                    !Enum.IsDefined(typeof(ArenaMapObjectGroup), item.group) ||
                    (item.overridePatrolSpeed && (!Finite(item.patrolSpeed) || item.patrolSpeed < 0f)))
                { error = "地图对象 ID、资源或坐标无效"; return false; }
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
