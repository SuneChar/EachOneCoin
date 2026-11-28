using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MinimapHudLayoutIntegrationTests
    {
        private readonly List<UnityEngine.Object> cleanupObjects = new List<UnityEngine.Object>();

        private Type arenaGameUiType;
        private Type minimapViewType;
        private Type minimapSizeModeType;
        private Type stripViewType;
        private Type stripLayoutModeType;
        private Type snapshotType;
        private Type matchCoordinatorType;
        private Type controlPromptImagesType;
        private Type rawImageType;
        private Type tmpTextType;

        [SetUp]
        public void SetUp()
        {
            arenaGameUiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            minimapViewType = Type.GetType("EachOneCoin.Gameplay.ArenaMinimapView, Assembly-CSharp", true);
            minimapSizeModeType = Type.GetType("EachOneCoin.Gameplay.MinimapSizeMode, Assembly-CSharp", true);
            stripViewType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudStripView, Assembly-CSharp", true);
            stripLayoutModeType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudLayoutMode, Assembly-CSharp", true);
            snapshotType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudSnapshot, Assembly-CSharp", true);
            matchCoordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            controlPromptImagesType = Type.GetType("ControlPromptImages, Assembly-CSharp", true);
            rawImageType = Type.GetType("UnityEngine.UI.RawImage, UnityEngine.UI", true);
            tmpTextType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanupObjects.Count - 1; i >= 0; i--)
            {
                if (cleanupObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(cleanupObjects[i]);
                }
            }
            cleanupObjects.Clear();
        }

        private GameObject Track(GameObject obj)
        {
            cleanupObjects.Add(obj);
            return obj;
        }

        private Texture2D CreateRuntimeTexture(string name)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            cleanupObjects.Add(tex);
            return tex;
        }

        private void BuildWiredHud(
            out Component arenaUi,
            out Component minimapView,
            out Component stripView,
            out Component matchCoordinator)
        {
            var root = Track(new GameObject("MinimapHudIntegrationRoot", typeof(RectTransform)));
            root.SetActive(false);

            arenaUi = root.AddComponent(arenaGameUiType);

            var minimapObj = new GameObject("ArenaMinimap", typeof(RectTransform));
            minimapObj.transform.SetParent(root.transform, false);
            minimapView = minimapObj.AddComponent(minimapViewType);
            var spawns = new[]
            {
                new GridPosition(1, 1),
                new GridPosition(19, 1),
                new GridPosition(1, 19),
                new GridPosition(19, 19)
            };
            var grid = new GridModel(21, 21, spawns);
            minimapViewType.GetMethod("InitializeStandalone", new[]
            {
                typeof(GridModel),
                typeof(IReadOnlyList<GridPosition>),
                typeof(IReadOnlyList<GridPosition>),
                typeof(Color[])
            }).Invoke(minimapView, new object[] { grid, spawns, Array.Empty<GridPosition>(), null });

            var stripObj = new GameObject("ParticipantHudStrip", typeof(RectTransform));
            stripObj.transform.SetParent(root.transform, false);
            stripView = stripObj.AddComponent(stripViewType);
            stripViewType.GetMethod("InitializeStandalone", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(stripView, new object[] { 4, null });

            // Populate 4 participating slots so vertical strip height reflects 4 active rows.
            Array snapshots = Array.CreateInstance(snapshotType, 4);
            FieldInfo slotIdField = snapshotType.GetField("SlotId");
            FieldInfo participatingField = snapshotType.GetField("IsParticipating");
            FieldInfo coinsField = snapshotType.GetField("Coins");
            FieldInfo healthField = snapshotType.GetField("Health");
            FieldInfo maxHealthField = snapshotType.GetField("MaxHealth");
            FieldInfo initHealthField = snapshotType.GetField("InitialMaxHealth");
            FieldInfo statusField = snapshotType.GetField("Status");
            FieldInfo colorField = snapshotType.GetField("Color");

            for (int i = 0; i < 4; i++)
            {
                object item = Activator.CreateInstance(snapshotType);
                slotIdField.SetValue(item, i);
                participatingField.SetValue(item, true);
                coinsField.SetValue(item, i == 0 ? 4 : i);
                healthField.SetValue(item, 3);
                maxHealthField.SetValue(item, 3);
                initHealthField.SetValue(item, 3);
                statusField.SetValue(item, ParticipantStatus.Active);
                colorField.SetValue(item, Color.cyan);
                snapshots.SetValue(item, i);
            }

            Type snapshotListType = typeof(IReadOnlyList<>).MakeGenericType(snapshotType);
            stripViewType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance, null, new[] { snapshotListType }, null)
                .Invoke(stripView, new object[] { snapshots });

            var matchObj = Track(new GameObject("MockMatchCoordinator"));
            matchObj.SetActive(false);
            matchCoordinator = matchObj.AddComponent(matchCoordinatorType);

            BindingFlags nonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
            arenaGameUiType.GetField("minimapView", nonPublic).SetValue(arenaUi, minimapView);
            arenaGameUiType.GetField("participantStripView", nonPublic).SetValue(arenaUi, stripView);
            arenaGameUiType.GetField("match", nonPublic).SetValue(arenaUi, matchCoordinator);
        }

        [Test]
        public void ArenaGameUI_ToggleMinimapSize_SynchronizesMinimapAndParticipantStripLayout()
        {
            BuildWiredHud(out Component arenaUi, out Component minimapView, out Component stripView, out _);

            MethodInfo setMode = arenaGameUiType.GetMethod("SetMinimapSizeMode", new[] { minimapSizeModeType });
            MethodInfo toggle = arenaGameUiType.GetMethod("ToggleMinimapSize", Type.EmptyTypes);
            PropertyInfo currentModeProp = arenaGameUiType.GetProperty("CurrentMinimapSizeMode");
            PropertyInfo stripModeProp = stripViewType.GetProperty("LayoutMode");
            PropertyInfo textureRefreshProp = minimapViewType.GetProperty("TextureRefreshCount");

            object expandedMode = Enum.Parse(minimapSizeModeType, "Expanded");
            object compactMode = Enum.Parse(minimapSizeModeType, "Compact");
            object horizontalMode = Enum.Parse(stripLayoutModeType, "Horizontal");
            object verticalMode = Enum.Parse(stripLayoutModeType, "VerticalRightAligned");

            var mapRect = (RectTransform)minimapView.transform;
            var stripRect = (RectTransform)stripView.transform;

            setMode.Invoke(arenaUi, new[] { expandedMode });
            int refreshBefore = (int)textureRefreshProp.GetValue(minimapView);

            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));
            Assert.That(mapRect.sizeDelta, Is.EqualTo(new Vector2(368f, 368f)));
            Assert.That(stripModeProp.GetValue(stripView), Is.EqualTo(horizontalMode));
            Assert.That(stripRect.sizeDelta, Is.EqualTo(new Vector2(368f, 154f)));
            Assert.That(stripRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 388f)));

            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.True);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(compactMode));
            Assert.That(mapRect.sizeDelta, Is.EqualTo(new Vector2(210f, 210f)));
            Assert.That(stripModeProp.GetValue(stripView), Is.EqualTo(verticalMode));
            Assert.That(stripRect.sizeDelta, Is.EqualTo(new Vector2(180f, 306f)));
            Assert.That(stripRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 230f)));
            Assert.That((int)textureRefreshProp.GetValue(minimapView), Is.EqualTo(refreshBefore));

            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.True);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));
            Assert.That(mapRect.sizeDelta, Is.EqualTo(new Vector2(368f, 368f)));
            Assert.That(stripModeProp.GetValue(stripView), Is.EqualTo(horizontalMode));
            Assert.That(stripRect.sizeDelta, Is.EqualTo(new Vector2(368f, 154f)));
            Assert.That(stripRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 388f)));
            Assert.That((int)textureRefreshProp.GetValue(minimapView), Is.EqualTo(refreshBefore));
        }

        [Test]
        public void ArenaGameUI_ToggleMinimapSize_BlockedWhenPausedOrOverlayMenuOpen()
        {
            BuildWiredHud(out Component arenaUi, out _, out _, out Component matchCoordinator);

            BindingFlags nonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo toggle = arenaGameUiType.GetMethod("ToggleMinimapSize", Type.EmptyTypes);
            PropertyInfo currentModeProp = arenaGameUiType.GetProperty("CurrentMinimapSizeMode");
            FieldInfo settingsOpenField = arenaGameUiType.GetField("settingsOpen", nonPublic);
            FieldInfo resultShownField = arenaGameUiType.GetField("resultShown", nonPublic);
            FieldInfo networkMenuOpenField = arenaGameUiType.GetField("networkMenuOpen", nonPublic);
            PropertyInfo isPausedProp = matchCoordinatorType.GetProperty("IsPaused");
            MethodInfo setIsPaused = isPausedProp.GetSetMethod(true);

            object expandedMode = Enum.Parse(minimapSizeModeType, "Expanded");
            object compactMode = Enum.Parse(minimapSizeModeType, "Compact");

            settingsOpenField.SetValue(arenaUi, true);
            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.False);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));
            settingsOpenField.SetValue(arenaUi, false);

            resultShownField.SetValue(arenaUi, true);
            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.False);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));
            resultShownField.SetValue(arenaUi, false);

            networkMenuOpenField.SetValue(arenaUi, true);
            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.False);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));
            networkMenuOpenField.SetValue(arenaUi, false);

            setIsPaused.Invoke(matchCoordinator, new object[] { true });
            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.False);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(expandedMode));

            setIsPaused.Invoke(matchCoordinator, new object[] { false });
            Assert.That((bool)toggle.Invoke(arenaUi, null), Is.True);
            Assert.That(currentModeProp.GetValue(arenaUi), Is.EqualTo(compactMode));
        }

        [Test]
        public void ControlPromptImages_Refresh_TogglesMinimapPromptAndHandlesMissingReferencesSafely()
        {
            var promptRoot = Track(new GameObject("ControlPromptsRuntime", typeof(RectTransform)));
            Component prompts = promptRoot.AddComponent(controlPromptImagesType);
            BindingFlags nonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

            Texture2D mouseTex = CreateRuntimeTexture("mouse");
            Texture2D wTex = CreateRuntimeTexture("w");
            Texture2D aTex = CreateRuntimeTexture("a");
            Texture2D sTex = CreateRuntimeTexture("s");
            Texture2D dTex = CreateRuntimeTexture("d");
            Texture2D arrowsTex = CreateRuntimeTexture("arrows");
            Texture2D escTex = CreateRuntimeTexture("esc");
            Texture2D mTex = CreateRuntimeTexture("keyboard_m");
            Texture2D stickTex = CreateRuntimeTexture("stick");
            Texture2D xboxATex = CreateRuntimeTexture("xbox_a");
            Texture2D xboxBTex = CreateRuntimeTexture("xbox_b");
            Texture2D xboxMenuTex = CreateRuntimeTexture("xbox_menu");
            Texture2D xboxViewTex = CreateRuntimeTexture("xbox_view");

            Array movementIcons = Array.CreateInstance(rawImageType, 5);
            for (int i = 0; i < 5; i++)
            {
                var iconObj = new GameObject("Move_" + i, typeof(RectTransform));
                iconObj.transform.SetParent(promptRoot.transform, false);
                movementIcons.SetValue(iconObj.AddComponent(rawImageType), i);
            }

            Component CreateChild(string name, Type compType)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(promptRoot.transform, false);
                return obj.AddComponent(compType);
            }

            Component actionIcon = CreateChild("ActionIcon", rawImageType);
            Component backIcon = CreateChild("BackIcon", rawImageType);
            Component movementLabel = CreateChild("MovementLabel", tmpTextType);
            Component actionLabel = CreateChild("ActionLabel", tmpTextType);
            Component backLabel = CreateChild("BackLabel", tmpTextType);

            controlPromptImagesType.GetField("mouseMove", nonPublic).SetValue(prompts, mouseTex);
            controlPromptImagesType.GetField("mouseLeft", nonPublic).SetValue(prompts, mouseTex);
            controlPromptImagesType.GetField("keyboardW", nonPublic).SetValue(prompts, wTex);
            controlPromptImagesType.GetField("keyboardA", nonPublic).SetValue(prompts, aTex);
            controlPromptImagesType.GetField("keyboardS", nonPublic).SetValue(prompts, sTex);
            controlPromptImagesType.GetField("keyboardD", nonPublic).SetValue(prompts, dTex);
            controlPromptImagesType.GetField("keyboardArrows", nonPublic).SetValue(prompts, arrowsTex);
            controlPromptImagesType.GetField("keyboardEscape", nonPublic).SetValue(prompts, escTex);
            controlPromptImagesType.GetField("keyboardM", nonPublic).SetValue(prompts, mTex);
            controlPromptImagesType.GetField("xboxStick", nonPublic).SetValue(prompts, stickTex);
            controlPromptImagesType.GetField("xboxA", nonPublic).SetValue(prompts, xboxATex);
            controlPromptImagesType.GetField("xboxB", nonPublic).SetValue(prompts, xboxBTex);
            controlPromptImagesType.GetField("xboxMenu", nonPublic).SetValue(prompts, xboxMenuTex);
            controlPromptImagesType.GetField("xboxView", nonPublic).SetValue(prompts, xboxViewTex);

            controlPromptImagesType.GetField("movementIcons", nonPublic).SetValue(prompts, movementIcons);
            controlPromptImagesType.GetField("actionIcon", nonPublic).SetValue(prompts, actionIcon);
            controlPromptImagesType.GetField("backIcon", nonPublic).SetValue(prompts, backIcon);
            controlPromptImagesType.GetField("movementLabel", nonPublic).SetValue(prompts, movementLabel);
            controlPromptImagesType.GetField("actionLabel", nonPublic).SetValue(prompts, actionLabel);
            controlPromptImagesType.GetField("backLabel", nonPublic).SetValue(prompts, backLabel);

            MethodInfo refresh = controlPromptImagesType.GetMethod("Refresh", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            Assert.That(refresh, Is.Not.Null);

            // Null-safe when minimapIcon / minimapLabel are unassigned.
            Assert.DoesNotThrow(() => refresh.Invoke(prompts, new object[] { true, false, true, false }));
            Assert.DoesNotThrow(() => refresh.Invoke(prompts, new object[] { true, true, false, true }));

            // Wire minimapIcon & minimapLabel and verify keyboard / gamepad / non-playing transitions.
            Component minimapIcon = CreateChild("MinimapIcon", rawImageType);
            Component minimapLabel = CreateChild("MinimapLabel", tmpTextType);
            controlPromptImagesType.GetField("minimapIcon", nonPublic).SetValue(prompts, minimapIcon);
            controlPromptImagesType.GetField("minimapLabel", nonPublic).SetValue(prompts, minimapLabel);

            PropertyInfo textureProp = rawImageType.GetProperty("texture");
            PropertyInfo textProp = tmpTextType.GetProperty("text");

            refresh.Invoke(prompts, new object[] { true, false, true, false });
            Assert.That(minimapIcon.gameObject.activeSelf, Is.True);
            Assert.That(minimapLabel.gameObject.activeSelf, Is.True);
            Assert.That(textureProp.GetValue(minimapIcon), Is.SameAs(mTex));
            Assert.That(textProp.GetValue(minimapLabel), Is.EqualTo("小地图"));

            refresh.Invoke(prompts, new object[] { true, true, true, false });
            Assert.That(minimapIcon.gameObject.activeSelf, Is.True);
            Assert.That(minimapLabel.gameObject.activeSelf, Is.True);
            Assert.That(textureProp.GetValue(minimapIcon), Is.SameAs(xboxViewTex));
            Assert.That(textProp.GetValue(minimapLabel), Is.EqualTo("小地图"));

            refresh.Invoke(prompts, new object[] { true, false, false, true });
            Assert.That(minimapIcon.gameObject.activeSelf, Is.False);
            Assert.That(minimapLabel.gameObject.activeSelf, Is.False);
        }
    }
}
