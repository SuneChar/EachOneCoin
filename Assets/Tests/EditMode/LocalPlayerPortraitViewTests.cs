using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class LocalPlayerPortraitViewTests
    {
        private readonly List<Sprite> runtimeSprites = new List<Sprite>();
        private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();

        private GameObject root;
        private Component view;
        private object provider;
        private Sprite defaultPortraitSprite;

        private Type viewType;
        private Type appearanceType;
        private Type layerType;
        private Type providerInterfaceType;
        private Type defaultProviderType;
        private Type imageType;

        private MethodInfo initMethod;
        private MethodInfo setParticipantMethod;
        private MethodInfo triggerHitFlashMethod;
        private MethodInfo tickMethod;
        private MethodInfo providerGetAppearanceMethod;
        private MethodInfo providerSetOverrideMethod;
        private MethodInfo providerClearOverrideMethod;

        private PropertyInfo portraitSizeProp;
        private PropertyInfo currentSlotIdProp;
        private PropertyInfo currentCosmeticIdProp;
        private PropertyInfo activeOverlayCountProp;
        private PropertyInfo currentStatusProp;
        private PropertyInfo isHitFlashingProp;
        private PropertyInfo imageSpriteProp;
        private PropertyInfo imageColorProp;
        private PropertyInfo imageEnabledProp;
        private PropertyInfo imagePreserveAspectProp;

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
            viewType = Type.GetType("EachOneCoin.Gameplay.LocalPlayerPortraitView, Assembly-CSharp", true);
            appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
            layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
            providerInterfaceType = Type.GetType("EachOneCoin.Gameplay.IAvatarAppearanceProvider, Assembly-CSharp", true);
            defaultProviderType = Type.GetType("EachOneCoin.Gameplay.DefaultAvatarAppearanceProvider, Assembly-CSharp", true);
            imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);

            initMethod = viewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            setParticipantMethod = viewType.GetMethod(
                "SetParticipant",
                new[] { typeof(int), appearanceType, typeof(ParticipantStatus) });
            triggerHitFlashMethod = viewType.GetMethod("TriggerHitFlash", Type.EmptyTypes);
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });

            providerGetAppearanceMethod = defaultProviderType.GetMethod("GetAppearance", new[] { typeof(int), typeof(Color) });
            providerSetOverrideMethod = defaultProviderType.GetMethod("SetOverride", new[] { typeof(int), appearanceType });
            providerClearOverrideMethod = defaultProviderType.GetMethod("ClearOverride", new[] { typeof(int) });

            portraitSizeProp = viewType.GetProperty("PortraitSize");
            currentSlotIdProp = viewType.GetProperty("CurrentSlotId");
            currentCosmeticIdProp = viewType.GetProperty("CurrentCosmeticId");
            activeOverlayCountProp = viewType.GetProperty("ActiveOverlayCount");
            currentStatusProp = viewType.GetProperty("CurrentStatus");
            isHitFlashingProp = viewType.GetProperty("IsHitFlashing");
            imageSpriteProp = imageType.GetProperty("sprite", BindingFlags.Public | BindingFlags.Instance);
            imageColorProp = imageType.GetProperty("color", BindingFlags.Public | BindingFlags.Instance);
            imageEnabledProp = imageType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            imagePreserveAspectProp = imageType.GetProperty("preserveAspect", BindingFlags.Public | BindingFlags.Instance);

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

            Assert.That(providerInterfaceType.IsAssignableFrom(defaultProviderType), Is.True);

            defaultPortraitSprite = CreateRuntimeSprite("DefaultPortrait", Color.white);
            provider = Activator.CreateInstance(defaultProviderType, new object[] { defaultPortraitSprite });

            root = new GameObject("LocalPlayerPortraitViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initMethod.Invoke(view, new object[] { provider, null });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }

            provider = null;
            defaultPortraitSprite = null;

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

        private float PortraitSize => (float)portraitSizeProp.GetValue(view);
        private int CurrentSlotId => (int)currentSlotIdProp.GetValue(view);
        private string CurrentCosmeticId => (string)currentCosmeticIdProp.GetValue(view);
        private int ActiveOverlayCount => (int)activeOverlayCountProp.GetValue(view);
        private ParticipantStatus CurrentStatus => (ParticipantStatus)currentStatusProp.GetValue(view);
        private bool IsHitFlashing => (bool)isHitFlashingProp.GetValue(view);

        private void SetParticipant(int slotId, object appearance, ParticipantStatus status)
            => setParticipantMethod.Invoke(view, new object[] { slotId, appearance, status });

        private void TriggerHitFlash()
            => triggerHitFlashMethod.Invoke(view, null);

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        private object ProviderGetAppearance(int slotId, Color fallbackColor)
            => providerGetAppearanceMethod.Invoke(provider, new object[] { slotId, fallbackColor });

        private void ProviderSetOverride(int slotId, object appearance)
            => providerSetOverrideMethod.Invoke(provider, new object[] { slotId, appearance });

        private void ProviderClearOverride(int slotId)
            => providerClearOverrideMethod.Invoke(provider, new object[] { slotId });

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

        private Color GetImageColor(string hierarchyPath)
        {
            Component image = FindImageComponent(hierarchyPath);
            return (Color)imageColorProp.GetValue(image);
        }

        private bool IsImageEnabled(string hierarchyPath)
        {
            Component image = FindImageComponent(hierarchyPath);
            return (bool)imageEnabledProp.GetValue(image);
        }

        [Test]
        public void SetParticipant_AppliesAppearanceCosmeticAndOverlayCount()
        {
            Sprite customPortrait = CreateRuntimeSprite("HeroPortrait", Color.white);
            Sprite customFrame = CreateRuntimeSprite("HeroFrame", Color.yellow);
            Sprite overlayA = CreateRuntimeSprite("OverlayA", Color.red);
            Sprite overlayB = CreateRuntimeSprite("OverlayB", Color.blue);

            object appearance = CreateAppearance(
                new Color(0.2f, 0.7f, 1f, 1f),
                basePortrait: customPortrait,
                frameSprite: customFrame,
                overlaySprites: new[] { overlayA, null, overlayB },
                cosmeticId: "skin_cyber_01");

            SetParticipant(2, appearance, ParticipantStatus.Active);

            Assert.That(CurrentSlotId, Is.EqualTo(2));
            Assert.That(CurrentCosmeticId, Is.EqualTo("skin_cyber_01"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(2));
            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(IsHitFlashing, Is.False);
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/BasePortraitImage"), Is.EqualTo(customPortrait));
            Assert.That(GetImageSprite("ContentRoot/FrameImage"), Is.EqualTo(customFrame));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.EqualTo(overlayA));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1"), Is.EqualTo(overlayB));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1"), Is.True);

            object singleOverlay = CreateAppearance(
                new Color(0.2f, 0.7f, 1f, 1f),
                basePortrait: customPortrait,
                frameSprite: customFrame,
                overlaySprites: new[] { overlayB },
                cosmeticId: "skin_cyber_02");

            SetParticipant(2, singleOverlay, ParticipantStatus.Active);

            Assert.That(CurrentCosmeticId, Is.EqualTo("skin_cyber_02"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(1));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.EqualTo(overlayB));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.True);
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1"), Is.False);
            Assert.That(root.transform.Find("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AppearanceProvider_HotSwapOverrideUpdatesPortraitAutomatically()
        {
            object initialAppearance = ProviderGetAppearance(1, Color.cyan);
            SetParticipant(1, initialAppearance, ParticipantStatus.Active);

            Assert.That(CurrentSlotId, Is.EqualTo(1));
            Assert.That(CurrentCosmeticId, Is.Null);
            Assert.That(ActiveOverlayCount, Is.EqualTo(0));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/BasePortraitImage"), Is.EqualTo(defaultPortraitSprite));

            Sprite hotSwapPortrait = CreateRuntimeSprite("HotSwapPortrait", Color.magenta);
            Sprite hotSwapFrame = CreateRuntimeSprite("HotSwapFrame", Color.yellow);
            Sprite overlayHat = CreateRuntimeSprite("HotSwapHat", Color.red);
            Sprite overlayBadge = CreateRuntimeSprite("HotSwapBadge", Color.green);

            object overrideAppearance = CreateAppearance(
                Color.magenta,
                basePortrait: hotSwapPortrait,
                frameSprite: hotSwapFrame,
                overlaySprites: new[] { overlayHat, overlayBadge },
                cosmeticId: "dlc_gold_mask");

            ProviderSetOverride(1, overrideAppearance);

            Assert.That(CurrentCosmeticId, Is.EqualTo("dlc_gold_mask"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(2));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/BasePortraitImage"), Is.EqualTo(hotSwapPortrait));
            Assert.That(GetImageSprite("ContentRoot/FrameImage"), Is.EqualTo(hotSwapFrame));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.EqualTo(overlayHat));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1"), Is.EqualTo(overlayBadge));

            object otherSlotOverride = CreateAppearance(
                Color.red,
                basePortrait: defaultPortraitSprite,
                overlaySprites: new[] { overlayHat },
                cosmeticId: "other_slot_skin");
            ProviderSetOverride(3, otherSlotOverride);

            Assert.That(CurrentCosmeticId, Is.EqualTo("dlc_gold_mask"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(2));

            ProviderClearOverride(1);

            Assert.That(CurrentCosmeticId, Is.Null);
            Assert.That(ActiveOverlayCount, Is.EqualTo(0));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/BasePortraitImage"), Is.EqualTo(defaultPortraitSprite));
        }

        [Test]
        public void TriggerHitFlash_DecaysToFalseAfterTick()
        {
            object appearance = CreateAppearance(Color.cyan, basePortrait: defaultPortraitSprite);
            SetParticipant(0, appearance, ParticipantStatus.Active);

            Assert.That(IsHitFlashing, Is.False);
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.False);

            TriggerHitFlash();

            Assert.That(IsHitFlashing, Is.True);
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.True);

            Tick(0f);
            Tick(-0.5f);
            Assert.That(IsHitFlashing, Is.True);

            Tick(0.14f);
            Assert.That(IsHitFlashing, Is.True);

            Tick(0.15f);
            Assert.That(IsHitFlashing, Is.False);
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.False);

            var contentRoot = (RectTransform)root.transform.Find("ContentRoot");
            Assert.That(contentRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SetParticipant_TracksRespawningAndLostStatus()
        {
            object appearance = CreateAppearance(new Color(0.9f, 0.25f, 0.2f, 1f), basePortrait: defaultPortraitSprite);
            SetParticipant(0, appearance, ParticipantStatus.Active);

            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.False);

            SetParticipant(0, appearance, ParticipantStatus.Respawning);

            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.True);
            Color respawningOverlay = GetImageColor("ContentRoot/CircleMaskRoot/StatusOverlay");
            Assert.That(respawningOverlay.a, Is.GreaterThan(0f));

            SetParticipant(0, appearance, ParticipantStatus.Lost);

            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Lost));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.True);
            Color lostOverlay = GetImageColor("ContentRoot/CircleMaskRoot/StatusOverlay");
            Assert.That(lostOverlay.a, Is.GreaterThan(0f));
            Assert.That(lostOverlay, Is.Not.EqualTo(respawningOverlay));

            Color lostPortraitColor = GetImageColor("ContentRoot/CircleMaskRoot/BasePortraitImage");
            Assert.That(lostPortraitColor.r, Is.EqualTo(lostPortraitColor.g).Within(0.001f));

            SetParticipant(0, appearance, ParticipantStatus.Active);
            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(IsImageEnabled("ContentRoot/CircleMaskRoot/StatusOverlay"), Is.False);
        }

        [Test]
        public void Initialize_UsesEnlargedDefaultPortraitSizeOf378()
        {
            Assert.That(PortraitSize, Is.EqualTo(378f).Within(0.001f));

            var rootRect = (RectTransform)root.transform;
            Assert.That(rootRect.sizeDelta.x, Is.EqualTo(378f).Within(0.001f));
            Assert.That(rootRect.sizeDelta.y, Is.EqualTo(378f).Within(0.001f));
        }

        private object CreateOverlayLayer(
            Sprite sprite,
            Vector2 center,
            Vector2 size,
            float rotationZ = 0f,
            bool useFactionTint = false)
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

            object boxed = CreateAppearance(tintColor, basePortrait: defaultPortraitSprite, cosmeticId: cosmeticId);
            appearanceOverlayLayersField.SetValue(boxed, layerArray);
            return boxed;
        }

        [Test]
        public void SetParticipant_AppliesNormalizedOverlayLayersAnchorsRotationAndEmptyNone()
        {
            Sprite scarf = CreateRuntimeSprite("LayerScarf", Color.red);
            Sprite cap = CreateRuntimeSprite("LayerCap", Color.blue);

            object layered = CreateLayeredAppearance(
                Color.cyan,
                "slot_0",
                CreateOverlayLayer(scarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f), rotationZ: 0f),
                CreateOverlayLayer(cap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f), rotationZ: -12f));

            SetParticipant(0, layered, ParticipantStatus.Active);

            var overlayContainer = (RectTransform)root.transform.Find("ContentRoot/CircleMaskRoot/OverlayContainer");
            Assert.That(overlayContainer.anchorMin, Is.EqualTo(new Vector2(0.04f, 0f)));
            Assert.That(overlayContainer.anchorMax, Is.EqualTo(new Vector2(0.96f, 0.96f)));
            Assert.That(CurrentCosmeticId, Is.EqualTo("slot_0"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(2));

            var overlay0Rect = (RectTransform)overlayContainer.Find("Overlay_0");
            var overlay1Rect = (RectTransform)overlayContainer.Find("Overlay_1");
            Assert.That(overlay0Rect.anchorMin.x, Is.EqualTo(0.382f - 1.389f * 0.5f).Within(0.001f));
            Assert.That(overlay0Rect.anchorMax.y, Is.EqualTo(0.292f + 0.463f * 0.5f).Within(0.001f));
            Assert.That(overlay1Rect.anchorMin.x, Is.EqualTo(0.552f - 1.205f * 0.5f).Within(0.001f));
            Assert.That(overlay1Rect.anchorMax.y, Is.EqualTo(0.856f + 0.803f * 0.5f).Within(0.001f));
            Assert.That(Mathf.DeltaAngle(overlay1Rect.localEulerAngles.z, -12f), Is.EqualTo(0f).Within(0.1f));
            Assert.That((bool)imagePreserveAspectProp.GetValue(FindImageComponent("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0")), Is.False);
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.SameAs(scarf));
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1"), Is.SameAs(cap));

            // Switching to "none" outfit with empty OverlayLayers hides all overlay children cleanly.
            object noneOutfit = CreateLayeredAppearance(Color.cyan, "none");
            SetParticipant(0, noneOutfit, ParticipantStatus.Active);

            Assert.That(CurrentCosmeticId, Is.EqualTo("none"));
            Assert.That(ActiveOverlayCount, Is.EqualTo(0));
            Assert.That(overlay0Rect.gameObject.activeSelf, Is.False);
            Assert.That(overlay1Rect.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SetParticipant_StatusDimAppliesToOverlayLayersAndRespectsUseFactionTint()
        {
            Sprite plainAccessory = CreateRuntimeSprite("PlainAccessory", Color.white);
            Sprite tintedAccessory = CreateRuntimeSprite("TintedAccessory", Color.white);
            Color factionColor = new Color(0.94f, 0.27f, 0.28f, 1f);

            object appearance = CreateLayeredAppearance(
                factionColor,
                "slot_tinted",
                CreateOverlayLayer(plainAccessory, new Vector2(0.5f, 0.7f), new Vector2(1f, 0.5f), useFactionTint: false),
                CreateOverlayLayer(tintedAccessory, new Vector2(0.5f, 0.3f), new Vector2(1f, 0.5f), useFactionTint: true));

            SetParticipant(0, appearance, ParticipantStatus.Active);
            Color activePlain = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0");
            Color activeTinted = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1");
            Assert.That(activePlain, Is.EqualTo(Color.white));
            Assert.That(activeTinted.r, Is.EqualTo(factionColor.r).Within(0.01f));
            Assert.That(activeTinted.g, Is.EqualTo(factionColor.g).Within(0.01f));

            SetParticipant(0, appearance, ParticipantStatus.Respawning);
            Color respawningPlain = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0");
            Color respawningTinted = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1");
            Assert.That(respawningPlain.a, Is.LessThan(1f));
            Assert.That(respawningTinted.r, Is.LessThan(activeTinted.r));

            SetParticipant(0, appearance, ParticipantStatus.Lost);
            Color lostPlain = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0");
            Color lostTinted = GetImageColor("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_1");
            Assert.That(lostPlain.r, Is.LessThan(respawningPlain.r));
            Assert.That(lostTinted.r, Is.EqualTo(lostTinted.g).Within(0.02f));
        }

        [Test]
        public void SetParticipant_SwitchingSlotUpdatesOverlaysAndClearsPendingHitFlash()
        {
            Sprite cap = CreateRuntimeSprite("Cap", Color.blue);
            Sprite headset = CreateRuntimeSprite("Headset", Color.cyan);

            object slot0 = CreateLayeredAppearance(Color.red, "slot_0", CreateOverlayLayer(cap, new Vector2(0.5f, 0.8f), new Vector2(1.1f, 0.7f)));
            object slot2 = CreateLayeredAppearance(Color.green, "slot_2", CreateOverlayLayer(headset, new Vector2(0.5f, 0.65f), new Vector2(1.1f, 0.7f)));

            SetParticipant(0, slot0, ParticipantStatus.Lost);
            TriggerHitFlash();
            Assert.That(IsHitFlashing, Is.True);

            SetParticipant(2, slot2, ParticipantStatus.Active);
            Assert.That(CurrentSlotId, Is.EqualTo(2));
            Assert.That(CurrentCosmeticId, Is.EqualTo("slot_2"));
            Assert.That(CurrentStatus, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(IsHitFlashing, Is.False);
            Assert.That(GetImageSprite("ContentRoot/CircleMaskRoot/OverlayContainer/Overlay_0"), Is.SameAs(headset));
        }
    }
}
