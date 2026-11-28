using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class ReplayFlowTests
    {
        private const string ArenaPath = "Assets/Scenes/ArenaScene.unity";
        private Scene arena;
        private string generatedPath;
        private float oldTimeScale;
        private Type selectionType;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            TestSessionGuard.EnsureAvailable(GameArchitecture.Interface.GetModel<MatchSessionModel>());
            Assert.That(SceneManager.GetSceneByPath(ArenaPath).isLoaded, Is.False);
            selectionType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplaySelection, Assembly-CSharp", true);
            yield return SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Additive);
            arena = SceneManager.GetSceneByPath(ArenaPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (selectionType != null) selectionType.GetProperty("Pending").SetValue(null, null);
            if (arena.IsValid() && arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
            Time.timeScale = oldTimeScale;
            if (!string.IsNullOrEmpty(generatedPath) && File.Exists(generatedPath)) File.Delete(generatedPath);
        }

        [UnityTest]
        public IEnumerator FinishedSingleMatch_IsSavedAndCanBePlayedOffline()
        {
            Type matchType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            Type recorderType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayRecorder, Assembly-CSharp", true);
            Type storageType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayStorage, Assembly-CSharp", true);
            Type playerType = Type.GetType("EachOneCoin.Gameplay.Replay.ReplayPlayer, Assembly-CSharp", true);
            var match = arena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(matchType, true)).Single();
            var model = (MatchModel)matchType.GetProperty("Model").GetValue(match);
            var rules = (MatchSystem)matchType.GetProperty("Rules").GetValue(match);
            var recorder = match.GetComponent(recorderType);
            Assert.That(recorder, Is.Not.Null);
            rules.AdvanceTime(model.RemainingTime);
            yield return null;
            generatedPath = (string)recorderType.GetProperty("SavedPath").GetValue(recorder);
            Assert.That(generatedPath, Is.Not.Null.And.Not.Empty);
            object[] args = { generatedPath, null, null };
            Assert.That((bool)storageType.GetMethod("TryLoad").Invoke(null, args), Is.True, args[2] as string);
            object data = args[1];
            yield return SceneManager.UnloadSceneAsync(arena);
            selectionType.GetProperty("Pending").SetValue(null, data);
            yield return SceneManager.LoadSceneAsync(ArenaPath, LoadSceneMode.Additive);
            arena = SceneManager.GetSceneByPath(ArenaPath);
            yield return null;
            match = arena.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren(matchType, true)).Single();
            Assert.That((bool)matchType.GetProperty("IsReplay").GetValue(match), Is.True);
            var player = match.GetComponent(playerType);
            Assert.That(player, Is.Not.Null);
            Assert.That((bool)playerType.GetField("failed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player), Is.False);
            playerType.GetMethod("TogglePause").Invoke(player, null);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            playerType.GetMethod("TogglePause").Invoke(player, null);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
