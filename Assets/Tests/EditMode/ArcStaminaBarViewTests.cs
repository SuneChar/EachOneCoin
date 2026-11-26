using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArcStaminaBarViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo setStaminaMethod;
        private MethodInfo setGameplayVisibleMethod;
        private MethodInfo tickVisualMethod;
        private PropertyInfo currentAlphaProp;
        private PropertyInfo displayedRatioProp;
        private PropertyInfo trailRatioProp;
        private PropertyInfo isDangerProp;
        private PropertyInfo mainFillAmountProp;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.ArcStaminaBarView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", Type.EmptyTypes);
            setStaminaMethod = viewType.GetMethod("SetStamina", new[] { typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool) });
            setGameplayVisibleMethod = viewType.GetMethod("SetGameplayVisible", new[] { typeof(bool) });
            tickVisualMethod = viewType.GetMethod("TickVisual", new[] { typeof(float), typeof(float) });

            currentAlphaProp = viewType.GetProperty("CurrentAlpha");
            displayedRatioProp = viewType.GetProperty("DisplayedRatio");
            trailRatioProp = viewType.GetProperty("TrailRatio");
            isDangerProp = viewType.GetProperty("IsDangerState");
            mainFillAmountProp = viewType.GetProperty("MainFillAmount");

            root = new GameObject("ArcStaminaBarViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initMethod.Invoke(view, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        private float CurrentAlpha => (float)currentAlphaProp.GetValue(view);
        private float DisplayedRatio => (float)displayedRatioProp.GetValue(view);
        private float TrailRatio => (float)trailRatioProp.GetValue(view);
        private bool IsDanger => (bool)isDangerProp.GetValue(view);
        private float MainFillAmount => (float)mainFillAmountProp.GetValue(view);

        private void SetStamina(float current, float max, bool exhausted = false, bool smooth = false, bool immediate = false)
            => setStaminaMethod.Invoke(view, new object[] { current, max, exhausted, smooth, immediate });

        private void Tick(float dt, float pulseTime = 0f)
            => tickVisualMethod.Invoke(view, new object[] { dt, pulseTime });

        [Test]
        public void InitialFullStamina_StartsHiddenAndFadesInOnDrain()
        {
            SetStamina(100f, 100f, false, false, true);
            Assert.That(CurrentAlpha, Is.EqualTo(0f).Within(0.001f));

            SetStamina(85f, 100f, false, false, false);
            Tick(0.20f);

            Assert.That(CurrentAlpha, Is.GreaterThan(0.9f));
        }

        [Test]
        public void RecoveringToFull_FadesOutAfterIdleDelay()
        {
            SetStamina(100f, 100f, false, false, true);
            SetStamina(60f, 100f, false, false, false);
            Tick(0.25f);
            Assert.That(CurrentAlpha, Is.EqualTo(1f).Within(0.001f));

            SetStamina(100f, 100f, false, false, false);
            Tick(0.50f);
            Assert.That(CurrentAlpha, Is.EqualTo(1f).Within(0.001f));

            Tick(0.40f);
            Assert.That(CurrentAlpha, Is.LessThan(1f));

            Tick(0.50f);
            Assert.That(CurrentAlpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DrainingStamina_TrailLagsBehindAndCatchesUp()
        {
            SetStamina(100f, 100f, false, false, true);

            SetStamina(60f, 100f, false, false, false);
            Assert.That(DisplayedRatio, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(TrailRatio, Is.EqualTo(1.0f).Within(0.001f));

            Tick(0.05f);
            Assert.That(TrailRatio, Is.EqualTo(1.0f).Within(0.001f));

            Tick(0.40f);
            Assert.That(TrailRatio, Is.LessThan(1.0f));
            Assert.That(TrailRatio, Is.GreaterThan(DisplayedRatio));

            Tick(1.20f);
            Assert.That(TrailRatio, Is.EqualTo(DisplayedRatio).Within(0.001f));
        }

        [Test]
        public void LowOrExhaustedStamina_EntersDangerStateAndMapsExactTickFillAmount()
        {
            SetStamina(60f, 100f, false, false, true);
            Assert.That(IsDanger, Is.False);
            float expectedThreeTiers = Mathf.Lerp(20f / 360f, 340f / 360f, 0.6f);
            Assert.That(MainFillAmount, Is.EqualTo(expectedThreeTiers).Within(0.001f));

            SetStamina(18f, 100f, false, false, false);
            Assert.That(IsDanger, Is.True);

            SetStamina(50f, 100f, true, false, false);
            Assert.That(IsDanger, Is.True);

            SetStamina(0f, 100f, true, false, false);
            Assert.That(MainFillAmount, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SetGameplayVisibleFalse_ImmediatelyHidesBar()
        {
            SetStamina(50f, 100f, false, false, true);
            Assert.That(CurrentAlpha, Is.EqualTo(1f).Within(0.001f));

            setGameplayVisibleMethod.Invoke(view, new object[] { false });
            Assert.That(CurrentAlpha, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
