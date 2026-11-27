using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class CenterBannerPromptViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo showMineWarningMethod;
        private MethodInfo showCoinsSpawnedPopMethod;
        private MethodInfo showPlayerNearVictoryMethod;
        private MethodInfo showCustomBannerMethod;
        private MethodInfo tickMethod;
        private MethodInfo clearMethod;
        private PropertyInfo isVisibleProp;
        private PropertyInfo activeCategoryProp;
        private PropertyInfo activeTitleProp;
        private PropertyInfo activeSubtitleProp;
        private PropertyInfo isIconPopActiveProp;
        private PropertyInfo queuedCountProp;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.CenterBannerPromptView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            showMineWarningMethod = viewType.GetMethod("ShowMineWarning", new[] { typeof(string) });
            showCoinsSpawnedPopMethod = viewType.GetMethod("ShowCoinsSpawnedPop", new[] { typeof(string), typeof(int) });
            showPlayerNearVictoryMethod = viewType.GetMethod("ShowPlayerNearVictory", new[]
            {
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(Color)
            });
            showCustomBannerMethod = viewType.GetMethod("ShowCustomBanner", new[]
            {
                typeof(string),
                typeof(string),
                typeof(Sprite),
                typeof(bool),
                typeof(float),
                typeof(int),
                typeof(Color?)
            });
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });
            clearMethod = viewType.GetMethod("Clear", Type.EmptyTypes);

            isVisibleProp = viewType.GetProperty("IsVisible");
            activeCategoryProp = viewType.GetProperty("ActiveCategory");
            activeTitleProp = viewType.GetProperty("ActiveTitle");
            activeSubtitleProp = viewType.GetProperty("ActiveSubtitle");
            isIconPopActiveProp = viewType.GetProperty("IsIconPopActive");
            queuedCountProp = viewType.GetProperty("QueuedCount");

            root = new GameObject("CenterBannerPromptViewTests", typeof(RectTransform));
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

        private bool IsVisible => (bool)isVisibleProp.GetValue(view);
        private string ActiveCategory => activeCategoryProp.GetValue(view).ToString();
        private string ActiveTitle => (string)activeTitleProp.GetValue(view);
        private string ActiveSubtitle => (string)activeSubtitleProp.GetValue(view);
        private bool IsIconPopActive => (bool)isIconPopActiveProp.GetValue(view);
        private int QueuedCount => (int)queuedCountProp.GetValue(view);

        private CanvasGroup RootCanvasGroup => root.GetComponent<CanvasGroup>();
        private Transform PopIconContainer => root.transform.Find("ContentRoot/PopIconContainer");

        private void ShowMineWarning(string mineDirectionName)
            => showMineWarningMethod.Invoke(view, new object[] { mineDirectionName });

        private void ShowCoinsSpawnedPop(string mineDirectionName, int count)
            => showCoinsSpawnedPopMethod.Invoke(view, new object[] { mineDirectionName, count });

        private void ShowPlayerNearVictory(string playerName, int coins, int coinsToWin, Color playerColor)
            => showPlayerNearVictoryMethod.Invoke(view, new object[] { playerName, coins, coinsToWin, playerColor });

        private void ShowCustomBanner(
            string title,
            string subtitle = null,
            Sprite icon = null,
            bool emphasizeIconPop = false,
            float duration = 1.8f,
            int priority = 0,
            Color? accentColor = null)
            => showCustomBannerMethod.Invoke(view, new object[]
            {
                title,
                subtitle,
                icon,
                emphasizeIconPop,
                duration,
                priority,
                accentColor
            });

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        private void Clear()
            => clearMethod.Invoke(view, null);

        private static bool ContainsDigit(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i])) return true;
            }
            return false;
        }

        [Test]
        public void Initialize_StartsHiddenWithoutCountdown()
        {
            Assert.That(IsVisible, Is.False);
            Assert.That(ActiveCategory, Is.EqualTo("Custom"));
            Assert.That(ActiveTitle, Is.EqualTo(string.Empty));
            Assert.That(ActiveSubtitle, Is.EqualTo(string.Empty));
            Assert.That(IsIconPopActive, Is.False);
            Assert.That(QueuedCount, Is.EqualTo(0));
            Assert.That(RootCanvasGroup, Is.Not.Null);
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(PopIconContainer, Is.Not.Null);
            Assert.That(PopIconContainer.gameObject.activeSelf, Is.False);

            Tick(1.0f);
            Assert.That(IsVisible, Is.False);
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ShowMineWarning_ShowsDirectionWithoutCountdownAndAutoHidesAfterTick()
        {
            ShowMineWarning("北侧");

            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("MineWarning"));
            Assert.That(ActiveTitle, Is.EqualTo("北侧即将刷新金币"));
            Assert.That(ActiveSubtitle, Is.EqualTo(string.Empty));
            Assert.That(ContainsDigit(ActiveTitle), Is.False);
            Assert.That(ContainsDigit(ActiveSubtitle), Is.False);
            Assert.That(IsIconPopActive, Is.False);
            Assert.That(QueuedCount, Is.EqualTo(0));

            Tick(0.20f);
            Assert.That(IsVisible, Is.True);
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));

            Tick(1.20f);
            Assert.That(IsVisible, Is.True);
            Assert.That(RootCanvasGroup.alpha, Is.LessThan(1f));
            Assert.That(RootCanvasGroup.alpha, Is.GreaterThan(0f));

            Tick(0.25f);
            Assert.That(IsVisible, Is.False);
            Assert.That(ActiveTitle, Is.EqualTo(string.Empty));
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ShowCoinsSpawnedPop_ReplacesWarningAndTriggersBigCoinIconPop()
        {
            ShowMineWarning("北侧");
            Tick(0.20f);
            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("MineWarning"));
            Assert.That(IsIconPopActive, Is.False);

            ShowCoinsSpawnedPop("北侧", 3);

            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("CoinsSpawned"));
            Assert.That(ActiveTitle, Is.EqualTo("北侧已刷新金币！"));
            Assert.That(ActiveSubtitle, Is.EqualTo(string.Empty));
            Assert.That(IsIconPopActive, Is.True);
            Assert.That(QueuedCount, Is.EqualTo(0));
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(PopIconContainer.gameObject.activeSelf, Is.True);

            Tick(0.16f);
            Assert.That(PopIconContainer.localScale.x, Is.GreaterThan(1.15f));

            Tick(1.20f);
            Assert.That(IsVisible, Is.False);
            Assert.That(IsIconPopActive, Is.False);
            Assert.That(PopIconContainer.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ShowPlayerNearVictory_PreemptsLowerPriorityBannerAndFormatsThresholdMessage()
        {
            ShowCoinsSpawnedPop("南侧", 2);
            Tick(0.20f);
            Assert.That(ActiveCategory, Is.EqualTo("CoinsSpawned"));
            Assert.That(IsIconPopActive, Is.True);

            ShowPlayerNearVictory("玩家A", 2, 3, Color.red);

            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("NearVictory"));
            Assert.That(ActiveTitle, Is.EqualTo("玩家A 已获得 2 枚金币，即将获胜！"));
            Assert.That(ActiveSubtitle, Is.EqualTo("目标：3 枚"));
            Assert.That(IsIconPopActive, Is.False);
            Assert.That(PopIconContainer.gameObject.activeSelf, Is.False);
            Assert.That(QueuedCount, Is.EqualTo(0));

            Tick(2.0f);
            Assert.That(IsVisible, Is.True);

            Tick(0.50f);
            Assert.That(IsVisible, Is.False);
        }

        [Test]
        public void ShowCustomBanner_QueuesBehindHigherPriorityAndPlaysAfterFinish()
        {
            ShowMineWarning("西侧");
            Assert.That(ActiveCategory, Is.EqualTo("MineWarning"));
            Assert.That(QueuedCount, Is.EqualTo(0));

            ShowCustomBanner("普通提示", "副标题A", null, false, 1.0f, 1, null);
            Assert.That(ActiveCategory, Is.EqualTo("MineWarning"));
            Assert.That(ActiveTitle, Is.EqualTo("西侧即将刷新金币"));
            Assert.That(QueuedCount, Is.EqualTo(1));

            ShowCustomBanner("重要提示", "副标题B", null, false, 1.0f, 5, Color.cyan);
            Assert.That(ActiveCategory, Is.EqualTo("MineWarning"));
            Assert.That(QueuedCount, Is.EqualTo(2));

            Tick(1.65f);
            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("Custom"));
            Assert.That(ActiveTitle, Is.EqualTo("重要提示"));
            Assert.That(ActiveSubtitle, Is.EqualTo("副标题B"));
            Assert.That(QueuedCount, Is.EqualTo(1));

            Tick(1.0f);
            Assert.That(IsVisible, Is.True);
            Assert.That(ActiveCategory, Is.EqualTo("Custom"));
            Assert.That(ActiveTitle, Is.EqualTo("普通提示"));
            Assert.That(ActiveSubtitle, Is.EqualTo("副标题A"));
            Assert.That(QueuedCount, Is.EqualTo(0));

            Tick(1.0f);
            Assert.That(IsVisible, Is.False);
            Assert.That(ActiveTitle, Is.EqualTo(string.Empty));
            Assert.That(QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void Clear_HidesActiveBannerAndEmptiesQueue()
        {
            ShowCoinsSpawnedPop("东侧", 4);
            ShowCustomBanner("排队提示1", null, null, false, 1.5f, 1, null);
            ShowCustomBanner("排队提示2", null, null, false, 1.5f, 2, null);
            Tick(0.20f);

            Assert.That(IsVisible, Is.True);
            Assert.That(IsIconPopActive, Is.True);
            Assert.That(QueuedCount, Is.EqualTo(2));
            Assert.That(RootCanvasGroup.alpha, Is.GreaterThan(0.9f));

            Clear();

            Assert.That(IsVisible, Is.False);
            Assert.That(IsIconPopActive, Is.False);
            Assert.That(QueuedCount, Is.EqualTo(0));
            Assert.That(ActiveTitle, Is.EqualTo(string.Empty));
            Assert.That(ActiveSubtitle, Is.EqualTo(string.Empty));
            Assert.That(RootCanvasGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(PopIconContainer.gameObject.activeSelf, Is.False);

            Tick(1.0f);
            Assert.That(IsVisible, Is.False);
            Assert.That(QueuedCount, Is.EqualTo(0));
        }
    }
}
