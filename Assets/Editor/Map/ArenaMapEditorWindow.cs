using EachOneCoin.Gameplay;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Editor
{
    public sealed class ArenaMapEditorWindow : EditorWindow
    {
        private ArenaMapDefinition map;
        private ArenaTerrain brush;
        private int brushHeight;
        private int brushDirection;
        private int requestedWidth = 21;
        private int requestedHeight = 21;
        private Vector2 scroll;
        private const float CellSize = 24f;

        [MenuItem("Tools/EachOneCoin/地图编辑器")]
        private static void Open() => GetWindow<ArenaMapEditorWindow>("地图编辑器");

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            map = (ArenaMapDefinition)EditorGUILayout.ObjectField("地图", map, typeof(ArenaMapDefinition), false);
            if (EditorGUI.EndChangeCheck() && map != null) { requestedWidth = map.Width; requestedHeight = map.Height; }
            if (GUILayout.Button("新建", GUILayout.Width(60))) CreateMap();
            EditorGUILayout.EndHorizontal();
            if (map == null) { EditorGUILayout.HelpBox("请选择或新建地图资源。", MessageType.Info); return; }

            var data = new SerializedObject(map);
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("mapId"), new GUIContent("地图 ID"));
            EditorGUILayout.PropertyField(data.FindProperty("displayName"), new GUIContent("地图名称"));
            EditorGUILayout.PropertyField(data.FindProperty("slowMultiplier"), new GUIContent("减速倍率"));
            EditorGUILayout.PropertyField(data.FindProperty("defaultTilePrefab"), new GUIContent("默认地块"));
            EditorGUILayout.PropertyField(data.FindProperty("wallPrefab"), new GUIContent("无伤墙"));
            EditorGUILayout.PropertyField(data.FindProperty("spawns"), new GUIContent("出生点"), true);
            EditorGUILayout.PropertyField(data.FindProperty("mines"), new GUIContent("矿点"), true);
            EditorGUILayout.PropertyField(data.FindProperty("objects"), new GUIContent("障碍物"), true);
            data.ApplyModifiedProperties();
            EditorGUILayout.LabelField($"地图尺寸：{map.Width} × {map.Height}");
            requestedWidth = EditorGUILayout.IntField("新宽度", requestedWidth);
            requestedHeight = EditorGUILayout.IntField("新高度", requestedHeight);
            if (GUILayout.Button("调整尺寸")) ResizeMap();
            brush = (ArenaTerrain)EditorGUILayout.EnumPopup("地形画笔", brush);
            brushHeight = EditorGUILayout.IntField("高度等级", brushHeight);
            brushDirection = EditorGUILayout.IntSlider("斜坡方向（0北 1东 2南 3西）", brushDirection, 0, 3);
            EditorGUILayout.HelpBox("每级高度为 0.25 单位。斜坡沿方向升高一级；台阶连接相邻一级高度。", MessageType.None);
            if (!ArenaMapValidator.TryValidate(map, out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            else EditorGUILayout.HelpBox("基础数据校验通过。地形运行时尚未接入。", MessageType.Info);
            DrawGrid();
        }

        private void DrawGrid()
        {
            if (map.Cells == null || map.Width <= 0 || map.Height <= 0 || (long)map.Width * map.Height != map.Cells.Length) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float width = map.Width * CellSize;
            float height = map.Height * CellSize;
            Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            var evt = Event.current;
            if (evt.type == EventType.Repaint)
            {
                int left = Mathf.Clamp(Mathf.FloorToInt(scroll.x / CellSize), 0, map.Width);
                int right = Mathf.Clamp(Mathf.CeilToInt((scroll.x + position.width) / CellSize), 0, map.Width);
                int bottom = Mathf.Clamp(Mathf.FloorToInt(scroll.y / CellSize), 0, map.Height);
                int top = Mathf.Clamp(Mathf.CeilToInt((scroll.y + position.height) / CellSize), 0, map.Height);
                for (int row = bottom; row < top; row++) for (int x = left; x < right; x++)
                {
                    int y = map.Height - 1 - row;
                    var cell = map.Cells[x * map.Height + y];
                    var rect = new Rect(area.x + x * CellSize, area.y + row * CellSize, CellSize - 1, CellSize - 1);
                    EditorGUI.DrawRect(rect, ColorFor(cell.terrain));
                    GUI.Label(rect, cell.height.ToString(), EditorStyles.centeredGreyMiniLabel);
                }
            }
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && area.Contains(evt.mousePosition))
            {
                int x = Mathf.FloorToInt((evt.mousePosition.x - area.x) / CellSize);
                int y = map.Height - 1 - Mathf.FloorToInt((evt.mousePosition.y - area.y) / CellSize);
                if (x >= 0 && x < map.Width && y >= 0 && y < map.Height)
                {
                    Undo.RecordObject(map, "编辑地图地形");
                    int index = x * map.Height + y;
                    var painted = map.Cells[index];
                    painted.terrain = brush;
                    painted.height = brushHeight;
                    painted.direction = brushDirection;
                    map.Cells[index] = painted;
                    EditorUtility.SetDirty(map);
                    evt.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static Color ColorFor(ArenaTerrain terrain)
        {
            switch (terrain)
            {
                case ArenaTerrain.Hole: return Color.black;
                case ArenaTerrain.Wall: return new Color(0.35f, 0.35f, 0.4f);
                case ArenaTerrain.Slow: return new Color(0.2f, 0.45f, 0.8f);
                case ArenaTerrain.Ramp: return new Color(0.8f, 0.6f, 0.2f);
                case ArenaTerrain.Step: return new Color(0.65f, 0.4f, 0.2f);
                default: return new Color(0.35f, 0.65f, 0.35f);
            }
        }

        private void ResizeMap()
        {
            long count = (long)requestedWidth * requestedHeight;
            if (requestedWidth <= 0 || requestedHeight <= 0 || count > int.MaxValue)
            {
                EditorUtility.DisplayDialog("尺寸无效", "宽高必须为正，且格子总数不能超出整数范围。", "确定");
                return;
            }
            if (count > 1000000 && !EditorUtility.DisplayDialog("大型地图", $"地图包含 {count} 个格子，可能消耗大量内存。继续吗？", "继续", "取消")) return;
            ArenaMapCell[] cells;
            try { cells = new ArenaMapCell[(int)count]; }
            catch (System.OutOfMemoryException) { EditorUtility.DisplayDialog("内存不足", "无法为该地图分配格子。", "确定"); return; }
            if (map.Cells == null || map.Width <= 0 || map.Height <= 0 || (long)map.Width * map.Height != map.Cells.Length)
            {
                EditorUtility.DisplayDialog("地图数据无效", "原地图的格子数量与尺寸不匹配，无法安全调整。", "确定");
                return;
            }
            int copyWidth = Mathf.Min(map.Width, requestedWidth);
            int copyHeight = Mathf.Min(map.Height, requestedHeight);
            for (int x = 0; x < copyWidth; x++)
                System.Array.Copy(map.Cells, x * map.Height, cells, x * requestedHeight, copyHeight);
            Undo.RecordObject(map, "调整地图尺寸");
            var data = new SerializedObject(map);
            data.FindProperty("width").intValue = requestedWidth;
            data.FindProperty("height").intValue = requestedHeight;
            var target = data.FindProperty("cells");
            target.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = target.GetArrayElementAtIndex(i);
                cell.FindPropertyRelative("terrain").enumValueIndex = (int)cells[i].terrain;
                cell.FindPropertyRelative("height").intValue = cells[i].height;
                cell.FindPropertyRelative("direction").intValue = cells[i].direction;
                cell.FindPropertyRelative("tilePrefabOverride").objectReferenceValue = cells[i].tilePrefabOverride;
                cell.FindPropertyRelative("meshOverride").objectReferenceValue = cells[i].meshOverride;
            }
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        private void CreateMap()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建地图", "ArenaMap", "asset", "选择地图保存位置");
            if (string.IsNullOrEmpty(path)) return;
            var created = CreateInstance<ArenaMapDefinition>();
            var data = new SerializedObject(created);
            data.FindProperty("mapId").stringValue = System.Guid.NewGuid().ToString("N");
            data.FindProperty("displayName").stringValue = System.IO.Path.GetFileNameWithoutExtension(path);
            data.FindProperty("defaultTilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/ArenaTile.prefab");
            data.FindProperty("wallPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Obstacles/StaticObstacle.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(created, path);
            var catalog = AssetDatabase.LoadAssetAtPath<ArenaMapCatalog>("Assets/Data/Maps/ArenaMapCatalog.asset");
            if (catalog != null)
            {
                var catalogData = new SerializedObject(catalog);
                var maps = catalogData.FindProperty("maps");
                maps.InsertArrayElementAtIndex(maps.arraySize);
                maps.GetArrayElementAtIndex(maps.arraySize - 1).objectReferenceValue = created;
                catalogData.ApplyModifiedProperties();
                EditorUtility.SetDirty(catalog);
            }
            else Debug.LogWarning("未找到地图目录，创建的地图不会出现在选图列表中。", created);
            AssetDatabase.SaveAssets();
            map = created;
            Selection.activeObject = created;
        }
    }
}
