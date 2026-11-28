using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MatchAvatarAppearanceProviderTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<UnityEngine.Object> cleanupObjects = new List<UnityEngine.Object>();

        private Type profileType;
        private Type catalogType;
        private Type coordinatorType;
        private Type characterType;
        private Type layerType;
        private Type appearanceType;
        private Type providerInterfaceType;
        private Type matchProviderType;

        private MethodInfo fromProfile2Method;
        private MethodInfo fromProfile3Method;
        private MethodInfo contentEqualsMethod;

        [SetUp]
        public void SetUp()
        {
            profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            catalogType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceCatalog, Assembly-CSharp", true);
            coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            layerType = Type.GetType("EachOneCoin.Gameplay.PortraitOverlayLayer, Assembly-CSharp", true);
            appearanceType = Type.GetType("EachOneCoin.Gameplay.AvatarAppearanceData, Assembly-CSharp", true);
            providerInterfaceType = Type.GetType("EachOneCoin.Gameplay.IAvatarAppearanceProvider, Assembly-CSharp", true);
            matchProviderType = Type.GetType("EachOneCoin.Gameplay.MatchAvatarAppearanceProvider, Assembly-CSharp", true);

            fromProfile2Method = appearanceType.GetMethod(
                "FromProfile",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { profileType, typeof(Color) },
                null);
            fromProfile3Method = appearanceType.GetMethod(
                "FromProfile",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { profileType, typeof(Color), typeof(Sprite) },
                null);
            contentEqualsMethod = appearanceType.GetMethod(
                "ContentEquals",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { appearanceType, appearanceType },
                null);

            Assert.That(providerInterfaceType.IsAssignableFrom(matchProviderType), Is.True);
            Assert.That(typeof(IDisposable).IsAssignableFrom(matchProviderType), Is.True);
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

        private ScriptableObject CreateProfile(
            string id,
            string slotName,
            string displayName,
            Sprite[] layerSprites,
            Sprite baseOverride = null,
            Sprite frameOverride = null)
        {
            var profile = ScriptableObject.CreateInstance(profileType);
            cleanupObjects.Add(profile);

            var so = new SerializedObject(profile);
            so.FindProperty("appearanceId").stringValue = id;
            so.FindProperty("accessorySlotName").stringValue = slotName;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("portraitBaseOverride").objectReferenceValue = baseOverride;
            so.FindProperty("portraitFrameOverride").objectReferenceValue = frameOverride;

            var layersProp = so.FindProperty("portraitOverlayLayers");
            int count = layerSprites != null ? layerSprites.Length : 0;
            layersProp.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                var elem = layersProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("Sprite").objectReferenceValue = layerSprites[i];
                elem.FindPropertyRelative("NormalizedCenter").vector2Value = new Vector2(0.5f, 0.7f - i * 0.2f);
                elem.FindPropertyRelative("NormalizedSize").vector2Value = new Vector2(1.1f, 0.6f);
                elem.FindPropertyRelative("RotationZ").floatValue = i * 5f;
                elem.FindPropertyRelative("UseFactionTint").boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private ScriptableObject CreateFiveOutfitCatalog(Sprite scarf, Sprite cap, Sprite visor, Sprite headset, Sprite sash)
        {
            var profiles = new[]
            {
                CreateProfile("slot_0", "Slot 0", "运动帽围巾", new[] { scarf, cap }),
                CreateProfile("slot_1", "Slot 1", "遮阳帽", new[] { visor }),
                CreateProfile("slot_2", "Slot 2", "运动耳机", new[] { headset }),
                CreateProfile("slot_3", "Slot 3", "荣誉绶带", new[] { sash }),
                CreateProfile("none", "", "无配饰", Array.Empty<Sprite>())
            };

            var catalog = ScriptableObject.CreateInstance(catalogType);
            cleanupObjects.Add(catalog);
            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("profiles");
            prop.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private Component CreateCoordinatorWithCatalog(ScriptableObject catalog)
        {
            var root = new GameObject("MatchProviderTestCoordinator");
            root.SetActive(false);
            cleanupObjects.Add(root);

            var coordinator = root.AddComponent(coordinatorType);
            coordinatorType.GetField("appearanceCatalog", PrivateInstance).SetValue(coordinator, catalog);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            Array characters = Array.CreateInstance(characterType, 4);
            for (int i = 0; i < 4; i++)
            {
                var inst = UnityEngine.Object.Instantiate(prefab, root.transform);
                cleanupObjects.Add(inst);
                characters.SetValue(inst.GetComponent(characterType), i);
            }
            coordinatorType.GetProperty("Characters").GetSetMethod(true).Invoke(coordinator, new object[] { characters });
            return coordinator;
        }

        [Test]
        public void FromProfile_MapsCosmeticIdOverridesAndPortraitOverlayLayersIncludingNone()
        {
            Sprite customBase = CreateRuntimeSprite("BaseOverride", Color.white);
            Sprite customFrame = CreateRuntimeSprite("FrameOverride", Color.yellow);
            Sprite scarf = CreateRuntimeSprite("Scarf", Color.red);
            Sprite cap = CreateRuntimeSprite("Cap", Color.blue);

            ScriptableObject slot0 = CreateProfile("slot_0", "Slot 0", "运动帽围巾", new[] { scarf, cap }, customBase, customFrame);
            object data0 = fromProfile2Method.Invoke(null, new object[] { slot0, Color.cyan });

            Assert.That(appearanceType.GetField("CosmeticId").GetValue(data0), Is.EqualTo("slot_0"));
            Assert.That(appearanceType.GetField("BasePortrait").GetValue(data0), Is.SameAs(customBase));
            Assert.That(appearanceType.GetField("FrameSprite").GetValue(data0), Is.SameAs(customFrame));
            Assert.That(appearanceType.GetField("TintColor").GetValue(data0), Is.EqualTo(Color.cyan));
            var layers0 = (Array)appearanceType.GetField("OverlayLayers").GetValue(data0);
            Assert.That(layers0, Is.Not.Null);
            Assert.That(layers0.Length, Is.EqualTo(2));
            Assert.That(layerType.GetField("Sprite").GetValue(layers0.GetValue(0)), Is.SameAs(scarf));
            Assert.That(layerType.GetField("Sprite").GetValue(layers0.GetValue(1)), Is.SameAs(cap));

            ScriptableObject none = CreateProfile("none", "", "无配饰", Array.Empty<Sprite>());
            object dataNone = fromProfile2Method.Invoke(null, new object[] { none, Color.green });
            Assert.That(appearanceType.GetField("CosmeticId").GetValue(dataNone), Is.EqualTo("none"));
            var layersNone = (Array)appearanceType.GetField("OverlayLayers").GetValue(dataNone);
            Assert.That(layersNone == null || layersNone.Length == 0, Is.True);
        }

        [Test]
        public void FromProfile_NullProfileFallsBackToDefaultAppearance()
        {
            Sprite fallbackPortrait = CreateRuntimeSprite("FallbackPortrait", Color.white);
            object fallbackData = fromProfile3Method.Invoke(null, new object[] { null, Color.magenta, fallbackPortrait });

            Assert.That(appearanceType.GetField("CosmeticId").GetValue(fallbackData), Is.Null);
            Assert.That(appearanceType.GetField("BasePortrait").GetValue(fallbackData), Is.SameAs(fallbackPortrait));
            Assert.That(appearanceType.GetField("TintColor").GetValue(fallbackData), Is.EqualTo(Color.magenta));
            Assert.That(appearanceType.GetField("OverlayLayers").GetValue(fallbackData), Is.Null);
        }

        [Test]
        public void Provider_ReadsCoordinatorAppearancesCachesAndForwardsAppearanceChanged()
        {
            Sprite scarf = CreateRuntimeSprite("Scarf", Color.red);
            Sprite cap = CreateRuntimeSprite("Cap", Color.blue);
            Sprite visor = CreateRuntimeSprite("Visor", Color.yellow);
            Sprite headset = CreateRuntimeSprite("Headset", Color.cyan);
            Sprite sash = CreateRuntimeSprite("Sash", Color.green);

            ScriptableObject catalog = CreateFiveOutfitCatalog(scarf, cap, visor, headset, sash);
            Component coordinator = CreateCoordinatorWithCatalog(catalog);
            MethodInfo applyMethod = coordinatorType.GetMethod("ApplyParticipantAppearance");
            applyMethod.Invoke(coordinator, new object[] { 0, 0 });
            applyMethod.Invoke(coordinator, new object[] { 1, 2 });
            applyMethod.Invoke(coordinator, new object[] { 2, 4 });

            object provider = Activator.CreateInstance(matchProviderType, new object[] { coordinator, null });
            var disposable = (IDisposable)provider;
            MethodInfo getAppearance = matchProviderType.GetMethod("GetAppearance", new[] { typeof(int), typeof(Color) });
            EventInfo changedEvent = matchProviderType.GetEvent("AppearanceChanged");

            var notifiedSlots = new List<int>();
            Action<int> handler = slot => notifiedSlots.Add(slot);
            changedEvent.AddEventHandler(provider, handler);

            try
            {
                object slot0First = getAppearance.Invoke(provider, new object[] { 0, Color.red });
                object slot0Cached = getAppearance.Invoke(provider, new object[] { 0, Color.red });
                Assert.That(appearanceType.GetField("CosmeticId").GetValue(slot0First), Is.EqualTo("slot_0"));
                Assert.That(((Array)appearanceType.GetField("OverlayLayers").GetValue(slot0First)).Length, Is.EqualTo(2));
                Assert.That((bool)contentEqualsMethod.Invoke(null, new[] { slot0First, slot0Cached }), Is.True);

                object slot2None = getAppearance.Invoke(provider, new object[] { 2, Color.green });
                Assert.That(appearanceType.GetField("CosmeticId").GetValue(slot2None), Is.EqualTo("none"));
                Assert.That(((Array)appearanceType.GetField("OverlayLayers").GetValue(slot2None)).Length, Is.EqualTo(0));

                // Hot-swap slot 0 from slot_0 (2 layers) to none (0 layers) and slot 1 from slot_2 to slot_3.
                applyMethod.Invoke(coordinator, new object[] { 0, 4 });
                applyMethod.Invoke(coordinator, new object[] { 1, 3 });

                Assert.That(notifiedSlots, Is.EqualTo(new[] { 0, 1 }));

                object slot0Updated = getAppearance.Invoke(provider, new object[] { 0, Color.red });
                Assert.That(appearanceType.GetField("CosmeticId").GetValue(slot0Updated), Is.EqualTo("none"));
                Assert.That(((Array)appearanceType.GetField("OverlayLayers").GetValue(slot0Updated)).Length, Is.EqualTo(0));

                object slot1Updated = getAppearance.Invoke(provider, new object[] { 1, Color.blue });
                Assert.That(appearanceType.GetField("CosmeticId").GetValue(slot1Updated), Is.EqualTo("slot_3"));
                var slot1Layers = (Array)appearanceType.GetField("OverlayLayers").GetValue(slot1Updated);
                Assert.That(slot1Layers.Length, Is.EqualTo(1));
                Assert.That(layerType.GetField("Sprite").GetValue(slot1Layers.GetValue(0)), Is.SameAs(sash));
            }
            finally
            {
                changedEvent.RemoveEventHandler(provider, handler);
                disposable.Dispose();
            }
        }

        [Test]
        public void Provider_DisposeUnsubscribesFromCoordinatorAndReturnsSafeFallback()
        {
            Sprite scarf = CreateRuntimeSprite("Scarf", Color.red);
            Sprite cap = CreateRuntimeSprite("Cap", Color.blue);
            Sprite visor = CreateRuntimeSprite("Visor", Color.yellow);
            Sprite headset = CreateRuntimeSprite("Headset", Color.cyan);
            Sprite sash = CreateRuntimeSprite("Sash", Color.green);

            ScriptableObject catalog = CreateFiveOutfitCatalog(scarf, cap, visor, headset, sash);
            Component coordinator = CreateCoordinatorWithCatalog(catalog);
            MethodInfo applyMethod = coordinatorType.GetMethod("ApplyParticipantAppearance");
            applyMethod.Invoke(coordinator, new object[] { 0, 1 });

            object provider = Activator.CreateInstance(matchProviderType, new object[] { coordinator, null });
            var disposable = (IDisposable)provider;
            MethodInfo getAppearance = matchProviderType.GetMethod("GetAppearance", new[] { typeof(int), typeof(Color) });
            EventInfo changedEvent = matchProviderType.GetEvent("AppearanceChanged");

            int callCount = 0;
            Action<int> handler = _ => callCount++;
            changedEvent.AddEventHandler(provider, handler);

            applyMethod.Invoke(coordinator, new object[] { 0, 2 });
            Assert.That(callCount, Is.EqualTo(1));

            disposable.Dispose();
            disposable.Dispose();

            applyMethod.Invoke(coordinator, new object[] { 0, 3 });
            Assert.That(callCount, Is.EqualTo(1));

            object afterDispose = getAppearance.Invoke(provider, new object[] { 0, Color.white });
            Assert.That(appearanceType.GetField("CosmeticId").GetValue(afterDispose), Is.Null);
        }
    }
}
