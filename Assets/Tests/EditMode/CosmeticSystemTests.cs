using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class CosmeticSystemTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;

        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
        private Type profileType;
        private Type catalogType;
        private Type policyType;
        private Type storeType;
        private Type characterType;
        private Type coordinatorType;
        private string storageKey;
        private bool hadSavedPreference;
        private string originalSavedPreference;

        [SetUp]
        public void SetUp()
        {
            profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            catalogType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceCatalog, Assembly-CSharp", true);
            policyType = Type.GetType("EachOneCoin.Gameplay.CosmeticAssignmentPolicy, Assembly-CSharp", true);
            storeType = Type.GetType("EachOneCoin.Gameplay.CosmeticPreferenceStore, Assembly-CSharp", true);
            characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);

            storageKey = (string)storeType.GetProperty("CurrentKey", PrivateStatic).GetValue(null);
            hadSavedPreference = PlayerPrefs.HasKey(storageKey);
            originalSavedPreference = hadSavedPreference ? PlayerPrefs.GetString(storageKey, string.Empty) : string.Empty;
        }

        [TearDown]
        public void TearDown()
        {
            if (hadSavedPreference)
            {
                PlayerPrefs.SetString(storageKey, originalSavedPreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(storageKey);
            }
            PlayerPrefs.Save();

            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();
        }

        private ScriptableObject CreateProfile(string id, string slotName, string displayName)
        {
            var profile = ScriptableObject.CreateInstance(profileType);
            createdObjects.Add(profile);
            var so = new SerializedObject(profile);
            so.FindProperty("appearanceId").stringValue = id;
            so.FindProperty("accessorySlotName").stringValue = slotName;
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private ScriptableObject CreateFiveOutfitCatalog()
        {
            var profiles = new[]
            {
                CreateProfile("slot_0", "Slot 0", "运动帽围巾"),
                CreateProfile("slot_1", "Slot 1", "遮阳帽"),
                CreateProfile("slot_2", "Slot 2", "运动耳机"),
                CreateProfile("slot_3", "Slot 3", "荣誉绶带"),
                CreateProfile("none", "", "无配饰")
            };
            var catalog = ScriptableObject.CreateInstance(catalogType);
            createdObjects.Add(catalog);
            var so = new SerializedObject(catalog);
            var arrayProp = so.FindProperty("profiles");
            arrayProp.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        [Test]
        public void Catalog_ResolvesFiveOutfitsIncludingNoneAndFallsBackOnUnknown()
        {
            var catalog = CreateFiveOutfitCatalog();

            Assert.That(catalogType.GetProperty("Count").GetValue(catalog), Is.EqualTo(5));
            var indexOf = catalogType.GetMethod("IndexOf");
            var resolve = catalogType.GetMethod("ResolveIndexOrDefault");
            var getProfile = catalogType.GetMethod("GetProfile");
            var getId = catalogType.GetMethod("GetId");
            var getDisplayName = catalogType.GetMethod("GetDisplayName");

            Assert.That(indexOf.Invoke(catalog, new object[] { "slot_0" }), Is.EqualTo(0));
            Assert.That(indexOf.Invoke(catalog, new object[] { "slot_2" }), Is.EqualTo(2));
            Assert.That(indexOf.Invoke(catalog, new object[] { "none" }), Is.EqualTo(4));
            Assert.That(indexOf.Invoke(catalog, new object[] { "unknown_id" }), Is.EqualTo(-1));
            Assert.That(indexOf.Invoke(catalog, new object[] { null }), Is.EqualTo(-1));

            Assert.That(resolve.Invoke(catalog, new object[] { "none", 0 }), Is.EqualTo(4));
            Assert.That(resolve.Invoke(catalog, new object[] { "unknown", 2 }), Is.EqualTo(2));
            Assert.That(resolve.Invoke(catalog, new object[] { "unknown", 99 }), Is.EqualTo(0));

            Assert.That(getId.Invoke(catalog, new object[] { 4 }), Is.EqualTo("none"));
            Assert.That(getDisplayName.Invoke(catalog, new object[] { 4 }), Is.EqualTo("无配饰"));
            Assert.That(getProfile.Invoke(catalog, new object[] { -1 }), Is.Null);
            Assert.That(getProfile.Invoke(catalog, new object[] { 5 }), Is.Null);

            var noneProfile = getProfile.Invoke(catalog, new object[] { 4 });
            var firstProfile = getProfile.Invoke(catalog, new object[] { 0 });
            Assert.That(profileType.GetProperty("IsEmptyOutfit").GetValue(noneProfile), Is.True);
            Assert.That(profileType.GetProperty("IsEmptyOutfit").GetValue(firstProfile), Is.False);

            var nameless = CreateProfile("custom_id", "Slot 1", "");
            Assert.That(profileType.GetProperty("DisplayName").GetValue(nameless), Is.EqualTo("custom_id"));
        }

        [Test]
        public void PreferenceStore_IsolatesKeyByDataPathAndRoundTripsSelection()
        {
            var buildKey = storeType.GetMethod("BuildStorageKey", PrivateStatic);
            string keyA = (string)buildKey.Invoke(null, new object[] { "D:/Projects/EachOneCoin/Assets", true });
            string keyALower = (string)buildKey.Invoke(null, new object[] { "d:/projects/eachonecoin/assets", true });
            string keyClone = (string)buildKey.Invoke(null, new object[] { "D:/Projects/EachOneCoin_clone_0/Assets", true });
            string keyPlain = (string)buildKey.Invoke(null, new object[] { "D:/Projects/EachOneCoin/Assets", false });

            Assert.That(keyA, Is.EqualTo(keyALower));
            Assert.That(keyA, Is.Not.EqualTo(keyClone));
            Assert.That(keyPlain, Is.EqualTo("EachOneCoin.AppearanceId"));

            var clear = storeType.GetMethod("Clear", PublicStatic);
            var load = storeType.GetMethod("LoadSelectedAppearanceId", PublicStatic);
            var save = storeType.GetMethod("SaveSelectedAppearanceId", PublicStatic);

            clear.Invoke(null, null);
            Assert.That(load.Invoke(null, new object[] { "slot_0" }), Is.EqualTo("slot_0"));

            save.Invoke(null, new object[] { "none" });
            Assert.That(load.Invoke(null, new object[] { "slot_0" }), Is.EqualTo("none"));

            save.Invoke(null, new object[] { "   " });
            Assert.That(load.Invoke(null, new object[] { "slot_0" }), Is.EqualTo("none"));

            save.Invoke(null, new object[] { " slot_3 " });
            Assert.That(load.Invoke(null, new object[] { "slot_0" }), Is.EqualTo("slot_3"));

            clear.Invoke(null, null);
            Assert.That(load.Invoke(null, new object[] { "slot_1" }), Is.EqualTo("slot_1"));
        }

        [Test]
        public void AssignmentPolicy_FiveOutfits_NpcsPreferUnusedAndHumansMayDuplicate()
        {
            var sanitize = policyType.GetMethod("SanitizeIndex", PublicStatic);
            var resolve = policyType.GetMethod("ResolveMatchAppearances", PublicStatic);
            byte unspecified = (byte)policyType.GetField("UnspecifiedIndex", PublicStatic).GetValue(null);

            Assert.That(sanitize.Invoke(null, new object[] { (byte)4, 5, 0 }), Is.EqualTo((byte)4));
            Assert.That(sanitize.Invoke(null, new object[] { unspecified, 5, 2 }), Is.EqualTo((byte)2));
            Assert.That(sanitize.Invoke(null, new object[] { (byte)99, 5, 3 }), Is.EqualTo((byte)3));
            Assert.That(sanitize.Invoke(null, new object[] { (byte)99, 2, 3 }), Is.EqualTo((byte)1));
            Assert.That(sanitize.Invoke(null, new object[] { (byte)1, 0, 2 }), Is.EqualTo((byte)0));

            // Single-player: human picks "none" (4), 3 NPCs get 0, 1, 2 (all accessorized, no duplicates).
            var output = new byte[4];
            resolve.Invoke(null, new object[]
            {
                5, (byte)1, (byte)15,
                new byte[] { 4, unspecified, unspecified, unspecified },
                output,
                true
            });
            Assert.That(output, Is.EqualTo(new byte[] { 4, 0, 1, 2 }));

            // Single-player: human picks slot_2 (2), 3 NPCs get 0, 1, 3.
            resolve.Invoke(null, new object[]
            {
                5, (byte)1, (byte)15,
                new byte[] { 2, unspecified, unspecified, unspecified },
                output,
                true
            });
            Assert.That(output, Is.EqualTo(new byte[] { 2, 0, 1, 3 }));

            // Multiplayer: 2 humans both choose "none" (duplicates allowed), 2 NPCs get 0 and 1.
            resolve.Invoke(null, new object[]
            {
                5, (byte)0b0011, (byte)0b1111,
                new byte[] { 4, 4, unspecified, unspecified },
                output,
                true
            });
            Assert.That(output, Is.EqualTo(new byte[] { 4, 4, 0, 1 }));

            // Duplicate rejection mode: second human requesting 1 gets deduplicated to 0.
            resolve.Invoke(null, new object[]
            {
                5, (byte)0b0011, (byte)0b1111,
                new byte[] { 1, 1, unspecified, unspecified },
                output,
                false
            });
            Assert.That(output, Is.EqualTo(new byte[] { 1, 0, 2, 3 }));
        }

        [Test]
        public void MatchCoordinator_AppliesCatalogAndNoneOutfitAndRaisesEvent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);

            var root = new GameObject("CosmeticCoordinatorTest");
            root.SetActive(false);
            createdObjects.Add(root);

            var coordinator = root.AddComponent(coordinatorType);
            var characters = Array.CreateInstance(characterType, 4);
            var characterObjects = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                var inst = UnityEngine.Object.Instantiate(prefab, root.transform);
                createdObjects.Add(inst);
                characterObjects[i] = inst;
                characters.SetValue(inst.GetComponent(characterType), i);
            }

            coordinatorType.GetProperty("Characters").GetSetMethod(true).Invoke(coordinator, new object[] { characters });

            // Without catalog assigned, falls back to ArenaCharacter's 4 configured profiles.
            Assert.That(coordinatorType.GetProperty("AppearanceCatalogCount").GetValue(coordinator), Is.EqualTo(4));

            var catalog = CreateFiveOutfitCatalog();
            coordinatorType.GetField("appearanceCatalog", PrivateInstance).SetValue(coordinator, catalog);
            Assert.That(coordinatorType.GetProperty("AppearanceCatalogCount").GetValue(coordinator), Is.EqualTo(5));

            var raisedSlots = new List<int>();
            Action<int> handler = slot => raisedSlots.Add(slot);
            var evt = coordinatorType.GetEvent("ParticipantAppearanceChanged");
            evt.AddEventHandler(coordinator, handler);

            var applyMethod = coordinatorType.GetMethod("ApplyParticipantAppearance");
            var getIndexMethod = coordinatorType.GetMethod("GetParticipantAppearanceIndex");
            var getProfileMethod = coordinatorType.GetMethod("GetParticipantAppearance");
            var getIdMethod = coordinatorType.GetMethod("GetParticipantAppearanceId");

            try
            {
                // Apply "none" (index 4) to slot 0: all 4 accessory slots off, Eye preserved.
                applyMethod.Invoke(coordinator, new object[] { 0, 4 });
                Assert.That(raisedSlots, Is.EqualTo(new[] { 0 }));
                Assert.That(getIndexMethod.Invoke(coordinator, new object[] { 0 }), Is.EqualTo(4));
                Assert.That(getIdMethod.Invoke(coordinator, new object[] { 0 }), Is.EqualTo("none"));
                Assert.That(getProfileMethod.Invoke(coordinator, new object[] { 0 }),
                    Is.SameAs(catalogType.GetMethod("GetProfile").Invoke(catalog, new object[] { 4 })));

                var accessories0 = characterObjects[0].transform.Find("Root/Accessories");
                Assert.That(accessories0, Is.Not.Null);
                for (int s = 0; s < 4; s++)
                {
                    Assert.That(accessories0.Find("Slot " + s).gameObject.activeSelf, Is.False, "Expected Slot " + s + " inactive for none outfit");
                }
                Assert.That(accessories0.Find("Eye").gameObject.activeSelf, Is.True);
                var refCap0 = accessories0.Find("Reference Capsule");
                if (refCap0 != null) Assert.That(refCap0.gameObject.activeSelf, Is.False);

                // Apply index 2 to slot 1: only Slot 2 active.
                applyMethod.Invoke(coordinator, new object[] { 1, 2 });
                Assert.That(getIndexMethod.Invoke(coordinator, new object[] { 1 }), Is.EqualTo(2));
                Assert.That(getIdMethod.Invoke(coordinator, new object[] { 1 }), Is.EqualTo("slot_2"));
                var accessories1 = characterObjects[1].transform.Find("Root/Accessories");
                for (int s = 0; s < 4; s++)
                {
                    Assert.That(accessories1.Find("Slot " + s).gameObject.activeSelf, Is.EqualTo(s == 2));
                }

                // Out-of-range index 99 on slot 2 safely sanitizes to slot index 2.
                applyMethod.Invoke(coordinator, new object[] { 2, 99 });
                Assert.That(getIndexMethod.Invoke(coordinator, new object[] { 2 }), Is.EqualTo(2));
                Assert.That(getIdMethod.Invoke(coordinator, new object[] { 2 }), Is.EqualTo("slot_2"));
            }
            finally
            {
                evt.RemoveEventHandler(coordinator, handler);
            }
        }
    }
}
