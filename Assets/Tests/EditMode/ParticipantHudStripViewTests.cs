using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ParticipantHudStripViewTests
    {
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();

        private GameObject root;
        private Component view;
        private Type viewType;
        private Type layoutModeType;
        private Type snapshotType;
        private Type appearanceType;
        private Type layerType;
        private Type imageType;

        private MethodInfo initStandaloneMethod;
        private MethodInfo computeLeaderMaskMethod;
        private MethodInfo refreshMethod;
        private MethodInfo tickMethod;
        private MethodInfo setLayoutModeMethod;
        private MethodInfo setLayoutModeWithRectMethod;
        private MethodInfo isSlotVisibleMethod;
        private MethodInfo isCrownVisibleMethod;
        private MethodInfo isMedalVisibleMethod;
        private MethodInfo getMedalRankTextMethod;
        private MethodInfo getCoinTextMethod;
        private MethodInfo getMiniHeartStateMethod;
        private MethodInfo getMiniHeartCountMethod;
        private MethodInfo getHealthSegmentColorMethod;
        private MethodInfo getSlotCosmeticIdMethod;
        private MethodInfo getSlotOverlayCountMethod;

        private PropertyInfo lastLeaderMaskProp;
        private PropertyInfo capacityProp;
        private PropertyInfo layoutModeProp;
        private PropertyInfo imageSpriteProp;
        private PropertyInfo imageEnabledProp;
        private PropertyInfo imagePreserveAspectProp;

        private FieldInfo snapshotSlotIdField;
        private FieldInfo snapshotDisplayNameField;
        private FieldInfo snapshotIsParticipatingField;
        private FieldInfo snapshotIsLocalField;
        private FieldInfo snapshotCoinsField;
        private FieldInfo snapshotFinishOrderField;
        private FieldInfo snapshotHealthField;
        private FieldInfo snapshotMaxHealthField;
        private FieldInfo snapshotInitialMaxHealthField;
        private FieldInfo snapshotStatusField;
        private FieldInfo snapshotColorField;
        private FieldInfo snapshotAppearanceField;

        private FieldInfo appearanceBasePortraitField;
        private FieldInfo appearanceTintColorField;
        private FieldInfo appearanceFrameSpriteField;
        private FieldInfo appearanceOverlaySpritesField;
        private FieldInfo appearanceOverlayLayersField;
        private FieldInfo appearanceCosmeticIdField;
        private FieldInfo layerSpriteField;
        private FieldInfo layerCenterField;
        private FieldInfo layerSizeField;
        private FieldInfo layerRotationField;
        private FieldInfo layerUseFactionTintField;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudStripView, Assembly-CSharp", true);
            layoutModeType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudLayoutMode, Assembly-CSharp", true);
            snapshotType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudSnapshot, Assembly-CSharp", true);
            appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
            layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
            imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

            Type snapshotListType = typeof(IReadOnlyList<>).MakeGenericType(snapshotType);

            initStandaloneMethod = viewType.GetMethod("InitializeStandalone", BindingFlags.Public | BindingFlags.Instance);
            computeLeaderMaskMethod = viewType.GetMethod(
                "ComputeLeaderMask",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { snapshotListType },
                null);
            refreshMethod = viewType.GetMethod(
                "Refresh",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { snapshotListType },
                null);
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });
            setLayoutModeMethod = viewType.GetMethod("SetLayoutMode", new[] { layoutModeType });
            setLayoutModeWithRectMethod = viewType.GetMethod("SetLayoutMode", new[] { layoutModeType, typeof(float), typeof(Vector2) });
            isSlotVisibleMethod = viewType.GetMethod("IsSlotVisible", new[] { typeof(int) });
            isCrownVisibleMethod = viewType.GetMethod("IsCrownVisible", new[] { typeof(int) });
            isMedalVisibleMethod = viewType.GetMethod("IsMedalVisible", new[] { typeof(int) });
            getMedalRankTextMethod = viewType.GetMethod("GetMedalRankText", new[] { typeof(int) });
            getCoinTextMethod = viewType.GetMethod("GetCoinText", new[] { typeof(int) });
            getMiniHeartStateMethod = viewType.GetMethod("GetMiniHeartState", new[] { typeof(int), typeof(int) });
            getMiniHeartCountMethod = viewType.GetMethod("GetMiniHeartCount", new[] { typeof(int) });
            getHealthSegmentColorMethod = viewType.GetMethod("GetHealthSegmentColor", new[] { typeof(int), typeof(int) });
            getSlotCosmeticIdMethod = viewType.GetMethod("GetSlotCosmeticId", new[] { typeof(int) });
            getSlotOverlayCountMethod = viewType.GetMethod("GetSlotOverlayCount", new[] { typeof(int) });

            lastLeaderMaskProp = viewType.GetProperty("LastLeaderMask");
            capacityProp = viewType.GetProperty("Capacity");
            layoutModeProp = viewType.GetProperty("LayoutMode");
            imageSpriteProp = imageType.GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
            imageEnabledProp = imageType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            imagePreserveAspectProp = imageType.GetProperty("preserveAspect", BindingFlags.Public | BindingFlags.Instance);

            snapshotSlotIdField = snapshotType.GetField("SlotId", BindingFlags.Public | BindingFlags.Instance);
            snapshotDisplayNameField = snapshotType.GetField("DisplayName", BindingFlags.Public | BindingFlags.Instance);
            snapshotIsParticipatingField = snapshotType.GetField("IsParticipating", BindingFlags.Public | BindingFlags.Instance);
            snapshotIsLocalField = snapshotType.GetField("IsLocal", BindingFlags.Public | BindingFlags.Instance);
            snapshotCoinsField = snapshotType.GetField("Coins", BindingFlags.Public | BindingFlags.Instance);
            snapshotFinishOrderField = snapshotType.GetField("FinishOrder", BindingFlags.Public | BindingFlags.Instance);
            snapshotHealthField = snapshotType.GetField("Health", BindingFlags.Public | BindingFlags.Instance);
            snapshotMaxHealthField = snapshotType.GetField("MaxHealth", BindingFlags.Public | BindingFlags.Instance);
            snapshotInitialMaxHealthField = snapshotType.GetField("InitialMaxHealth", BindingFlags.Public | BindingFlags.Instance);
            snapshotStatusField = snapshotType.GetField("Status", BindingFlags.Public | BindingFlags.Instance);
            snapshotColorField = snapshotType.GetField("Color", BindingFlags.Public | BindingFlags.Instance);
            snapshotAppearanceField = snapshotType.GetField("Appearance", BindingFlags.Public | BindingFlags.Instance);

            appearanceBasePortraitField = appearanceType.GetField("BasePortrait", BindingFlags.Public | BindingFlags.Instance);
            appearanceTintColorField = appearanceType.GetField("TintColor", BindingFlags.Public | BindingFlags.Instance);
            appearanceFrameSpriteField = appearanceType.GetField("FrameSprite", BindingFlags.Public | BindingFlags.Instance);
            appearanceOverlaySpritesField = appearanceType.GetField("OverlaySprites", BindingFlags.Public | BindingFlags.Instance);
            appearanceOverlayLayersField = appearanceType.GetField("OverlayLayers", BindingFlags.Public | BindingFlags.Instance);
            appearanceCosmeticIdField = appearanceType.GetField("CosmeticId", BindingFlags.Public | BindingFlags.Instance);

            layerSpriteField = layerType.GetField("Sprite", BindingFlags.Public | BindingFlags.Instance);
            layerCenterField = layerType.GetField("NormalizedCenter", BindingFlags.Public | BindingFlags.Instance);
            layerSizeField = layerType.GetField("NormalizedSize", BindingFlags.Public | BindingFlags.Instance);
            layerRotationField = layerType.GetField("RotationZ", BindingFlags.Public | BindingFlags.Instance);
            layerUseFactionTintField = layerType.GetField("UseFactionTint", BindingFlags.Public | BindingFlags.Instance);

            root = new GameObject("ParticipantHudStripViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initStandaloneMethod.Invoke(view, new object[] { 4, null });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }

            for (int i = 0; i < runtimeSprites.Count; i++)
            {
                if (runtimeSprites[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeSprites[i]);
                }
            }
            runtimeSprites.Clear();

            for (int i = 0; i < runtimeTextures.Count; i++)
            {
                if (runtimeTextures[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeTextures[i]);
                }
            }
            runtimeTextures.Clear();
        }

        private byte LastLeaderMask => (byte)lastLeaderMaskProp.GetValue(view);
        private int Capacity => (int)capacityProp.GetValue(view);

        private byte ComputeLeaderMask(Array snapshots)
            => (byte)computeLeaderMaskMethod.Invoke(null, new object[] { snapshots });

        private void Refresh(Array snapshots)
            => refreshMethod.Invoke(view, new object[] { snapshots });

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        private bool IsSlotVisible(int index)
            => (bool)isSlotVisibleMethod.Invoke(view, new object[] { index });

        private bool IsCrownVisible(int index)
            => (bool)isCrownVisibleMethod.Invoke(view, new object[] { index });

        private bool IsMedalVisible(int index)
            => (bool)isMedalVisibleMethod.Invoke(view, new object[] { index });

        private string GetMedalRankText(int index)
            => (string)getMedalRankTextMethod.Invoke(view, new object[] { index });

        private string GetCoinText(int index)
            => (string)getCoinTextMethod.Invoke(view, new object[] { index });

        private string GetMiniHeartState(int slotIndex, int heartIndex)
            => getMiniHeartStateMethod.Invoke(view, new object[] { slotIndex, heartIndex }).ToString();

        private int GetMiniHeartCount(int slotIndex)
            => (int)getMiniHeartCountMethod.Invoke(view, new object[] { slotIndex });

        private Color GetHealthSegmentColor(int slotIndex, int segmentIndex)
            => (Color)getHealthSegmentColorMethod.Invoke(view, new object[] { slotIndex, segmentIndex });

        private string GetSlotCosmeticId(int index)
            => (string)getSlotCosmeticIdMethod.Invoke(view, new object[] { index });

        private int GetSlotOverlayCount(int index)
            => (int)getSlotOverlayCountMethod.Invoke(view, new object[] { index });

        private Sprite CreateRuntimeSprite(string name, Color fillColor)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = name + "_Tex",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = fillColor;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            runtimeTextures.Add(tex);

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = name + "_Sprite";
            sprite.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private object CreateAppearance(
            Color tintColor,
            Sprite basePortrait = null,
            Sprite frameSprite = null,
            Sprite[] overlaySprites = null,
            string cosmeticId = null)
        {
            object boxed = Activator.CreateInstance(appearanceType);
            appearanceBasePortraitField.SetValue(boxed, basePortrait);
            appearanceTintColorField.SetValue(boxed, tintColor);
            appearanceFrameSpriteField.SetValue(boxed, frameSprite);
            appearanceOverlaySpritesField.SetValue(boxed, overlaySprites);
            appearanceCosmeticIdField.SetValue(boxed, cosmeticId);
            return boxed;
        }

        private object CreateSnapshot(
            int slotId,
            int coins = 0,
            ParticipantStatus status = ParticipantStatus.Active,
            int finishOrder = 0,
            bool isParticipating = true,
            bool isLocal = false,
            int health = 3,
            int maxHealth = 3,
            int initialMaxHealth = 3,
            Color? color = null,
            object appearance = null)
        {
            Color resolvedColor = color ?? Color.cyan;
            object boxed = Activator.CreateInstance(snapshotType);
            snapshotSlotIdField.SetValue(boxed, slotId);
            snapshotDisplayNameField.SetValue(boxed, "P" + (slotId + 1));
            snapshotIsParticipatingField.SetValue(boxed, isParticipating);
            snapshotIsLocalField.SetValue(boxed, isLocal);
            snapshotCoinsField.SetValue(boxed, coins);
            snapshotFinishOrderField.SetValue(boxed, finishOrder);
            snapshotHealthField.SetValue(boxed, health);
            snapshotMaxHealthField.SetValue(boxed, maxHealth);
            snapshotInitialMaxHealthField.SetValue(boxed, initialMaxHealth);
            snapshotStatusField.SetValue(boxed, status);
            snapshotColorField.SetValue(boxed, resolvedColor);
            snapshotAppearanceField.SetValue(boxed, appearance ?? CreateAppearance(resolvedColor));
            return boxed;
        }

        private Array CreateSnapshotArray(params object[] items)
        {
            Array array = Array.CreateInstance(snapshotType, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                array.SetValue(items[i], i);
            }
            return array;
        }

        private Component FindImageComponent(string hierarchyPath)
        {
            Transform target = root.transform.Find(hierarchyPath);
            Assert.That(target, Is.Not.Null, "Expected UI object at path: " + hierarchyPath);
            Component image = target.GetComponent(imageType);
            Assert.That(image, Is.Not.Null, "Expected Image component at path: " + hierarchyPath);
            return image;
        }

        private Sprite GetImageSprite(string hierarchyPath)
        {
            Component image = FindImageComponent(hierarchyPath);
            return (Sprite)imageSpriteProp.GetValue(image);
        }

        private bool IsImageEnabled(string hierarchyPath)
        {
            Component image = FindImageComponent(hierarchyPath);
            return (bool)imageEnabledProp.GetValue(image);
        }

        [Test]
        public void ComputeLeaderMask_ReturnsZeroWhenAllHaveZeroCoins()
        {
            Assert.That(Capacity, Is.EqualTo(4));
            Assert.That(ComputeLeaderMask(null), Is.EqualTo((byte)0));
            Assert.That(ComputeLeaderMask(CreateSnapshotArray()), Is.EqualTo((byte)0));

            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 0, isLocal: true),
                CreateSnapshot(1, coins: 0),
                CreateSnapshot(2, coins: 0),
                CreateSnapshot(3, coins: 0));

            Assert.That(ComputeLeaderMask(snapshots), Is.EqualTo((byte)0));

            Refresh(snapshots);
            Assert.That(LastLeaderMask, Is.EqualTo((byte)0));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(IsSlotVisible(i), Is.True);
                Assert.That(IsCrownVisible(i), Is.False);
                Assert.That(IsMedalVisible(i), Is.False);
                Assert.That(GetCoinText(i), Is.EqualTo("00"));
            }
        }

        [Test]
        public void ComputeLeaderMask_HighlightsSingleAndTiedActiveLeaders()
        {
            Array singleLeader = CreateSnapshotArray(
                CreateSnapshot(0, coins: 1),
                CreateSnapshot(1, coins: 4, isLocal: true),
                CreateSnapshot(2, coins: 2),
                CreateSnapshot(3, coins: 0));

            byte singleMask = ComputeLeaderMask(singleLeader);
            Assert.That(singleMask, Is.EqualTo((byte)(1 << 1)));

            Refresh(singleLeader);
            Assert.That(LastLeaderMask, Is.EqualTo((byte)(1 << 1)));
            Assert.That(IsCrownVisible(0), Is.False);
            Assert.That(IsCrownVisible(1), Is.True);
            Assert.That(IsCrownVisible(2), Is.False);
            Assert.That(IsCrownVisible(3), Is.False);
            Assert.That(GetCoinText(1), Is.EqualTo("04"));

            Array tiedLeaders = CreateSnapshotArray(
                CreateSnapshot(0, coins: 5),
                CreateSnapshot(1, coins: 2, isLocal: true),
                CreateSnapshot(2, coins: 5),
                CreateSnapshot(3, coins: 5, status: ParticipantStatus.Respawning));

            byte expectedTiedMask = (byte)((1 << 0) | (1 << 2) | (1 << 3));
            Assert.That(ComputeLeaderMask(tiedLeaders), Is.EqualTo(expectedTiedMask));

            Refresh(tiedLeaders);
            Assert.That(LastLeaderMask, Is.EqualTo(expectedTiedMask));
            Assert.That(IsCrownVisible(0), Is.True);
            Assert.That(IsCrownVisible(1), Is.False);
            Assert.That(IsCrownVisible(2), Is.True);
            Assert.That(IsCrownVisible(3), Is.True);
        }

        [Test]
        public void ComputeLeaderMask_ExcludesWonParticipantsSoCrownMovesToNextLeader()
        {
            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 7, status: ParticipantStatus.Won, finishOrder: 1),
                CreateSnapshot(1, coins: 5, status: ParticipantStatus.Active, isLocal: true),
                CreateSnapshot(2, coins: 3, status: ParticipantStatus.Active),
                CreateSnapshot(3, coins: 5, status: ParticipantStatus.Active));

            byte expectedMask = (byte)((1 << 1) | (1 << 3));
            Assert.That(ComputeLeaderMask(snapshots), Is.EqualTo(expectedMask));

            Refresh(snapshots);
            Assert.That(LastLeaderMask, Is.EqualTo(expectedMask));

            Assert.That(IsMedalVisible(0), Is.True);
            Assert.That(GetMedalRankText(0), Is.EqualTo("1"));
            Assert.That(IsCrownVisible(0), Is.False);
            Assert.That(GetCoinText(0), Is.EqualTo(string.Empty));

            Assert.That(IsCrownVisible(1), Is.True);
            Assert.That(IsMedalVisible(1), Is.False);
            Assert.That(GetCoinText(1), Is.EqualTo("05"));

            Assert.That(IsCrownVisible(2), Is.False);
            Assert.That(IsMedalVisible(2), Is.False);
            Assert.That(GetCoinText(2), Is.EqualTo("03"));

            Assert.That(IsCrownVisible(3), Is.True);
            Assert.That(IsMedalVisible(3), Is.False);
            Assert.That(GetCoinText(3), Is.EqualTo("05"));

            Array onlyWonHasCoins = CreateSnapshotArray(
                CreateSnapshot(0, coins: 7, status: ParticipantStatus.Won, finishOrder: 1),
                CreateSnapshot(1, coins: 0, status: ParticipantStatus.Active),
                CreateSnapshot(2, coins: 0, status: ParticipantStatus.Active),
                CreateSnapshot(3, coins: 0, status: ParticipantStatus.Active));

            Assert.That(ComputeLeaderMask(onlyWonHasCoins), Is.EqualTo((byte)0));
            Refresh(onlyWonHasCoins);
            Assert.That(IsCrownVisible(0), Is.False);
            Assert.That(IsCrownVisible(1), Is.False);
            Assert.That(IsCrownVisible(2), Is.False);
            Assert.That(IsCrownVisible(3), Is.False);
        }

        [Test]
        public void Refresh_WonParticipantsSwitchToGoldSilverBronzeMedalsWithRanks123()
        {
            Array initialActive = CreateSnapshotArray(
                CreateSnapshot(0, coins: 6, status: ParticipantStatus.Active),
                CreateSnapshot(1, coins: 5, status: ParticipantStatus.Active),
                CreateSnapshot(2, coins: 4, status: ParticipantStatus.Active),
                CreateSnapshot(3, coins: 3, status: ParticipantStatus.Active));

            Refresh(initialActive);
            Assert.That(IsCrownVisible(0), Is.True);
            Assert.That(IsMedalVisible(0), Is.False);
            Assert.That(GetCoinText(0), Is.EqualTo("06"));

            Array podiumSnapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 7, status: ParticipantStatus.Won, finishOrder: 1),
                CreateSnapshot(1, coins: 7, status: ParticipantStatus.Won, finishOrder: 2),
                CreateSnapshot(2, coins: 7, status: ParticipantStatus.Won, finishOrder: 3),
                CreateSnapshot(3, coins: 4, status: ParticipantStatus.Active));

            Refresh(podiumSnapshots);

            for (int i = 0; i < 3; i++)
            {
                Assert.That(IsSlotVisible(i), Is.True);
                Assert.That(IsMedalVisible(i), Is.True);
                Assert.That(IsCrownVisible(i), Is.False);
                Assert.That(GetCoinText(i), Is.EqualTo(string.Empty));
                Assert.That(root.transform.Find("ParticipantSlot_" + i + "/TopMetricLayer/MedalGroup").gameObject.activeSelf, Is.True);
                Assert.That(root.transform.Find("ParticipantSlot_" + i + "/TopMetricLayer/CoinGroup").gameObject.activeSelf, Is.False);
            }

            Assert.That(GetMedalRankText(0), Is.EqualTo("1"));
            Assert.That(GetMedalRankText(1), Is.EqualTo("2"));
            Assert.That(GetMedalRankText(2), Is.EqualTo("3"));

            Sprite goldSprite = GetImageSprite("ParticipantSlot_0/TopMetricLayer/MedalGroup/MedalIcon");
            Sprite silverSprite = GetImageSprite("ParticipantSlot_1/TopMetricLayer/MedalGroup/MedalIcon");
            Sprite bronzeSprite = GetImageSprite("ParticipantSlot_2/TopMetricLayer/MedalGroup/MedalIcon");
            Assert.That(goldSprite, Is.Not.Null);
            Assert.That(silverSprite, Is.Not.Null);
            Assert.That(bronzeSprite, Is.Not.Null);
            Assert.That(goldSprite, Is.Not.EqualTo(silverSprite));
            Assert.That(silverSprite, Is.Not.EqualTo(bronzeSprite));
            Assert.That(goldSprite, Is.Not.EqualTo(bronzeSprite));

            Assert.That(IsMedalVisible(3), Is.False);
            Assert.That(GetMedalRankText(3), Is.EqualTo(string.Empty));
            Assert.That(IsCrownVisible(3), Is.True);
            Assert.That(GetCoinText(3), Is.EqualTo("04"));
        }

        [Test]
        public void Refresh_UpdatesMiniHeartsThreeStatesAndHidesNonParticipatingSlots()
        {
            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 2, status: ParticipantStatus.Active, isParticipating: true, health: 1, maxHealth: 2, initialMaxHealth: 3),
                CreateSnapshot(1, coins: 1, status: ParticipantStatus.Active, isParticipating: true, health: 3, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(2, coins: 9, status: ParticipantStatus.Active, isParticipating: false, health: 3, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(3, coins: 0, status: ParticipantStatus.Lost, isParticipating: true, health: 0, maxHealth: 1, initialMaxHealth: 3));

            Refresh(snapshots);

            Assert.That(IsSlotVisible(0), Is.True);
            Assert.That(GetMiniHeartCount(0), Is.EqualTo(3));
            Assert.That(GetMiniHeartState(0, 0), Is.EqualTo("Full"));
            Assert.That(GetMiniHeartState(0, 1), Is.EqualTo("Dim"));
            Assert.That(GetMiniHeartState(0, 2), Is.EqualTo("Broken"));

            Assert.That(IsSlotVisible(1), Is.True);
            Assert.That(GetMiniHeartCount(1), Is.EqualTo(3));
            Assert.That(GetMiniHeartState(1, 0), Is.EqualTo("Full"));
            Assert.That(GetMiniHeartState(1, 1), Is.EqualTo("Full"));
            Assert.That(GetMiniHeartState(1, 2), Is.EqualTo("Full"));

            Assert.That(IsSlotVisible(2), Is.False);
            Assert.That(IsCrownVisible(2), Is.False);
            Assert.That(IsMedalVisible(2), Is.False);
            Assert.That(GetMiniHeartCount(2), Is.EqualTo(0));
            Assert.That(GetCoinText(2), Is.EqualTo(string.Empty));
            Assert.That(root.transform.Find("ParticipantSlot_2").gameObject.activeSelf, Is.False);

            Assert.That(IsSlotVisible(3), Is.True);
            Assert.That(GetMiniHeartCount(3), Is.EqualTo(3));
            Assert.That(GetMiniHeartState(3, 0), Is.EqualTo("Dim"));
            Assert.That(GetMiniHeartState(3, 1), Is.EqualTo("Broken"));
            Assert.That(GetMiniHeartState(3, 2), Is.EqualTo("Broken"));

            Array reducedParticipants = CreateSnapshotArray(
                CreateSnapshot(0, coins: 2, status: ParticipantStatus.Active, isParticipating: true, health: 2, maxHealth: 2, initialMaxHealth: 3),
                CreateSnapshot(1, coins: 1, status: ParticipantStatus.Active, isParticipating: false, health: 3, maxHealth: 3, initialMaxHealth: 3));

            Refresh(reducedParticipants);
            Assert.That(IsSlotVisible(0), Is.True);
            Assert.That(GetMiniHeartState(0, 0), Is.EqualTo("Full"));
            Assert.That(GetMiniHeartState(0, 1), Is.EqualTo("Full"));
            Assert.That(GetMiniHeartState(0, 2), Is.EqualTo("Broken"));
            Assert.That(IsSlotVisible(1), Is.False);
            Assert.That(IsSlotVisible(2), Is.False);
            Assert.That(IsSlotVisible(3), Is.False);
        }

        [Test]
        public void Refresh_AppliesCosmeticIdAndOverlaySpritesForFutureCosmetics()
        {
            Sprite customPortrait = CreateRuntimeSprite("CustomPortrait", Color.white);
            Sprite customFrame = CreateRuntimeSprite("CustomFrame", Color.yellow);
            Sprite overlayHat = CreateRuntimeSprite("OverlayHat", Color.red);
            Sprite overlayGlasses = CreateRuntimeSprite("OverlayGlasses", Color.blue);

            object richAppearance = CreateAppearance(
                Color.green,
                basePortrait: customPortrait,
                frameSprite: customFrame,
                overlaySprites: new[] { overlayHat, null, overlayGlasses },
                cosmeticId: "cosmetic_royal_set");

            Array firstRefresh = CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, appearance: richAppearance, color: Color.green),
                CreateSnapshot(1, coins: 1, color: Color.red));

            Refresh(firstRefresh);

            Assert.That(GetSlotCosmeticId(0), Is.EqualTo("cosmetic_royal_set"));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(2));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/BasePortrait"), Is.EqualTo(customPortrait));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/FrameImage"), Is.EqualTo(customFrame));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.EqualTo(overlayHat));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.EqualTo(overlayGlasses));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.True);
            Assert.That(GetSlotCosmeticId(1), Is.Null);
            Assert.That(GetSlotOverlayCount(1), Is.EqualTo(0));

            object singleOverlayAppearance = CreateAppearance(
                Color.green,
                basePortrait: customPortrait,
                frameSprite: customFrame,
                overlaySprites: new[] { overlayGlasses },
                cosmeticId: "cosmetic_visor_only");

            Refresh(CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, appearance: singleOverlayAppearance, color: Color.green),
                CreateSnapshot(1, coins: 1, color: Color.red)));

            Assert.That(GetSlotCosmeticId(0), Is.EqualTo("cosmetic_visor_only"));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(1));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.EqualTo(overlayGlasses));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.False);

            object clearedAppearance = CreateAppearance(Color.green, cosmeticId: null);
            Refresh(CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, appearance: clearedAppearance, color: Color.green),
                CreateSnapshot(1, coins: 1, color: Color.red)));

            Assert.That(GetSlotCosmeticId(0), Is.Null);
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(0));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.False);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.False);
        }

        [Test]
        public void Tick_AnimatesLeaderCrownAndIgnoresNonPositiveDeltaTime()
        {
            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 4),
                CreateSnapshot(1, coins: 1));

            Refresh(snapshots);
            Assert.That(IsCrownVisible(0), Is.True);
            Assert.That(IsCrownVisible(1), Is.False);

            var leaderCrownRect = (RectTransform)root.transform.Find("ParticipantSlot_0/CrownIcon");
            Vector2 initialPos = leaderCrownRect.anchoredPosition;
            Vector3 initialScale = leaderCrownRect.localScale;

            Tick(0f);
            Tick(-0.5f);
            Assert.That(leaderCrownRect.anchoredPosition, Is.EqualTo(initialPos));
            Assert.That(leaderCrownRect.localScale, Is.EqualTo(initialScale));

            Tick(0.25f);
            Assert.That(leaderCrownRect.anchoredPosition.y, Is.GreaterThan(initialPos.y + 0.5f));
            Assert.That(leaderCrownRect.localScale.x, Is.GreaterThan(1f));
        }

        [Test]
        public void Refresh_RemovesCardBackgroundsAndEmbedsThreeStateSegmentedHealthBar()
        {
            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, status: ParticipantStatus.Active, isParticipating: true, isLocal: true, health: 1, maxHealth: 2, initialMaxHealth: 3),
                CreateSnapshot(1, coins: 1, status: ParticipantStatus.Active, isParticipating: true, health: 3, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(2, coins: 0, status: ParticipantStatus.Lost, isParticipating: true, health: 0, maxHealth: 1, initialMaxHealth: 3),
                CreateSnapshot(3, coins: 0, status: ParticipantStatus.Active, isParticipating: false, health: 3, maxHealth: 3, initialMaxHealth: 3));

            Refresh(snapshots);

            for (int i = 0; i < 4; i++)
            {
                Transform slotRoot = root.transform.Find("ParticipantSlot_" + i);
                Assert.That(slotRoot, Is.Not.Null);
                Assert.That(slotRoot.Find("CardBackground"), Is.Null);
                Assert.That(slotRoot.Find("LocalHighlight"), Is.Null);
            }

            var avatarRoot = (RectTransform)root.transform.Find("ParticipantSlot_0/AvatarRoot");
            Assert.That(avatarRoot, Is.Not.Null);
            Assert.That(avatarRoot.sizeDelta.x, Is.EqualTo(71.5f).Within(0.5f));
            Assert.That(avatarRoot.sizeDelta.y, Is.EqualTo(71.5f).Within(0.5f));

            Transform frameTransform = avatarRoot.Find("FrameImage");
            Assert.That(frameTransform, Is.Not.Null);

            var healthBarRect = (RectTransform)avatarRoot.Find("HealthBarRoot");
            Assert.That(healthBarRect, Is.Not.Null);
            Assert.That(healthBarRect.parent, Is.EqualTo(avatarRoot));
            Assert.That(healthBarRect.GetSiblingIndex(), Is.GreaterThan(frameTransform.GetSiblingIndex()));
            Assert.That(healthBarRect.sizeDelta.x, Is.EqualTo(71.5f * 0.81f).Within(0.5f));
            Assert.That(healthBarRect.sizeDelta.x, Is.LessThan(avatarRoot.sizeDelta.x));
            Assert.That(healthBarRect.sizeDelta.y, Is.LessThan(healthBarRect.sizeDelta.x * 0.25f));
            Assert.That(healthBarRect.sizeDelta.y, Is.LessThan(avatarRoot.sizeDelta.y * 0.25f));
            Assert.That(healthBarRect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(healthBarRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(healthBarRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(healthBarRect.anchoredPosition.y, Is.InRange(0f, 1.5f));
            Assert.That(healthBarRect.anchoredPosition.y, Is.EqualTo(avatarRoot.sizeDelta.y * 0.004f).Within(0.05f));

            var crownRect = (RectTransform)root.transform.Find("ParticipantSlot_0/CrownIcon");
            Assert.That(crownRect, Is.Not.Null);
            Assert.That(crownRect.sizeDelta.x, Is.EqualTo(36f).Within(0.5f));
            Assert.That(crownRect.sizeDelta.y, Is.EqualTo(28f).Within(0.5f));

            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/HealthBarRoot/HealthSegment_0"), Is.True);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/HealthBarRoot/HealthSegment_1"), Is.True);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/HealthBarRoot/HealthSegment_2"), Is.True);

            Color fullColor = GetHealthSegmentColor(0, 0);
            Color dimColor = GetHealthSegmentColor(0, 1);
            Color brokenColor = GetHealthSegmentColor(0, 2);

            Assert.That(fullColor.g, Is.GreaterThan(fullColor.r + 0.4f));
            Assert.That(fullColor.g, Is.GreaterThan(fullColor.b + 0.3f));

            float fullChroma = Mathf.Max(fullColor.r, Mathf.Max(fullColor.g, fullColor.b))
                - Mathf.Min(fullColor.r, Mathf.Min(fullColor.g, fullColor.b));
            float dimChroma = Mathf.Max(dimColor.r, Mathf.Max(dimColor.g, dimColor.b))
                - Mathf.Min(dimColor.r, Mathf.Min(dimColor.g, dimColor.b));
            Assert.That(dimColor.g, Is.GreaterThan(dimColor.r));
            Assert.That(dimColor.g, Is.GreaterThan(dimColor.b));
            Assert.That(dimChroma, Is.LessThan(0.2f));
            Assert.That(dimChroma, Is.LessThan(fullChroma * 0.5f));

            Assert.That(brokenColor.r, Is.GreaterThan(brokenColor.g + 0.4f));
            Assert.That(brokenColor.r, Is.GreaterThan(brokenColor.b + 0.4f));

            TargetInvocationException outOfRangeEx = Assert.Throws<TargetInvocationException>(
                () => GetHealthSegmentColor(0, 3));
            Assert.That(outOfRangeEx.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SetLayoutMode_VerticalRightAligned_EnforcesLeftToRightOrderAndSquareAvatar()
        {
            object verticalMode = Enum.Parse(layoutModeType, "VerticalRightAligned");

            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 5, status: ParticipantStatus.Active, isLocal: true, health: 2, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(1, coins: 2, status: ParticipantStatus.Active, health: 3, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(2, coins: 7, status: ParticipantStatus.Won, finishOrder: 1, health: 3, maxHealth: 3, initialMaxHealth: 3),
                CreateSnapshot(3, coins: 0, status: ParticipantStatus.Active, health: 1, maxHealth: 2, initialMaxHealth: 3));

            Refresh(snapshots);
            setLayoutModeWithRectMethod.Invoke(view, new object[] { verticalMode, 368f, new Vector2(-12f, 230f) });

            var rootRect = (RectTransform)root.transform;
            Assert.That(layoutModeProp.GetValue(view), Is.EqualTo(verticalMode));
            Assert.That(rootRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 230f)));
            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(180f, 306f)));

            var slot0 = (RectTransform)root.transform.Find("ParticipantSlot_0");
            var crownRect = (RectTransform)slot0.Find("CrownIcon");
            var metricRect = (RectTransform)slot0.Find("TopMetricLayer");
            var coinTextRect = (RectTransform)slot0.Find("TopMetricLayer/CoinGroup/CoinText");
            var avatarRect = (RectTransform)slot0.Find("AvatarRoot");
            var healthRect = (RectTransform)slot0.Find("AvatarRoot/HealthBarRoot");

            Assert.That(slot0.sizeDelta, Is.EqualTo(new Vector2(180f, 70f)));
            Assert.That(avatarRect.sizeDelta.x, Is.EqualTo(60f).Within(0.01f));
            Assert.That(avatarRect.sizeDelta.y, Is.EqualTo(avatarRect.sizeDelta.x).Within(0.001f));

            float avatarRight = avatarRect.anchoredPosition.x + avatarRect.sizeDelta.x * 0.5f;
            float avatarLeft = avatarRect.anchoredPosition.x - avatarRect.sizeDelta.x * 0.5f;
            float metricRight = metricRect.anchoredPosition.x + metricRect.sizeDelta.x * 0.5f;
            float metricLeft = metricRect.anchoredPosition.x - metricRect.sizeDelta.x * 0.5f;
            float coinTextRightInSlot = metricRect.anchoredPosition.x + coinTextRect.anchoredPosition.x + coinTextRect.sizeDelta.x * 0.5f;
            float crownRight = crownRect.anchoredPosition.x + crownRect.sizeDelta.x * 0.5f;
            float crownLeft = crownRect.anchoredPosition.x - crownRect.sizeDelta.x * 0.5f;

            Assert.That(avatarRight, Is.EqualTo(0f).Within(0.01f));
            Assert.That(avatarLeft, Is.EqualTo(-60f).Within(0.01f));
            Assert.That(metricRight, Is.EqualTo(-68f).Within(0.01f));
            Assert.That(metricLeft, Is.EqualTo(-136f).Within(0.01f));
            Assert.That(coinTextRightInSlot, Is.LessThanOrEqualTo(metricRight + 0.01f));
            Assert.That(coinTextRightInSlot, Is.LessThan(avatarLeft));
            Assert.That(crownRight, Is.EqualTo(-142f).Within(0.01f));
            Assert.That(crownLeft, Is.GreaterThanOrEqualTo(-180f));
            Assert.That(crownRight, Is.LessThan(metricLeft));
            Assert.That(metricRight, Is.LessThan(avatarLeft));

            Assert.That(healthRect.parent, Is.EqualTo(avatarRect));
            Assert.That(healthRect.sizeDelta.x, Is.EqualTo(49f).Within(0.1f));
            Assert.That(healthRect.sizeDelta.y, Is.EqualTo(12f).Within(0.1f));
            Assert.That(healthRect.anchoredPosition.y, Is.EqualTo(60f * 0.004f).Within(0.05f));

            Assert.That(IsMedalVisible(2), Is.True);
            Assert.That(IsCrownVisible(2), Is.False);
            Assert.That(root.transform.Find("ParticipantSlot_2/TopMetricLayer/MedalGroup").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("ParticipantSlot_2/TopMetricLayer/CoinGroup").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Tick_InVerticalMode_PreservesCrownHorizontalAnchorWhileBobbingVertically()
        {
            object verticalMode = Enum.Parse(layoutModeType, "VerticalRightAligned");

            Array snapshots = CreateSnapshotArray(
                CreateSnapshot(0, coins: 4),
                CreateSnapshot(1, coins: 1));

            Refresh(snapshots);
            setLayoutModeMethod.Invoke(view, new[] { verticalMode });

            var crown0 = (RectTransform)root.transform.Find("ParticipantSlot_0/CrownIcon");
            float expectedX = crown0.anchoredPosition.x;
            Assert.That(expectedX, Is.EqualTo(-158f).Within(0.1f));

            Tick(0.25f);
            Assert.That(crown0.anchoredPosition.x, Is.EqualTo(expectedX).Within(0.01f));
            Assert.That(Mathf.Abs(crown0.anchoredPosition.y), Is.GreaterThan(0.2f));

            Tick(0.25f);
            Assert.That(crown0.anchoredPosition.x, Is.EqualTo(expectedX).Within(0.01f));

            // Switching leader while in vertical mode must immediately anchor the new leader's crown at expectedX.
            Refresh(CreateSnapshotArray(
                CreateSnapshot(0, coins: 4),
                CreateSnapshot(1, coins: 6)));

            var crown1 = (RectTransform)root.transform.Find("ParticipantSlot_1/CrownIcon");
            Assert.That(IsCrownVisible(1), Is.True);
            Assert.That(crown1.anchoredPosition.x, Is.EqualTo(expectedX).Within(0.01f));
        }

        [Test]
        public void SetLayoutMode_VerticalRightAligned_PacksVisibleSlotsCompactlyAndRestoresHorizontal()
        {
            object horizontalMode = Enum.Parse(layoutModeType, "Horizontal");
            object verticalMode = Enum.Parse(layoutModeType, "VerticalRightAligned");

            Array twoVisible = CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, isParticipating: true),
                CreateSnapshot(1, coins: 0, isParticipating: false),
                CreateSnapshot(2, coins: 1, isParticipating: true),
                CreateSnapshot(3, coins: 0, isParticipating: false));

            Refresh(twoVisible);
            setLayoutModeWithRectMethod.Invoke(view, new object[] { verticalMode, 368f, new Vector2(-12f, 230f) });

            var rootRect = (RectTransform)root.transform;
            var slot0 = (RectTransform)root.transform.Find("ParticipantSlot_0");
            var slot2 = (RectTransform)root.transform.Find("ParticipantSlot_2");

            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(180f, 154f)));
            Assert.That(slot0.anchoredPosition, Is.EqualTo(new Vector2(0f, -4f)));
            Assert.That(slot2.anchoredPosition, Is.EqualTo(new Vector2(0f, -80f)));

            // Adding a third visible slot dynamically expands and repacks the vertical column.
            Array threeVisible = CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, isParticipating: true),
                CreateSnapshot(1, coins: 2, isParticipating: true),
                CreateSnapshot(2, coins: 1, isParticipating: true),
                CreateSnapshot(3, coins: 0, isParticipating: false));

            Refresh(threeVisible);
            var slot1 = (RectTransform)root.transform.Find("ParticipantSlot_1");
            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(180f, 230f)));
            Assert.That(slot0.anchoredPosition, Is.EqualTo(new Vector2(0f, -4f)));
            Assert.That(slot1.anchoredPosition, Is.EqualTo(new Vector2(0f, -80f)));
            Assert.That(slot2.anchoredPosition, Is.EqualTo(new Vector2(0f, -156f)));

            // Switching back to Horizontal restores exact original layout geometry.
            setLayoutModeWithRectMethod.Invoke(view, new object[] { horizontalMode, 368f, new Vector2(-12f, 388f) });
            Assert.That(layoutModeProp.GetValue(view), Is.EqualTo(horizontalMode));
            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(368f, 154f)));
            Assert.That(rootRect.anchoredPosition, Is.EqualTo(new Vector2(-12f, 388f)));

            var avatar0 = (RectTransform)slot0.Find("AvatarRoot");
            var crown0 = (RectTransform)slot0.Find("CrownIcon");
            var metric0 = (RectTransform)slot0.Find("TopMetricLayer");
            Assert.That(avatar0.sizeDelta.x, Is.EqualTo(71.5f).Within(0.5f));
            Assert.That(avatar0.sizeDelta.y, Is.EqualTo(71.5f).Within(0.5f));
            Assert.That(avatar0.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(crown0.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(crown0.sizeDelta.x, Is.EqualTo(36f).Within(0.5f));
            Assert.That(metric0.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(metric0.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
        }

        [Test]
        public void SetLayoutMode_PreservesCosmeticOverlaysAndAvatarMaskHierarchyAcrossToggles()
        {
            object horizontalMode = Enum.Parse(layoutModeType, "Horizontal");
            object verticalMode = Enum.Parse(layoutModeType, "VerticalRightAligned");

            Sprite customPortrait = CreateRuntimeSprite("TogglePortrait", Color.white);
            Sprite customFrame = CreateRuntimeSprite("ToggleFrame", Color.yellow);
            Sprite overlayHat = CreateRuntimeSprite("ToggleHat", Color.red);
            Sprite overlayGlasses = CreateRuntimeSprite("ToggleGlasses", Color.blue);

            object richAppearance = CreateAppearance(
                Color.green,
                basePortrait: customPortrait,
                frameSprite: customFrame,
                overlaySprites: new[] { overlayHat, overlayGlasses },
                cosmeticId: "cosmetic_toggle_contract");

            Refresh(CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, appearance: richAppearance, color: Color.green),
                CreateSnapshot(1, coins: 1, color: Color.red)));

            setLayoutModeMethod.Invoke(view, new[] { verticalMode });
            setLayoutModeMethod.Invoke(view, new[] { horizontalMode });
            setLayoutModeMethod.Invoke(view, new[] { verticalMode });

            var avatarRoot = (RectTransform)root.transform.Find("ParticipantSlot_0/AvatarRoot");
            var avatarMask = (RectTransform)root.transform.Find("ParticipantSlot_0/AvatarRoot/AvatarMask");
            Assert.That(avatarRoot.sizeDelta.x, Is.EqualTo(avatarRoot.sizeDelta.y).Within(0.001f));
            Assert.That(avatarMask.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(avatarMask.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(GetSlotCosmeticId(0), Is.EqualTo("cosmetic_toggle_contract"));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(2));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/BasePortrait"), Is.EqualTo(customPortrait));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/FrameImage"), Is.EqualTo(customFrame));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.EqualTo(overlayHat));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.EqualTo(overlayGlasses));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.True);
        }

        private object CreateOverlayLayer(Sprite sprite, Vector2 center, Vector2 size, float rotationZ = 0f, bool useFactionTint = false)
        {
            object boxed = Activator.CreateInstance(layerType);
            layerSpriteField.SetValue(boxed, sprite);
            layerCenterField.SetValue(boxed, center);
            layerSizeField.SetValue(boxed, size);
            layerRotationField.SetValue(boxed, rotationZ);
            layerUseFactionTintField.SetValue(boxed, useFactionTint);
            return boxed;
        }

        private object CreateLayeredAppearance(Color tintColor, string cosmeticId, params object[] layers)
        {
            Array layerArray = Array.CreateInstance(layerType, layers != null ? layers.Length : 0);
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++) layerArray.SetValue(layers[i], i);
            }

            object boxed = CreateAppearance(tintColor, cosmeticId: cosmeticId);
            appearanceOverlayLayersField.SetValue(boxed, layerArray);
            return boxed;
        }

        [Test]
        public void Refresh_AppliesNormalizedOverlayLayersAndAlignsOverlayContainerWithBasePortrait()
        {
            Sprite scarf = CreateRuntimeSprite("StripScarf", Color.red);
            Sprite cap = CreateRuntimeSprite("StripCap", Color.blue);
            Sprite visor = CreateRuntimeSprite("StripVisor", Color.yellow);
            Sprite headset = CreateRuntimeSprite("StripHeadset", Color.cyan);

            object app0 = CreateLayeredAppearance(
                Color.red,
                "slot_0",
                CreateOverlayLayer(scarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f)),
                CreateOverlayLayer(cap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f)));
            object app1 = CreateLayeredAppearance(
                Color.blue,
                "slot_1",
                CreateOverlayLayer(visor, new Vector2(0.537f, 0.704f), new Vector2(1.071f, 0.510f)));
            object app2 = CreateLayeredAppearance(
                Color.green,
                "slot_2",
                CreateOverlayLayer(headset, new Vector2(0.475f, 0.679f), new Vector2(1.112f, 0.742f)));
            object app3None = CreateLayeredAppearance(Color.yellow, "none");

            Refresh(CreateSnapshotArray(
                CreateSnapshot(0, coins: 3, appearance: app0, color: Color.red),
                CreateSnapshot(1, coins: 2, appearance: app1, color: Color.blue),
                CreateSnapshot(2, coins: 1, appearance: app2, color: Color.green),
                CreateSnapshot(3, coins: 0, appearance: app3None, color: Color.yellow)));

            for (int i = 0; i < 4; i++)
            {
                var container = (RectTransform)root.transform.Find("ParticipantSlot_" + i + "/AvatarRoot/AvatarMask/OverlayContainer");
                Assert.That(container.anchorMin, Is.EqualTo(new Vector2(0.04f, 0f)));
                Assert.That(container.anchorMax, Is.EqualTo(new Vector2(0.96f, 0.96f)));
            }

            Assert.That(GetSlotCosmeticId(0), Is.EqualTo("slot_0"));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(2));
            Assert.That(GetSlotCosmeticId(1), Is.EqualTo("slot_1"));
            Assert.That(GetSlotOverlayCount(1), Is.EqualTo(1));
            Assert.That(GetSlotCosmeticId(2), Is.EqualTo("slot_2"));
            Assert.That(GetSlotOverlayCount(2), Is.EqualTo(1));
            Assert.That(GetSlotCosmeticId(3), Is.EqualTo("none"));
            Assert.That(GetSlotOverlayCount(3), Is.EqualTo(0));

            var slot0Overlay0 = (RectTransform)root.transform.Find("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0");
            Assert.That(slot0Overlay0.anchorMin.x, Is.EqualTo(0.382f - 1.389f * 0.5f).Within(0.001f));
            Assert.That(slot0Overlay0.anchorMax.y, Is.EqualTo(0.292f + 0.463f * 0.5f).Within(0.001f));
            Assert.That((bool)imagePreserveAspectProp.GetValue(FindImageComponent("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0")), Is.False);
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.SameAs(scarf));
            Assert.That(GetImageSprite("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.SameAs(cap));
        }

        [Test]
        public void Refresh_SwitchingBetweenMultiLayerSingleLayerAndNoneReusesOverlayPoolWithoutExtraChildren()
        {
            Sprite scarf = CreateRuntimeSprite("PoolScarf", Color.red);
            Sprite cap = CreateRuntimeSprite("PoolCap", Color.blue);
            Sprite visor = CreateRuntimeSprite("PoolVisor", Color.yellow);

            object twoLayers = CreateLayeredAppearance(
                Color.red,
                "slot_0",
                CreateOverlayLayer(scarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f)),
                CreateOverlayLayer(cap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f)));
            object oneLayer = CreateLayeredAppearance(
                Color.red,
                "slot_1",
                CreateOverlayLayer(visor, new Vector2(0.537f, 0.704f), new Vector2(1.071f, 0.510f)));
            object noneLayer = CreateLayeredAppearance(Color.red, "none");

            Refresh(CreateSnapshotArray(CreateSnapshot(0, coins: 2, appearance: twoLayers, color: Color.red)));
            Transform container = root.transform.Find("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer");
            Assert.That(container.childCount, Is.EqualTo(2));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(2));

            Refresh(CreateSnapshotArray(CreateSnapshot(0, coins: 2, appearance: oneLayer, color: Color.red)));
            Assert.That(container.childCount, Is.EqualTo(2));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(1));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.False);

            Refresh(CreateSnapshotArray(CreateSnapshot(0, coins: 2, appearance: noneLayer, color: Color.red)));
            Assert.That(container.childCount, Is.EqualTo(2));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(0));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.False);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.False);

            // Restore 2 layers and toggle vertical mode to verify pool reuse + normalized anchors persist.
            Refresh(CreateSnapshotArray(CreateSnapshot(0, coins: 2, appearance: twoLayers, color: Color.red)));
            setLayoutModeMethod.Invoke(view, new[] { Enum.Parse(layoutModeType, "VerticalRightAligned") });
            Assert.That(container.childCount, Is.EqualTo(2));
            Assert.That(GetSlotOverlayCount(0), Is.EqualTo(2));
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(IsImageEnabled("ParticipantSlot_0/AvatarRoot/AvatarMask/OverlayContainer/Overlay_1"), Is.True);
        }

        private static void ComputeRasterizedPixelSpan(float minPx, float maxPx, out int count, out int firstPx, out int lastPx)
        {
            firstPx = Mathf.CeilToInt(minPx - 0.5f);
            lastPx = Mathf.CeilToInt(maxPx - 0.5f) - 1;
            count = lastPx >= firstPx ? (lastPx - firstPx + 1) : 0;
        }

        private static void ComputeScreenPixelBounds(
            RectTransform target,
            RectTransform canvasRect,
            float scaleFactor,
            out float leftPx,
            out float rightPx,
            out float bottomPx,
            out float topPx)
        {
            Rect local = target.rect;
            Vector3 bl = canvasRect.InverseTransformPoint(target.TransformPoint(new Vector3(local.xMin, local.yMin, 0f)));
            Vector3 tr = canvasRect.InverseTransformPoint(target.TransformPoint(new Vector3(local.xMax, local.yMax, 0f)));
            Rect cRect = canvasRect.rect;
            leftPx = (bl.x - cRect.xMin) * scaleFactor;
            rightPx = (tr.x - cRect.xMin) * scaleFactor;
            bottomPx = (bl.y - cRect.yMin) * scaleFactor;
            topPx = (tr.y - cRect.yMin) * scaleFactor;
        }

        [Test]
        public void HealthBar_PixelSnapping_ProducesIdenticalHeightAndIntactSymmetricBordersAcrossScalesAndModes()
        {
            float[] testScales = { 0.5f, 0.5333333f, 0.58f, 0.6666667f, 1.0f };
            object horizontalMode = Enum.Parse(layoutModeType, "Horizontal");
            object verticalMode = Enum.Parse(layoutModeType, "VerticalRightAligned");

            var canvasObj = new GameObject("HealthBarPixelSnapCanvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var canvasRect = (RectTransform)canvasObj.transform;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.zero;
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);

                var stripObj = new GameObject("StripUnderCanvas", typeof(RectTransform));
                stripObj.transform.SetParent(canvasRect, false);
                Component stripView = stripObj.AddComponent(viewType);
                initStandaloneMethod.Invoke(stripView, new object[] { 4, null });

                Array snapshots = CreateSnapshotArray(
                    CreateSnapshot(0, coins: 4, health: 3, maxHealth: 3, initialMaxHealth: 3, isLocal: true),
                    CreateSnapshot(1, coins: 3, health: 2, maxHealth: 3, initialMaxHealth: 3),
                    CreateSnapshot(2, coins: 2, health: 1, maxHealth: 2, initialMaxHealth: 3),
                    CreateSnapshot(3, coins: 1, health: 0, maxHealth: 1, initialMaxHealth: 3, status: ParticipantStatus.Lost));
                refreshMethod.Invoke(stripView, new object[] { snapshots });

                for (int sIdx = 0; sIdx < testScales.Length; sIdx++)
                {
                    float scale = testScales[sIdx];
                    canvas.scaleFactor = scale;
                    canvasRect.sizeDelta = new Vector2(1920f, 1080f);

                    for (int modeIdx = 0; modeIdx < 2; modeIdx++)
                    {
                        object targetMode = modeIdx == 0 ? horizontalMode : verticalMode;
                        Vector2 offset = modeIdx == 0 ? new Vector2(-12f, 388f) : new Vector2(-12f, 230f);
                        setLayoutModeWithRectMethod.Invoke(stripView, new object[] { targetMode, 368f, offset });
                        tickMethod.Invoke(stripView, new object[] { 0.016f });

                        int expectedBarH = -1;
                        int expectedSegH = -1;
                        int expectedBarW = -1;

                        for (int slotIdx = 0; slotIdx < 4; slotIdx++)
                        {
                            var barRect = (RectTransform)stripObj.transform.Find("ParticipantSlot_" + slotIdx + "/AvatarRoot/HealthBarRoot");
                            var seg0 = (RectTransform)barRect.Find("HealthSegment_0");
                            var seg1 = (RectTransform)barRect.Find("HealthSegment_1");
                            var seg2 = (RectTransform)barRect.Find("HealthSegment_2");

                            ComputeScreenPixelBounds(barRect, canvasRect, scale, out float bL, out float bR, out float bB, out float bT);
                            ComputeScreenPixelBounds(seg0, canvasRect, scale, out float s0L, out float s0R, out float s0B, out float s0T);
                            ComputeScreenPixelBounds(seg1, canvasRect, scale, out float s1L, out float s1R, out _, out _);
                            ComputeScreenPixelBounds(seg2, canvasRect, scale, out float s2L, out float s2R, out _, out _);

                            ComputeRasterizedPixelSpan(bB, bT, out int barHPx, out int barYMin, out int barYMax);
                            ComputeRasterizedPixelSpan(s0B, s0T, out int segHPx, out int segYMin, out int segYMax);
                            ComputeRasterizedPixelSpan(bL, bR, out int barWPx, out int barXMin, out int barXMax);
                            ComputeRasterizedPixelSpan(s0L, s0R, out int seg0WPx, out int s0XMin, out int s0XMax);
                            ComputeRasterizedPixelSpan(s1L, s1R, out int seg1WPx, out int s1XMin, out int s1XMax);
                            ComputeRasterizedPixelSpan(s2L, s2R, out int seg2WPx, out int s2XMin, out int s2XMax);

                            int topBorderPx = barYMax - segYMax;
                            int bottomBorderPx = segYMin - barYMin;
                            int leftBorderPx = s0XMin - barXMin;
                            int rightBorderPx = barXMax - s2XMax;
                            int gap01Px = s1XMin - s0XMax - 1;
                            int gap12Px = s2XMin - s1XMax - 1;

                            string ctx = "scale=" + scale + " mode=" + targetMode + " slot=" + slotIdx;
                            Assert.That(topBorderPx, Is.GreaterThanOrEqualTo(1), "Missing top border: " + ctx);
                            Assert.That(bottomBorderPx, Is.GreaterThanOrEqualTo(1), "Missing bottom border: " + ctx);
                            Assert.That(leftBorderPx, Is.GreaterThanOrEqualTo(1), "Missing left border: " + ctx);
                            Assert.That(rightBorderPx, Is.GreaterThanOrEqualTo(1), "Missing right border: " + ctx);
                            Assert.That(topBorderPx, Is.EqualTo(bottomBorderPx), "Asymmetric Y border: " + ctx);
                            Assert.That(leftBorderPx, Is.EqualTo(rightBorderPx), "Asymmetric X border: " + ctx);
                            Assert.That(gap01Px, Is.GreaterThanOrEqualTo(1), "Missing gap 0-1: " + ctx);
                            Assert.That(gap12Px, Is.GreaterThanOrEqualTo(1), "Missing gap 1-2: " + ctx);
                            Assert.That(gap01Px, Is.EqualTo(gap12Px), "Asymmetric segment gaps: " + ctx);
                            Assert.That(seg0WPx, Is.GreaterThanOrEqualTo(1), ctx);
                            Assert.That(seg1WPx, Is.GreaterThanOrEqualTo(1), ctx);
                            Assert.That(seg2WPx, Is.GreaterThanOrEqualTo(1), ctx);

                            if (expectedBarH < 0)
                            {
                                expectedBarH = barHPx;
                                expectedSegH = segHPx;
                                expectedBarW = barWPx;
                            }
                            else
                            {
                                Assert.That(barHPx, Is.EqualTo(expectedBarH), "Inconsistent bar height across slots: " + ctx);
                                Assert.That(segHPx, Is.EqualTo(expectedSegH), "Inconsistent segment height across slots: " + ctx);
                                Assert.That(barWPx, Is.EqualTo(expectedBarW), "Inconsistent bar width across slots: " + ctx);
                            }
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObj);
            }
        }
    }
}
