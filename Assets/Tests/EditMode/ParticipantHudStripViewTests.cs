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
        private Type snapshotType;
        private Type appearanceType;
        private Type imageType;

        private MethodInfo initStandaloneMethod;
        private MethodInfo computeLeaderMaskMethod;
        private MethodInfo refreshMethod;
        private MethodInfo tickMethod;
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
        private PropertyInfo imageSpriteProp;
        private PropertyInfo imageEnabledProp;

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
        private FieldInfo appearanceCosmeticIdField;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudStripView, Assembly-CSharp", true);
            snapshotType = Type.GetType("EachOneCoin.Gameplay.ParticipantHudSnapshot, Assembly-CSharp", true);
            appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
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
            imageSpriteProp = imageType.GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
            imageEnabledProp = imageType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);

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
            appearanceCosmeticIdField = appearanceType.GetField("CosmeticId", BindingFlags.Public | BindingFlags.Instance);

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
    }
}
