using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class PlayerStatusPrefabTests
    {
        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }

        private Component CreateView(string assetName, string typeName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/" + assetName + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            instance = UnityEngine.Object.Instantiate(prefab);
            var type = Type.GetType("EachOneCoin.Gameplay." + typeName + ", Assembly-CSharp", true);
            var component = instance.GetComponent(type);
            Assert.That(component, Is.Not.Null);
            return component;
        }

        [Test]
        public void Portrait_InitializeTwice_UsesPrefabHierarchyAndKeepsLayout()
        {
            var view = CreateView("LocalPlayerPortrait", "LocalPlayerPortraitView");
            var rect = (RectTransform)instance.transform;
            rect.anchoredPosition = new Vector2(17f, -31f);
            int childCount = rect.childCount;
            var initialize = view.GetType().GetMethod("Initialize");
            initialize.Invoke(view, new object[] { null, null });
            initialize.Invoke(view, new object[] { null, null });
            Assert.That(rect.childCount, Is.EqualTo(childCount));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(17f, -31f)));
            Assert.That(rect.Find("ContentRoot/CircleMaskRoot/BasePortraitImage"), Is.Not.Null);
        }

        [Test]
        public void Stamina_InitializeTwice_UsesPrefabLayersAndKeepsSize()
        {
            var view = CreateView("ArcStaminaBar", "ArcStaminaBarView");
            var rect = (RectTransform)instance.transform;
            rect.sizeDelta = new Vector2(73f, 160f);
            int childCount = rect.childCount;
            var initialize = view.GetType().GetMethod("Initialize");
            initialize.Invoke(view, null);
            initialize.Invoke(view, null);
            Assert.That(rect.childCount, Is.EqualTo(childCount));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(73f, 160f)));
            Assert.That(rect.Find("MainFill"), Is.Not.Null);
            view.GetType().GetMethod("SetStamina").Invoke(view, new object[] { 50f, 100f, false, false, true });
            Assert.That((float)view.GetType().GetProperty("NormalizedStamina").GetValue(view), Is.EqualTo(0.5f));
        }

        [Test]
        public void Hearts_InitializeWithDifferentCapacity_RebuildsPrefabSlots()
        {
            var view = CreateView("HealthHearts", "HealthHeartsView");
            var initialize = view.GetType().GetMethod("Initialize");
            initialize.Invoke(view, new object[] { 2 });
            Assert.That(instance.transform.childCount, Is.EqualTo(2));
            Assert.That(instance.transform.Find("HeartSlot_0/Base"), Is.Not.Null);
            Assert.That(instance.transform.Find("HeartSlot_0/Shard_13"), Is.Not.Null);
            var slotType = Type.GetType("EachOneCoin.Gameplay.HealthHeartSlotView, Assembly-CSharp", true);
            Assert.That(instance.transform.Find("HeartSlot_0").GetComponent(slotType), Is.Not.Null);
            initialize.Invoke(view, new object[] { 4 });
            Assert.That(instance.transform.childCount, Is.EqualTo(4));
            Assert.That(view.GetType().GetProperty("Capacity").GetValue(view), Is.EqualTo(4));
        }
    }
}
