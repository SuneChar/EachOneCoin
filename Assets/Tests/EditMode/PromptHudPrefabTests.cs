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
