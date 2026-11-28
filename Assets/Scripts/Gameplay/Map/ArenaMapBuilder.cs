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
                    var tile = UnityEngine.Object.Instantiate(cell.tilePrefabOverride != null ? cell.tilePrefabOverride : map.DefaultTilePrefab, staging.transform);
                    tile.name = "tile";
                    tile.transform.position = new Vector3(x - map.Width / 2f + 0.5f, -0.2f, y - map.Height / 2f + 0.5f);
                    if (cell.meshOverride != null) tile.GetComponentInChildren<MeshFilter>(true).sharedMesh = cell.meshOverride;
                    created.Add(tile);
                }
                while (tiles.childCount > 0) UnityEngine.Object.DestroyImmediate(tiles.GetChild(0).gameObject);
                foreach (var tile in created) tile.transform.SetParent(tiles, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(staging); }
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
                if (entry.overridePatrolSpeed && entry.prefab.GetComponent<LinearPatrolObstacle>() == null)
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
                    instance.transform.SetPositionAndRotation(entry.position, Quaternion.Euler(entry.rotation));
                    if (entry.overridePatrolSpeed) instance.GetComponent<LinearPatrolObstacle>().ConfigureSpeedBeforeActivation(entry.patrolSpeed);
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
