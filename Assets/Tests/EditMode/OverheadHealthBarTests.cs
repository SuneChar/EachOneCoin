using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class OverheadHealthBarTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private Type healthBarType;
        private Type nameplateType;
        private Type imageType;

        [SetUp]
        public void SetUp()
        {
            healthBarType = Type.GetType("EachOneCoin.Gameplay.SegmentedHealthBarView, Assembly-CSharp", true);
            nameplateType = Type.GetType("EachOneCoin.Gameplay.CharacterNameplate, Assembly-CSharp", true);
            imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();
        }

        private GameObject CreateTrackedObject(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            createdObjects.Add(go);
            return go;
        }

        private Component CreateStandaloneBar(float width = 160f, float height = 16f)
        {
            var go = CreateTrackedObject("StandaloneHealthBar", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            return go.AddComponent(healthBarType);
        }

        [Test]
        public void SegmentedHealthBar_InitializesThreeFullGreenSegmentsWithExpectedGeometry()
        {
            var bar = CreateStandaloneBar(160f, 16f);
            var setHealth = healthBarType.GetMethod("SetHealth");
            var getSegmentState = healthBarType.GetMethod("GetSegmentState");
            var getSegmentColor = healthBarType.GetMethod("GetSegmentColor");

            setHealth.Invoke(bar, new object[] { 3, 3, 3, true });

            Assert.That(healthBarType.GetProperty("SegmentCount").GetValue(bar), Is.EqualTo(3));

            var frameImage = bar.GetComponent(imageType);
            Assert.That(frameImage, Is.Not.Null);
            Assert.That(imageType.GetProperty("raycastTarget").GetValue(frameImage), Is.False);
            Assert.That(imageType.GetProperty("sprite").GetValue(frameImage), Is.Null);
            var frameColor = (Color)imageType.GetProperty("color").GetValue(frameImage);
            Assert.That(frameColor.r, Is.EqualTo(0.075f).Within(0.005f));
            Assert.That(frameColor.g, Is.EqualTo(0.090f).Within(0.005f));
            Assert.That(frameColor.b, Is.EqualTo(0.122f).Within(0.005f));

            Assert.That((float)healthBarType.GetProperty("FrameThickness").GetValue(bar), Is.EqualTo(3f).Within(0.001f));

            const float expectedWidth = (160f - 6f - 6f) / 3f;
            const float expectedHeight = 10f;
            for (int i = 0; i < 3; i++)
            {
                Transform seg = bar.transform.Find("HealthSegment_" + i);
                Assert.That(seg, Is.Not.Null);
                var segImg = seg.GetComponent(imageType);
                Assert.That(segImg, Is.Not.Null);
                Assert.That(imageType.GetProperty("enabled").GetValue(segImg), Is.True);
                Assert.That(imageType.GetProperty("raycastTarget").GetValue(segImg), Is.False);
                Assert.That(imageType.GetProperty("sprite").GetValue(segImg), Is.Null);

                var segRect = (RectTransform)seg;
                Assert.That(segRect.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.05f));
                Assert.That(segRect.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.05f));
                Assert.That(segRect.anchoredPosition.x, Is.EqualTo(3f + i * (expectedWidth + 3f)).Within(0.05f));
                Assert.That(segRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f));
                Assert.That(segRect.anchorMin.y, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(segRect.anchorMax.y, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(segRect.pivot.y, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(getSegmentState.Invoke(bar, new object[] { i }).ToString(), Is.EqualTo("Full"));

                var color = (Color)getSegmentColor.Invoke(bar, new object[] { i });
                Assert.That(color.g, Is.GreaterThan(color.r + 0.4f));
                Assert.That(color.g, Is.GreaterThan(color.b + 0.3f));
            }
        }

        [Test]
        public void SegmentedHealthBar_RendersFiveHealthStatesMatchingParticipantStrip()
        {
            var bar = CreateStandaloneBar(160f, 16f);
            var setHealth = healthBarType.GetMethod("SetHealth");
            var getState = healthBarType.GetMethod("GetSegmentState");
            var getColor = healthBarType.GetMethod("GetSegmentColor");

            setHealth.Invoke(bar, new object[] { 3, 3, 3, true });
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Full"));

            setHealth.Invoke(bar, new object[] { 2, 3, 3, false });
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Dim"));

            setHealth.Invoke(bar, new object[] { 1, 2, 3, false });
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Dim"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Broken"));

            Color fullColor = (Color)getColor.Invoke(bar, new object[] { 0 });
            Color dimColor = (Color)getColor.Invoke(bar, new object[] { 1 });
            Color brokenColor = (Color)getColor.Invoke(bar, new object[] { 2 });

            Assert.That(fullColor.g, Is.GreaterThan(fullColor.r + 0.4f));
            Assert.That(dimColor.g, Is.GreaterThan(dimColor.r));
            Assert.That(dimColor.g, Is.GreaterThan(dimColor.b));
            Assert.That(brokenColor.r, Is.GreaterThan(brokenColor.g + 0.4f));
            Assert.That(brokenColor.r, Is.GreaterThan(brokenColor.b + 0.4f));

            setHealth.Invoke(bar, new object[] { 0, 1, 3, false });
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Dim"));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Broken"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Broken"));

            setHealth.Invoke(bar, new object[] { 0, 0, 3, false });
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Broken"));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Broken"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Broken"));

            var ex = Assert.Throws<TargetInvocationException>(() => getState.Invoke(bar, new object[] { 3 }));
            Assert.That(ex.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SegmentedHealthBar_DirtyCheckReusesPoolAndClampsInvalidValues()
        {
            var bar = CreateStandaloneBar(160f, 16f);
            var setHealth = healthBarType.GetMethod("SetHealth");
            var getState = healthBarType.GetMethod("GetSegmentState");

            setHealth.Invoke(bar, new object[] { 2, 3, 3, false });
            int countAfterFirst = bar.transform.childCount;
            setHealth.Invoke(bar, new object[] { 2, 3, 3, false });
            Assert.That(bar.transform.childCount, Is.EqualTo(countAfterFirst));
            Assert.That(healthBarType.GetProperty("LastHealth").GetValue(bar), Is.EqualTo(2));

            // Negative current clamps to 0.
            setHealth.Invoke(bar, new object[] { -5, 3, 3, false });
            Assert.That(healthBarType.GetProperty("LastHealth").GetValue(bar), Is.EqualTo(0));
            Assert.That(getState.Invoke(bar, new object[] { 0 }).ToString(), Is.EqualTo("Dim"));

            // Above max clamps to max.
            setHealth.Invoke(bar, new object[] { 99, 2, 3, false });
            Assert.That(healthBarType.GetProperty("LastHealth").GetValue(bar), Is.EqualTo(2));
            Assert.That(getState.Invoke(bar, new object[] { 1 }).ToString(), Is.EqualTo("Full"));
            Assert.That(getState.Invoke(bar, new object[] { 2 }).ToString(), Is.EqualTo("Broken"));

            // Pool grows to 4 and disables extras when shrinking back to 2 without destroying children.
            setHealth.Invoke(bar, new object[] { 4, 4, 4, false });
            Assert.That(healthBarType.GetProperty("SegmentCount").GetValue(bar), Is.EqualTo(4));
            Assert.That(bar.transform.childCount, Is.EqualTo(4));

            setHealth.Invoke(bar, new object[] { 1, 2, 2, false });
            Assert.That(healthBarType.GetProperty("SegmentCount").GetValue(bar), Is.EqualTo(2));
            Assert.That(bar.transform.childCount, Is.EqualTo(4));
            Assert.That(imageType.GetProperty("enabled").GetValue(bar.transform.Find("HealthSegment_2").GetComponent(imageType)), Is.False);
            Assert.That(imageType.GetProperty("enabled").GetValue(bar.transform.Find("HealthSegment_3").GetComponent(imageType)), Is.False);
        }

        [Test]
        public void Nameplate_HealthBarSitsFlushBelowNameWithMatchingWidthAndIndependentVisibility()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = UnityEngine.Object.Instantiate(prefab);
            createdObjects.Add(root);

            var nameplate = root.GetComponent(nameplateType);
            Assert.That(nameplate, Is.Not.Null);

            var initMethod = nameplateType.GetMethod("Initialize");
            var setHealthBarVisible = nameplateType.GetMethod("SetHealthBarVisible");
            var setHealth = nameplateType.GetMethod("SetHealth");
            var setVisible = nameplateType.GetMethod("SetVisible");
            var isBarVisibleProp = nameplateType.GetProperty("IsHealthBarVisible");

            initMethod.Invoke(nameplate, new object[] { null, 2.1f });

            Transform canvas = root.transform.Find("Character Nameplate");
            Assert.That(canvas, Is.Not.Null);
            Transform barTransform = canvas.Find("HealthBarRoot");
            Assert.That(barTransform, Is.Not.Null);

            var barRect = (RectTransform)barTransform;
            Assert.That(barRect.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(barRect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(barRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(barRect.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(barRect.sizeDelta, Is.EqualTo(new Vector2(0f, 16f)));

            // Default hidden after Initialize while nameplate canvas stays active.
            Assert.That(isBarVisibleProp.GetValue(nameplate), Is.False);
            Assert.That(barTransform.gameObject.activeSelf, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.True);

            // Show and push health; verify rightmost segment aligns to 160px nameplate width minus 3px border.
            setHealthBarVisible.Invoke(nameplate, new object[] { true });
            setHealth.Invoke(nameplate, new object[] { 2, 3, 3, true });
            Assert.That(isBarVisibleProp.GetValue(nameplate), Is.True);
            Assert.That(barTransform.gameObject.activeSelf, Is.True);

            var lastSeg = (RectTransform)barTransform.Find("HealthSegment_2");
            Assert.That(lastSeg, Is.Not.Null);
            Assert.That(lastSeg.anchoredPosition.x + lastSeg.sizeDelta.x, Is.EqualTo(160f - 3f).Within(0.15f));
            Assert.That(lastSeg.sizeDelta.y, Is.EqualTo(10f).Within(0.05f));

            // Hiding health bar keeps nameplate canvas visible.
            setHealthBarVisible.Invoke(nameplate, new object[] { false });
            Assert.That(barTransform.gameObject.activeSelf, Is.False);
            Assert.That(canvas.gameObject.activeSelf, Is.True);

            // Re-enabling health bar and hiding entire nameplate hides whole canvas, then restores cleanly.
            setHealthBarVisible.Invoke(nameplate, new object[] { true });
            setVisible.Invoke(nameplate, new object[] { false });
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            setVisible.Invoke(nameplate, new object[] { true });
            Assert.That(canvas.gameObject.activeSelf, Is.True);
            Assert.That(barTransform.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CharacterPrefab_ReinitializesOverheadHealthBarWithoutDuplicatesAndPreservesNameNodes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/ArenaCharacter.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = UnityEngine.Object.Instantiate(prefab);
            createdObjects.Add(root);

            var nameplate = root.GetComponent(nameplateType);
            var canvas = root.transform.Find("Character Nameplate");
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.Find("Background"), Is.Not.Null);
            Assert.That(canvas.Find("Name"), Is.Not.Null);

            // If Editor setup already authored HealthBarRoot in the prefab, validate its serialized geometry strictly.
            var serializedNameplate = new SerializedObject(nameplate);
            var authoredBar = serializedNameplate.FindProperty("healthBar").objectReferenceValue as Component;
            if (authoredBar != null)
            {
                var authoredRect = (RectTransform)authoredBar.transform;
                Assert.That(authoredBar.transform.parent, Is.SameAs(canvas));
                Assert.That(authoredBar.name, Is.EqualTo("HealthBarRoot"));
                Assert.That(authoredRect.anchorMin, Is.EqualTo(new Vector2(0f, 0f)));
                Assert.That(authoredRect.anchorMax, Is.EqualTo(new Vector2(1f, 0f)));
                Assert.That(authoredRect.pivot, Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(authoredRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(authoredRect.sizeDelta, Is.EqualTo(new Vector2(0f, 16f)));
                var authoredImg = authoredBar.GetComponent(imageType);
                Assert.That(authoredImg, Is.Not.Null);
                Assert.That(imageType.GetProperty("raycastTarget").GetValue(authoredImg), Is.False);
            }

            int rootChildCount = root.transform.childCount;
            var initMethod = nameplateType.GetMethod("Initialize");
            var setHealth = nameplateType.GetMethod("SetHealth");
            initMethod.Invoke(nameplate, new object[] { null, 2.1f });
            initMethod.Invoke(nameplate, new object[] { null, 2.4f });
            setHealth.Invoke(nameplate, new object[] { 2, 3, 3, false });
            setHealth.Invoke(nameplate, new object[] { 2, 3, 3, false });

            Assert.That(root.transform.childCount, Is.EqualTo(rootChildCount));
            Assert.That(canvas.Find("Background"), Is.Not.Null);
            Assert.That(canvas.Find("Name"), Is.Not.Null);

            int barNodeCount = 0;
            for (int i = 0; i < canvas.childCount; i++)
            {
                if (canvas.GetChild(i).name == "HealthBarRoot") barNodeCount++;
            }
            Assert.That(barNodeCount, Is.EqualTo(1));
        }
    }
}
