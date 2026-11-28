using EachOneCoin.Gameplay;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Editor
{
    public sealed class ArenaMapEditorWindow : EditorWindow
    {
        private enum PaintMode { Terrain, Spawn, Mine, Static, Moving, Spike, EraseObject }
        private static readonly string[] PaintModeLabels = { "地形", "出生点", "矿点", "静态墙", "移动障碍", "尖刺陷阱", "擦除障碍" };
        private static readonly string[] RampLabels = { "↑", "→", "↓", "←" };
        private static readonly string[] TerrainLabels = { "普通地面", "空洞", "减速地面", "墙", "斜坡", "台阶" };
        private ArenaMapDefinition map;
        private PaintMode paintMode;
        private int pointSlot;
        private ArenaTerrain brush;
        private int brushHeight;
        private int brushDirection;
        private int requestedWidth = 21;
        private int requestedHeight = 21;
        private Vector2 scroll;
        private GUIStyle cellLabelStyle;
        private ArenaMapDefinition validatedMap;
        private bool validationDirty = true;
        private string validationError;
        private const float CellSize = 24f;

        [MenuItem("Tools/EachOneCoin/地图编辑器")]
        private static void Open() => GetWindow<ArenaMapEditorWindow>("地图编辑器");

        private void OnEnable()
        {
            Undo.undoRedoPerformed += InvalidateValidation;
            Undo.postprocessModifications += OnPostprocessModifications;
            EditorApplication.projectChanged += InvalidateValidation;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= InvalidateValidation;
            Undo.postprocessModifications -= OnPostprocessModifications;
            EditorApplication.projectChanged -= InvalidateValidation;
        }

        private void InvalidateValidation() { validationDirty = true; Repaint(); }
        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] changes)
        {
            foreach (var change in changes)
                if (change.currentValue != null && change.currentValue.target == map) { validationDirty = true; break; }
            return changes;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            map = (ArenaMapDefinition)EditorGUILayout.ObjectField("地图", map, typeof(ArenaMapDefinition), false);
            if (EditorGUI.EndChangeCheck() && map != null) { requestedWidth = map.Width; requestedHeight = map.Height; validationDirty = true; }
            if (GUILayout.Button("新建", GUILayout.Width(60))) CreateMap();
            EditorGUILayout.EndHorizontal();
            if (map == null) { EditorGUILayout.HelpBox("请选择或新建地图资源。", MessageType.Info); return; }

            var data = new SerializedObject(map);
            data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("mapId"), new GUIContent("地图 ID"));
            EditorGUILayout.PropertyField(data.FindProperty("displayName"), new GUIContent("地图名称"));
            EditorGUILayout.PropertyField(data.FindProperty("heightUnit"), new GUIContent("每级高度"));
            EditorGUILayout.PropertyField(data.FindProperty("slowMultiplier"), new GUIContent("减速倍率"));
            EditorGUILayout.PropertyField(data.FindProperty("defaultTilePrefab"), new GUIContent("默认地块"));
            EditorGUILayout.PropertyField(data.FindProperty("wallPrefab"), new GUIContent("无伤墙"));
            EditorGUILayout.PropertyField(data.FindProperty("spawns"), new GUIContent("出生点"), true);
            EditorGUILayout.PropertyField(data.FindProperty("mines"), new GUIContent("矿点"), true);
            EditorGUILayout.PropertyField(data.FindProperty("objects"), new GUIContent("障碍物"), true);
            if (data.ApplyModifiedProperties()) validationDirty = true;
            EditorGUILayout.LabelField($"地图尺寸：{map.Width} × {map.Height}");
            requestedWidth = EditorGUILayout.IntField("新宽度", requestedWidth);
            requestedHeight = EditorGUILayout.IntField("新高度", requestedHeight);
            if (GUILayout.Button("调整尺寸")) ResizeMap();
            paintMode = (PaintMode)EditorGUILayout.Popup("编辑模式", (int)paintMode, PaintModeLabels);
            if (paintMode == PaintMode.Terrain)
            {
                brush = (ArenaTerrain)EditorGUILayout.Popup("地形画笔", (int)brush, TerrainLabels);
                brushHeight = EditorGUILayout.IntField("高度等级", brushHeight);
                brushDirection = EditorGUILayout.IntSlider("斜坡方向（0北 1东 2南 3西）", brushDirection, 0, 3);
                EditorGUILayout.HelpBox($"当前每级高度为 {map.HeightUnit:0.###} 单位。斜坡沿方向升高一级；台阶连接相邻一级高度。", MessageType.None);
            }
            else if (paintMode == PaintMode.Spawn || paintMode == PaintMode.Mine)
                pointSlot = EditorGUILayout.IntSlider("点位编号", pointSlot + 1, 1, 4) - 1;
            if (validatedMap != map || validationDirty)
            {
                validatedMap = map;
                ArenaMapValidator.TryValidate(map, out validationError);
                validationDirty = false;
            }
            if (validationError != null) EditorGUILayout.HelpBox(validationError, MessageType.Error);
            else EditorGUILayout.HelpBox("地图基础校验通过。", MessageType.Info);
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
                if (cellLabelStyle == null)
                {
                    cellLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                    cellLabelStyle.normal.textColor = Color.white;
                }
                var marks = BuildCellLabels();
                for (int row = bottom; row < top; row++) for (int x = left; x < right; x++)
                {
                    int y = map.Height - 1 - row;
                    var cell = map.Cells[x * map.Height + y];
                    var rect = new Rect(area.x + x * CellSize, area.y + row * CellSize, CellSize - 1, CellSize - 1);
                    EditorGUI.DrawRect(rect, ColorFor(cell.terrain));
                    int key = x * map.Height + y;
                    string content = marks.TryGetValue(key, out string label) ? label : cell.terrain == ArenaTerrain.Ramp ? RampLabels[Mathf.Clamp(cell.direction, 0, 3)] : cell.height != 0 ? cell.height.ToString() : "";
                    if (content.Length > 0) GUI.Label(rect, content, cellLabelStyle);
                }
            }
            if ((evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag) && evt.button == 0 && area.Contains(evt.mousePosition))
            {
                int x = Mathf.FloorToInt((evt.mousePosition.x - area.x) / CellSize);
                int y = map.Height - 1 - Mathf.FloorToInt((evt.mousePosition.y - area.y) / CellSize);
                if (x >= 0 && x < map.Width && y >= 0 && y < map.Height)
                {
                    PaintCell(x, y);
                    evt.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private System.Collections.Generic.Dictionary<int, string> BuildCellLabels()
        {
            var labels = new System.Collections.Generic.Dictionary<int, string>();
            void Put(Vector2Int point, string label)
            {
                if (point.x >= 0 && point.y >= 0 && point.x < map.Width && point.y < map.Height)
                    labels[point.x * map.Height + point.y] = label;
            }
            if (map.Objects != null) foreach (var item in map.Objects)
            {
                var cell = ArenaMapTopology.WorldToCell(map, item.position);
                Put(new Vector2Int(cell.X, cell.Y), item.group == ArenaMapObjectGroup.Static ? "W" : item.group == ArenaMapObjectGroup.Moving ? "D" : "T");
            }
            if (map.Mines != null) for (int i = 0; i < map.Mines.Length; i++) Put(map.Mines[i], "M" + (i + 1));
            if (map.Spawns != null) for (int i = 0; i < map.Spawns.Length; i++) Put(map.Spawns[i], "P" + (i + 1));
            return labels;
        }

        private void PaintCell(int x, int y)
        {
            validationDirty = true;
            if (paintMode == PaintMode.Terrain)
            {
                Undo.RecordObject(map, "编辑地图地形");
                int index = x * map.Height + y;
                var painted = map.Cells[index];
                painted.terrain = brush;
                painted.height = brushHeight;
                painted.direction = brushDirection;
                map.Cells[index] = painted;
                EditorUtility.SetDirty(map);
                return;
            }
            var data = new SerializedObject(map);
            if (paintMode == PaintMode.Spawn || paintMode == PaintMode.Mine)
            {
                var points = data.FindProperty(paintMode == PaintMode.Spawn ? "spawns" : "mines");
                if (pointSlot >= points.arraySize) return;
                points.GetArrayElementAtIndex(pointSlot).vector2IntValue = new Vector2Int(x, y);
                data.ApplyModifiedProperties();
                return;
            }
            var objects = data.FindProperty("objects");
            int existing = -1;
            if (map.Objects != null) for (int i = 0; i < map.Objects.Length; i++)
            {
                var cell = ArenaMapTopology.WorldToCell(map, map.Objects[i].position);
                if (cell.X == x && cell.Y == y) { existing = i; break; }
            }
            if (paintMode == PaintMode.EraseObject)
            {
                if (existing >= 0) { objects.DeleteArrayElementAtIndex(existing); data.ApplyModifiedProperties(); }
                return;
            }
            if (existing >= 0) return;
            int group = paintMode == PaintMode.Static ? 0 : paintMode == PaintMode.Moving ? 1 : 2;
            string prefabName = group == 0 ? "StaticObstacle" : group == 1 ? "MovingObstacle" : "SpikeTrap";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Obstacles/" + prefabName + ".prefab");
            if (prefab == null) { Debug.LogWarning("缺少障碍物 Prefab：" + prefabName); return; }
            objects.InsertArrayElementAtIndex(objects.arraySize);
            var entry = objects.GetArrayElementAtIndex(objects.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
            entry.FindPropertyRelative("group").enumValueIndex = group;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("position").vector3Value = new Vector3(x - map.Width * 0.5f + 0.5f, group == 0 ? 0.5f : group == 2 ? 0.01f : 0f, y - map.Height * 0.5f + 0.5f);
            entry.FindPropertyRelative("rotation").vector3Value = Vector3.zero;
            entry.FindPropertyRelative("overridePatrolSpeed").boolValue = false;
            entry.FindPropertyRelative("patrolSpeed").floatValue = 0f;
            entry.FindPropertyRelative("overridePatrolEndOffset").boolValue = false;
            entry.FindPropertyRelative("patrolEndOffset").vector3Value = Vector3.zero;
            entry.FindPropertyRelative("overrideInitiallyVisible").boolValue = false;
            entry.FindPropertyRelative("initiallyVisible").boolValue = false;
            data.ApplyModifiedProperties();
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
            validationDirty = true;
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
            float offsetX = (map.Width - requestedWidth) * 0.5f;
            float offsetZ = (map.Height - requestedHeight) * 0.5f;
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
            var objects = data.FindProperty("objects");
            for (int i = 0; i < objects.arraySize; i++)
            {
                var position = objects.GetArrayElementAtIndex(i).FindPropertyRelative("position");
                var value = position.vector3Value;
                value.x += offsetX; value.z += offsetZ;
                position.vector3Value = value;
            }
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        private void CreateMap()
        {
            string path = EditorUtility.SaveFilePanelInProject("新建地图", "ArenaMap", "asset", "选择地图保存位置");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
            {
                EditorUtility.DisplayDialog("无法创建地图", "该路径已有资源，请选择新的文件名，避免覆盖现有资源。", "确定");
                return;
            }
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
