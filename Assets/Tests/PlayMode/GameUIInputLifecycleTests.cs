using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class GameUIInputLifecycleTests
    {
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
