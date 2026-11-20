using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ObstacleProximityWarningTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject root;
        private Component match;
        private Component warning;
        private Type warningType;
        private Collider first;
        private Collider second;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ObstacleProximityWarningTests");
            var matchType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            warningType = Type.GetType("EachOneCoin.Gameplay.ObstacleProximityWarning, Assembly-CSharp", true);
            match = root.AddComponent(matchType);
            ((Behaviour)match).enabled = false;
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform);
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.28f; controller.height = 1f; controller.center = Vector3.up * 0.5f;
            warning = player.AddComponent(warningType);
            warningType.GetField("match", Private).SetValue(warning, match);
            warningType.GetField("controller", Private).SetValue(warning, controller);
            ((Behaviour)warning).enabled = false;
            first = CreateHazard("First", 1f);
            second = CreateHazard("Second", 2f);
            Physics.SyncTransforms();
        }

        private Collider CreateHazard(string name, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(x, 0.5f, 0f);
            var collider = go.AddComponent<BoxCollider>();
            go.AddComponent(Type.GetType("LethalObstacle, Assembly-CSharp", true));
            return collider;
        }

        private void Detect() { Physics.SyncTransforms(); warningType.GetMethod("Detect", Private).Invoke(warning, null); }
        private Collider Target => (Collider)warningType.GetField("target", Private).GetValue(warning);

        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(root); }

        [Test]
        public void Detect_SelectsNearestAndKeepsWarningWhenAnotherHazardRemains()
        {
            Detect();
            Assert.That(Target, Is.SameAs(first));
            first.enabled = false;
            Detect();
            Assert.That(Target, Is.SameAs(second));
            second.enabled = false;
            Detect();
            Assert.That(Target, Is.Null);
        }

        [Test]
        public void Detect_IgnoresNonHazardsAndDistantHazards()
        {
            first.enabled = false;
            second.transform.position = new Vector3(10f, 0.5f, 0f);
            var harmless = new GameObject("Harmless");
            harmless.transform.SetParent(root.transform);
            harmless.AddComponent<BoxCollider>();
            Detect();
            Assert.That(Target, Is.Null);
        }

        [Test]
        public void Detect_HysteresisRetainsTargetUntilAnotherIsClearlyCloser()
        {
            Detect();
            second.transform.position = new Vector3(0.95f, 0.5f, 0f);
            Detect();
            Assert.That(Target, Is.SameAs(first));
            second.transform.position = new Vector3(0.7f, 0.5f, 0f);
            Detect();
            Assert.That(Target, Is.SameAs(second));
        }

        [Test]
        public void ClearWarning_RemovesPreviousTarget()
        {
            Detect();
            warningType.GetMethod("ClearWarning").Invoke(warning, null);
            Assert.That(Target, Is.Null);
        }
    }
}
