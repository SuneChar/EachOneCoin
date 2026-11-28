using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class ArenaMatchConfigTests
    {
        private GameObject root;
        private ScriptableObject config;
        private Type configType;
        private Type coordinatorType;
        private Component coordinator;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            configType = Type.GetType("EachOneCoin.Gameplay.ArenaMatchConfig, Assembly-CSharp", true);
            coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            config = ScriptableObject.CreateInstance(configType);
            root = new GameObject("ArenaMatchConfigTests");
            root.SetActive(false);
            coordinator = root.AddComponent(coordinatorType);
            coordinatorType.GetField("mapDefinition", Private).SetValue(coordinator, AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Maps/DefaultArenaMap.asset"));
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        private void SetRules(int coins, float duration)
        {
            configType.GetField("coinsToWin", Private).SetValue(config, coins);
            configType.GetField("durationSeconds", Private).SetValue(config, duration);
        }

        private (int coins, float duration) Resolve(bool network, bool host = false)
        {
            object[] args = { network, host, 0, 0f };
            coordinatorType.GetMethod("ResolveMatchRules", Private).Invoke(coordinator, args);
            return ((int)args[2], (float)args[3]);
        }

        [Test]
        public void DefaultAsset_PreservesExistingRules()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Match/DefaultArenaMatchConfig.asset");
            Assert.That(asset, Is.Not.Null);
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("coinsToWin").intValue, Is.EqualTo(10));
            Assert.That(serialized.FindProperty("durationSeconds").floatValue, Is.EqualTo(60f));
            Assert.That(Resolve(false), Is.EqualTo((10, 60f)));
        }

        [Test]
        public void CustomRules_ApplyToSinglePlayerAndHostButNotUninitializedClient()
        {
            SetRules(6, 40f);
            coordinatorType.GetField("matchConfig", Private).SetValue(coordinator, config);
            Assert.That(Resolve(false), Is.EqualTo((6, 40f)));
            Assert.That(Resolve(true, true), Is.EqualTo((6, 40f)));
            Assert.That(Resolve(true), Is.EqualTo((10, 60f)));
        }

        [TestCase(0, 60f)]
        [TestCase(10, 0f)]
        [TestCase(10, -1f)]
        public void InvalidRules_FallBackToDefaults(int coins, float duration)
        {
            SetRules(coins, duration);
            coordinatorType.GetField("matchConfig", Private).SetValue(coordinator, config);
            LogAssert.Expect(LogType.Warning, "单机对局配置无效，已回退为 60 秒、10 枚金币。");
            Assert.That(Resolve(false), Is.EqualTo((10, 60f)));
        }

        [Test]
        public void HostWithoutConfiguration_IsRejected()
        {
            object[] args = { true, true, 0, 0f };
            Assert.That(coordinatorType.GetMethod("ResolveMatchRules", Private).Invoke(coordinator, args), Is.EqualTo(false));
        }

        [TestCase(0, 60f)]
        [TestCase(10001, 60f)]
        [TestCase(10, 86401f)]
        public void HostWithInvalidOrUnsupportedConfiguration_IsRejected(int coins, float duration)
        {
            SetRules(coins, duration);
            coordinatorType.GetField("matchConfig", Private).SetValue(coordinator, config);
            object[] args = { true, true, 0, 0f };
            Assert.That(coordinatorType.GetMethod("ResolveMatchRules", Private).Invoke(coordinator, args), Is.EqualTo(false));
        }

        [Test]
        public void Validation_WarnsWhenRoundEndsAtFirstCoinEvent()
        {
            SetRules(10, 5f);
            LogAssert.Expect(LogType.Warning, new Regex("最多生成 0 枚"));
            configType.GetMethod("OnValidate", Private).Invoke(config, null);
        }

        [Test]
        public void Validation_WarnsWhenRoundExceedsFixedSchedule()
        {
            SetRules(10, 90f);
            LogAssert.Expect(LogType.Warning, new Regex("第 58 秒后不会继续生成金币"));
            configType.GetMethod("OnValidate", Private).Invoke(config, null);
        }

        [Test]
        public void NonFiniteDuration_IsRejected()
        {
            var method = configType.GetMethod("TryGetRules");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                SetRules(10, invalid);
                object[] args = { 0, 0f };
                Assert.That(method.Invoke(config, args), Is.EqualTo(false));
            }
        }
    }
}
