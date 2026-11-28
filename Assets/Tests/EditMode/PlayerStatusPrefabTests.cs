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

        [Test]
        public void Portrait_Prefab_AlignsOverlayContainerAndRendersNormalizedLayersAndNone()
        {
            var view = CreateView("LocalPlayerPortrait", "LocalPlayerPortraitView");
            var type = view.GetType();
            type.GetMethod("Initialize").Invoke(view, new object[] { null, null });

            var overlayContainer = (RectTransform)instance.transform.Find("ContentRoot/CircleMaskRoot/OverlayContainer");
            Assert.That(overlayContainer, Is.Not.Null);
            Assert.That(overlayContainer.anchorMin, Is.EqualTo(new Vector2(0.04f, 0f)));
            Assert.That(overlayContainer.anchorMax, Is.EqualTo(new Vector2(0.96f, 0.96f)));

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            try
            {
                var appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
                var layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
                var setParticipant = type.GetMethod("SetParticipant", new[] { typeof(int), appearanceType, typeof(ParticipantStatus) });

                object layer = Activator.CreateInstance(layerType);
                layerType.GetField("Sprite").SetValue(layer, sprite);
                layerType.GetField("NormalizedCenter").SetValue(layer, new Vector2(0.55f, 0.85f));
                layerType.GetField("NormalizedSize").SetValue(layer, new Vector2(1.2f, 0.8f));
                Array layers = Array.CreateInstance(layerType, 1);
                layers.SetValue(layer, 0);

                object layered = Activator.CreateInstance(appearanceType);
                appearanceType.GetField("TintColor").SetValue(layered, Color.red);
                appearanceType.GetField("CosmeticId").SetValue(layered, "slot_0");
                appearanceType.GetField("OverlayLayers").SetValue(layered, layers);

                setParticipant.Invoke(view, new object[] { 0, layered, ParticipantStatus.Active });
                Assert.That(type.GetProperty("ActiveOverlayCount").GetValue(view), Is.EqualTo(1));
                var overlay0 = (RectTransform)overlayContainer.Find("Overlay_0");
                Assert.That(overlay0, Is.Not.Null);
                Assert.That(overlay0.anchorMin.x, Is.EqualTo(0.55f - 0.6f).Within(0.001f));
                Assert.That(overlay0.anchorMax.y, Is.EqualTo(0.85f + 0.4f).Within(0.001f));

                object none = Activator.CreateInstance(appearanceType);
                appearanceType.GetField("TintColor").SetValue(none, Color.red);
                appearanceType.GetField("CosmeticId").SetValue(none, "none");
                appearanceType.GetField("OverlayLayers").SetValue(none, Array.CreateInstance(layerType, 0));

                setParticipant.Invoke(view, new object[] { 0, none, ParticipantStatus.Active });
                Assert.That(type.GetProperty("ActiveOverlayCount").GetValue(view), Is.EqualTo(0));
                Assert.That(overlay0.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
