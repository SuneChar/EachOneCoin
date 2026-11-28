using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EachOneCoin.Tests
{
    public class ReplayUiPrefabTests
    {
        [Test]
        public void StartPanel_HasReplayEntryButton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Boot/StartPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var entry = prefab.transform.Find("ReplayButton");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.GetComponent<Button>(), Is.Not.Null);
        }

        [Test]
        public void ReplayPanel_HasAllConfiguredButtons()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Boot/ReplayPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            for (int i = 0; i < 6; i++) Assert.That(prefab.transform.Find("ReplayRow" + i)?.GetComponent<Button>(), Is.Not.Null);
            foreach (var name in new[] { "Previous", "Next", "Back" }) Assert.That(prefab.transform.Find(name)?.GetComponent<Button>(), Is.Not.Null);
        }

        [Test]
        public void ReplayControls_ViewReferencesAreAssigned()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/ReplayControls.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.activeSelf, Is.False);
            var type = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayControlsView, Assembly-CSharp", true);
            var view = prefab.GetComponent(type);
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            foreach (var field in new[] { "statusText", "pauseLabel", "pauseButton", "backButton" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}
