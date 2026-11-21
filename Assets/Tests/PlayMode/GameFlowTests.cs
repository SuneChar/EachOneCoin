using System;
using System.Collections;
using System.Reflection;
using EachOneCoin;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class GameFlowTests
    {
        private Type managerType;
        private GameObject managerObject;
        private Component manager;
        private float originalTimeScale;
        private bool ownsTestState;

        [SetUp]
        public void SetUp()
        {
            managerType = Type.GetType("GameManager, Assembly-CSharp", true);
            Assert.That(UnityEngine.Object.FindObjectsOfType(managerType).Length, Is.Zero,
                "The test requires an isolated scene with no active GameManager.");
            originalTimeScale = Time.timeScale;
            ownsTestState = true;
            GameArchitecture.Interface.Deinit();
            CreateManager();
        }

        [TearDown]
        public void TearDown()
        {
            if (!ownsTestState) return;
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            GameArchitecture.Interface.Deinit();
            Time.timeScale = originalTimeScale;
            ownsTestState = false;
        }

        private void CreateManager()
        {
            managerObject = new GameObject("GameFlowTests Manager");
            managerObject.SetActive(false);
            manager = managerObject.AddComponent(managerType);
            managerType.GetField("coinsToWin", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, 2);
            managerObject.SetActive(true);
        }

        private object Call(string name, params object[] args)
        {
            return managerType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public).Invoke(manager, args);
        }

        private T Get<T>(string name)
        {
            return (T)managerType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(manager);
        }

        private string State => Get<object>("State").ToString();

        [Test]
        public void Damage_OnlyPlayingAndLethalEndsGame()
        {
            int maximum = Get<int>("CurrentHealth");
            Call("TakeDamage", 10);
            Assert.That(Get<int>("CurrentHealth"), Is.EqualTo(maximum));
            Call("StartGame");
            Call("TakeDamage", 25);
            Assert.That(Get<int>("CurrentHealth"), Is.EqualTo(maximum - 25));
            Call("PauseGame");
            Call("TakeDamage", 10);
            Assert.That(Get<int>("CurrentHealth"), Is.EqualTo(maximum - 25));
            Call("ResumeGame");
            Call("TakeDamage", int.MaxValue);
            Assert.That(Get<int>("CurrentHealth"), Is.Zero);
            Assert.That(State, Is.EqualTo("Lost"));
        }

        [Test]
        public void CollectingCoins_WinsAndRejectsFurtherPickups()
        {
            Assert.That((bool)Call("CollectCoin"), Is.False);
            Call("StartGame");
            Assert.That((bool)Call("CollectCoin"), Is.True);
            Assert.That((bool)Call("CollectCoin"), Is.True);
            Assert.That(State, Is.EqualTo("Won"));
            Assert.That(Get<int>("CoinsCollected"), Is.EqualTo(2));
            Assert.That((bool)Call("CollectCoin"), Is.False);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_RestoresPreviousTimeScale()
        {
            Time.timeScale = 0.5f;
            Call("StartGame");
            Call("PauseGame");
            Assert.That(State, Is.EqualTo("Paused"));
            Assert.That(Time.timeScale, Is.Zero);
            Call("ResumeGame");
            Assert.That(State, Is.EqualTo("Playing"));
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewManager_ResetsSessionAfterLoss()
        {
            Call("StartGame");
            Call("CollectCoin");
            Call("TakeDamage", int.MaxValue);
            Assert.That(State, Is.EqualTo("Lost"));
            UnityEngine.Object.DestroyImmediate(managerObject);
            CreateManager();
            Assert.That(State, Is.EqualTo("Waiting"));
            Assert.That(Get<int>("CoinsCollected"), Is.Zero);
            Assert.That(Get<int>("CurrentHealth"), Is.EqualTo(Get<int>("MaxHealth")));
            yield return null;
        }
    }
}
