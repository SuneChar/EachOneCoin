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
        private int LastMineIndex() => (int)coins.GetType().GetField("lastMineIndex", Private).GetValue(coins);
        private System.Collections.Generic.IEnumerable<Transform> WarningMarkers() => warningRoot.Cast<Transform>().Where(t => t.name == "Coin Warning");
        private System.Collections.Generic.IEnumerable<Transform> MineCoins() => coinRoot.Cast<Transform>().Where(t => t.name == "Mine Coin");
        private GridPosition[] MineCoinCells() => MineCoins().Select(t => (GridPosition)Call(grid, "WorldToCell", t.position)).ToArray();
        private static int Manhattan(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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
        public IEnumerator CharacterAnimation_TracksMovementAndPreservesHiddenSprites()
        {
            var character = Character(0);
            var animator = character.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            var hidden = character.transform.Find("Root/PlayerSprite").GetComponent<SpriteRenderer>();
            Assert.That(hidden.enabled, Is.False);
            Call(character, "SetActiveState", false);
            Call(character, "SetActiveState", true);
            Assert.That(hidden.enabled, Is.False, "Restoring the character must not reveal the old sprite.");
            yield return null;
            Call(character, "ApplyNetworkPosition", character.transform.position + Vector3.right * 0.1f);
            yield return new WaitForEndOfFrame();
            Assert.That(animator.GetBool("Moving"), Is.True);
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(animator.GetBool("Moving"), Is.False);
            Call(character, "Teleport", character.transform.position + Vector3.right * 2f);
            yield return new WaitForEndOfFrame();
            Assert.That(animator.GetBool("Moving"), Is.False, "Teleporting must not start a running animation.");
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
            var coin = coinRoot.Cast<Transform>().FirstOrDefault(t => t.name == "Mine Coin");
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
        public IEnumerator MineSchedule_WarnsThenDropsTwoDispersedCoins()
        {
            Assert.That(model.Mines.Count, Is.EqualTo(4));
            Call(coins, "AdvanceMatch", 3.6f);
            Assert.That(MineCoinCells().Length, Is.Zero, "Coins must not appear before the scheduled event.");
            Assert.That(WarningMarkers().Count(), Is.EqualTo(2), "Both coins of an event are announced in advance.");
            var warnedCells = WarningMarkers().Select(m => (GridPosition)Call(grid, "WorldToCell", m.position)).ToArray();
            int warnedMine = LastMineIndex();
            Assert.That(warnedMine, Is.InRange(0, 3));
            Assert.That(WarningMarkers().All(m => Manhattan((GridPosition)Call(grid, "WorldToCell", m.position), model.Mines[warnedMine]) <= 5), Is.True);
            Call(coins, "AdvanceMatch", 1.4f);
            var cells = MineCoinCells();
            Assert.That(cells.Length, Is.EqualTo(2));
            Assert.That(cells, Is.EquivalentTo(warnedCells), "Coins must appear in the announced cells.");
            Assert.That(cells[0], Is.Not.EqualTo(cells[1]));
            Assert.That(Manhattan(cells[0], cells[1]), Is.GreaterThanOrEqualTo(2), "Coins of one event must not be neighbours.");
            Assert.That(cells.All(c => Manhattan(c, model.Mines[warnedMine]) >= 2), Is.True, "Coins must stay off the cells next to the mine.");
            yield return null;
            Assert.That(WarningMarkers().Count(), Is.Zero, "Warnings clear once the coins spawn.");
        }

        [UnityTest]
        public IEnumerator MineSchedule_PlayerAndNpcEnteringWarnings_DoesNotMoveEitherCoin()
        {
            Call(coins, "AdvanceMatch", 3.6f);
            var warnedCells = WarningMarkers().Select(m => (GridPosition)Call(grid, "WorldToCell", m.position)).ToArray();
            Assert.That(warnedCells.Length, Is.EqualTo(2));
            Call(Character(0), "Teleport", CellToWorld(warnedCells[0]));
            Call(Character(1), "Teleport", CellToWorld(warnedCells[1]));
            Physics.SyncTransforms();
            Assert.That((bool)Call(match, "IsObstructed", warnedCells[0]), Is.True, "The player must occupy the first warned cell.");
            Assert.That((bool)Call(match, "IsObstructed", warnedCells[1]), Is.True, "The NPC must occupy the second warned cell.");
            Call(coins, "AdvanceMatch", 1.4f);
            Assert.That(MineCoinCells(), Is.EquivalentTo(warnedCells));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MineSchedule_ObstacleEnteringWarning_DoesNotMoveEitherCoin()
        {
            Call(coins, "AdvanceMatch", 3.6f);
            var warnedCells = WarningMarkers().Select(m => (GridPosition)Call(grid, "WorldToCell", m.position)).ToArray();
            Assert.That(warnedCells.Length, Is.EqualTo(2));
            var obstacle = new GameObject("Test coin warning obstruction", typeof(BoxCollider));
            try
            {
                obstacle.transform.position = CellToWorld(warnedCells[0]) + Vector3.up * 0.5f;
                Physics.SyncTransforms();
                Assert.That((bool)Call(match, "IsObstructed", warnedCells[0]), Is.True, "The obstacle must occupy the warned cell.");
                Call(coins, "AdvanceMatch", 1.4f);
                Assert.That(MineCoinCells(), Is.EquivalentTo(warnedCells));
            }
            finally { UnityEngine.Object.Destroy(obstacle); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MineSchedule_DeliversSixteenCoinsBeforeFortyAndTwentySixInTotal()
        {
            Call(coins, "AdvanceMatch", 40f);
            Assert.That(MineCoinCells().Length, Is.EqualTo(16));
            Call(coins, "AdvanceMatch", 20f);
            Assert.That(MineCoinCells().Length, Is.EqualTo(26));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MineCoin_RequiresConnectedPaint()
        {
            Call(coins, "AdvanceMatch", 5f);
            var coin = MineCoins().First();
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
            Assert.That(model.Participants[0].Coins, Is.Zero, "A coin outside the connected region cannot be collected.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoinSchedule_ReplicaRestoresWarningAndRejectsInvalidState()
        {
            var statusType = Type.GetType("EachOneCoin.Networking.MineStatusMessage, Assembly-CSharp", true);
            object Status(int mine, ushort[] xs, ushort[] ys)
            {
                var value = Activator.CreateInstance(statusType);
                statusType.GetField("elapsed").SetValue(value, 3.5d);
                statusType.GetField("nextEvent").SetValue(value, 0);
                statusType.GetField("warningMine").SetValue(value, mine);
                statusType.GetField("warningX").SetValue(value, xs);
                statusType.GetField("warningY").SetValue(value, ys);
                return value;
            }
            var empty = Status(-1, Array.Empty<ushort>(), Array.Empty<ushort>());
            var warning = Status(2, new ushort[] { 4, 6 }, new ushort[] { 5, 5 });
            Assert.That((bool)Call(coins, "ApplyReplicaSchedule", warning), Is.True);
            Assert.That(WarningMarkers().Count(), Is.EqualTo(2));
            Assert.That((bool)Call(coins, "ApplyReplicaSchedule", Status(4, new ushort[] { 2 }, new ushort[] { 2 })), Is.False);
            Assert.That(WarningMarkers().Count(), Is.EqualTo(2), "Invalid snapshots must not modify the current warning.");
            Assert.That((bool)Call(coins, "ApplyReplicaSchedule", empty), Is.True);
            yield return null;
            Assert.That(WarningMarkers().Count(), Is.Zero);
            Assert.That((bool)Call(coins, "ApplyReplicaSchedule", warning), Is.True);
            Assert.That(WarningMarkers().Count(), Is.EqualTo(2), "A reconnect snapshot must restore the active warning.");
        }

        [UnityTest]
        public IEnumerator MineCoin_MagnetCollectsInSameCellAndWithinRadiusButNotBeyond()
        {
            var character = Character(0);
            var cell = model.Participants[0].Spawn;
            Assert.That(model.Grid.IsConnected(0, cell), Is.True);
            var kind = Enum.Parse(coins.GetType().GetNestedType("CoinKind", BindingFlags.NonPublic), "Mine");
            var spawn = coins.GetType().GetMethod("Spawn", Private);
            var controller = character.GetComponent<CharacterController>();
            int before = model.Participants[0].Coins;

            void Place(Vector3 offset)
            {
                controller.enabled = false;
                var position = CellToWorld(cell) + offset;
                position.y = character.transform.position.y;
                character.transform.position = position;
                controller.enabled = true;
                Physics.SyncTransforms();
            }

            spawn.Invoke(coins, new[] { (object)cell, kind });
            Place(new Vector3(0.45f, 0f, 0.45f));
            Call(coins, "TryCollect", character);
            Assert.That(model.Participants[0].Coins, Is.EqualTo(before + 1), "Same-cell corner must collect.");

            spawn.Invoke(coins, new[] { (object)cell, kind });
            Place(new Vector3(0.7f, 0f, 0f));
            Call(coins, "TryCollect", character);
            Assert.That(model.Participants[0].Coins, Is.EqualTo(before + 2), "A coin within the 0.8-unit radius must collect.");

            spawn.Invoke(coins, new[] { (object)cell, kind });
            Place(new Vector3(0.9f, 0f, 0f));
            Call(coins, "TryCollect", character);
            Assert.That(model.Participants[0].Coins, Is.EqualTo(before + 2), "A coin outside the radius must remain.");
            Assert.That((bool)Call(coins, "HasCoin", cell), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedMine_BlocksMovementAndKnockbackWithoutDamage()
        {
            var character = Character(0);
            var mine = model.Mines[0];
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
        public IEnumerator Mines_KeepFourFixedPositionsAndNeverRepeatConsecutively()
        {
            Assert.That(model.Mines.Count, Is.EqualTo(4));
            var positions = model.Mines.ToArray();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(4));
            Assert.That(LastMineIndex(), Is.EqualTo(-1));
            var chosen = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 5; i++)
            {
                Call(coins, "AdvanceMatch", 5f);
                chosen.Add(LastMineIndex());
                Assert.That(model.Mines.ToArray(), Is.EqualTo(positions));
            }
            for (int i = 1; i < chosen.Count; i++)
                Assert.That(chosen[i], Is.Not.EqualTo(chosen[i - 1]), "Consecutive events must use different mines.");
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
        public IEnumerator Npc_StraightPathDoesNotBrakeAtIntermediateCells()
        {
            var spawn = model.Participants[1].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var npc = Character(1);
            // Reproduce the small cross-axis offset left by a previous corner.
            Call(npc, "Teleport", CellToWorld(spawn) + Vector3.forward * 0.14f);
            var target = new GridPosition(spawn.X + dx * 4, spawn.Y);
            for (int step = 1; step <= 4; step++)
                Assert.That((bool)Call(match, "IsBlocked", new GridPosition(spawn.X + dx * step, spawn.Y)), Is.False, "Test corridor must be clear.");
            Call(coins, "Drop", target, 1);
            Physics.SyncTransforms();
            Call(npcs, "ResetPath", 1);
            var paths = (System.Collections.IList[])npcs.GetType().GetField("paths", Private).GetValue(npcs);
            ((GridPosition[])npcs.GetType().GetField("pathOrigins", Private).GetValue(npcs))[1] = spawn;
            for (int step = 1; step <= 4; step++) paths[1].Add(new GridPosition(spawn.X + dx * step, spawn.Y));
            var indices = (int[])npcs.GetType().GetField("pathIndices", Private).GetValue(npcs);
            int crossings = 0;
            for (int frame = 0; frame < 200 && indices[1] < 3; frame++)
            {
                int before = indices[1];
                Call(npcs, "Tick", 1, 0.01f);
                if (indices[1] > before && indices[1] <= 2)
                {
                    float speed = ((Vector3)Get(npc, "MovementVelocity")).magnitude;
                    Assert.That(speed, Is.GreaterThan(5f), "NPC braked at an intermediate straight waypoint.");
                    crossings++;
                }
            }
            Assert.That(crossings, Is.EqualTo(2), "NPC did not cross two straight intermediate waypoints.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_OffCenterWaypointCommitsToAxisAndMakesProgress()
        {
            var spawn = model.Participants[1].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var npc = Character(1);
            var waypoint = new GridPosition(spawn.X + dx, spawn.Y);
            var target = new GridPosition(spawn.X + dx * 3, spawn.Y);
            Assert.That((bool)Call(match, "IsBlocked", waypoint), Is.False);
            Assert.That((bool)Call(match, "IsBlocked", target), Is.False);
            Call(npc, "Teleport", CellToWorld(spawn) + new Vector3(dx * 0.8f, 0f, 0.2f));
            Call(coins, "Drop", target, 1);
            Physics.SyncTransforms();
            Call(npcs, "ResetPath", 1);
            var paths = (System.Collections.IList[])npcs.GetType().GetField("paths", Private).GetValue(npcs);
            ((GridPosition[])npcs.GetType().GetField("pathOrigins", Private).GetValue(npcs))[1] = spawn;
            for (int step = 1; step <= 3; step++) paths[1].Add(new GridPosition(spawn.X + dx * step, spawn.Y));
            var indices = (int[])npcs.GetType().GetField("pathIndices", Private).GetValue(npcs);
            int lastAxis = 0, changes = 0;
            for (int frame = 0; frame < 150 && indices[1] == 0; frame++)
            {
                Call(npcs, "Tick", 1, 0.01f);
                var velocity = (Vector3)Get(npc, "MovementVelocity");
                int axis = Mathf.Abs(velocity.x) > 0.001f ? 1 : Mathf.Abs(velocity.z) > 0.001f ? 2 : 0;
                if (axis != 0 && lastAxis != 0 && axis != lastAxis) changes++;
                if (axis != 0) lastAxis = axis;
            }
            Assert.That(indices[1], Is.GreaterThan(0), "NPC did not reach the off-center waypoint.");
            Assert.That(changes, Is.LessThanOrEqualTo(1), "NPC oscillated between movement axes near the waypoint.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_NoProgressAgainstColliderTriggersRetry()
        {
            var spawn = model.Participants[1].Spawn;
            int dx = spawn.X < 10 ? 1 : -1;
            var npc = Character(1);
            Call(npc, "Teleport", CellToWorld(spawn));
            var target = new GridPosition(spawn.X + dx * 3, spawn.Y);
            Call(coins, "Drop", target, 1);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = CellToWorld(spawn) + new Vector3(dx * 0.7f, 0.5f, 0f);
                wall.transform.localScale = new Vector3(0.3f, 1f, 1f);
                Physics.SyncTransforms();
                Call(npcs, "ResetPath", 1);
                var paths = (System.Collections.IList[])npcs.GetType().GetField("paths", Private).GetValue(npcs);
                ((GridPosition[])npcs.GetType().GetField("pathOrigins", Private).GetValue(npcs))[1] = spawn;
                for (int step = 1; step <= 3; step++) paths[1].Add(new GridPosition(spawn.X + dx * step, spawn.Y));
                var retries = (float[])npcs.GetType().GetField("retries", Private).GetValue(npcs);
                for (int frame = 0; frame < 200 && retries[1] <= 0f; frame++) Call(npcs, "Tick", 1, 0.01f);
                Assert.That(retries[1], Is.GreaterThan(0f), "NPC did not retry after failing to make progress.");
            }
            finally { UnityEngine.Object.Destroy(wall); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_OpenAreaExplorationPrefersMultipleCells()
        {
            var npc = Character(1);
            Call(npc, "Teleport", CellToWorld(model.Participants[1].Spawn));
            Physics.SyncTransforms();
            var path = new System.Collections.Generic.List<GridPosition>();
            bool found = (bool)npcs.GetType().GetMethod("FindAlternativePath", Private).Invoke(npcs, new object[] { 1, path });
            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.GreaterThanOrEqualTo(3), "Open-area exploration should not repeatedly choose the nearest cell.");
            Assert.That(path.All(p => !(bool)Call(match, "IsBlocked", p)), Is.True);
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
        public IEnumerator CoinReplica_SameCellEntriesHaveDistinctIdsAndRemoveIndependently()
        {
            var position = new GridPosition(2, 2);
            Call(coins, "Drop", position, 2);
            var entries = (Array)Call(coins, "CaptureReplicaEntries");
            Assert.That(entries.Length, Is.EqualTo(2));
            var first = entries.GetValue(0);
            var second = entries.GetValue(1);
            var entryType = first.GetType();
            Assert.That(entryType.GetField("id").GetValue(first), Is.Not.EqualTo(entryType.GetField("id").GetValue(second)));
            Assert.That(entryType.GetField("x").GetValue(first), Is.EqualTo(entryType.GetField("x").GetValue(second)));
            Assert.That(entryType.GetField("y").GetValue(first), Is.EqualTo(entryType.GetField("y").GetValue(second)));
            Assert.That((bool)Call(coins, "ApplyReplicaChange", (byte)1, first), Is.True);
            var remaining = (Array)Call(coins, "CaptureReplicaEntries");
            Assert.That(remaining.Length, Is.EqualTo(1));
            Assert.That(entryType.GetField("id").GetValue(remaining.GetValue(0)), Is.EqualTo(entryType.GetField("id").GetValue(second)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Nameplates_ShowAssignedNamesAndFollowActiveState()
        {
            var npcType = npcs.GetType();
            var codeMethod = npcType.GetMethod("PersonalityCode");
            var nameMethod = npcType.GetMethod("PersonalityName", BindingFlags.Static | BindingFlags.Public);
            for (int i = 0; i < 4; i++)
            {
                var character = Character(i);
                var nameplate = character.GetType().GetField("nameplate", Private).GetValue(character);
                Assert.That(nameplate, Is.Not.Null);
                var label = nameplate.GetType().GetField("label", Private).GetValue(nameplate);
                string expected = i == 0 ? "玩家1" : (string)nameMethod.Invoke(null, new[] { codeMethod.Invoke(npcs, new object[] { i }) });
                Assert.That(Get(label, "text"), Is.EqualTo(expected));
                var font = Get(label, "font");
                Assert.That(font, Is.Not.Null);
                var hasCharacter = font.GetType().GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
                Assert.That(hasCharacter, Is.Not.Null, "Expected TextMesh Pro character lookup API was not found.");
                foreach (char c in expected) Assert.That((bool)hasCharacter.Invoke(font, new object[] { c, true, false }), Is.True, "Nameplate font is missing " + c);
            }
            var npc = Character(1);
            var plate = npc.GetType().GetField("nameplate", Private).GetValue(npc);
            var canvas = (Canvas)plate.GetType().GetField("canvas", Private).GetValue(plate);
            Call(npc, "SetActiveState", false);
            Assert.That(canvas.gameObject.activeSelf, Is.False);
            Call(npc, "SetActiveState", true);
            Assert.That(canvas.gameObject.activeSelf, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_AssignsDistinctPersonalitiesAndKeepsThemOnPathReset()
        {
            Call(npcs, "AssignPersonalities", (byte)14);
            var values = (Array)npcs.GetType().GetField("personalities", Private).GetValue(npcs);
            var before = Enumerable.Range(1, 3).Select(i => values.GetValue(i).ToString()).ToArray();
            Assert.That(before.Distinct().Count(), Is.EqualTo(3));
            Call(npcs, "ResetPath", 1);
            Assert.That(values.GetValue(1).ToString(), Is.EqualTo(before[0]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_WithoutCoinTargetFindsAlternativePath()
        {
            var path = new System.Collections.Generic.List<GridPosition>();
            bool found = (bool)npcs.GetType().GetMethod("FindAlternativePath", Private).Invoke(npcs, new object[] { 1, path });
            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.GreaterThan(0));
            Assert.That(path.All(p => !(bool)Call(match, "IsBlocked", p)), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_DifficultyControlsReactionAndWindupWithoutChangingPersonality()
        {
            var npc = Character(1);
            Call(npc, "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(11, 2)));
            Physics.SyncTransforms();
            var type = npcs.GetType();
            var difficultyField = type.GetField("difficulty", Private);
            var windups = (float[])type.GetField("windup", Private).GetValue(npcs);
            var tickAttack = type.GetMethod("TickAttack", Private);
            type.GetField("windupSeconds", Private).SetValue(npcs, 0.6f);
            byte personality = (byte)Call(npcs, "PersonalityCode", 1);

            difficultyField.SetValue(npcs, Enum.Parse(difficultyField.FieldType, "Easy"));
            Assert.That((bool)tickAttack.Invoke(npcs, new object[] { 1, 0.1f }), Is.False);
            Assert.That((bool)Call(npcs, "IsWindingUp", 1), Is.False);
            Assert.That((bool)tickAttack.Invoke(npcs, new object[] { 1, 0.21f }), Is.True);
            Assert.That(windups[1], Is.EqualTo(0.9f).Within(0.001f));
            Call(npcs, "ResetPath", 1);

            difficultyField.SetValue(npcs, Enum.Parse(difficultyField.FieldType, "Normal"));
            Assert.That((bool)tickAttack.Invoke(npcs, new object[] { 1, 0.01f }), Is.True);
            Assert.That(windups[1], Is.EqualTo(0.6f).Within(0.001f));
            Call(npcs, "ResetPath", 1);

            difficultyField.SetValue(npcs, Enum.Parse(difficultyField.FieldType, "Hard"));
            Assert.That((bool)tickAttack.Invoke(npcs, new object[] { 1, 0.01f }), Is.True);
            Assert.That(windups[1], Is.EqualTo(0.42f).Within(0.001f));
            Assert.That((byte)Call(npcs, "PersonalityCode", 1), Is.EqualTo(personality));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_EasyReactionRestartsWhenTargetLeavesRange()
        {
            Call(Character(1), "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(11, 2)));
            Physics.SyncTransforms();
            var type = npcs.GetType();
            var difficulty = type.GetField("difficulty", Private);
            difficulty.SetValue(npcs, Enum.Parse(difficulty.FieldType, "Easy"));
            var tick = type.GetMethod("TickAttack", Private);
            Assert.That((bool)tick.Invoke(npcs, new object[] { 1, 0.2f }), Is.False);
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(14, 2)));
            Physics.SyncTransforms();
            Assert.That((bool)tick.Invoke(npcs, new object[] { 1, 0.1f }), Is.False);
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(11, 2)));
            Physics.SyncTransforms();
            Assert.That((bool)tick.Invoke(npcs, new object[] { 1, 0.2f }), Is.False, "Reaction time must restart when a target is reacquired.");
            Assert.That((bool)tick.Invoke(npcs, new object[] { 1, 0.11f }), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_AttackWaitsForWindupAndCanMissWhenTargetMoves()
        {
            var npc = Character(1);
            var player = Character(0);
            Call(npc, "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(player, "Teleport", CellToWorld(new GridPosition(11, 2)));
            Physics.SyncTransforms();
            int health = model.Participants[0].Health;
            Call(npcs, "Tick", 1, 0.1f);
            Assert.That((bool)Call(npcs, "IsWindingUp", 1), Is.True);
            Assert.That(model.Participants[0].Health, Is.EqualTo(health));
            Call(player, "Teleport", CellToWorld(new GridPosition(13, 2)));
            Physics.SyncTransforms();
            Call(npcs, "Tick", 1, 0.7f);
            Assert.That((bool)Call(npcs, "IsWindingUp", 1), Is.False);
            Assert.That(model.Participants[0].Health, Is.EqualTo(health));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_WindupIsCancelledWhenPathIsReset()
        {
            Call(Character(1), "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(11, 2)));
            Physics.SyncTransforms();
            Call(npcs, "Tick", 1, 0.01f);
            Assert.That((bool)Call(npcs, "IsWindingUp", 1), Is.True);
            Call(npcs, "ResetPath", 1);
            Assert.That((bool)Call(npcs, "IsWindingUp", 1), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_RoutesAroundFixedMineToCoin()
        {
            var start = new GridPosition(5, 7);
            var target = new GridPosition(5, 13);
            Call(Character(1), "Teleport", CellToWorld(start));
            Call(coins, "Drop", target, 1);
            Physics.SyncTransforms();
            var path = new System.Collections.Generic.List<GridPosition>();
            bool found = (bool)npcs.GetType().GetMethod("FindPath", Private).Invoke(npcs, new object[] { 1, false, path });
            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.GreaterThan(6), "The west mine at (5,10) forces a detour around its cell.");
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
            ((GridPosition[])npcs.GetType().GetField("pathOrigins", Private).GetValue(npcs))[1] = spawn;
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
