using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class RightHudPrefabTests
    {
        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }

        private Component CreateView(string assetName, string typeName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/" + assetName + ".prefab");
            Assert.That(asset, Is.Not.Null);
            instance = UnityEngine.Object.Instantiate(asset);
            var type = Type.GetType("EachOneCoin.Gameplay." + typeName + ", Assembly-CSharp", true);
            return instance.GetComponent(type);
        }

        [Test]
        public void Minimap_UsesGridAndMarkerTemplatesWithoutDuplicateInitialization()
        {
            var view = CreateView("ArenaMinimap", "ArenaMinimapView");
            var spawns = new[] { new GridPosition(1, 1), new GridPosition(19, 19) };
            var grid = new GridModel(21, 21, spawns);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("bottomRightOffset").vector2Value = new Vector2(-35f, 27f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var init = view.GetType().GetMethod("InitializeStandalone");
            init.Invoke(view, new object[] { grid, spawns, new GridPosition[0], null });
            int count = instance.transform.childCount;
            init.Invoke(view, new object[] { grid, spawns, new GridPosition[0], null });
            Assert.That(instance.transform.childCount, Is.EqualTo(count));
            Assert.That(((RectTransform)instance.transform).anchoredPosition, Is.EqualTo(new Vector2(-35f, 27f)));
            Assert.That(instance.transform.Find("GridLayer"), Is.Not.Null);
            var markerType = Type.GetType("EachOneCoin.Gameplay.MinimapPlayerMarkerView, Assembly-CSharp", true);
            Assert.That(instance.transform.Find("PlayerMarker_0").GetComponent(markerType), Is.Not.Null);
            serialized.Update();
            var persistentSprite = serialized.FindProperty("circleSprite").objectReferenceValue;
            var rawImage = serialized.FindProperty("gridRawImage").objectReferenceValue;
            var runtimeTexture = (UnityEngine.Object)rawImage.GetType().GetProperty("texture").GetValue(rawImage);
            Assert.That(runtimeTexture, Is.Not.Null);
            view.GetType().GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
            Assert.That(runtimeTexture == null, Is.True, "Generated grid texture must be destroyed by cleanup.");
            Assert.That(persistentSprite != null, Is.True, "Prefab Sprite assets must remain alive.");
            UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
        }

        [Test]
        public void ParticipantStrip_RebuildsTemplateSlotsWhenCapacityChanges()
        {
            var view = CreateView("ParticipantHudStrip", "ParticipantHudStripView");
            var init = view.GetType().GetMethod("InitializeStandalone");
            init.Invoke(view, new object[] { 2, null });
            Assert.That(instance.transform.childCount, Is.EqualTo(2));
            var type = Type.GetType("EachOneCoin.Gameplay.ParticipantHudSlotView, Assembly-CSharp", true);
            Assert.That(instance.transform.Find("ParticipantSlot_0").GetComponent(type), Is.Not.Null);
            init.Invoke(view, new object[] { 4, null });
            Assert.That(instance.transform.childCount, Is.EqualTo(4));
            Assert.That(view.GetType().GetProperty("Capacity").GetValue(view), Is.EqualTo(4));
        }

        [Test]
        public void Clock_UsesAuthoredHierarchyAndUpdatesTime()
        {
            var view = CreateView("MatchClockAndMine", "MatchClockAndMineView");
            var rect = (RectTransform)instance.transform;
            rect.anchoredPosition = new Vector2(-44f, -33f);
            int count = rect.childCount;
            var init = view.GetType().GetMethod("Initialize");
            init.Invoke(view, new object[] { null });
            init.Invoke(view, new object[] { null });
            view.GetType().GetMethod("SetTime").Invoke(view, new object[] { 42 });
            Assert.That(rect.childCount, Is.EqualTo(count));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-44f, -33f)));
            Assert.That(view.GetType().GetProperty("LastRemainingSeconds").GetValue(view), Is.EqualTo(42));
        }
    }
}
