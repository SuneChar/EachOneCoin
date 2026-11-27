using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class LocalCoinCounterViewTests
    {
        private GameObject root;
        private Component view;
        private Type viewType;
        private MethodInfo initMethod;
        private MethodInfo setCoinsMethod;
        private MethodInfo tickMethod;
        private PropertyInfo currentCoinsProp;
        private PropertyInfo targetCoinsProp;
        private PropertyInfo displayedTextProp;
        private PropertyInfo isAnimatingProp;

        [SetUp]
        public void SetUp()
        {
            viewType = Type.GetType("EachOneCoin.Gameplay.LocalCoinCounterView, Assembly-CSharp", true);
            initMethod = viewType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            setCoinsMethod = viewType.GetMethod("SetCoins", new[] { typeof(int), typeof(int), typeof(bool) });
            tickMethod = viewType.GetMethod("Tick", new[] { typeof(float) });

            currentCoinsProp = viewType.GetProperty("CurrentCoins");
            targetCoinsProp = viewType.GetProperty("TargetCoins");
            displayedTextProp = viewType.GetProperty("DisplayedText");
            isAnimatingProp = viewType.GetProperty("IsAnimating");

            root = new GameObject("LocalCoinCounterViewTests", typeof(RectTransform));
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

        private int CurrentCoins => (int)currentCoinsProp.GetValue(view);
        private int TargetCoins => (int)targetCoinsProp.GetValue(view);
        private string DisplayedText => (string)displayedTextProp.GetValue(view);
        private bool IsAnimating => (bool)isAnimatingProp.GetValue(view);
        private RectTransform CapsuleRoot => (RectTransform)root.transform.Find("CapsuleRoot");

        private void SetCoins(int coins, int coinsToWin, bool animate = true)
            => setCoinsMethod.Invoke(view, new object[] { coins, coinsToWin, animate });

        private void Tick(float deltaTime)
            => tickMethod.Invoke(view, new object[] { deltaTime });

        [Test]
        public void Initialize_WithPrefabReferences_PreservesLayoutAndDoesNotCreateChildren()
        {
            var prefabRoot = new GameObject("AuthoredCounter", typeof(RectTransform));
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            try
            {
                var authored = prefabRoot.AddComponent(viewType);
                var capsule = new GameObject("CapsuleRoot", typeof(RectTransform));
                capsule.transform.SetParent(prefabRoot.transform, false);
                var imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
                var textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true);
                var background = new GameObject("Background", typeof(RectTransform));
                background.transform.SetParent(capsule.transform, false);
                var backgroundImage = background.AddComponent(imageType);
                var icon = new GameObject("CoinIcon", typeof(RectTransform));
                icon.transform.SetParent(capsule.transform, false);
                var iconImage = icon.AddComponent(imageType);
                imageType.GetProperty("sprite").SetValue(iconImage, sprite);
                var text = new GameObject("CountText", typeof(RectTransform));
                text.transform.SetParent(capsule.transform, false);
                var textComponent = text.AddComponent(textType);
                var rootRect = (RectTransform)prefabRoot.transform;
                rootRect.anchoredPosition = new Vector2(37f, -61f);
                rootRect.sizeDelta = new Vector2(222f, 77f);
                foreach (var entry in new[] { ("capsuleRoot", (object)capsule.transform), ("backgroundImage", backgroundImage), ("coinIconImage", iconImage), ("countText", textComponent) })
                    viewType.GetField(entry.Item1, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(authored, entry.Item2);

                initMethod.Invoke(authored, new object[] { null });
                initMethod.Invoke(authored, new object[] { null });
                setCoinsMethod.Invoke(authored, new object[] { 7, 10, false });

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(new Vector2(37f, -61f)));
                Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(222f, 77f)));
                Assert.That(prefabRoot.transform.childCount, Is.EqualTo(1));
                Assert.That(capsule.transform.childCount, Is.EqualTo(3));
                Assert.That(imageType.GetProperty("sprite").GetValue(iconImage), Is.SameAs(sprite));
                Assert.That(displayedTextProp.GetValue(authored), Is.EqualTo("07"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SetCoins_FormatsTwoDigitsAndTriggersAnimationOnChange()
        {
            Assert.That(CurrentCoins, Is.EqualTo(0));
            Assert.That(DisplayedText, Is.EqualTo("00"));
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot, Is.Not.Null);

            SetCoins(1, 3, false);
            Assert.That(CurrentCoins, Is.EqualTo(1));
            Assert.That(TargetCoins, Is.EqualTo(3));
            Assert.That(DisplayedText, Is.EqualTo("01"));
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot.localScale, Is.EqualTo(Vector3.one));

            SetCoins(2, 3, true);
            Assert.That(CurrentCoins, Is.EqualTo(2));
            Assert.That(DisplayedText, Is.EqualTo("02"));
            Assert.That(IsAnimating, Is.True);
            Assert.That(CapsuleRoot.localScale.x, Is.GreaterThan(1f));

            Tick(0.10f);
            Assert.That(IsAnimating, Is.True);
            Assert.That(CapsuleRoot.localScale.x, Is.GreaterThan(1.05f));

            Tick(0.20f);
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(CapsuleRoot.anchoredPosition, Is.EqualTo(Vector2.zero));

            SetCoins(12, 15, false);
            Assert.That(DisplayedText, Is.EqualTo("12"));
            Assert.That(IsAnimating, Is.False);
        }

        [Test]
        public void SetCoins_NegativeValueClampsToZero()
        {
            SetCoins(-5, -3, false);
            Assert.That(CurrentCoins, Is.EqualTo(0));
            Assert.That(TargetCoins, Is.EqualTo(0));
            Assert.That(DisplayedText, Is.EqualTo("00"));
            Assert.That(IsAnimating, Is.False);

            SetCoins(2, 3, false);
            SetCoins(-1, 3, true);
            Assert.That(CurrentCoins, Is.EqualTo(0));
            Assert.That(TargetCoins, Is.EqualTo(3));
            Assert.That(DisplayedText, Is.EqualTo("00"));
            Assert.That(IsAnimating, Is.True);

            Tick(0.40f);
            Assert.That(IsAnimating, Is.False);
        }

        [Test]
        public void SetCoins_SameValueDoesNotRestartAnimation()
        {
            SetCoins(2, 3, false);
            Assert.That(IsAnimating, Is.False);

            SetCoins(2, 3, true);
            Assert.That(IsAnimating, Is.False);

            SetCoins(2, 5, true);
            Assert.That(TargetCoins, Is.EqualTo(5));
            Assert.That(DisplayedText, Is.EqualTo("02"));
            Assert.That(IsAnimating, Is.False);

            SetCoins(3, 5, true);
            Assert.That(IsAnimating, Is.True);

            Tick(0.20f);
            Assert.That(IsAnimating, Is.True);

            SetCoins(3, 5, true);
            Tick(0.10f);
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void SetCoins_DecreasingCoinsTriggersLossFlashAndResetsAfterTick()
        {
            SetCoins(3, 5, false);
            Assert.That(DisplayedText, Is.EqualTo("03"));
            Assert.That(IsAnimating, Is.False);

            SetCoins(2, 5, true);
            Assert.That(CurrentCoins, Is.EqualTo(2));
            Assert.That(DisplayedText, Is.EqualTo("02"));
            Assert.That(IsAnimating, Is.True);

            Tick(0.08f);
            Assert.That(IsAnimating, Is.True);
            Assert.That(Mathf.Abs(CapsuleRoot.anchoredPosition.x), Is.GreaterThan(1f));
            Assert.That(CapsuleRoot.localScale.x, Is.LessThan(1f));

            Tick(0.30f);
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(CapsuleRoot.anchoredPosition, Is.EqualTo(Vector2.zero));

            SetCoins(3, 5, true);
            Assert.That(IsAnimating, Is.True);

            SetCoins(3, 5, false);
            Assert.That(IsAnimating, Is.False);
            Assert.That(CapsuleRoot.localScale, Is.EqualTo(Vector3.one));
        }
    }
}
