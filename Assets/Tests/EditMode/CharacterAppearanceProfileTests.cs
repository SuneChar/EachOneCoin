using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class CharacterAppearanceProfileTests
    {
        private GameObject root;
        private UnityEngine.Object temporaryProfile;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (temporaryProfile != null) UnityEngine.Object.DestroyImmediate(temporaryProfile);
        }

        [Test]
        public void CharacterPrefab_UsesFourProfilesAndPreservesNestedAccessories()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = UnityEngine.Object.Instantiate(prefab);
            var type = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var character = root.GetComponent(type);
            var accessories = root.transform.Find("Root/Accessories");
            Assert.That(accessories, Is.Not.Null);
            var serialized = new SerializedObject(character);
            var profiles = serialized.FindProperty("appearanceProfiles");
            Assert.That(profiles.arraySize, Is.EqualTo(4));
            var resolve = type.GetMethod("ResolveAccessorySlotName", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(profiles.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
                Assert.That(accessories.Find("Slot " + i), Is.Not.Null);
                Assert.That(resolve.Invoke(character, new object[] { accessories, i }), Is.EqualTo("Slot " + i));
            }
            Assert.That(root.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(root.transform.Find("Main Camera").GetComponent<Camera>(), Is.Not.Null);
        }

        [Test]
        public void CharacterPrefab_NameplateIsAuthoredAndReinitializesWithoutDuplicates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = UnityEngine.Object.Instantiate(prefab);
            var characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var nameplateType = Type.GetType("EachOneCoin.Gameplay.CharacterNameplate, Assembly-CSharp", true);
            var character = root.GetComponent(characterType);
            var nameplate = root.GetComponent(nameplateType);
            Assert.That(nameplate, Is.Not.Null);
            Assert.That(new SerializedObject(character).FindProperty("nameplate").objectReferenceValue, Is.SameAs(nameplate));
            var canvas = root.transform.Find("Character Nameplate");
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.parent, Is.SameAs(root.transform));
            Assert.That(canvas.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvas.Find("Background"), Is.Not.Null);
            var label = canvas.Find("Name");
            Assert.That(label, Is.Not.Null);
            int childCount = root.transform.childCount;
            var init = nameplateType.GetMethod("Initialize");
            init.Invoke(nameplate, new object[] { null, 2.1f });
            init.Invoke(nameplate, new object[] { null, 2.6f });
            Assert.That(root.transform.childCount, Is.EqualTo(childCount));
            Assert.That(canvas.localPosition.y, Is.EqualTo(2.6f).Within(0.001f));
            nameplateType.GetMethod("SetName").Invoke(nameplate, new object[] { "玩家1" });
            var textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
            var text = label.GetComponent(textType);
            Assert.That(textType.GetProperty("text").GetValue(text), Is.EqualTo("玩家1"));
            nameplateType.GetMethod("SetVisible").Invoke(nameplate, new object[] { false });
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            nameplateType.GetMethod("SetVisible").Invoke(nameplate, new object[] { true });
            Assert.That(canvas.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void MissingOrInvalidProfile_FallsBackToSlotForId()
        {
            root = new GameObject("Character", typeof(CharacterController));
            var type = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var character = root.AddComponent(type);
            var accessories = new GameObject("Accessories").transform;
            accessories.SetParent(root.transform);
            new GameObject("Slot 1").transform.SetParent(accessories);
            var resolve = type.GetMethod("ResolveAccessorySlotName", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolve.Invoke(character, new object[] { accessories, 1 }), Is.EqualTo("Slot 1"));

            var profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            temporaryProfile = ScriptableObject.CreateInstance(profileType);
            var profileSo = new SerializedObject(temporaryProfile);
            profileSo.FindProperty("accessorySlotName").stringValue = "Slot missing";
            profileSo.ApplyModifiedPropertiesWithoutUndo();
            var characterSo = new SerializedObject(character);
            var profiles = characterSo.FindProperty("appearanceProfiles");
            profiles.arraySize = 2;
            profiles.GetArrayElementAtIndex(1).objectReferenceValue = temporaryProfile;
            characterSo.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(resolve.Invoke(character, new object[] { accessories, 1 }), Is.EqualTo("Slot 1"));
            new GameObject("Slot 0").transform.SetParent(accessories);
            profileSo.Update();
            profileSo.FindProperty("accessorySlotName").stringValue = "Slot 0";
            profileSo.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(resolve.Invoke(character, new object[] { accessories, 1 }), Is.EqualTo("Slot 0"));
        }

        [Test]
        public void ApplyAppearance_SupportsSwitchingAndEmptyOutfitPreservesEye()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = UnityEngine.Object.Instantiate(prefab);
            var characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            var character = root.GetComponent(characterType);
            var accessories = root.transform.Find("Root/Accessories");
            Assert.That(accessories, Is.Not.Null);

            var getFallback = characterType.GetMethod("GetFallbackProfile", new[] { typeof(int) });
            Assert.That(getFallback.Invoke(character, new object[] { 0 }), Is.Not.Null);
            Assert.That(getFallback.Invoke(character, new object[] { 3 }), Is.Not.Null);
            Assert.That(getFallback.Invoke(character, new object[] { 4 }), Is.Null);

            var applyByIndex = characterType.GetMethod("ApplyAppearance", new[] { typeof(int) });
            var applyByProfile = characterType.GetMethod("ApplyAppearance", new[] { profileType, typeof(int) });
            var currentIndexProp = characterType.GetProperty("CurrentAppearanceIndex");
            var currentProfileProp = characterType.GetProperty("CurrentAppearanceProfile");

            applyByIndex.Invoke(character, new object[] { 2 });
            Assert.That(currentIndexProp.GetValue(character), Is.EqualTo(2));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(accessories.Find("Slot " + i).gameObject.activeSelf, Is.EqualTo(i == 2));
            }
            Assert.That(accessories.Find("Eye").gameObject.activeSelf, Is.True);
            var reference = accessories.Find("Reference Capsule");
            if (reference != null) Assert.That(reference.gameObject.activeSelf, Is.False);

            // Empty accessorySlotName ("none" outfit) turns off all 4 slots while preserving Eye.
            temporaryProfile = ScriptableObject.CreateInstance(profileType);
            var noneSo = new SerializedObject(temporaryProfile);
            noneSo.FindProperty("appearanceId").stringValue = "none";
            noneSo.FindProperty("accessorySlotName").stringValue = "";
            noneSo.FindProperty("displayName").stringValue = "无配饰";
            noneSo.ApplyModifiedPropertiesWithoutUndo();

            applyByProfile.Invoke(character, new object[] { temporaryProfile, 4 });
            Assert.That(currentIndexProp.GetValue(character), Is.EqualTo(4));
            Assert.That(currentProfileProp.GetValue(character), Is.SameAs(temporaryProfile));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(accessories.Find("Slot " + i).gameObject.activeSelf, Is.False);
            }
            Assert.That(accessories.Find("Eye").gameObject.activeSelf, Is.True);
            if (reference != null) Assert.That(reference.gameObject.activeSelf, Is.False);

            // While character is visually inactive, switching outfit keeps SpriteRenderers disabled.
            characterType.GetMethod("SetActiveState").Invoke(character, new object[] { false });
            applyByIndex.Invoke(character, new object[] { 1 });
            Assert.That(accessories.Find("Slot 1").gameObject.activeSelf, Is.True);
            foreach (var sr in accessories.Find("Slot 1").GetComponentsInChildren<SpriteRenderer>(true))
            {
                Assert.That(sr.enabled, Is.False);
            }
        }
    }
}
