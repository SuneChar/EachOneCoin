using System;
using System.Reflection;
using EachOneCoin;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ArenaMapValidationTests
{
    private ScriptableObject map;
    private SerializedObject serialized;
    private MethodInfo validate;

    [SetUp]
    public void SetUp()
    {
        var type = Type.GetType("EachOneCoin.Gameplay.ArenaMapDefinition, Assembly-CSharp", true);
        map = ScriptableObject.CreateInstance(type);
        validate = Type.GetType("EachOneCoin.Gameplay.ArenaMapValidator, Assembly-CSharp", true).GetMethod("TryValidate");
        serialized = new SerializedObject(map);
        serialized.FindProperty("mapId").stringValue = "test-map";
        serialized.FindProperty("defaultTilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/ArenaTile.prefab");
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(map);

    private bool IsValid(out string error)
    {
        object[] arguments = { map, null };
        bool result = (bool)validate.Invoke(null, arguments);
        error = (string)arguments[1];
        return result;
    }

    [Test]
    public void DefaultLayout_WithId_IsValid()
    {
        Assert.That(IsValid(out var error), Is.True, error);
    }

    [Test]
    public void DuplicateSpawn_IsRejected()
    {
        var spawns = serialized.FindProperty("spawns");
        spawns.GetArrayElementAtIndex(1).vector2IntValue = spawns.GetArrayElementAtIndex(0).vector2IntValue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out _), Is.False);
    }

    [Test]
    public void SpawnOnHole_IsRejected()
    {
        var spawns = serialized.FindProperty("spawns");
        Vector2Int spawn = spawns.GetArrayElementAtIndex(0).vector2IntValue;
        int index = spawn.x * serialized.FindProperty("height").intValue + spawn.y;
        var cell = serialized.FindProperty("cells").GetArrayElementAtIndex(index);
        cell.FindPropertyRelative("terrain").enumValueIndex = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out _), Is.False);
    }

    [Test]
    public void MineOnSpawn_IsRejected()
    {
        var point = serialized.FindProperty("spawns").GetArrayElementAtIndex(0).vector2IntValue;
        serialized.FindProperty("mines").GetArrayElementAtIndex(0).vector2IntValue = point;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out _), Is.False);
    }

    [Test]
    public void Ramp_ConnectsAlignedLevelsAndRejectsSideEntry()
    {
        var cells = serialized.FindProperty("cells");
        var ramp = cells.GetArrayElementAtIndex(11 * 21 + 10);
        ramp.FindPropertyRelative("terrain").enumValueIndex = 4;
        ramp.FindPropertyRelative("direction").intValue = 1;
        cells.GetArrayElementAtIndex(12 * 21 + 10).FindPropertyRelative("height").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out var error), Is.True, error);
        var topology = Type.GetType("EachOneCoin.Gameplay.ArenaMapTopology, Assembly-CSharp", true);
        var traverse = topology.GetMethod("CanTraverse");
        bool Can(GridPosition a, GridPosition b) => (bool)traverse.Invoke(null, new object[] { map, a, b, false });
        Assert.That(Can(new GridPosition(10, 10), new GridPosition(11, 10)), Is.True);
        Assert.That(Can(new GridPosition(11, 10), new GridPosition(12, 10)), Is.True);
        Assert.That(Can(new GridPosition(11, 9), new GridPosition(11, 10)), Is.False);
        float height = (float)topology.GetMethod("SurfaceHeight").Invoke(null, new object[] { map, new Vector3(1f, 0f, 0f) });
        Assert.That(height, Is.EqualTo(0.125f).Within(0.001f));
        serialized.FindProperty("heightUnit").floatValue = 0.5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        height = (float)topology.GetMethod("SurfaceHeight").Invoke(null, new object[] { map, new Vector3(1f, 0f, 0f) });
        Assert.That(height, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void Step_ConnectsOneLevelButOrdinaryCliffDoesNot()
    {
        var cells = serialized.FindProperty("cells");
        var step = cells.GetArrayElementAtIndex(11 * 21 + 10);
        step.FindPropertyRelative("terrain").enumValueIndex = 5;
        step.FindPropertyRelative("height").intValue = 1;
        cells.GetArrayElementAtIndex(12 * 21 + 10).FindPropertyRelative("height").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var topology = Type.GetType("EachOneCoin.Gameplay.ArenaMapTopology, Assembly-CSharp", true);
        var traverse = topology.GetMethod("CanTraverse");
        bool Can(GridPosition a, GridPosition b) => (bool)traverse.Invoke(null, new object[] { map, a, b, false });
        Assert.That(Can(new GridPosition(10, 10), new GridPosition(11, 10)), Is.True);
        Assert.That(Can(new GridPosition(11, 10), new GridPosition(12, 10)), Is.True);
        Assert.That(Can(new GridPosition(12, 10), new GridPosition(13, 10)), Is.False);
    }

    [Test]
    public void Ramp_WithMissingHighConnection_IsRejected()
    {
        var ramp = serialized.FindProperty("cells").GetArrayElementAtIndex(11 * 21 + 10);
        ramp.FindPropertyRelative("terrain").enumValueIndex = 4;
        ramp.FindPropertyRelative("direction").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out _), Is.False);
    }

    [Test]
    public void Fingerprint_IsStableForCloneAndChangesWithTerrain()
    {
        var fingerprint = Type.GetType("EachOneCoin.Gameplay.ArenaMapFingerprint, Assembly-CSharp", true).GetMethod("Compute");
        string first = (string)fingerprint.Invoke(null, new object[] { map });
        var clone = UnityEngine.Object.Instantiate(map);
        try
        {
            Assert.That((string)fingerprint.Invoke(null, new object[] { clone }), Is.EqualTo(first));
            var data = new SerializedObject(clone);
            data.FindProperty("cells").GetArrayElementAtIndex(0).FindPropertyRelative("terrain").enumValueIndex = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That((string)fingerprint.Invoke(null, new object[] { clone }), Is.Not.EqualTo(first));
        }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }

    [Test]
    public void EditorBrush_PlacesPointsAndAddsThenErasesObstacle()
    {
        var type = Type.GetType("EachOneCoin.Editor.ArenaMapEditorWindow, Assembly-CSharp-Editor", true);
        var window = ScriptableObject.CreateInstance(type);
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("map", flags).SetValue(window, map);
            var mode = type.GetField("paintMode", flags);
            var paint = type.GetMethod("PaintCell", flags);
            mode.SetValue(window, Enum.Parse(mode.FieldType, "Spawn"));
            paint.Invoke(window, new object[] { 3, 3 });
            serialized.Update();
            Assert.That(serialized.FindProperty("spawns").GetArrayElementAtIndex(0).vector2IntValue, Is.EqualTo(new Vector2Int(3, 3)));
            mode.SetValue(window, Enum.Parse(mode.FieldType, "Moving"));
            paint.Invoke(window, new object[] { 4, 4 });
            serialized.Update();
            Assert.That(serialized.FindProperty("objects").arraySize, Is.EqualTo(1));
            var item = serialized.FindProperty("objects").GetArrayElementAtIndex(0);
            Assert.That(item.FindPropertyRelative("group").enumValueIndex, Is.EqualTo(1));
            Assert.That(item.FindPropertyRelative("prefab").objectReferenceValue, Is.Not.Null);
            Assert.That(IsValid(out var error), Is.True, error);
            mode.SetValue(window, Enum.Parse(mode.FieldType, "Spawn"));
            paint.Invoke(window, new object[] { 4, 4 });
            Assert.That(IsValid(out _), Is.False, "出生点和障碍重叠必须被拒绝");
            paint.Invoke(window, new object[] { 3, 3 });
            mode.SetValue(window, Enum.Parse(mode.FieldType, "Moving"));
            paint.Invoke(window, new object[] { 4, 4 });
            serialized.Update();
            Assert.That(serialized.FindProperty("objects").arraySize, Is.EqualTo(1));
            var patrol = serialized.FindProperty("objects").GetArrayElementAtIndex(0);
            patrol.FindPropertyRelative("overridePatrolEndOffset").boolValue = true;
            patrol.FindPropertyRelative("patrolEndOffset").vector3Value = Vector3.right * 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(IsValid(out _), Is.False, "越界巡逻终点必须被拒绝");
            patrol = serialized.FindProperty("objects").GetArrayElementAtIndex(0);
            patrol.FindPropertyRelative("patrolEndOffset").vector3Value = Vector3.right * 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(IsValid(out _), Is.True);
            mode.SetValue(window, Enum.Parse(mode.FieldType, "EraseObject"));
            paint.Invoke(window, new object[] { 4, 4 });
            serialized.Update();
            Assert.That(serialized.FindProperty("objects").arraySize, Is.Zero);
        }
        finally { Undo.ClearUndo(map); UnityEngine.Object.DestroyImmediate(window); }
    }

    [Test]
    public void EditorResize_PreservesExistingCellsAndChangesDimensions()
    {
        var type = Type.GetType("EachOneCoin.Editor.ArenaMapEditorWindow, Assembly-CSharp-Editor", true);
        var window = ScriptableObject.CreateInstance(type);
        try
        {
            var cells = serialized.FindProperty("cells");
            cells.GetArrayElementAtIndex(1).FindPropertyRelative("height").intValue = 7;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("map", flags).SetValue(window, map);
            var mode = type.GetField("paintMode", flags);
            mode.SetValue(window, Enum.Parse(mode.FieldType, "Moving"));
            type.GetMethod("PaintCell", flags).Invoke(window, new object[] { 4, 4 });
            type.GetField("requestedWidth", flags).SetValue(window, 23);
            type.GetField("requestedHeight", flags).SetValue(window, 20);
            type.GetMethod("ResizeMap", flags).Invoke(window, null);
            serialized.Update();
            Assert.That(serialized.FindProperty("width").intValue, Is.EqualTo(23));
            Assert.That(serialized.FindProperty("height").intValue, Is.EqualTo(20));
            Assert.That(serialized.FindProperty("cells").arraySize, Is.EqualTo(460));
            Assert.That(serialized.FindProperty("cells").GetArrayElementAtIndex(1).FindPropertyRelative("height").intValue, Is.EqualTo(7));
            Vector3 position = serialized.FindProperty("objects").GetArrayElementAtIndex(0).FindPropertyRelative("position").vector3Value;
            var topology = Type.GetType("EachOneCoin.Gameplay.ArenaMapTopology, Assembly-CSharp", true);
            var cell = (GridPosition)topology.GetMethod("WorldToCell").Invoke(null, new object[] { map, position });
            Assert.That(cell, Is.EqualTo(new GridPosition(4, 4)));
        }
        finally { Undo.ClearUndo(map); UnityEngine.Object.DestroyImmediate(window); }
    }

    [Test]
    public void MismatchedCellCount_IsRejected()
    {
        serialized.FindProperty("width").intValue = 22;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(IsValid(out _), Is.False);
    }
}
