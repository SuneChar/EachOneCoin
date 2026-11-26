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
        private MatchSystem rules;
        private Component grid;
        private Component coins;
        private Transform coinRoot;
        private Transform warningRoot;
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
        private bool stateCaptured;

        private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
        private Component Character(int id) => (Component)characters.GetValue(id);
        private Vector3 CellToWorld(GridPosition cell) => (Vector3)Call(grid, "CellToWorld", cell);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            stateCaptured = false;
            ownsScene = false;
            TestSessionGuard.EnsureAvailable(GameArchitecture.Interface.GetModel<MatchSessionModel>());
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
            stateCaptured = true;
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
            rules = (MatchSystem)Get(match, "Rules");
            grid = (Component)Get(match, "Grid");
            coins = (Component)Get(match, "Coins");
            coinRoot = (Transform)coins.GetType().GetField("coinRoot", Private).GetValue(coins);
            warningRoot = (Transform)coins.GetType().GetField("warningRoot", Private).GetValue(coins);
            Assert.That(coinRoot, Is.Not.Null, "CoinSpawner coinRoot is not configured.");
            Assert.That(warningRoot, Is.Not.Null, "CoinSpawner warningRoot is not configured.");
            combat = (Component)Get(match, "Combat");
            npcs = (Component)Get(match, "Npcs");
            characters = (Array)Get(match, "Characters");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!stateCaptured) yield break;
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
        public IEnumerator GroundShadows_FollowAndRespectOwnerLifetime()
        {
            var character = Character(0);
            var shadow = character.transform.parent.Find("Ground Shadow");
            Assert.That(shadow, Is.Not.Null);
            Call(character, "Teleport", CellToWorld(new GridPosition(10, 10)));
            yield return null;
            Assert.That(Vector2.Distance(new Vector2(shadow.position.x, shadow.position.z),
                new Vector2(character.transform.position.x, character.transform.position.z)), Is.LessThan(0.01f));
            Call(character, "SetActiveState", false);
            Assert.That(shadow.gameObject.activeSelf, Is.False);
            Call(character, "SetActiveState", true);
            Assert.That(shadow.gameObject.activeSelf, Is.True);

            Call(coins, "Tick", 2f);
            yield return null;
            var coin = coinRoot.Cast<Transform>().FirstOrDefault(t => t.name == "Fixed Coin" || t.name == "Random Coin");
            Assert.That(coin, Is.Not.Null);
            var shadowType = Type.GetType("EachOneCoin.Gameplay.GroundShadow, Assembly-CSharp", true);
            var component = coin.GetComponent(shadowType);
            Assert.That(component, Is.Not.Null);
            var coinShadow = (Transform)shadowType.GetField("shadow", Private).GetValue(component);
            Assert.That(coinShadow, Is.Not.Null);
            var rotation = coinShadow.rotation;
            coin.Rotate(Vector3.up, 90f, Space.World);
            yield return null;
            Assert.That(Quaternion.Angle(rotation, coinShadow.rotation), Is.LessThan(0.01f));
            UnityEngine.Object.Destroy(coin.gameObject);
            yield return null;
            yield return null;
            Assert.That(coinShadow == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Aim_PauseCancelsAndHidesIndicator()
        {
            var character = Character(0);
            Call(character, "SetAttackAim", true, Vector3.forward, 1.1f, 100f);
            match.GetType().GetField("aiming", Private).SetValue(match, true);
            var visual = character.GetType().GetField("attackVisual", Private).GetValue(character);
            var aim = (Transform)visual.GetType().GetField("aim", Private).GetValue(visual);
            Assert.That(aim.gameObject.activeSelf, Is.True);
            Call(match, "Pause");
            try
            {
                Assert.That(match.GetType().GetField("aiming", Private).GetValue(match), Is.EqualTo(false));
                Assert.That(aim.gameObject.activeSelf, Is.False);
            }
            finally { Call(match, "Resume"); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_SectorHitsAllTargetsInsideButNotBehind()
        {
            var origin = CellToWorld(new GridPosition(10, 2));
            Call(Character(0), "Teleport", origin);
            Call(Character(1), "Teleport", origin + Quaternion.Euler(0f, 30f, 0f) * Vector3.right * 0.9f);
            Call(Character(2), "Teleport", origin + Quaternion.Euler(0f, -30f, 0f) * Vector3.right * 0.9f);
            Call(Character(3), "Teleport", origin + Vector3.left * 0.9f);
            Physics.SyncTransforms();
            Call(combat, "Attack", 0, Vector3.right);
            Assert.That(model.Participants[1].Health, Is.EqualTo(2));
            Assert.That(model.Participants[2].Health, Is.EqualTo(2));
            Assert.That(model.Participants[3].Health, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_SectorBoundaryMatchesRangeAndAngle()
        {
            var method = combat.GetType().GetMethod("IsWithinAttack");
            Vector3 direction = Vector3.forward;
            Assert.That(method.Invoke(null, new object[] { Quaternion.Euler(0, 50f, 0) * direction * 1.1f, direction, 1.1f, 100f }), Is.EqualTo(true));
            Assert.That(method.Invoke(null, new object[] { Quaternion.Euler(0, 51f, 0) * direction, direction, 1.1f, 100f }), Is.EqualTo(false));
            Assert.That(method.Invoke(null, new object[] { direction * 1.11f, direction, 1.1f, 100f }), Is.EqualTo(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_KnocksTargetSixCellsWithoutPaintingThePath()
        {
            var attacker = Character(0);
            var target = Character(1);
            Call(attacker, "Teleport", CellToWorld(new GridPosition(2, 2)));
            Call(target, "Teleport", CellToWorld(new GridPosition(3, 2)));
            Physics.SyncTransforms();
            var start = target.transform.position;
            var intermediate = new GridPosition(5, 2);
            var ownerBefore = model.Grid.OwnerAt(intermediate);
            Call(combat, "Attack", 0, Vector3.right);
            Assert.That(model.Participants[1].Health, Is.EqualTo(2));
            Call(target, "TickAttackKnockback", 0.18f);
            Assert.That(model.Grid.OwnerAt(intermediate), Is.EqualTo(ownerBefore));
            Call(target, "TickAttackKnockback", 1f);
            Assert.That(target.transform.position.x - start.x, Is.EqualTo(6f).Within(0.05f));
            Assert.That(model.Grid.OwnerAt(intermediate), Is.EqualTo(ownerBefore));
            Assert.That(model.Grid.OwnerAt(new GridPosition(9, 2)), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_WallStopsKnockbackAndPaintsLandingCell()
        {
            var attacker = Character(0);
            var target = Character(1);
            Call(attacker, "Teleport", CellToWorld(new GridPosition(2, 2)));
            Call(target, "Teleport", CellToWorld(new GridPosition(3, 2)));
            var wall = new GameObject("Test knockback wall", typeof(BoxCollider));
            wall.transform.position = CellToWorld(new GridPosition(6, 2)) + Vector3.up * 0.5f;
            wall.transform.localScale = new Vector3(0.2f, 1f, 2f);
            try
            {
                Physics.SyncTransforms();
                var start = target.transform.position;
                Call(combat, "Attack", 0, Vector3.right);
                Call(target, "TickAttackKnockback", 1f);
                Assert.That(target.transform.position.x - start.x, Is.GreaterThan(0.1f).And.LessThan(3f));
                var landing = (GridPosition)Call(grid, "WorldToCell", target.transform.position);
                Assert.That(model.Grid.OwnerAt(landing), Is.EqualTo(1));
                Assert.That(Get(target, "IsKnockedBack"), Is.EqualTo(true));
            }
            finally { UnityEngine.Object.Destroy(wall); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimeoutFrame_CollectsWinningCoinBeforeSettlement()
        {
            var spawn = model.Participants[0].Spawn;
            for (int i = 0; i < model.CoinsToWin - 1; i++) Assert.That(rules.TryCollectCoin(0, spawn), Is.True);
            var next = new GridPosition(spawn.X + 1, spawn.Y);
            Assert.That((bool)Call(match, "IsBlocked", next), Is.False);
            Assert.That(rules.TryPaint(0, next), Is.True);
            Call(Character(0), "Teleport", CellToWorld(next));
            Call(coins, "Drop", next, 1);
            typeof(MatchModel).GetProperty("RemainingTime").SetValue(model, 0.000001f);
            match.GetType().GetMethod("Update", Private).Invoke(match, null);
            Assert.That(model.Participants[0].Status, Is.EqualTo(ParticipantStatus.Won));
            Assert.That(model.Status, Is.EqualTo(MatchStatus.Finished));
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Lost));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_DropsCoinAndRespawnsTargetAtOwnSpawn()
        {
            var spawn = model.Participants[1].Spawn;
            Assert.That(rules.TryCollectCoin(1, spawn), Is.True);
            var location = CellToWorld(new GridPosition(10, 10));
            Call(Character(0), "Teleport", location);
            Call(Character(1), "Teleport", location + Vector3.right * 0.5f);
            Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                Call(combat, "Attack", 0, Vector3.right);
                if (i < 2) Call(combat, "Tick", 1f);
            }
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(model.Participants[1].Coins, Is.Zero);
            Assert.That(coinRoot.Cast<Transform>().Any(t => t.name == "Dropped Coin"), Is.True);
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
            var warning = warningRoot.Cast<Transform>().FirstOrDefault(t => t.name == "Coin Spawn Warning");
            Assert.That(warning, Is.Not.Null, "No available warning position in the Arena scene.");
            var position = warning.position;
            Call(coins, "Tick", 2f);
            yield return null;
            Assert.That(coinRoot.Cast<Transform>().Any(t => (t.name == "Fixed Coin" || t.name == "Random Coin") &&
                Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(position.x, position.z)) < 0.01f), Is.True);
        }

        [UnityTest]
        public IEnumerator DisconnectedPaint_DarkensAndRestoresAfterReconnect()
        {
            var spawn = model.Participants[0].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var first = new GridPosition(spawn.X + dx, spawn.Y);
            var second = new GridPosition(spawn.X + 2 * dx, spawn.Y);
            Assert.That(rules.TryPaint(0, first), Is.True);
            Assert.That(rules.TryPaint(0, second), Is.True);
            Call(grid, "Refresh", model.Grid);
            var renderers = (Renderer[,])grid.GetType().GetField("tiles", Private).GetValue(grid);
            var tile = renderers[second.X, second.Y];
            Assert.That(tile, Is.Not.Null);
            var block = new MaterialPropertyBlock();
            tile.GetPropertyBlock(block);
            var connected = block.GetColor("_BaseColor");
            Assert.That(rules.TryPaint(1, first), Is.True);
            Call(grid, "Refresh", model.Grid);
            tile.GetPropertyBlock(block);
            var disconnected = block.GetColor("_BaseColor");
            Assert.That(disconnected.r, Is.LessThan(connected.r));
            Assert.That(rules.TryPaint(0, first), Is.True);
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
