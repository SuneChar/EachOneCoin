using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class GameUIInputLifecycleTests
    {
        [UnityTest]
        public IEnumerator DeviceChanged_IsSubscribedOnlyWhileGameUIIsEnabled()
        {
            var trackerType = Type.GetType("InputDeviceTracker, Assembly-CSharp", true);
            var uiType = Type.GetType("GameUI, Assembly-CSharp", true);
            var instanceField = trackerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousInstance = instanceField.GetValue(null);
            var root = new GameObject("GameUIInputLifecycleTests");
            root.SetActive(false);
            var tracker = root.AddComponent(trackerType);
            var ui = (Behaviour)root.AddComponent(uiType);
            ui.enabled = false;
            var eventField = trackerType.GetField("DeviceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            int CountSubscriptions()
            {
                var callback = (Delegate)eventField.GetValue(tracker);
                if (callback == null) return 0;
                int count = 0;
                foreach (var item in callback.GetInvocationList()) if (item.Target == ui) count++;
                return count;
            }
            try
            {
                root.SetActive(true);
                yield return null;
                Assert.That(CountSubscriptions(), Is.Zero);
                ui.enabled = true;
                Assert.That(CountSubscriptions(), Is.EqualTo(1));
                ui.enabled = false;
                Assert.That(CountSubscriptions(), Is.Zero);
                ui.enabled = true;
                Assert.That(CountSubscriptions(), Is.EqualTo(1));
            }
            finally
            {
                instanceField.SetValue(null, previousInstance);
                UnityEngine.Object.Destroy(root);
            }
        }
    }
}
