using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MatchClockAndMineViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo setTimeMethod;
        private MethodInfo setMineScheduleMethod;
        private MethodInfo tickMethod;
        private PropertyInfo lastRemainingSecondsProp;
        private PropertyInfo displayedTimeTextProp;
        private PropertyInfo displayedMineTextProp;
        private PropertyInfo displayedCountdownTextProp;
        private PropertyInfo isWarningHighlightedProp;
        private PropertyInfo isMineConnectedIndicatorProp;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.MatchClockAndMineView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            setTimeMethod = viewType.GetMethod("SetTime", new[] { typeof(int) });
            setMineScheduleMethod = viewType.GetMethod("SetMineSchedule", new[]
            {
                typeof(bool),
                typeof(int),
                typeof(string),
                typeof(float),
                typeof(bool),
                typeof(bool)
            });
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });

            lastRemainingSecondsProp = viewType.GetProperty("LastRemainingSeconds");
            displayedTimeTextProp = viewType.GetProperty("DisplayedTimeText");
            displayedMineTextProp = viewType.GetProperty("DisplayedMineText");
            displayedCountdownTextProp = viewType.GetProperty("DisplayedCountdownText");
            isWarningHighlightedProp = viewType.GetProperty("IsWarningHighlighted");
            isMineConnectedIndicatorProp = viewType.GetProperty("IsMineConnectedIndicator");

            root = new GameObject("MatchClockAndMineViewTests", typeof(RectTransform));
            view = root.AddComponent(viewType);
            initMethod.Invoke(view, new object[] { null });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }
        }

        private int LastRemainingSeconds => (int)lastRemainingSecondsProp.GetValue(view);
        private string DisplayedTimeText => (string)displayedTimeTextProp.GetValue(view);
        private string DisplayedMineText => (string)displayedMineTextProp.GetValue(view);
        private string DisplayedCountdownText => (string)displayedCountdownTextProp.GetValue(view);
        private bool IsWarningHighlighted => (bool)isWarningHighlightedProp.GetValue(view);
        private bool IsMineConnectedIndicator => (bool)isMineConnectedIndicatorProp.GetValue(view);

        private void SetTime(int remainingSeconds)
            => setTimeMethod.Invoke(view, new object[] { remainingSeconds });

        private void SetMineSchedule(bool hasUpcoming, int mineIndex, string mineName, float secondsUntilSpawn, bool isWarning, bool isConnected)
            => setMineScheduleMethod.Invoke(view, new object[] { hasUpcoming, mineIndex, mineName, secondsUntilSpawn, isWarning, isConnected });

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        [Test]
        public void SetTime_UpdatesCountdownTextAndClampsNegativeToZero()
        {
            SetTime(100);
            Assert.That(LastRemainingSeconds, Is.EqualTo(100));
            Assert.That(DisplayedTimeText, Is.EqualTo("100s"));

            SetTime(9);
            Tick(0.25f);
            Assert.That(LastRemainingSeconds, Is.EqualTo(9));
            Assert.That(DisplayedTimeText, Is.EqualTo("9s"));

            SetTime(305);
            Assert.That(LastRemainingSeconds, Is.EqualTo(305));
            Assert.That(DisplayedTimeText, Is.EqualTo("305s"));

            SetTime(-12);
            Assert.That(LastRemainingSeconds, Is.EqualTo(0));
            Assert.That(DisplayedTimeText, Is.EqualTo("0s"));
        }

        [Test]
        public void SetMineSchedule_NonWarningShowsPendingAndIntegerCountdown()
        {
            SetMineSchedule(true, 1, "北侧", 12.2f, false, true);

            Assert.That(DisplayedMineText, Is.EqualTo("待定"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("13s"));
            Assert.That(IsWarningHighlighted, Is.False);
            Assert.That(IsMineConnectedIndicator, Is.False);

            SetMineSchedule(true, 0, "南侧", 4.0f, false, true);

            Assert.That(DisplayedMineText, Is.EqualTo("待定"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("4s"));
            Assert.That(IsWarningHighlighted, Is.False);
            Assert.That(IsMineConnectedIndicator, Is.False);
        }

        [Test]
        public void SetMineSchedule_WarningRevealsDirectionConnectedStateAndHighlights()
        {
            SetMineSchedule(true, 0, "南侧", 5.2f, false, true);
            Assert.That(DisplayedMineText, Is.EqualTo("待定"));
            Assert.That(IsWarningHighlighted, Is.False);
            Assert.That(IsMineConnectedIndicator, Is.False);

            SetMineSchedule(true, 0, "南侧", 1.5f, true, true);
            Tick(0.20f);

            Assert.That(DisplayedMineText, Is.EqualTo("南侧"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("1.5s"));
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.True);

            SetMineSchedule(true, 2, "西侧", 0.8f, true, false);

            Assert.That(DisplayedMineText, Is.EqualTo("西侧"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("0.8s"));
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.False);

            SetMineSchedule(true, 3, null, 0.3f, true, true);

            Assert.That(DisplayedMineText, Is.EqualTo("东侧"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("0.3s"));
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.True);
        }

        [Test]
        public void SetMineSchedule_NoUpcomingEventsShowsCompletedState()
        {
            SetMineSchedule(true, 0, "南侧", 1.2f, true, true);
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.True);

            SetMineSchedule(false, 0, "南侧", 0f, true, true);

            Assert.That(DisplayedMineText, Is.EqualTo("已全部产出"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("--"));
            Assert.That(IsWarningHighlighted, Is.False);
            Assert.That(IsMineConnectedIndicator, Is.False);
        }

        [Test]
        public void SetMineSchedule_WarningWithUnknownMineAndResetToPending_UpdatesStateCleanly()
        {
            SetMineSchedule(true, -1, "北侧", 1.2f, true, true);

            Assert.That(DisplayedMineText, Is.EqualTo("待定"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("1.2s"));
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.False);

            SetMineSchedule(true, 1, "北侧", -0.5f, true, true);

            Assert.That(DisplayedMineText, Is.EqualTo("北侧"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("0.0s"));
            Assert.That(IsWarningHighlighted, Is.True);
            Assert.That(IsMineConnectedIndicator, Is.True);

            SetMineSchedule(true, 2, "西侧", 14.3f, false, true);

            Assert.That(DisplayedMineText, Is.EqualTo("待定"));
            Assert.That(DisplayedCountdownText, Is.EqualTo("15s"));
            Assert.That(IsWarningHighlighted, Is.False);
            Assert.That(IsMineConnectedIndicator, Is.False);
        }
    }
}
