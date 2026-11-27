using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaGridVisualConfigTests
    {
        private GameObject root;
        private UnityEngine.Object temporaryConfig;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (temporaryConfig != null) UnityEngine.Object.DestroyImmediate(temporaryConfig);
        }

        [Test]
        public void DefaultConfig_PreservesExistingSceneAppearance()
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Grid/DefaultArenaGridVisualConfig.asset");
            Assert.That(config, Is.Not.Null);
            var so = new SerializedObject(config);
            Assert.That(so.FindProperty("tileBrightness").floatValue, Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(so.FindProperty("disconnectedBrightness").floatValue, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(so.FindProperty("paintOpacity").floatValue, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(so.FindProperty("playerColors").arraySize, Is.EqualTo(4));
        }

        [Test]
        public void PlayerColor_UsesConfigAndFallsBackWhenConfigOrEntryMissing()
        {
            root = new GameObject("GridView");
            var gridType = Type.GetType("EachOneCoin.Gameplay.ArenaGridView, Assembly-CSharp", true);
            var configType = Type.GetType("EachOneCoin.Gameplay.ArenaGridVisualConfig, Assembly-CSharp", true);
            var grid = root.AddComponent(gridType);
            var method = gridType.GetMethod("PlayerColor");
            var original = (Color)method.Invoke(grid, new object[] { 0 });
            Assert.That(original, Is.EqualTo(new Color(0.94f, 0.27f, 0.28f)));
            temporaryConfig = ScriptableObject.CreateInstance(configType);
            var configSo = new SerializedObject(temporaryConfig);
            var colors = configSo.FindProperty("playerColors");
            colors.arraySize = 1;
            colors.GetArrayElementAtIndex(0).colorValue = Color.magenta;
            configSo.ApplyModifiedPropertiesWithoutUndo();
            gridType.GetField("visualConfig", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, temporaryConfig);
            Assert.That((Color)method.Invoke(grid, new object[] { 0 }), Is.EqualTo(Color.magenta));
            Assert.That((Color)method.Invoke(grid, new object[] { 1 }), Is.EqualTo(new Color(0.25f, 0.51f, 0.95f)));
        }
    }
}
