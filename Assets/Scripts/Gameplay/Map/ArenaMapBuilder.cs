using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EachOneCoin.Gameplay
{
    // Rebuilds the existing scene's obstacle groups from a validated map.
    public static class ArenaMapBuilder
    {
        public static void BuildTiles(ArenaMapDefinition map, Scene scene)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Map construction requires Play Mode.");
            if (!ArenaMapValidator.TryValidate(map, out var error)) throw new ArgumentException(error, nameof(map));
            Transform world = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "---World---") { world = root.transform; break; }
            var tiles = world != null ? world.Find("Arena Models") : null;
            if (tiles == null || map.DefaultTilePrefab == null) throw new InvalidOperationException("Tile root or default tile prefab is missing.");
            foreach (var cell in map.Cells)
            {
                var prefab = cell.tilePrefabOverride != null ? cell.tilePrefabOverride : map.DefaultTilePrefab;
                if (prefab.GetComponentInChildren<Renderer>(true) == null || (cell.meshOverride != null && prefab.GetComponentInChildren<MeshFilter>(true) == null))
                    throw new InvalidOperationException("A tile prefab is missing its renderer or mesh filter.");
            }
            var staging = new GameObject("Map tile staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            var created = new List<GameObject>(map.Cells.Length);
            try
            {
                for (int x = 0; x < map.Width; x++) for (int y = 0; y < map.Height; y++)
                {
                    var cell = map.Cells[x * map.Height + y];
                    if (cell.terrain == ArenaTerrain.Hole) continue;
                    var tile = UnityEngine.Object.Instantiate(cell.tilePrefabOverride != null ? cell.tilePrefabOverride : map.DefaultTilePrefab, staging.transform);
                    tile.name = "tile";
                    float surface = cell.height * map.HeightUnit;
                    if (cell.terrain == ArenaTerrain.Ramp) surface += map.HeightUnit * 0.5f;
                    tile.transform.position = new Vector3(x - map.Width / 2f + 0.5f, surface - 0.2f, y - map.Height / 2f + 0.5f);
                    if (cell.terrain == ArenaTerrain.Ramp)
                    {
                        float angle = Mathf.Atan(map.HeightUnit) * Mathf.Rad2Deg;
                        tile.transform.rotation = cell.direction == 0 ? Quaternion.Euler(-angle, 0f, 0f) :
                            cell.direction == 1 ? Quaternion.Euler(0f, 0f, angle) :
                            cell.direction == 2 ? Quaternion.Euler(angle, 0f, 0f) : Quaternion.Euler(0f, 0f, -angle);
                    }
                    if (cell.meshOverride != null) tile.GetComponentInChildren<MeshFilter>(true).sharedMesh = cell.meshOverride;
                    if (cell.terrain == ArenaTerrain.Slow)
                    {
                        var renderer = tile.GetComponentInChildren<Renderer>(true);
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetColor("_BaseColor", new Color(0.45f, 0.65f, 1f));
                        block.SetColor("_Color", new Color(0.45f, 0.65f, 1f));
                        renderer.SetPropertyBlock(block);
                    }
                    if (cell.terrain == ArenaTerrain.Wall)
                    {
                        var wall = UnityEngine.Object.Instantiate(map.WallPrefab, tile.transform);
                        wall.name = "Map Wall";
                        wall.transform.position = new Vector3(tile.transform.position.x, surface + 0.5f, tile.transform.position.z);
                        wall.transform.rotation = Quaternion.identity;
                    }
                    created.Add(tile);
                }
                while (tiles.childCount > 0) UnityEngine.Object.DestroyImmediate(tiles.GetChild(0).gameObject);
                foreach (var tile in created) tile.transform.SetParent(tiles, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(staging); }
        }

        public static void ResizeBoundary(ArenaMapDefinition map, Scene scene)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Map construction requires Play Mode.");
            int minLevel = 0, maxLevel = 0;
            foreach (var cell in map.Cells)
            {
                minLevel = Mathf.Min(minLevel, cell.height);
                maxLevel = Mathf.Max(maxLevel, cell.height + (cell.terrain == ArenaTerrain.Ramp ? 1 : 0));
            }
            if (map.Width == ArenaGridView.Size && map.Height == ArenaGridView.Size && minLevel == 0 && maxLevel == 0) return;
            Transform world = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "---World---") { world = root.transform; break; }
            var boundary = world != null ? world.Find("Arena Boundary") : null;
            if (boundary == null) throw new InvalidOperationException("Arena boundary was not found.");
            var colliders = new BoxCollider[4];
            for (int i = 0; i < colliders.Length; i++)
            {
                var child = boundary.Find("Boundary Collider " + i);
                colliders[i] = child != null ? child.GetComponent<BoxCollider>() : null;
                if (colliders[i] == null) throw new InvalidOperationException("Arena boundary collider was not found.");
            }
            Transform template = null;
            foreach (Transform child in boundary) if (child.GetComponent<MeshRenderer>() != null) { template = child; break; }
            if (template == null) throw new InvalidOperationException("Arena boundary visual was not found.");
            var staging = new GameObject("Boundary staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            var created = new List<GameObject>();
            try
            {
                for (int i = 0; i < map.Width; i++)
                {
                    float x = i - map.Width / 2f + 0.5f;
                    float south = ArenaMapTopology.SurfaceHeight(map, new Vector3(x, 0f, -map.Height / 2f + 0.001f));
                    float north = ArenaMapTopology.SurfaceHeight(map, new Vector3(x, 0f, map.Height / 2f - 0.001f));
                    AddFence(template, staging.transform, created, new Vector3(x, south, -map.Height / 2f), 0f);
                    AddFence(template, staging.transform, created, new Vector3(x, north, map.Height / 2f), 180f);
                }
                for (int i = 0; i < map.Height; i++)
                {
                    float z = i - map.Height / 2f + 0.5f;
                    float west = ArenaMapTopology.SurfaceHeight(map, new Vector3(-map.Width / 2f + 0.001f, 0f, z));
                    float east = ArenaMapTopology.SurfaceHeight(map, new Vector3(map.Width / 2f - 0.001f, 0f, z));
                    AddFence(template, staging.transform, created, new Vector3(-map.Width / 2f, west, z), 90f);
                    AddFence(template, staging.transform, created, new Vector3(map.Width / 2f, east, z), 270f);
                }
                for (int i = boundary.childCount - 1; i >= 0; i--)
                    if (boundary.GetChild(i).GetComponent<MeshRenderer>() != null) UnityEngine.Object.DestroyImmediate(boundary.GetChild(i).gameObject);
                float bottom = minLevel * map.HeightUnit - 0.5f;
                float top = maxLevel * map.HeightUnit + 1.5f;
                float centerY = (bottom + top) * 0.5f;
                float colliderHeight = top - bottom;
                colliders[0].transform.position = new Vector3(0f, centerY, -map.Height / 2f);
                colliders[1].transform.position = new Vector3(0f, centerY, map.Height / 2f);
                colliders[2].transform.position = new Vector3(-map.Width / 2f, centerY, 0f);
                colliders[3].transform.position = new Vector3(map.Width / 2f, centerY, 0f);
                colliders[0].size = colliders[1].size = new Vector3(map.Width + 0.5f, colliderHeight, 0.3f);
                colliders[2].size = colliders[3].size = new Vector3(0.3f, colliderHeight, map.Height + 0.5f);
                foreach (var fence in created) fence.transform.SetParent(boundary, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(staging); }
        }

        private static void AddFence(Transform template, Transform parent, List<GameObject> output, Vector3 position, float rotation)
        {
            var fence = UnityEngine.Object.Instantiate(template.gameObject, parent);
            fence.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation, 0f));
            output.Add(fence);
        }

        public static void BuildObstacles(ArenaMapDefinition map, Scene scene)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Map construction requires Play Mode.");
            if (!ArenaMapValidator.TryValidate(map, out var error)) throw new ArgumentException(error, nameof(map));
            Transform world = null;
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "---World---") { world = root.transform; break; }
            if (world == null) throw new InvalidOperationException("Arena world root was not found.");
            var groups = new[] { world.Find("Static Obstacles"), world.Find("Moving Obstacles"), world.Find("Spike Traps") };
            foreach (var group in groups) if (group == null) throw new InvalidOperationException("An obstacle group was not found.");
            foreach (var entry in map.Objects)
            {
                if ((entry.overridePatrolSpeed || entry.overridePatrolEndOffset) && entry.prefab.GetComponent<LinearPatrolObstacle>() == null)
                    throw new ArgumentException("A patrol speed override requires a patrol obstacle.");
                if (entry.overrideInitiallyVisible && entry.prefab.GetComponent<TimedSpikeTrap>() == null)
                    throw new ArgumentException("A visibility override requires a spike trap.");
            }
            var staging = new GameObject("Map obstacle staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, scene);
            var created = new List<GameObject>();
            try
            {
                foreach (var entry in map.Objects)
                {
                    var instance = UnityEngine.Object.Instantiate(entry.prefab, staging.transform);
                    instance.name = entry.id;
                    Vector3 position = entry.position;
                    position.y += ArenaMapTopology.SurfaceHeight(map, position);
                    instance.transform.SetPositionAndRotation(position, Quaternion.Euler(entry.rotation));
                    if (entry.overridePatrolSpeed) instance.GetComponent<LinearPatrolObstacle>().ConfigureSpeedBeforeActivation(entry.patrolSpeed);
                    if (entry.overridePatrolEndOffset) instance.GetComponent<LinearPatrolObstacle>().ConfigureEndOffsetBeforeActivation(entry.patrolEndOffset);
                    if (entry.overrideInitiallyVisible) instance.GetComponent<TimedSpikeTrap>().ConfigureInitialVisibilityBeforeStart(entry.initiallyVisible);
                    created.Add(instance);
                }
                // Destroy only runtime scene instances; the scene asset and source prefabs are unchanged.
                foreach (var group in groups)
                    while (group.childCount > 0) UnityEngine.Object.DestroyImmediate(group.GetChild(0).gameObject);
                for (int i = 0; i < created.Count; i++) created[i].transform.SetParent(groups[(int)map.Objects[i].group], true);
            }
            finally { UnityEngine.Object.DestroyImmediate(staging); }
        }
    }
}
