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
    public class OverheadHealthBarPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/ArenaScene.unity";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private Scene arena;
        private Component match;
        private MatchModel model;
        private Component grid;
        private Component combat;
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

        private Component NameplateOf(int id)
        {
            var character = Character(id);
            return (Component)character.GetType().GetField("nameplate", Private).GetValue(character);
        }

        private bool IsHealthBarVisible(int id)
        {
            var nameplate = NameplateOf(id);
            return (bool)Get(nameplate, "IsHealthBarVisible");
        }

        private Component HealthBarOf(int id)
        {
            var nameplate = NameplateOf(id);
            return (Component)Get(nameplate, "HealthBar");
        }

        private Canvas NameplateCanvasOf(int id)
        {
            var nameplate = NameplateOf(id);
            return (Canvas)nameplate.GetType().GetField("canvas", Private).GetValue(nameplate);
        }

        private string[] SegmentStatesOf(int id)
        {
            var bar = HealthBarOf(id);
            int count = (int)Get(bar, "SegmentCount");
            var getState = bar.GetType().GetMethod("GetSegmentState");
            var states = new string[count];
            for (int i = 0; i < count; i++)
            {
                states[i] = getState.Invoke(bar, new object[] { i }).ToString();
            }
            return states;
        }

        private IEnumerator RunSinglePausedLateUpdate()
        {
            Call(match, "Pause");
            ((Behaviour)match).enabled = true;
            yield return null;
            ((Behaviour)match).enabled = false;
            Call(match, "Resume");
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

            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False, "OverheadHealthBar PlayMode test requires an isolated scene.");
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
            combat = (Component)Get(match, "Combat");
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
        public IEnumerator OverheadHealthBars_ShowForAllCharactersIncludingPlayerAndReflectDamageObstacleRespawnAndSpectate()
        {
            Assert.That(Get(match, "CurrentViewSlot"), Is.EqualTo(0));
            Assert.That(NameplateCanvasOf(0).gameObject.activeSelf, Is.True);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(IsHealthBarVisible(i), Is.True);
                Assert.That(SegmentStatesOf(i), Is.EqualTo(new[] { "Full", "Full", "Full" }));
            }

            // Attack Characters[1]: 3/3 -> 2/3 => [Full, Full, Dim]
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(Character(1), "Teleport", CellToWorld(new GridPosition(10, 3)));
            Assert.That((bool)Call(combat, "TryAttack", 0, Vector3.forward), Is.True);
            Assert.That(model.Participants[1].Health, Is.EqualTo(2));
            Assert.That(model.Participants[1].MaxHealth, Is.EqualTo(3));
            yield return RunSinglePausedLateUpdate();
            Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Full", "Full", "Dim" }));

            // Obstacle hit on Characters[1]: 2/3 -> 1/2 => [Full, Dim, Broken]
            Assert.That((bool)Call(match, "HitObstacle", 1), Is.True);
            Assert.That(model.Participants[1].Health, Is.EqualTo(1));
            Assert.That(model.Participants[1].MaxHealth, Is.EqualTo(2));
            yield return RunSinglePausedLateUpdate();
            Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Full", "Dim", "Broken" }));

            // Knockdown Characters[1] into Respawning: nameplate canvas hides
            Call(combat, "Tick", 1f);
            Call(Character(0), "Teleport", CellToWorld(new GridPosition(10, 2)));
            Call(Character(1), "Teleport", CellToWorld(new GridPosition(10, 3)));
            Assert.That((bool)Call(combat, "TryAttack", 0, Vector3.forward), Is.True);
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(NameplateCanvasOf(1).gameObject.activeSelf, Is.False);

            // Respawn Characters[1]: 2/2 with initialMax 3 => [Full, Full, Broken]
            Call(combat, "Tick", 2.5f);
            Assert.That(model.Participants[1].Status, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(model.Participants[1].Health, Is.EqualTo(2));
            Assert.That(model.Participants[1].MaxHealth, Is.EqualTo(2));
            Assert.That(NameplateCanvasOf(1).gameObject.activeSelf, Is.True);
            yield return RunSinglePausedLateUpdate();
            Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Full", "Full", "Broken" }));

            // Player slot 0 obstacle hit: 3/3 -> 2/2 => [Full, Full, Broken] on player's own overhead bar
            Assert.That((bool)Call(match, "HitObstacle", 0), Is.True);
            Assert.That(model.Participants[0].Health, Is.EqualTo(2));
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2));
            yield return RunSinglePausedLateUpdate();
            Assert.That(IsHealthBarVisible(0), Is.True);
            Assert.That(SegmentStatesOf(0), Is.EqualTo(new[] { "Full", "Full", "Broken" }));

            // Spectate target switch to slot 2: all visible characters including spectated target keep overhead bars visible
            Call(match, "SetViewTarget", 2);
            Assert.That(Get(match, "CurrentViewSlot"), Is.EqualTo(2));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(IsHealthBarVisible(i), Is.True);
            }

            // Switch back to slot 0
            Call(match, "SetViewTarget", 0);
            Assert.That(Get(match, "CurrentViewSlot"), Is.EqualTo(0));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(IsHealthBarVisible(i), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator NetworkClientOverheadHealthBars_FallbackBeforeFirstCombatStateAndSyncViaLateUpdate()
        {
            var serverType = Type.GetType("Mirror.NetworkServer, Mirror", true);
            Assert.That(serverType.GetProperty("active", BindingFlags.Public | BindingFlags.Static).GetValue(null), Is.EqualTo(false));

            var controllerType = Type.GetType("EachOneCoin.Networking.NetworkMatchController, Assembly-CSharp", true);
            var combatMsgType = Type.GetType("EachOneCoin.Networking.CombatStateMessage, Assembly-CSharp", true);
            var participantMsgType = Type.GetType("EachOneCoin.Networking.CombatParticipant, Assembly-CSharp", true);

            var controller = match.gameObject.AddComponent(controllerType);
            try
            {
                controllerType.GetField("match", Private).SetValue(controller, match);
                match.GetType().GetField("<IsNetworkMovementTest>k__BackingField", Private).SetValue(match, true);
                match.GetType().GetField("<NetworkController>k__BackingField", Private).SetValue(match, controller);

                // Before first CombatStateMessage arrives, HasCombatState is false and LateUpdate must actively restore InitialHealth (not 0/0 Broken)
                Assert.That((bool)Get(controller, "HasCombatState"), Is.False);
                Call(Character(1), "SetOverheadHealth", 0, 0, 3, true);
                Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Broken", "Broken", "Broken" }));
                ((int[])match.GetType().GetField("lastOverheadHealth", Private).GetValue(match))[1] = -1;
                ((Behaviour)match).enabled = true;
                yield return null;
                ((Behaviour)match).enabled = false;
                Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Full", "Full", "Full" }));

                // Deliver authoritative CombatStateMessage with slot 1 at 1/2 health
                object MakeParticipant(int health, int maxHealth)
                {
                    var p = Activator.CreateInstance(participantMsgType);
                    participantMsgType.GetField("status").SetValue(p, (byte)ParticipantStatus.Active);
                    participantMsgType.GetField("npcPersonality").SetValue(p, (byte)0);
                    participantMsgType.GetField("health").SetValue(p, health);
                    participantMsgType.GetField("maxHealth").SetValue(p, maxHealth);
                    participantMsgType.GetField("stamina").SetValue(p, 100f);
                    participantMsgType.GetField("position").SetValue(p, Vector3.zero);
                    participantMsgType.GetField("npcWindup").SetValue(p, false);
                    participantMsgType.GetField("npcWindupDirection").SetValue(p, Vector3.zero);
                    return p;
                }

                var msg = Activator.CreateInstance(combatMsgType);
                combatMsgType.GetField("sequence").SetValue(msg, 1u);
                combatMsgType.GetField("serverTime").SetValue(msg, 1.0d);
                combatMsgType.GetField("p0").SetValue(msg, MakeParticipant(3, 3));
                combatMsgType.GetField("p1").SetValue(msg, MakeParticipant(1, 2));
                combatMsgType.GetField("p2").SetValue(msg, MakeParticipant(2, 3));
                combatMsgType.GetField("p3").SetValue(msg, MakeParticipant(0, 1));
                Call(controller, "ReceiveCombatState", msg);

                Assert.That((bool)Get(controller, "HasCombatState"), Is.True);

                // Verify MatchCoordinator.LateUpdate updates overhead bars even though Update() returns early in network mode
                ((Behaviour)match).enabled = true;
                yield return null;
                ((Behaviour)match).enabled = false;

                Assert.That(SegmentStatesOf(1), Is.EqualTo(new[] { "Full", "Dim", "Broken" }));
                Assert.That(SegmentStatesOf(2), Is.EqualTo(new[] { "Full", "Full", "Dim" }));
                Assert.That(SegmentStatesOf(3), Is.EqualTo(new[] { "Dim", "Broken", "Broken" }));
            }
            finally
            {
                ((Behaviour)match).enabled = false;
                match.GetType().GetField("<IsNetworkMovementTest>k__BackingField", Private).SetValue(match, false);
                match.GetType().GetField("<NetworkController>k__BackingField", Private).SetValue(match, null);
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller);
            }
        }
    }
}
