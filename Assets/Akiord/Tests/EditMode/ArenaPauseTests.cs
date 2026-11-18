using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaPauseTests
    {
        private GameObject root;
        private Component coordinator;
        private Type coordinatorType;
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            root = new GameObject("ArenaPauseTests");
            coordinator = root.AddComponent(coordinatorType);
            var match = new MatchModel(2, 2, new[] { new GridPosition(0, 0) }, 3);
            match.Start();
            coordinatorType.GetField("<Model>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, match);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void PauseAndResume_RestorePreviousTimeScaleEvenWhenPauseIsRepeated()
        {
            Time.timeScale = 0.5f;
            coordinatorType.GetMethod("Pause").Invoke(coordinator, null);
            coordinatorType.GetMethod("Pause").Invoke(coordinator, null);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(coordinatorType.GetProperty("IsPaused").GetValue(coordinator), Is.EqualTo(true));
            coordinatorType.GetMethod("Resume").Invoke(coordinator, null);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Assert.That(coordinatorType.GetProperty("IsPaused").GetValue(coordinator), Is.EqualTo(false));
        }

        [Test]
        public void ResumeWithoutPause_DoesNotChangeTimeScale()
        {
            Time.timeScale = 0.75f;
            coordinatorType.GetMethod("Resume").Invoke(coordinator, null);
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
        }
    }
}
