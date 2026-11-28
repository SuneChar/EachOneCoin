using System;
using System.Reflection;
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
            type.GetField("requestedWidth", flags).SetValue(window, 23);
            type.GetField("requestedHeight", flags).SetValue(window, 20);
            type.GetMethod("ResizeMap", flags).Invoke(window, null);
            serialized.Update();
            Assert.That(serialized.FindProperty("width").intValue, Is.EqualTo(23));
            Assert.That(serialized.FindProperty("height").intValue, Is.EqualTo(20));
            Assert.That(serialized.FindProperty("cells").arraySize, Is.EqualTo(460));
            Assert.That(serialized.FindProperty("cells").GetArrayElementAtIndex(1).FindPropertyRelative("height").intValue, Is.EqualTo(7));
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
