using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class GameplayPrefabTests
    {
        private static GameObject Prefab(string relative)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/" + relative + ".prefab");
            Assert.That(asset, Is.Not.Null, relative);
            return asset;
        }

        [Test]
        public void CoinAndMine_PreserveModelAndBlockerConfiguration()
        {
            var coin = Prefab("Coins/Coin");
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(coin)),
                Is.EqualTo("Assets/Media/Platformer/coin-gold.fbx"));
            var mine = Prefab("Coins/GoldMine");
            var capsule = mine.GetComponent<CapsuleCollider>();
            var blocker = mine.GetComponent<BoxCollider>();
            Assert.That(capsule, Is.Not.Null);
            Assert.That(capsule.enabled, Is.False);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.enabled, Is.True);
            Assert.That(blocker.center, Is.EqualTo(new Vector3(0f, 1.5f, 0f)));
            Assert.That(blocker.size, Is.EqualTo(new Vector3(1.25f, 5f, 1.25f)));
        }

        [Test]
        public void WarningPrefab_UsesExplicitDiscAndDisabledColliders()
        {
            var marker = Prefab("Coins/CoinWarning");
            var type = Type.GetType("EachOneCoin.Gameplay.CoinWarningMarkerView, Assembly-CSharp", true);
            var view = marker.GetComponent(type);
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            var disc = serialized.FindProperty("pulseDisc").objectReferenceValue as Transform;
            Assert.That(disc, Is.Not.Null);
            Assert.That(disc.name, Is.EqualTo("Coin Warning Disc"));
            Assert.That(disc.IsChildOf(marker.transform), Is.True);
            Assert.That(serialized.FindProperty("warningRenderers").arraySize, Is.EqualTo(2));
            foreach (var collider in marker.GetComponentsInChildren<Collider>(true)) Assert.That(collider.enabled, Is.False);
        }

        [TestCase("MovingObstacle")]
        [TestCase("StaticObstacle")]
        public void ObstaclePrefab_PreservesLethalTriggerReference(string name)
        {
            var prefab = Prefab("Obstacles/" + name);
            var lethalType = Type.GetType("LethalObstacle, Assembly-CSharp", true);
            var lethal = prefab.GetComponent(lethalType);
            Assert.That(lethal, Is.Not.Null);
            var contact = new SerializedObject(lethal).FindProperty("lethalContact").objectReferenceValue as Component;
            Assert.That(contact, Is.Not.Null);
            Assert.That(contact.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(contact.GetComponent<Collider>().isTrigger, Is.True);
        }

        [Test]
        public void SpikeTrapPrefab_PreservesVisualAndProgressBindings()
        {
            var prefab = Prefab("Obstacles/SpikeTrap");
            var trapType = Type.GetType("TimedSpikeTrap, Assembly-CSharp", true);
            var trap = prefab.GetComponent(trapType);
            var serialized = new SerializedObject(trap);
            var visual = serialized.FindProperty("visual").objectReferenceValue as GameObject;
            var spikes = serialized.FindProperty("spikes").objectReferenceValue as Transform;
            Assert.That(visual, Is.Not.Null);
            Assert.That(spikes.IsChildOf(visual.transform), Is.True);
            var progressType = Type.GetType("GroundTrapProgressView, Assembly-CSharp", true);
            var progress = prefab.GetComponentInChildren(progressType, true);
            Assert.That(new SerializedObject(progress).FindProperty("trap").objectReferenceValue, Is.SameAs(trap));
        }
    }
}
