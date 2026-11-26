using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class HealthHeartsViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo setHealthMethod;
        private MethodInfo tickMethod;
        private MethodInfo getSlotStateMethod;
        private MethodInfo isSlotAnimatingMethod;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.HealthHeartsView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", new[] { typeof(int) });
            setHealthMethod = viewType.GetMethod("SetHealth", new[] { typeof(int), typeof(int), typeof(bool) });
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });
            getSlotStateMethod = viewType.GetMethod("GetSlotState", new[] { typeof(int) });
            isSlotAnimatingMethod = viewType.GetMethod("IsSlotAnimating", new[] { typeof(int) });

            root = new GameObject("HealthHeartsViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initMethod.Invoke(view, new object[] { 3 });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        private string SlotState(int index) => getSlotStateMethod.Invoke(view, new object[] { index }).ToString();
        private bool SlotAnimating(int index) => (bool)isSlotAnimatingMethod.Invoke(view, new object[] { index });
        private void SetHealth(int current, int max, bool animate = true) => setHealthMethod.Invoke(view, new object[] { current, max, animate });
        private void Tick(float dt) => tickMethod.Invoke(view, new object[] { dt });

        [Test]
        public void InitialSync_SetsAllThreeHeartsFullWithoutAnimation()
        {
            SetHealth(3, 3, false);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Full"));
            Assert.That(SlotState(2), Is.EqualTo("Full"));
            Assert.That(SlotAnimating(0), Is.False);
            Assert.That(SlotAnimating(1), Is.False);
            Assert.That(SlotAnimating(2), Is.False);
        }

        [Test]
        public void TakingDamage_BlinksThenDimsOnlyLostHeart()
        {
            SetHealth(3, 3, false);

            SetHealth(2, 3, true);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Full"));
            Assert.That(SlotState(2), Is.EqualTo("Dim"));
            Assert.That(SlotAnimating(0), Is.False);
            Assert.That(SlotAnimating(1), Is.False);
            Assert.That(SlotAnimating(2), Is.True);

            Tick(0.15f);
            Assert.That(SlotAnimating(2), Is.True);

            Tick(0.25f);
            Assert.That(SlotAnimating(2), Is.False);
            Assert.That(SlotState(2), Is.EqualTo("Dim"));
        }

        [Test]
        public void LosingMaxHealthAtFull_ShattersRightmostHeartIntoBroken()
        {
            SetHealth(3, 3, false);

            SetHealth(2, 2, true);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Full"));
            Assert.That(SlotState(2), Is.EqualTo("Broken"));
            Assert.That(SlotAnimating(2), Is.True);

            Tick(0.20f);
            Assert.That(SlotAnimating(2), Is.True);

            Tick(0.50f);
            Assert.That(SlotAnimating(2), Is.False);
            Assert.That(SlotState(2), Is.EqualTo("Broken"));
        }

        [Test]
        public void LosingMaxHealthWhileDamaged_ShattersAlreadyDimmedSlot()
        {
            SetHealth(3, 3, false);
            SetHealth(1, 3, false);

            SetHealth(1, 2, true);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Dim"));
            Assert.That(SlotState(2), Is.EqualTo("Broken"));
            Assert.That(SlotAnimating(0), Is.False);
            Assert.That(SlotAnimating(1), Is.False);
            Assert.That(SlotAnimating(2), Is.True);
        }

        [Test]
        public void RespawnHeal_RestoresOnlyActiveMaxHealthSlotsAndKeepsBrokenSlots()
        {
            SetHealth(0, 2, false);

            SetHealth(2, 2, true);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Full"));
            Assert.That(SlotState(2), Is.EqualTo("Broken"));
            Assert.That(SlotAnimating(0), Is.True);
            Assert.That(SlotAnimating(1), Is.True);
            Assert.That(SlotAnimating(2), Is.False);

            Tick(0.35f);
            Assert.That(SlotAnimating(0), Is.False);
            Assert.That(SlotAnimating(1), Is.False);
        }

        [Test]
        public void ReconnectSilentSync_AppliesStateImmediatelyWithoutTriggeringAnimations()
        {
            SetHealth(3, 3, false);

            SetHealth(1, 2, false);

            Assert.That(SlotState(0), Is.EqualTo("Full"));
            Assert.That(SlotState(1), Is.EqualTo("Dim"));
            Assert.That(SlotState(2), Is.EqualTo("Broken"));
            Assert.That(SlotAnimating(0), Is.False);
            Assert.That(SlotAnimating(1), Is.False);
            Assert.That(SlotAnimating(2), Is.False);
        }
    }
}
