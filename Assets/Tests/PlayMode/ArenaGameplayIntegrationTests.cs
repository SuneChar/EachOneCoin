using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class ArenaGameplayIntegrationTests
    {
        private const string ScenePath = "Assets/Scenes/ArenaScene.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene arena;
        private Component match;
        private MatchModel model;
        private Component grid;
        private Component coins;
        private Component combat;
        private Component npcs;
        private Array characters;
        private float originalTimeScale;
        private UnityEngine.Random.State originalRandomState;
        private FieldInfo trackerInstanceField;
        private object originalTracker;
        private EventSystem originalEventSystem;
        private GameObject originalSelection;
        private bool originalNavigation;
        private bool ownsScene;

        private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        private Component Character(int id) => (Component)characters.GetValue(id);
        private Vector3 CellToWorld(GridPosition cell) => (Vector3)Call(grid, "CellToWorld", cell);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalRandomState = UnityEngine.Random.state;
            var trackerType = Type.GetType("InputDeviceTracker, Assembly-CSharp", true);
            trackerInstanceField = trackerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            originalTracker = trackerInstanceField.GetValue(null);
            originalEventSystem = EventSystem.current;
            if (originalEventSystem != null)
            {
                originalSelection = originalEventSystem.currentSelectedGameObject;
                originalNavigation = originalEventSystem.sendNavigationEvents;
            }
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "Arena test requires an isolated scene.");
            Time.timeScale = 1f;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            arena = SceneManager.GetSceneByPath(ScenePath);
            ownsScene = true;
            var matchType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            match = arena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(matchType, true)).Single();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Get(match, "Model") == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Get(match, "Model"), Is.Not.Null, "Arena match did not initialize within five seconds.");
            ((Behaviour)match).enabled = false;
            model = (MatchModel)Get(match, "Model");
            grid = (Component)Get(match, "Grid");
            coins = (Component)Get(match, "Coins");
            combat = (Component)Get(match, "Combat");
            npcs = (Component)Get(match, "Npcs");
            characters = (Array)Get(match, "Characters");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (ownsScene && arena.IsValid() && arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
            if (trackerInstanceField != null) trackerInstanceField.SetValue(null, originalTracker);
            if (originalEventSystem != null)
            {
                originalEventSystem.sendNavigationEvents = originalNavigation;
                originalEventSystem.SetSelectedGameObject(originalSelection);
            }
            Time.timeScale = originalTimeScale;
            UnityEngine.Random.state = originalRandomState;
        }

        [UnityTest]
        public IEnumerator Attack_DropsCoinAndRespawnsTargetAtOwnSpawn()
        {
            var spawn = model.Participants[1].Spawn;
            Assert.That(model.TryCollectCoin(1, spawn), Is.True);
            var location = CellToWorld(new GridPosition(10, 10));
            Call(Character(0), "Teleport", location);
            Call(Character(1), "Teleport", location + Vector3.right * 0.5f);
            Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                Call(combat, "Attack", 0);
                if (i < 2) Call(combat, "Tick", 1f);
            }
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(model.Participants[1].Coins, Is.Zero);
            Assert.That(coins.transform.Cast<Transform>().Any(t => t.name == "Dropped Coin"), Is.True);
            Call(combat, "Tick", 3f);
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Active));
            var actual = Character(1).transform.position;
            var expected = CellToWorld(spawn);
            Assert.That(Vector2.Distance(new Vector2(actual.x, actual.z), new Vector2(expected.x, expected.z)), Is.LessThan(0.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoinWarning_ProducesCoinAtTheWarnedPosition()
        {
            var warning = coins.transform.Cast<Transform>().FirstOrDefault(t => t.name == "Coin Spawn Warning");
            Assert.That(warning, Is.Not.Null, "No available warning position in the Arena scene.");
            var position = warning.position;
            Call(coins, "Tick", 2f);
            yield return null;
            Assert.That(coins.transform.Cast<Transform>().Any(t => (t.name == "Fixed Coin" || t.name == "Random Coin") &&
                Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(position.x, position.z)) < 0.01f), Is.True);
        }

        [UnityTest]
        public IEnumerator DisconnectedPaint_DarkensAndRestoresAfterReconnect()
        {
            var spawn = model.Participants[0].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var first = new GridPosition(spawn.X + dx, spawn.Y);
            var second = new GridPosition(spawn.X + 2 * dx, spawn.Y);
            Assert.That(model.TryPaint(0, first), Is.True);
            Assert.That(model.TryPaint(0, second), Is.True);
            Call(grid, "Refresh", model.Grid);
            var renderers = (Renderer[,])grid.GetType().GetField("tiles", Private).GetValue(grid);
            var tile = renderers[second.X, second.Y];
            Assert.That(tile, Is.Not.Null);
            var block = new MaterialPropertyBlock();
            tile.GetPropertyBlock(block);
            var connected = block.GetColor("_BaseColor");
            Assert.That(model.TryPaint(1, first), Is.True);
            Call(grid, "Refresh", model.Grid);
            tile.GetPropertyBlock(block);
            var disconnected = block.GetColor("_BaseColor");
            Assert.That(disconnected.r, Is.LessThan(connected.r));
            Assert.That(model.TryPaint(0, first), Is.True);
            Call(grid, "Refresh", model.Grid);
            tile.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(connected));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_MovesTowardCoinAndPaintsNewCell()
        {
            var spawn = model.Participants[1].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var next = new GridPosition(spawn.X + dx, spawn.Y);
            var target = new GridPosition(spawn.X + 2 * dx, spawn.Y);
            Assert.That((bool)Call(match, "IsBlocked", next), Is.False, "Chosen NPC intermediate cell is blocked.");
            Assert.That((bool)Call(match, "IsBlocked", target), Is.False, "Chosen NPC target is blocked.");
            Call(coins, "Drop", target, 1);
            Assert.That((bool)Call(coins, "HasCoin", target), Is.True);
            var character = Character(1);
            var start = character.transform.position;
            Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (model.Grid.OwnerAt(next) != 1 && Time.realtimeSinceStartup < deadline)
            {
                Call(npcs, "Tick", 1, 0.05f);
                yield return null;
            }
            Assert.That(Vector3.Distance(start, character.transform.position), Is.GreaterThan(0.1f), "NPC did not move toward the coin.");
            Assert.That(model.Grid.OwnerAt(next), Is.EqualTo(1), "NPC did not paint the intermediate cell within five seconds.");
        }
    }
}
