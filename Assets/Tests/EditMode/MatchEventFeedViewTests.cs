using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MatchEventFeedViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo pushFormattedMethod;
        private MethodInfo tickMethod;
        private MethodInfo clearMethod;
        private MethodInfo getEntryTextMethod;
        private MethodInfo getEntryAlphaMethod;
        private PropertyInfo maxVisibleEntriesProp;
        private PropertyInfo activeEntryCountProp;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.MatchEventFeedView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            pushFormattedMethod = viewType.GetMethod("PushFormatted", new[] { typeof(string), typeof(Color) });
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });
            clearMethod = viewType.GetMethod("Clear", Type.EmptyTypes);
            getEntryTextMethod = viewType.GetMethod("GetEntryText", new[] { typeof(int) });
            getEntryAlphaMethod = viewType.GetMethod("GetEntryAlpha", new[] { typeof(int) });

            maxVisibleEntriesProp = viewType.GetProperty("MaxVisibleEntries");
            activeEntryCountProp = viewType.GetProperty("ActiveEntryCount");

            root = new GameObject("MatchEventFeedViewTests", typeof(RectTransform));
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

        private int MaxVisibleEntries => (int)maxVisibleEntriesProp.GetValue(view);
        private int ActiveEntryCount => (int)activeEntryCountProp.GetValue(view);

        private void PushFormatted(string richText, Color accentColor)
            => pushFormattedMethod.Invoke(view, new object[] { richText, accentColor });

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        private void Clear()
            => clearMethod.Invoke(view, null);

        private string GetEntryText(int index)
            => (string)getEntryTextMethod.Invoke(view, new object[] { index });

        private float GetEntryAlpha(int index)
            => (float)getEntryAlphaMethod.Invoke(view, new object[] { index });

        private int CountActiveChildren()
        {
            int count = 0;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                if (rootTransform.GetChild(i).gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        [Test]
        public void PushFormatted_AddsEntriesInOldestToNewestOrderAndIgnoresBlank()
        {
            Assert.That(MaxVisibleEntries, Is.EqualTo(4));
            Assert.That(ActiveEntryCount, Is.EqualTo(0));
            Assert.That(GetEntryText(0), Is.Null);
            Assert.That(GetEntryAlpha(0), Is.EqualTo(0f).Within(0.001f));

            PushFormatted(null, Color.white);
            PushFormatted(string.Empty, Color.white);
            PushFormatted("   \t ", Color.white);
            Assert.That(ActiveEntryCount, Is.EqualTo(0));

            PushFormatted("Event 1", Color.red);
            PushFormatted("Event 2", Color.green);
            PushFormatted("<b>Event 3</b>", Color.yellow);

            Assert.That(ActiveEntryCount, Is.EqualTo(3));
            Assert.That(GetEntryText(0), Is.EqualTo("Event 1"));
            Assert.That(GetEntryText(1), Is.EqualTo("Event 2"));
            Assert.That(GetEntryText(2), Is.EqualTo("<b>Event 3</b>"));
            Assert.That(GetEntryText(3), Is.Null);
            Assert.That(GetEntryText(-1), Is.Null);
            Assert.That(GetEntryAlpha(0), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetEntryAlpha(1), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetEntryAlpha(2), Is.EqualTo(1f).Within(0.001f));

            var firstRect = (RectTransform)root.transform.GetChild(0);
            var secondRect = (RectTransform)root.transform.GetChild(1);
            var thirdRect = (RectTransform)root.transform.GetChild(2);
            Assert.That(firstRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(secondRect.anchoredPosition.y, Is.LessThan(firstRect.anchoredPosition.y));
            Assert.That(thirdRect.anchoredPosition.y, Is.LessThan(secondRect.anchoredPosition.y));
        }

        [Test]
        public void PushFormatted_ExceedingCapacityEvictsOldestEntry()
        {
            PushFormatted("Event 1", Color.white);
            PushFormatted("Event 2", Color.red);
            PushFormatted("Event 3", Color.green);
            PushFormatted("Event 4", Color.blue);
            PushFormatted("Event 5", Color.yellow);

            Assert.That(ActiveEntryCount, Is.EqualTo(4));
            Assert.That(GetEntryText(0), Is.EqualTo("Event 2"));
            Assert.That(GetEntryText(1), Is.EqualTo("Event 3"));
            Assert.That(GetEntryText(2), Is.EqualTo("Event 4"));
            Assert.That(GetEntryText(3), Is.EqualTo("Event 5"));
            Assert.That(GetEntryText(4), Is.Null);
            Assert.That(root.transform.childCount, Is.EqualTo(4));
            Assert.That(CountActiveChildren(), Is.EqualTo(4));
        }

        [Test]
        public void Tick_FadesOutAndRemovesExpiredEntriesAndReflowsRemaining()
        {
            PushFormatted("First", Color.red);
            Tick(1.5f);
            PushFormatted("Second", Color.cyan);

            Assert.That(ActiveEntryCount, Is.EqualTo(2));
            var secondRect = (RectTransform)root.transform.GetChild(1);
            Assert.That(secondRect.anchoredPosition.y, Is.LessThan(0f));

            Tick(2.3f);
            Assert.That(GetEntryAlpha(0), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetEntryAlpha(1), Is.EqualTo(1f).Within(0.001f));

            Tick(0.4f);
            float midFadeAlpha = GetEntryAlpha(0);
            Assert.That(midFadeAlpha, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(GetEntryAlpha(1), Is.EqualTo(1f).Within(0.001f));

            Tick(0.2f);
            float lateFadeAlpha = GetEntryAlpha(0);
            Assert.That(lateFadeAlpha, Is.GreaterThan(0f));
            Assert.That(lateFadeAlpha, Is.LessThan(midFadeAlpha));

            Tick(0.15f);
            Assert.That(ActiveEntryCount, Is.EqualTo(1));
            Assert.That(GetEntryText(0), Is.EqualTo("Second"));
            Assert.That(GetEntryAlpha(0), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetEntryText(1), Is.Null);
            Assert.That(GetEntryAlpha(1), Is.EqualTo(0f).Within(0.001f));
            Assert.That(CountActiveChildren(), Is.EqualTo(1));
            Assert.That(secondRect.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(secondRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));

            Tick(1.5f);
            Assert.That(ActiveEntryCount, Is.EqualTo(0));
            Assert.That(GetEntryText(0), Is.Null);
            Assert.That(CountActiveChildren(), Is.EqualTo(0));
        }

        [Test]
        public void Clear_HidesAllActiveEntriesImmediately()
        {
            PushFormatted("Alpha", Color.red);
            PushFormatted("Beta", Color.green);
            PushFormatted("Gamma", Color.blue);
            Assert.That(ActiveEntryCount, Is.EqualTo(3));
            Assert.That(CountActiveChildren(), Is.EqualTo(3));

            Clear();

            Assert.That(ActiveEntryCount, Is.EqualTo(0));
            Assert.That(GetEntryText(0), Is.Null);
            Assert.That(GetEntryAlpha(0), Is.EqualTo(0f).Within(0.001f));
            Assert.That(CountActiveChildren(), Is.EqualTo(0));

            PushFormatted("After Clear", Color.yellow);
            Assert.That(ActiveEntryCount, Is.EqualTo(1));
            Assert.That(GetEntryText(0), Is.EqualTo("After Clear"));
            Assert.That(GetEntryAlpha(0), Is.EqualTo(1f).Within(0.001f));
            Assert.That(CountActiveChildren(), Is.EqualTo(1));
        }

        [Test]
        public void Tick_IgnoresInvalidDeltaTimeAndReusedEvictedEntryGetsFreshLifetime()
        {
            PushFormatted("E1", Color.white);
            Tick(4.2f);
            Assert.That(GetEntryAlpha(0), Is.LessThan(1f));

            PushFormatted("E2", Color.white);
            PushFormatted("E3", Color.white);
            PushFormatted("E4", Color.white);
            PushFormatted("E5", Color.white);

            Assert.That(ActiveEntryCount, Is.EqualTo(4));
            Assert.That(GetEntryText(0), Is.EqualTo("E2"));
            Assert.That(GetEntryText(3), Is.EqualTo("E5"));
            Assert.That(GetEntryAlpha(3), Is.EqualTo(1f).Within(0.001f));

            Tick(0f);
            Tick(-1f);
            Tick(float.NaN);
            Tick(float.PositiveInfinity);
            Assert.That(ActiveEntryCount, Is.EqualTo(4));
            Assert.That(GetEntryAlpha(3), Is.EqualTo(1f).Within(0.001f));

            Tick(1.0f);
            Assert.That(ActiveEntryCount, Is.EqualTo(4));
            Assert.That(GetEntryText(3), Is.EqualTo("E5"));
            Assert.That(GetEntryAlpha(3), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
