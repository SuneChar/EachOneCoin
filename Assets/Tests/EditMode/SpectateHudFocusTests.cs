using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class SpectateHudFocusTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        private readonly List<UnityEngine.Object> cleanupObjects = new List<UnityEngine.Object>();

        private Type arenaGameUiType;
        private Type healthHeartsType;
        private Type portraitViewType;
        private Type appearanceType;
        private Type layerType;
        private Type profileType;
        private Type catalogType;
        private Type coordinatorType;
        private Type characterType;
        private Type defaultProviderType;
        private Type matchProviderType;

        [SetUp]
        public void SetUp()
        {
            arenaGameUiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            healthHeartsType = Type.GetType("EachOneCoin.Gameplay.HealthHeartsView, Assembly-CSharp", true);
            portraitViewType = Type.GetType("EachOneCoin.Gameplay.LocalPlayerPortraitView, Assembly-CSharp", true);
            appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
            layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
            profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            catalogType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceCatalog, Assembly-CSharp", true);
            coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            defaultProviderType = Type.GetType("EachOneCoin.Gameplay.DefaultAvatarAppearanceProvider, Assembly-CSharp", true);
            matchProviderType = Type.GetType("EachOneCoin.Gameplay.MatchAvatarAppearanceProvider, Assembly-CSharp", true);
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

        private Sprite CreateRuntimeSprite(string name, Color color)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = name + "_Tex",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            cleanupObjects.Add(tex);

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name + "_Sprite";
            sprite.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            cleanupObjects.Add(sprite);
            return sprite;
        }

        private object CreateOverlayLayer(Sprite sprite, Vector2 center, Vector2 size)
        {
            object boxed = Activator.CreateInstance(layerType);
            layerType.GetField("Sprite").SetValue(boxed, sprite);
            layerType.GetField("NormalizedCenter").SetValue(boxed, center);
            layerType.GetField("NormalizedSize").SetValue(boxed, size);
            layerType.GetField("RotationZ").SetValue(boxed, 0f);
            layerType.GetField("UseFactionTint").SetValue(boxed, false);
            return boxed;
        }

        private object CreateAppearanceWithLayers(Color tint, string cosmeticId, params object[] layers)
        {
            Array layerArray = Array.CreateInstance(layerType, layers != null ? layers.Length : 0);
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++) layerArray.SetValue(layers[i], i);
            }

            object boxed = Activator.CreateInstance(appearanceType);
            appearanceType.GetField("TintColor").SetValue(boxed, tint);
            appearanceType.GetField("CosmeticId").SetValue(boxed, cosmeticId);
            appearanceType.GetField("OverlayLayers").SetValue(boxed, layerArray);
            return boxed;
        }

        [Test]
        public void ResolveObservedHudSlot_TracksSpectateTargetOnlyWhenSpectatingValidSlot()
        {
            MethodInfo resolve = arenaGameUiType.GetMethod("ResolveObservedHudSlot", PrivateStatic);
            Assert.That(resolve, Is.Not.Null);

            Assert.That((int)resolve.Invoke(null, new object[] { 0, false, 2 }), Is.EqualTo(0));
            Assert.That((int)resolve.Invoke(null, new object[] { 0, true, 2 }), Is.EqualTo(2));
            Assert.That((int)resolve.Invoke(null, new object[] { 1, true, 3 }), Is.EqualTo(3));
            Assert.That((int)resolve.Invoke(null, new object[] { 1, true, -1 }), Is.EqualTo(1));
            Assert.That((int)resolve.Invoke(null, new object[] { 1, true, 4 }), Is.EqualTo(1));
        }

        [Test]
        public void SpectateHealthSync_SwitchesTargetImmediatelyWithoutFalseShatterOrBlinkAnimations()
        {
            var root = Track(new GameObject("SpectateHealthRoot", typeof(RectTransform)));
            root.SetActive(false);

            Component arenaUi = root.AddComponent(arenaGameUiType);
            var heartsObj = new GameObject("HealthHearts", typeof(RectTransform));
            heartsObj.transform.SetParent(root.transform, false);
            Component hearts = heartsObj.AddComponent(healthHeartsType);
            healthHeartsType.GetMethod("Initialize", new[] { typeof(int) }).Invoke(hearts, new object[] { 3 });

            arenaGameUiType.GetField("healthHeartsView", PrivateInstance).SetValue(arenaUi, hearts);

            MethodInfo syncHealth = arenaGameUiType.GetMethod("SyncObservedNetworkHealth", PrivateInstance);
            MethodInfo getSlotState = healthHeartsType.GetMethod("GetSlotState", new[] { typeof(int) });
            MethodInfo isSlotAnimating = healthHeartsType.GetMethod("IsSlotAnimating", new[] { typeof(int) });

            string State(int index) => getSlotState.Invoke(hearts, new object[] { index }).ToString();
            bool Animating(int index) => (bool)isSlotAnimating.Invoke(hearts, new object[] { index });

            // 1. Initial sync for local player slot 0 (3/3): static, zero animation.
            syncHealth.Invoke(arenaUi, new object[] { 0, 3, 3, ParticipantStatus.Active, false, false });
            Assert.That(State(0), Is.EqualTo("Full"));
            Assert.That(State(1), Is.EqualTo("Full"));
            Assert.That(State(2), Is.EqualTo("Full"));
            Assert.That(Animating(0) || Animating(1) || Animating(2), Is.False);

            // 2. Local player slot 0 loses max health (3/3 -> 2/2): real shatter animation starts on heart 2.
            syncHealth.Invoke(arenaUi, new object[] { 0, 2, 2, ParticipantStatus.Active, false, false });
            Assert.That(State(2), Is.EqualTo("Broken"));
            Assert.That(Animating(2), Is.True);

            // 3. Local player dies and enters spectate of slot 2 (1/2) while heart 2 was still mid-shatter:
            //    Hearts MUST snap immediately to slot 2's state (Full, Dim, Broken) with ALL animations cancelled.
            syncHealth.Invoke(arenaUi, new object[] { 2, 1, 2, ParticipantStatus.Active, false, false });
            Assert.That(State(0), Is.EqualTo("Full"));
            Assert.That(State(1), Is.EqualTo("Dim"));
            Assert.That(State(2), Is.EqualTo("Broken"));
            Assert.That(Animating(0), Is.False);
            Assert.That(Animating(1), Is.False);
            Assert.That(Animating(2), Is.False);

            // 4. While remaining on spectated slot 2, slot 2 genuinely takes damage (1/2 -> 0/2): real blink plays.
            syncHealth.Invoke(arenaUi, new object[] { 2, 0, 2, ParticipantStatus.Respawning, false, false });
            Assert.That(State(0), Is.EqualTo("Dim"));
            Assert.That(Animating(0), Is.True);

            // 5. While remaining on spectated slot 2, slot 2 loses max health (0/2 -> 0/1): real shatter plays on heart 1.
            syncHealth.Invoke(arenaUi, new object[] { 2, 0, 1, ParticipantStatus.Respawning, false, false });
            Assert.That(State(1), Is.EqualTo("Broken"));
            Assert.That(Animating(1), Is.True);

            // 6. Spectator presses Tab to switch from slot 2 to slot 3 (3/3):
            //    No heal-pop / shatter / blink animations may play on the switch.
            syncHealth.Invoke(arenaUi, new object[] { 3, 3, 3, ParticipantStatus.Active, false, false });
            Assert.That(State(0), Is.EqualTo("Full"));
            Assert.That(State(1), Is.EqualTo("Full"));
            Assert.That(State(2), Is.EqualTo("Full"));
            Assert.That(Animating(0), Is.False);
            Assert.That(Animating(1), Is.False);
            Assert.That(Animating(2), Is.False);

            // 7. Switching to slot 1 who has fewer hearts (1/2) must NOT falsely play blink or shatter.
            syncHealth.Invoke(arenaUi, new object[] { 1, 1, 2, ParticipantStatus.Active, false, false });
            Assert.That(State(0), Is.EqualTo("Full"));
            Assert.That(State(1), Is.EqualTo("Dim"));
            Assert.That(State(2), Is.EqualTo("Broken"));
            Assert.That(Animating(0), Is.False);
            Assert.That(Animating(1), Is.False);
            Assert.That(Animating(2), Is.False);
        }

        [Test]
        public void SpectatePortraitSwitch_UpdatesCosmeticAndStatusAndClearsHitFlashWithoutFalseFlash()
        {
            var root = Track(new GameObject("SpectatePortraitRoot", typeof(RectTransform)));
            Component portrait = root.AddComponent(portraitViewType);
            portraitViewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(portrait, new object[] { null, null });

            MethodInfo setParticipant = portraitViewType.GetMethod(
                "SetParticipant",
                new[] { typeof(int), appearanceType, typeof(ParticipantStatus) });
            MethodInfo triggerHitFlash = portraitViewType.GetMethod("TriggerHitFlash", Type.EmptyTypes);
            PropertyInfo currentSlotProp = portraitViewType.GetProperty("CurrentSlotId");
            PropertyInfo currentCosmeticProp = portraitViewType.GetProperty("CurrentCosmeticId");
            PropertyInfo activeOverlaysProp = portraitViewType.GetProperty("ActiveOverlayCount");
            PropertyInfo currentStatusProp = portraitViewType.GetProperty("CurrentStatus");
            PropertyInfo isHitFlashingProp = portraitViewType.GetProperty("IsHitFlashing");

            Sprite scarf = CreateRuntimeSprite("Scarf", Color.red);
            Sprite cap = CreateRuntimeSprite("Cap", Color.blue);
            Sprite headset = CreateRuntimeSprite("Headset", Color.cyan);

            object slot0Appearance = CreateAppearanceWithLayers(
                Color.red,
                "slot_0",
                CreateOverlayLayer(scarf, new Vector2(0.382f, 0.292f), new Vector2(1.389f, 0.463f)),
                CreateOverlayLayer(cap, new Vector2(0.552f, 0.856f), new Vector2(1.205f, 0.803f)));
            object slot2Appearance = CreateAppearanceWithLayers(
                Color.green,
                "slot_2",
                CreateOverlayLayer(headset, new Vector2(0.475f, 0.679f), new Vector2(1.112f, 0.742f)));
            object slot3NoneAppearance = CreateAppearanceWithLayers(Color.yellow, "none");

            // Local player slot 0 takes a lethal hit (flashing red + Lost).
            setParticipant.Invoke(portrait, new object[] { 0, slot0Appearance, ParticipantStatus.Lost });
            triggerHitFlash.Invoke(portrait, null);
            Assert.That((int)currentSlotProp.GetValue(portrait), Is.EqualTo(0));
            Assert.That((bool)isHitFlashingProp.GetValue(portrait), Is.True);
            Assert.That((int)activeOverlaysProp.GetValue(portrait), Is.EqualTo(2));

            // Switching big portrait to spectated slot 2 clears hit flash and renders slot 2's cosmetic + Active status.
            setParticipant.Invoke(portrait, new object[] { 2, slot2Appearance, ParticipantStatus.Active });
            Assert.That((int)currentSlotProp.GetValue(portrait), Is.EqualTo(2));
            Assert.That(currentCosmeticProp.GetValue(portrait), Is.EqualTo("slot_2"));
            Assert.That((int)activeOverlaysProp.GetValue(portrait), Is.EqualTo(1));
            Assert.That(currentStatusProp.GetValue(portrait), Is.EqualTo(ParticipantStatus.Active));
            Assert.That((bool)isHitFlashingProp.GetValue(portrait), Is.False);

            // Switching spectate target to slot 3 wearing "none" hides all overlays with zero hit flash.
            setParticipant.Invoke(portrait, new object[] { 3, slot3NoneAppearance, ParticipantStatus.Active });
            Assert.That((int)currentSlotProp.GetValue(portrait), Is.EqualTo(3));
            Assert.That(currentCosmeticProp.GetValue(portrait), Is.EqualTo("none"));
            Assert.That((int)activeOverlaysProp.GetValue(portrait), Is.EqualTo(0));
            Assert.That((bool)isHitFlashingProp.GetValue(portrait), Is.False);
        }

        [Test]
        public void ArenaGameUI_AutoBindsMatchAvatarProviderRespectsExternalOverrideAndDisposesOnDestroy()
        {
            var root = Track(new GameObject("ArenaUiProviderLifecycle", typeof(RectTransform)));
            root.SetActive(false);

            Component arenaUi = root.AddComponent(arenaGameUiType);
            var matchObj = Track(new GameObject("MockMatchCoordinatorForUi"));
            matchObj.SetActive(false);
            Component coordinator = matchObj.AddComponent(coordinatorType);

            var profile = ScriptableObject.CreateInstance(profileType);
            cleanupObjects.Add(profile);
            var profileSo = new SerializedObject(profile);
            profileSo.FindProperty("appearanceId").stringValue = "slot_2";
            profileSo.FindProperty("accessorySlotName").stringValue = "Slot 2";
            profileSo.ApplyModifiedPropertiesWithoutUndo();

            var catalog = ScriptableObject.CreateInstance(catalogType);
            cleanupObjects.Add(catalog);
            var catalogSo = new SerializedObject(catalog);
            var profilesProp = catalogSo.FindProperty("profiles");
            profilesProp.arraySize = 1;
            profilesProp.GetArrayElementAtIndex(0).objectReferenceValue = profile;
            catalogSo.ApplyModifiedPropertiesWithoutUndo();
            coordinatorType.GetField("appearanceCatalog", PrivateInstance).SetValue(coordinator, catalog);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Array characters = Array.CreateInstance(characterType, 4);
            for (int i = 0; i < 4; i++)
            {
                var inst = UnityEngine.Object.Instantiate(prefab, matchObj.transform);
                cleanupObjects.Add(inst);
                characters.SetValue(inst.GetComponent(characterType), i);
            }
            coordinatorType.GetProperty("Characters").GetSetMethod(true).Invoke(coordinator, new object[] { characters });
            coordinatorType.GetMethod("ApplyParticipantAppearance").Invoke(coordinator, new object[] { 0, 0 });

            arenaGameUiType.GetField("match", PrivateInstance).SetValue(arenaUi, coordinator);

            MethodInfo ensureProvider = arenaGameUiType.GetMethod("EnsureMatchAvatarProvider", PrivateInstance);
            MethodInfo setProvider = arenaGameUiType.GetMethod("SetAvatarAppearanceProvider", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo onDestroy = arenaGameUiType.GetMethod("OnDestroy", PrivateInstance);
            FieldInfo providerField = arenaGameUiType.GetField("avatarProvider", PrivateInstance);
            FieldInfo ownedField = arenaGameUiType.GetField("ownedMatchAvatarProvider", PrivateInstance);

            ensureProvider.Invoke(arenaUi, null);
            object boundProvider = providerField.GetValue(arenaUi);
            Assert.That(boundProvider, Is.Not.Null);
            Assert.That(boundProvider.GetType(), Is.EqualTo(matchProviderType));
            Assert.That(ownedField.GetValue(arenaUi), Is.SameAs(boundProvider));

            // External test provider takes priority and is not overwritten by subsequent EnsureMatchAvatarProvider calls.
            object externalProvider = Activator.CreateInstance(defaultProviderType);
            setProvider.Invoke(arenaUi, new[] { externalProvider });
            Assert.That(providerField.GetValue(arenaUi), Is.SameAs(externalProvider));
            Assert.That(ownedField.GetValue(arenaUi), Is.Null);

            ensureProvider.Invoke(arenaUi, null);
            Assert.That(providerField.GetValue(arenaUi), Is.SameAs(externalProvider));

            // Resetting to null allows MatchAvatarAppearanceProvider auto-binding again, then OnDestroy disposes it cleanly.
            setProvider.Invoke(arenaUi, new object[] { null });
            ensureProvider.Invoke(arenaUi, null);
            Assert.That(ownedField.GetValue(arenaUi), Is.Not.Null);

            onDestroy.Invoke(arenaUi, null);
            Assert.That(ownedField.GetValue(arenaUi), Is.Null);
            Assert.DoesNotThrow(() => coordinatorType.GetMethod("ApplyParticipantAppearance").Invoke(coordinator, new object[] { 0, 0 }));
        }
    }
}
