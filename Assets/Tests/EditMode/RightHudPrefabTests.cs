using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class RightHudPrefabTests
    {
        private GameObject instance;
        private GameObject secondInstance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (secondInstance != null) UnityEngine.Object.DestroyImmediate(secondInstance);
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

        [Test]
        public void MinimapAndParticipantStripPrefabs_ToggleModesWithoutChildLeaksOrDestroyingSprites()
        {
            var minimapView = CreateView("ArenaMinimap", "ArenaMinimapView");
            var spawns = new[] { new GridPosition(1, 1), new GridPosition(19, 19) };
            var grid = new GridModel(21, 21, spawns);
            minimapView.GetType().GetMethod("InitializeStandalone").Invoke(minimapView, new object[] { grid, spawns, new GridPosition[0], null });

            var stripPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/ParticipantHudStrip.prefab");
            Assert.That(stripPrefab, Is.Not.Null);
            secondInstance = UnityEngine.Object.Instantiate(stripPrefab);
            var stripType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudStripView, Assembly-CSharp", true);
            var stripView = secondInstance.GetComponent(stripType);
            Assert.That(stripView, Is.Not.Null);
            stripType.GetMethod("InitializeStandalone").Invoke(stripView, new object[] { 4, null });

            int mapChildCount = instance.transform.childCount;
            int stripChildCount = secondInstance.transform.childCount;
            var refreshCountProp = minimapView.GetType().GetProperty("TextureRefreshCount");
            int textureRefreshBefore = (int)refreshCountProp.GetValue(minimapView);

            var mapSizeModeType = Type.GetType("EachOneCoin.Gameplay.MinimapSizeMode, Assembly-CSharp", true);
            var stripLayoutModeType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudLayoutMode, Assembly-CSharp", true);
            var setSizeMode = minimapView.GetType().GetMethod("SetSizeMode", new[] { mapSizeModeType });
            var setLayoutMode = stripType.GetMethod("SetLayoutMode", new[] { stripLayoutModeType, typeof(float), typeof(Vector2) });

            setSizeMode.Invoke(minimapView, new[] { Enum.Parse(mapSizeModeType, "Compact") });
            setLayoutMode.Invoke(stripView, new object[] { Enum.Parse(stripLayoutModeType, "VerticalRightAligned"), 368f, new Vector2(-12f, 230f) });

            Assert.That(((RectTransform)instance.transform).sizeDelta, Is.EqualTo(new Vector2(210f, 210f)));
            Assert.That((int)refreshCountProp.GetValue(minimapView), Is.EqualTo(textureRefreshBefore));
            Assert.That(instance.transform.childCount, Is.EqualTo(mapChildCount));
            Assert.That(secondInstance.transform.childCount, Is.EqualTo(stripChildCount));

            var avatarRect = (RectTransform)secondInstance.transform.Find("ParticipantSlot_0/AvatarRoot");
            Assert.That(avatarRect, Is.Not.Null);
            Assert.That(avatarRect.sizeDelta.x, Is.EqualTo(avatarRect.sizeDelta.y).Within(0.001f));

            setSizeMode.Invoke(minimapView, new[] { Enum.Parse(mapSizeModeType, "Expanded") });
            setLayoutMode.Invoke(stripView, new object[] { Enum.Parse(stripLayoutModeType, "Horizontal"), 368f, new Vector2(-12f, 388f) });

            Assert.That(((RectTransform)instance.transform).sizeDelta, Is.EqualTo(new Vector2(368f, 368f)));
            Assert.That(((RectTransform)secondInstance.transform).sizeDelta, Is.EqualTo(new Vector2(368f, 154f)));
            Assert.That((int)refreshCountProp.GetValue(minimapView), Is.EqualTo(textureRefreshBefore));
            Assert.That(instance.transform.childCount, Is.EqualTo(mapChildCount));
            Assert.That(secondInstance.transform.childCount, Is.EqualTo(stripChildCount));

            var serialized = new SerializedObject(minimapView);
            serialized.Update();
            Assert.That(serialized.FindProperty("circleSprite").objectReferenceValue != null, Is.True);
        }

        [Test]
        public void ParticipantStrip_Prefab_AlignsOverlayContainerWithBasePortraitAndRendersNormalizedLayers()
        {
            var stripView = CreateView("ParticipantHudStrip", "ParticipantHudStripView");
            var stripType = stripView.GetType();
            stripType.GetMethod("InitializeStandalone").Invoke(stripView, new object[] { 4, null });

            var basePortrait = (RectTransform)instance.transform.Find("ParticipantSlot_0/AvatarRoot/AvatarMask/BasePortrait");
            var overlayContainer = (RectTransform)instance.transform.Find("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer");
            Assert.That(basePortrait, Is.Not.Null);
            Assert.That(overlayContainer, Is.Not.Null);
            Assert.That(overlayContainer.anchorMin, Is.EqualTo(new Vector2(0.04f, 0f)));
            Assert.That(overlayContainer.anchorMax, Is.EqualTo(new Vector2(0.96f, 0.96f)));
            Assert.That(overlayContainer.anchorMin, Is.EqualTo(basePortrait.anchorMin));
            Assert.That(overlayContainer.anchorMax, Is.EqualTo(basePortrait.anchorMax));

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            try
            {
                var snapshotType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudSnapshot, Assembly-CSharp", true);
                var appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
                var layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
                var layoutModeType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudLayoutMode, Assembly-CSharp", true);

                object layer = Activator.CreateInstance(layerType);
                layerType.GetField("Sprite").SetValue(layer, sprite);
                layerType.GetField("NormalizedCenter").SetValue(layer, new Vector2(0.537f, 0.704f));
                layerType.GetField("NormalizedSize").SetValue(layer, new Vector2(1.071f, 0.510f));
                Array layers = Array.CreateInstance(layerType, 1);
                layers.SetValue(layer, 0);

                object app0 = Activator.CreateInstance(appearanceType);
                appearanceType.GetField("TintColor").SetValue(app0, Color.blue);
                appearanceType.GetField("CosmeticId").SetValue(app0, "slot_1");
                appearanceType.GetField("OverlayLayers").SetValue(app0, layers);

                object app1None = Activator.CreateInstance(appearanceType);
                appearanceType.GetField("TintColor").SetValue(app1None, Color.green);
                appearanceType.GetField("CosmeticId").SetValue(app1None, "none");
                appearanceType.GetField("OverlayLayers").SetValue(app1None, Array.CreateInstance(layerType, 0));

                object MakeSnap(int slot, object appearance, Color color)
                {
                    object s = Activator.CreateInstance(snapshotType);
                    snapshotType.GetField("SlotId").SetValue(s, slot);
                    snapshotType.GetField("IsParticipating").SetValue(s, true);
                    snapshotType.GetField("Health").SetValue(s, 3);
                    snapshotType.GetField("MaxHealth").SetValue(s, 3);
                    snapshotType.GetField("InitialMaxHealth").SetValue(s, 3);
                    snapshotType.GetField("Status").SetValue(s, ParticipantStatus.Active);
                    snapshotType.GetField("Color").SetValue(s, color);
                    snapshotType.GetField("Appearance").SetValue(s, appearance);
                    return s;
                }

                Array snaps = Array.CreateInstance(snapshotType, 2);
                snaps.SetValue(MakeSnap(0, app0, Color.blue), 0);
                snaps.SetValue(MakeSnap(1, app1None, Color.green), 1);

                var listType = typeof(System.Collections.Generic.IReadOnlyList<>).MakeGenericType(snapshotType);
                stripType.GetMethod("Refresh", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { listType }, null)
                    .Invoke(stripView, new object[] { snaps });

                var getOverlayCount = stripType.GetMethod("GetSlotOverlayCount", new[] { typeof(int) });
                Assert.That((int)getOverlayCount.Invoke(stripView, new object[] { 0 }), Is.EqualTo(1));
                Assert.That((int)getOverlayCount.Invoke(stripView, new object[] { 1 }), Is.EqualTo(0));

                var overlay0 = (RectTransform)overlayContainer.Find("Overlay_0");
                Assert.That(overlay0, Is.Not.Null);
                Assert.That(overlay0.anchorMin.x, Is.EqualTo(0.537f - 1.071f * 0.5f).Within(0.001f));

                // Toggling to VerticalRightAligned keeps OverlayContainer aligned and normalized layer intact.
                stripType.GetMethod("SetLayoutMode", new[] { layoutModeType })
                    .Invoke(stripView, new[] { Enum.Parse(layoutModeType, "VerticalRightAligned") });
                Assert.That(overlayContainer.anchorMin, Is.EqualTo(new Vector2(0.04f, 0f)));
                Assert.That(overlayContainer.anchorMax, Is.EqualTo(new Vector2(0.96f, 0.96f)));
                Assert.That((int)getOverlayCount.Invoke(stripView, new object[] { 0 }), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
