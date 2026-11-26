using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class GameUIInputLifecycleTests
    {
        [Test]
        public void ArenaUI_GameplayHasNoNavigationPageAndHudPauseIsNotNavigable()
        {
            var uiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var root = new GameObject("ArenaUIFocusTests", typeof(RectTransform));
            root.SetActive(false);
            var ui = root.AddComponent(uiType);
            GameObject hud = null, settings = null;
            foreach (var name in new[] { "hudPanel", "pausePanel", "settingsPanel", "resultPanel" })
            {
                var panel = new GameObject(name, typeof(RectTransform));
                panel.transform.SetParent(root.transform, false);
                uiType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, panel);
                if (name == "hudPanel") hud = panel;
                if (name == "settingsPanel") settings = panel;
            }
            var pause = new GameObject("PauseButton", typeof(RectTransform));
            pause.transform.SetParent(hud.transform, false);
            var button = pause.AddComponent(buttonType);
            try
            {
                root.SetActive(true);
                var navigation = buttonType.GetProperty("navigation").GetValue(button);
                Assert.That(navigation.GetType().GetProperty("mode").GetValue(navigation).ToString(), Is.EqualTo("None"));
                var currentPage = uiType.GetMethod("CurrentNavigationPanel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(currentPage.Invoke(ui, null), Is.Null);
                uiType.GetField("settingsOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, true);
                Assert.That(currentPage.Invoke(ui, null), Is.SameAs(settings));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ArenaUI_DeviceSubscriptionIsRemovedOnDestroy()
        {
            var trackerType = Type.GetType("InputDeviceTracker, Assembly-CSharp", true);
            var uiType = Type.GetType("EachOneCoin.Gameplay.ArenaGameUI, Assembly-CSharp", true);
            var instanceField = trackerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousInstance = instanceField.GetValue(null);
            var root = new GameObject("ArenaUIInputLifecycleTests");
            root.SetActive(false);
            var tracker = root.AddComponent(trackerType);
            var ui = (Behaviour)root.AddComponent(uiType);
            foreach (var name in new[] { "hudPanel", "pausePanel", "settingsPanel", "resultPanel" })
            {
                var panel = new GameObject(name);
                panel.transform.SetParent(root.transform);
                uiType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, panel);
            }
            var eventField = trackerType.GetField("DeviceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            int CountSubscriptions()
            {
                var callback = (Delegate)eventField.GetValue(tracker);
                if (callback == null) return 0;
                int count = 0;
                foreach (var item in callback.GetInvocationList()) if (ReferenceEquals(item.Target, ui)) count++;
                return count;
            }
            try
            {
                root.SetActive(true);
                Assert.That(CountSubscriptions(), Is.EqualTo(1));
                ui.enabled = false;
                Assert.That(CountSubscriptions(), Is.EqualTo(1));
                UnityEngine.Object.DestroyImmediate(ui);
                Assert.That(CountSubscriptions(), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                instanceField.SetValue(null, previousInstance);
            }
        }
    }
}
