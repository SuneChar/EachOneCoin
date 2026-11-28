using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class PromptHudPrefabTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        private Component CreateView(string assetName, string typeName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/" + assetName + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            root = UnityEngine.Object.Instantiate(prefab);
            var type = Type.GetType("EachOneCoin.Gameplay." + typeName + ", Assembly-CSharp", true);
            var view = root.GetComponent(type);
            Assert.That(view, Is.Not.Null);
            return view;
        }

        [Test]
        public void CenterBanner_UsesPrefabHierarchyAndKeepsAuthoredRootPosition()
        {
            var view = CreateView("CenterBannerPrompt", "CenterBannerPromptView");
            var rect = (RectTransform)root.transform;
            rect.anchoredPosition = new Vector2(42f, -110f);
            int count = rect.childCount;
            var init = view.GetType().GetMethod("Initialize");
            init.Invoke(view, new object[] { null });
            init.Invoke(view, new object[] { null });
            view.GetType().GetMethod("ShowMineWarning").Invoke(view, new object[] { "南侧" });
            Assert.That(rect.childCount, Is.EqualTo(count));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(42f, -110f)));
            Assert.That(view.GetType().GetProperty("IsVisible").GetValue(view), Is.EqualTo(true));
            Assert.That((string)view.GetType().GetProperty("ActiveTitle").GetValue(view), Does.Contain("南侧"));
        }

        [Test]
        public void EventFeed_PrefabPool_ReusesEntriesAndKeepsOrderWhenFull()
        {
            var view = CreateView("MatchEventFeed", "MatchEventFeedView");
            var type = view.GetType();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("accentBarWidth").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            type.GetMethod("Initialize").Invoke(view, new object[] { null });
            int capacity = (int)type.GetProperty("MaxVisibleEntries").GetValue(view);
            Assert.That(root.transform.childCount, Is.EqualTo(capacity));
            var entryType = Type.GetType("EachOneCoin.Gameplay.MatchEventFeedEntryView, Assembly-CSharp", true);
            Assert.That(root.GetComponentsInChildren(entryType, true).Length, Is.EqualTo(capacity));
            var accentRect = (RectTransform)root.transform.Find("FeedEntry_0/AccentBar");
            Assert.That(accentRect.offsetMax.x - accentRect.offsetMin.x, Is.EqualTo(5f).Within(0.001f));
            var before = Enumerable.Range(0, capacity).Select(i => root.transform.GetChild(i).gameObject).ToArray();
            for (int i = 0; i < capacity + 2; i++)
                type.GetMethod("PushFormatted").Invoke(view, new object[] { "entry" + i, Color.white });
            Assert.That(root.transform.childCount, Is.EqualTo(capacity));
            Assert.That(Enumerable.Range(0, capacity).Select(i => root.transform.GetChild(i).gameObject), Is.EquivalentTo(before));
            Assert.That(type.GetProperty("ActiveEntryCount").GetValue(view), Is.EqualTo(capacity));
            Assert.That(type.GetMethod("GetEntryText").Invoke(view, new object[] { 0 }), Is.EqualTo("entry2"));
        }

        [Test]
        public void ControlPrompts_UsesAuthoredHierarchyAcrossInputModes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Common/ControlPrompts.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = UnityEngine.Object.Instantiate(prefab);
            var type = Type.GetType("ControlPromptImages, Assembly-CSharp", true);
            var view = root.GetComponent(type);
            Assert.That(view, Is.Not.Null);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var icons = (Array)type.GetField("movementIcons", flags).GetValue(view);
            Assert.That(icons.Length, Is.EqualTo(5));
            var row = root.transform.Find("Prompt Row");
            Assert.That(row, Is.Not.Null);
            Assert.That(row.childCount, Is.EqualTo(10));
            var refresh = type.GetMethod("Refresh");
            var imageType = Type.GetType("UnityEngine.UI.RawImage, UnityEngine.UI", true);
            object Texture(object image) => imageType.GetProperty("texture").GetValue(image);
            object Field(string name) => type.GetField(name, flags).GetValue(view);
            string Label(string name) => (string)Field(name).GetType().GetProperty("text").GetValue(Field(name));

            refresh.Invoke(view, new object[] { true, false, false, true });
            Assert.That(Label("movementLabel"), Is.EqualTo("选择"));
            Assert.That(Label("actionLabel"), Is.EqualTo("确认"));
            Assert.That(Texture(icons.GetValue(0)), Is.SameAs(Field("mouseMove")));
            Assert.That(((Component)Field("backIcon")).gameObject.activeSelf, Is.True);
            refresh.Invoke(view, new object[] { true, false, true, false });
            Assert.That(Label("movementLabel"), Is.EqualTo("移动"));
            Assert.That(Label("actionLabel"), Is.EqualTo("暂停"));
            Assert.That(Texture(icons.GetValue(0)), Is.SameAs(Field("keyboardW")));
            Assert.That(Enumerable.Range(0, icons.Length).All(i => ((Component)icons.GetValue(i)).gameObject.activeSelf), Is.True);
            Assert.That(((Component)Field("backIcon")).gameObject.activeSelf, Is.False);
            refresh.Invoke(view, new object[] { true, true, true, true });
            Assert.That(Texture(icons.GetValue(0)), Is.SameAs(Field("xboxStick")));
            Assert.That(Texture(Field("actionIcon")), Is.SameAs(Field("xboxMenu")));
            Assert.That(Enumerable.Range(1, icons.Length - 1).All(i => !((Component)icons.GetValue(i)).gameObject.activeSelf), Is.True);
            refresh.Invoke(view, new object[] { false, false, false, false });
            Assert.That(root.activeSelf, Is.False);
            refresh.Invoke(view, new object[] { true, true, false, false });
            Assert.That(root.activeSelf, Is.True);
            Assert.That(Texture(Field("actionIcon")), Is.SameAs(Field("xboxA")));
            Assert.That(row.childCount, Is.EqualTo(10));
            Assert.That(root.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void HitFrame_UsesPrefabEdgesAndAppliesThicknessAndFade()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/ObstacleHitFrame.prefab");
            Assert.That(asset, Is.Not.Null);
            root = new GameObject("TestHitFrame", typeof(RectTransform));
            root.SetActive(false);
            var frame = UnityEngine.Object.Instantiate(asset, root.transform);
            var imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
            var uiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            var ui = root.AddComponent(uiType);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var images = Array.CreateInstance(imageType, 4);
            for (int i = 0; i < 4; i++)
            {
                var edge = frame.transform.Find("Edge " + i);
                Assert.That(edge, Is.Not.Null);
                var image = edge.GetComponent(imageType);
                Assert.That(image, Is.Not.Null);
                Assert.That(imageType.GetProperty("raycastTarget").GetValue(image), Is.EqualTo(false));
                images.SetValue(image, i);
            }
            uiType.GetField("hitFrame", flags).SetValue(ui, images);
            uiType.GetField("hitFrameThickness", flags).SetValue(ui, 31f);
            uiType.GetMethod("CreateHitFrame", flags).Invoke(ui, null);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            for (int i = 0; i < 4; i++)
            {
                var rect = (RectTransform)frame.transform.GetChild(i);
                Assert.That(i < 2 ? rect.sizeDelta.y : rect.sizeDelta.x, Is.EqualTo(31f));
            }
            uiType.GetField("hitFlashDuration", flags).SetValue(ui, 0.25f);
            uiType.GetField("hitFrameOpacity", flags).SetValue(ui, 0.65f);
            uiType.GetField("hitFlashRemaining", flags).SetValue(ui, 0.125f);
            uiType.GetMethod("UpdateHitFrame", flags).Invoke(ui, null);
            for (int i = 0; i < 4; i++)
            {
                var color = (Color)imageType.GetProperty("color").GetValue(images.GetValue(i));
                Assert.That(color.a, Is.EqualTo(0.325f).Within(0.001f));
            }
        }

        [Test]
        public void NetworkBanner_UsesConfiguredSceneReferencesWithoutDuplicateCreation()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Arena/NetworkStatusBanner.prefab");
            Assert.That(asset, Is.Not.Null);
            root = new GameObject("TestHud", typeof(RectTransform));
            root.SetActive(false);
            var banner = UnityEngine.Object.Instantiate(asset, root.transform);
            var uiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            var ui = root.AddComponent(uiType);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            uiType.GetField("hudPanel", flags).SetValue(ui, root);
            uiType.GetField("networkStatusBannerRoot", flags).SetValue(ui, banner);
            var textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
            var text = banner.transform.Find("StatusText").GetComponent(textType);
            uiType.GetField("networkStatusBannerText", flags).SetValue(ui, text);
            var ensure = uiType.GetMethod("EnsureNetworkStatusBanner", flags);
            ensure.Invoke(ui, null);
            ensure.Invoke(ui, null);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(banner.activeSelf, Is.False);
            Assert.That(uiType.GetField("networkStatusBannerRoot", flags).GetValue(ui), Is.SameAs(banner));
        }
    }
}
