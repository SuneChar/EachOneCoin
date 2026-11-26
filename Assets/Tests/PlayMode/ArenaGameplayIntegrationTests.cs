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
        private Component ActiveDisguisedObstacle()
        {
            var type = Type.GetType("EachOneCoin.Gameplay.DisguisedObstacle, Assembly-CSharp", true);
            return coinRoot.Cast<Transform>().Select(t => t.GetComponent(type)).FirstOrDefault(c => c != null && !(bool)Get(c, "Consumed"));
        }
        private void SpawnDisguisedObstacle(GridPosition cell)
        {
            var type = coins.GetType();
            var kind = Enum.Parse(type.GetNestedType("CoinKind", BindingFlags.NonPublic), "DisguisedObstacle");
            type.GetMethod("Spawn", Private).Invoke(coins, new[] { (object)cell, kind });
            Call(coins, "UpdateDisguisedObstacleVisibility");
        }
        private GridPosition ConnectCentralMine()
        {
            var spawn = model.Participants[0].Spawn;
            int x = spawn.X, y = spawn.Y;
            var cut = new GridPosition(x + 1, y);
            while (x < 8) { x++; rules.TryPaint(0, new GridPosition(x, y)); }
            while (y < 9) { y++; rules.TryPaint(0, new GridPosition(x, y)); }
            while (x < 10) { x++; rules.TryPaint(0, new GridPosition(x, y)); }
            return cut;
        }

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
            Call(character, "Teleport", CellToWorld(new GridPosition(10, 2)));
            yield return null;
            Assert.That(Vector2.Distance(new Vector2(shadow.position.x, shadow.position.z),
                new Vector2(character.transform.position.x, character.transform.position.z)), Is.LessThan(0.01f));
            Call(character, "SetActiveState", false);
            Assert.That(shadow.gameObject.activeSelf, Is.False);
            Call(character, "SetActiveState", true);
            Assert.That(shadow.gameObject.activeSelf, Is.True);

            Call(coins, "AdvanceMatch", 5f);
            yield return null;
            var coin = coinRoot.Cast<Transform>().FirstOrDefault(t => t.name == "Random Coin");
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
            var location = CellToWorld(new GridPosition(10, 2));
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
        public IEnumerator RandomCoin_SpawnsAtFiveAndIsReplacedAtFifteen()
        {
            Call(coins, "AdvanceMatch", 4f);
            Assert.That(coinRoot.Cast<Transform>().Count(t => t.name == "Random Coin"), Is.Zero);
            Call(coins, "AdvanceMatch", 1f);
            var first = coinRoot.Cast<Transform>().Single(t => t.name == "Random Coin");
            Call(coins, "AdvanceMatch", 10f);
            yield return null;
            yield return null;
            Assert.That(first == null, Is.True);
            Assert.That(coinRoot.Cast<Transform>().Count(t => t.name == "Random Coin"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisguisedObstacle_SpawnsWithCoinAndReplacesOnNextCycle()
        {
            Call(coins, "AdvanceMatch", 5f);
            var first = ActiveDisguisedObstacle();
            Assert.That(first, Is.Not.Null);
            Assert.That(coinRoot.Cast<Transform>().Count(t => t.name == "Random Coin"), Is.EqualTo(1));
            var real = coinRoot.Cast<Transform>().Single(t => t.name == "Random Coin");
            Assert.That(Vector3.Distance(real.position, first.transform.position), Is.GreaterThan(0.5f));
            Call(coins, "AdvanceMatch", 10f);
            Assert.That(Get(first, "Consumed"), Is.EqualTo(true));
            var next = ActiveDisguisedObstacle();
            Assert.That(next, Is.Not.Null.And.Not.SameAs(first));
            yield return null;
            yield return null;
            Assert.That(first == null, Is.True);
        }

        [UnityTest]
        public IEnumerator DisguisedObstacle_RevealsAtManhattanTwoAndHidesAfterLeaving()
        {
            var cell = new GridPosition(16, 8);
            SpawnDisguisedObstacle(cell);
            var obstacle = ActiveDisguisedObstacle();
            var far = new GridPosition(1, 19);
            for (int i = 0; i < characters.Length; i++) Call(Character(i), "Teleport", CellToWorld(far));
            Call(coins, "UpdateDisguisedObstacleVisibility");
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(false));
            Assert.That(Call(coins, "HasCoin", cell), Is.EqualTo(true));
            Assert.That(Call(match, "IsBlocked", cell), Is.EqualTo(false));
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(cell.X + 1, cell.Y + 2)));
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(false), "Manhattan distance 3 must stay disguised.");
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(cell.X + 1, cell.Y + 1)));
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(true), "Manhattan distance 2 must reveal the obstacle.");
            Assert.That(Call(coins, "HasCoin", cell), Is.EqualTo(false));
            Assert.That(Call(match, "IsBlocked", cell), Is.EqualTo(true));
            var renderer = obstacle.GetComponentInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.black));
            Call(Character(0), "Teleport", CellToWorld(far));
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(false));
            Assert.That(Call(match, "IsBlocked", cell), Is.EqualTo(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisguisedObstacle_ContactDamagesOnceAndProtectionStillConsumesNext()
        {
            var character = Character(0);
            SpawnDisguisedObstacle(new GridPosition(16, 8));
            var first = ActiveDisguisedObstacle();
            Call(character, "ContactDisguisedObstacle", first);
            Assert.That(Get(first, "Consumed"), Is.EqualTo(true));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2));
            Assert.That(model.Participants[0].Coins, Is.Zero);
            Call(Character(1), "ContactDisguisedObstacle", first);
            Assert.That(model.Participants[1].MaxHealth, Is.EqualTo(3), "The same obstacle cannot hit twice.");
            SpawnDisguisedObstacle(new GridPosition(17, 8));
            var second = ActiveDisguisedObstacle();
            Call(character, "ContactDisguisedObstacle", second);
            Assert.That(Get(second, "Consumed"), Is.EqualTo(true));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2), "Protection prevents damage, not disappearance.");
            Assert.That(model.Participants[0].Coins, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisguisedObstacle_MovementContactDoesNotAwardCoin()
        {
            var cell = new GridPosition(16, 1);
            SpawnDisguisedObstacle(cell);
            var obstacle = ActiveDisguisedObstacle();
            var character = Character(0);
            Call(character, "Teleport", CellToWorld(new GridPosition(14, 1)));
            Physics.SyncTransforms();
            Call(character, "Move", Vector3.right * 2f);
            Assert.That(Get(obstacle, "Consumed"), Is.EqualTo(true));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2));
            Assert.That(model.Participants[0].Coins, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisguisedObstacle_NpcTargetsDisguiseButAvoidsRevealedCell()
        {
            foreach (var mine in model.Mines) mine.Advance(5d, new[] { true, false, false, false });
            var cell = new GridPosition(16, 1);
            SpawnDisguisedObstacle(cell);
            var obstacle = ActiveDisguisedObstacle();
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(false));
            var path = new System.Collections.Generic.List<GridPosition>();
            var find = npcs.GetType().GetMethod("FindPath", Private);
            Assert.That(find.Invoke(npcs, new object[] { 1, false, path }), Is.EqualTo(true));
            Assert.That(path.Last(), Is.EqualTo(cell));
            Call(Character(1), "Teleport", CellToWorld(new GridPosition(18, 1)));
            Assert.That(Get(obstacle, "Revealed"), Is.EqualTo(true));
            Assert.That(Call(match, "IsBlocked", cell), Is.EqualTo(true));
            path.Clear();
            Assert.That(find.Invoke(npcs, new object[] { 1, false, path }), Is.EqualTo(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RandomCoin_CanBeCollectedWithoutConnectedPaint()
        {
            Call(coins, "AdvanceMatch", 5f);
            var coin = coinRoot.Cast<Transform>().Single(t => t.name == "Random Coin");
            var cell = (GridPosition)Call(grid, "WorldToCell", coin.position);
            Assert.That(model.Grid.IsConnected(0, cell), Is.False);
            var character = Character(0);
            var controller = character.GetComponent<CharacterController>();
            controller.enabled = false;
            var position = coin.position;
            position.y = character.transform.position.y;
            character.transform.position = position;
            controller.enabled = true;
            Call(coins, "TryCollect", character);
            Assert.That(model.Participants[0].Coins, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mine_ConnectedForFiveSecondsScattersThreeDistinctCoins()
        {
            Assert.That(model.Mines.Count, Is.EqualTo(5));
            Call(coins, "BeginFrame");
            ConnectCentralMine();
            Call(coins, "AdvanceMatch", 1f);
            Assert.That(model.Mines[0].Progress(0), Is.Zero, "A connection made during the frame must not receive time from before it existed.");
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 4f);
            Assert.That(model.Mines[0].Scattered, Is.False);
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 1f);
            Assert.That(model.Mines[0].Scattered, Is.True);
            var positions = coinRoot.Cast<Transform>().Where(t => t.name == "Mine Coin").Select(t => (GridPosition)Call(grid, "WorldToCell", t.position)).ToArray();
            Assert.That(positions.Length, Is.EqualTo(3));
            Assert.That(positions.Distinct().Count(), Is.EqualTo(3));
            Assert.That(positions.All(p => (bool)Call(match, "IsSpawn", p) == false), Is.True);
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 4f);
            Assert.That(coinRoot.Cast<Transform>().Count(t => t.name == "Mine Coin"), Is.EqualTo(3), "Mine coins must remain after the next rotation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedMine_BlocksMovementAndKnockbackWithoutDamage()
        {
            var character = Character(0);
            var mine = model.Mines[0].Position;
            Assert.That((bool)Call(match, "IsBlocked", mine), Is.True);
            Call(character, "Teleport", CellToWorld(new GridPosition(mine.X, mine.Y - 2)));
            Physics.SyncTransforms();
            var start = character.transform.position;
            Call(character, "Move", Vector3.forward * 4f);
            var afterMove = character.transform.position;
            Assert.That(afterMove.z, Is.GreaterThan(start.z));
            Assert.That(afterMove.z, Is.LessThan(CellToWorld(mine).z - 0.5f));
            Assert.That(model.Participants[0].Health, Is.EqualTo(3));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(3));
            Call(character, "Teleport", CellToWorld(new GridPosition(mine.X, mine.Y - 2)));
            Physics.SyncTransforms();
            Call(character, "StartAttackKnockback", Vector3.forward * 6f, 0.35f);
            Call(character, "TickAttackKnockback", 1f);
            Assert.That(character.transform.position.z, Is.LessThan(CellToWorld(mine).z - 0.5f));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedMines_ProduceIndependentlyInSameCycle()
        {
            var spawn = model.Participants[0].Spawn;
            int x = spawn.X, y = spawn.Y;
            while (x < 9) { x++; rules.TryPaint(0, new GridPosition(x, y)); }
            while (y < 10) { y++; rules.TryPaint(0, new GridPosition(x, y)); }
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 5f);
            Assert.That(model.Mines[0].Scattered, Is.True);
            Assert.That(model.Mines[1].Scattered, Is.True);
            Assert.That(model.Mines[2].Scattered, Is.False);
            Assert.That(coinRoot.Cast<Transform>().Count(t => t.name == "Mine Coin"), Is.EqualTo(6));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mines_StayFixedAcrossCyclesAndDoNotResetAtSixty()
        {
            Assert.That(model.Mines.Count, Is.EqualTo(5));
            var positions = model.Mines.Select(m => m.Position).ToArray();
            double initialElapsed = model.Mines[0].Elapsed;
            for (int i = 0; i < 5; i++)
            {
                Call(coins, "BeginFrame");
                Call(coins, "AdvanceMatch", 10f);
                Assert.That(model.Mines.Select(m => m.Position), Is.EqualTo(positions));
                Assert.That(model.Mines.All(m => Math.Abs(m.Elapsed - initialElapsed) < 0.0001), Is.True);
            }
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 10f);
            Assert.That(model.Mines.Select(m => m.Position), Is.EqualTo(positions));
            Assert.That(model.Mines.All(m => Math.Abs(m.Elapsed - 10d) < 0.0001), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mine_CutAndReconnectInSameFrameResetsProgress()
        {
            var cut = ConnectCentralMine();
            Call(coins, "BeginFrame");
            Call(coins, "AdvanceMatch", 3f);
            Assert.That(model.Mines[0].Progress(0), Is.EqualTo(3d).Within(0.0001));
            Call(coins, "BeginFrame");
            Assert.That(rules.TryPaint(1, cut), Is.True);
            Call(coins, "NotifyGridChanged");
            Assert.That(rules.TryPaint(0, cut), Is.True);
            Call(coins, "NotifyGridChanged");
            Call(coins, "AdvanceMatch", 0.1f);
            Assert.That(model.Mines[0].Progress(0), Is.Zero);
            Assert.That(model.Mines[0].Scattered, Is.False);
            yield return null;
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
        public IEnumerator NormalMovement_UsesOnlyOneAxisWhenStartingAndTurning()
        {
            var character = Character(0);
            Call(character, "Teleport", CellToWorld(new GridPosition(2, 2)));
            Physics.SyncTransforms();
            var start = character.transform.position;
            Call(character, "MoveWithVelocity", new Vector3(2f, 0f, 2f), 0.1f);
            var afterFirst = character.transform.position;
            Assert.That(afterFirst.x, Is.GreaterThan(start.x));
            Assert.That(afterFirst.z, Is.EqualTo(start.z).Within(0.001f));
            Call(character, "MoveWithVelocity", Vector3.forward * 2f, 0.1f);
            var afterTurn = character.transform.position;
            Assert.That(afterTurn.x, Is.EqualTo(afterFirst.x).Within(0.001f));
            Assert.That(afterTurn.z, Is.GreaterThan(afterFirst.z));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegionSpeed_TracksCurrentOwnershipAndMultipliesSprint()
        {
            var character = Character(0);
            var cell = new GridPosition(2, 2);
            Call(character, "Teleport", CellToWorld(cell));
            Physics.SyncTransforms();
            Assert.That((float)Get(character, "RegionSpeedMultiplier"), Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(rules.TryPaint(1, cell), Is.True);
            Assert.That((float)Get(character, "RegionSpeedMultiplier"), Is.EqualTo(0.8f).Within(0.001f));
            Call(character, "MoveWithVelocity", Vector3.right * 3f, 0.1f);
            Assert.That(((Vector3)Get(character, "MovementVelocity")).x, Is.EqualTo(2.4f).Within(0.001f));
            Assert.That(rules.TryPaint(0, cell), Is.True);
            Assert.That((float)Get(character, "RegionSpeedMultiplier"), Is.EqualTo(1.2f).Within(0.001f));

            var neutral = new GridPosition(4, 2);
            var controller = character.GetComponent<CharacterController>();
            controller.enabled = false;
            var position = CellToWorld(neutral);
            position.y = character.transform.position.y;
            character.transform.position = position;
            controller.enabled = true;
            Assert.That(model.Grid.OwnerAt(neutral), Is.Null);
            Assert.That((float)Get(character, "RegionSpeedMultiplier"), Is.EqualTo(1f).Within(0.001f));
            Physics.SyncTransforms();
            Call(character, "MoveWithVelocity", Vector3.right * 2f, 0.05f);
            Assert.That(model.Grid.OwnerAt(neutral), Is.EqualTo(0));
            Assert.That((float)Get(character, "RegionSpeedMultiplier"), Is.EqualTo(1.2f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_UsesSameRegionSpeedRules()
        {
            var npc = Character(1);
            var cell = new GridPosition(10, 2);
            Call(npc, "Teleport", CellToWorld(cell));
            Assert.That((float)Get(npc, "RegionSpeedMultiplier"), Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(rules.TryPaint(0, cell), Is.True);
            Assert.That((float)Get(npc, "RegionSpeedMultiplier"), Is.EqualTo(0.8f).Within(0.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_RoutesAroundFixedMineToCoin()
        {
            foreach (var mine in model.Mines) mine.Advance(5d, new[] { true, false, false, false });
            var start = new GridPosition(8, 10);
            var target = new GridPosition(12, 10);
            Call(Character(1), "Teleport", CellToWorld(start));
            Call(coins, "Drop", target, 1);
            Physics.SyncTransforms();
            var path = new System.Collections.Generic.List<GridPosition>();
            bool found = (bool)npcs.GetType().GetMethod("FindPath", Private).Invoke(npcs, new object[] { 1, false, path });
            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.GreaterThan(4));
            Assert.That(path.Last(), Is.EqualTo(target));
            Assert.That(path.All(p => !model.Grid.IsBlocked(p)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_TurnsAtWaypointWithoutDiagonalMovement()
        {
            var spawn = model.Participants[1].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            int dy = spawn.Y < 10 ? 1 : -1;
            var corner = new GridPosition(spawn.X + dx, spawn.Y);
            var target = new GridPosition(spawn.X + dx, spawn.Y + dy);
            Assert.That((bool)Call(match, "IsBlocked", corner), Is.False);
            Assert.That((bool)Call(match, "IsBlocked", target), Is.False);
            Call(coins, "Drop", target, 1);
            var paths = (System.Collections.IList[])npcs.GetType().GetField("paths", Private).GetValue(npcs);
            paths[1].Clear();
            paths[1].Add(corner);
            paths[1].Add(target);
            var npc = Character(1);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (model.Grid.OwnerAt(target) != 1 && Time.realtimeSinceStartup < deadline)
            {
                Vector3 before = npc.transform.position;
                Call(npcs, "Tick", 1, 0.05f);
                Vector3 delta = npc.transform.position - before;
                Assert.That(Mathf.Abs(delta.x) < 0.001f || Mathf.Abs(delta.z) < 0.001f, Is.True, "NPC moved diagonally.");
                yield return null;
            }
            Assert.That(model.Grid.OwnerAt(target), Is.EqualTo(1), "NPC did not reach the cell after turning.");
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
