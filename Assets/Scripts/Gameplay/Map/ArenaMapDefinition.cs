using System;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public enum ArenaTerrain { Ground, Hole, Slow, Wall, Ramp, Step }

    [Serializable]
    public struct ArenaMapCell
    {
        public ArenaTerrain terrain;
        public int height;
        [Range(0, 3)] public int direction;
        public GameObject tilePrefabOverride;
        public Mesh meshOverride;
    }

    public enum ArenaMapObjectGroup { Static, Moving, Spike }

    [Serializable]
    public struct ArenaMapObject
    {
        public string id;
        public ArenaMapObjectGroup group;
        public GameObject prefab;
        public Vector3 position;
        public Vector3 rotation;
        public bool overridePatrolSpeed;
        public float patrolSpeed;
        public bool overridePatrolEndOffset;
        public Vector3 patrolEndOffset;
        public bool overrideInitiallyVisible;
        public bool initiallyVisible;
    }

    [CreateAssetMenu(menuName = "EachOneCoin/Map/Arena Map", fileName = "ArenaMap")]
    public sealed class ArenaMapDefinition : ScriptableObject
    {
        [SerializeField] private string mapId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int width = 21;
        [SerializeField, Min(1)] private int height = 21;
        [SerializeField] private GameObject defaultTilePrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private ArenaMapCell[] cells = new ArenaMapCell[21 * 21];
        [SerializeField] private Vector2Int[] spawns = { new Vector2Int(1, 1), new Vector2Int(19, 1), new Vector2Int(1, 19), new Vector2Int(19, 19) };
        [SerializeField] private Vector2Int[] mines = { new Vector2Int(10, 5), new Vector2Int(10, 15), new Vector2Int(5, 10), new Vector2Int(15, 10) };
        [SerializeField] private ArenaMapObject[] objects = Array.Empty<ArenaMapObject>();
        [SerializeField, Min(0.01f)] private float heightUnit = ArenaMapTopology.DefaultHeightUnit;
        [SerializeField, Range(0.01f, 1f)] private float slowMultiplier = 0.5f;

        public string MapId => mapId;
        public string DisplayName => displayName;
        public int Width => width;
        public int Height => height;
        public GameObject DefaultTilePrefab => defaultTilePrefab;
        public GameObject WallPrefab => wallPrefab;
        public ArenaMapCell[] Cells => cells;
        public Vector2Int[] Spawns => spawns;
        public Vector2Int[] Mines => mines;
        public ArenaMapObject[] Objects => objects;
        public float HeightUnit => heightUnit;
        public float SlowMultiplier => slowMultiplier;
    }
}
