using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class ArenaObstacleProtectionTests
    {
        private GameObject root;
        private Component match;
        private Component[] characters;
        private Collider[] hazards;
        private MatchModel model;
        private Type characterType;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ArenaObstacleProtectionTests");
            var matchType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var hazardType = Type.GetType("LethalObstacle, Assembly-CSharp", true);
            match = root.AddComponent(matchType);
            ((Behaviour)match).enabled = false;
            model = new MatchModel(3, 3, new[] { new GridPosition(0, 0), new GridPosition(2, 2) }, 3);
            model.Start();
            matchType.GetProperty("Model").SetValue(match, model);
            characters = new Component[2];
            for (int i = 0; i < characters.Length; i++)
            {
                var go = new GameObject("Character " + i);
                go.transform.SetParent(root.transform);
                characters[i] = go.AddComponent(characterType);
                characterType.GetMethod("Initialize").Invoke(characters[i], new object[] { match, i, Color.white });
                ((Behaviour)characters[i]).enabled = false;
            }
            var typedCharacters = Array.CreateInstance(characterType, characters.Length);
            for (int i = 0; i < characters.Length; i++) typedCharacters.SetValue(characters[i], i);
            matchType.GetProperty("Characters").SetValue(match, typedCharacters);
            hazards = new Collider[2];
            for (int i = 0; i < hazards.Length; i++)
            {
                var go = new GameObject("Hazard " + i);
                go.transform.SetParent(root.transform);
                hazards[i] = go.AddComponent<BoxCollider>();
                go.AddComponent(hazardType);
            }
        }

        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(root); }

        private void Contact(int id, int hazard) => characterType.GetMethod("TryHazard", Private).Invoke(characters[id], new object[] { hazards[hazard] });
        private void Exit(int id, int hazard) => characterType.GetMethod("OnTriggerExit", Private).Invoke(characters[id], new object[] { hazards[hazard] });

        [Test]
        public void EffectiveObstacleContact_NotifiesOnlyOnceDuringProtection()
        {
            int hits = 0;
            var eventInfo = match.GetType().GetEvent("ObstacleHit");
            Action<int> handler = id => { if (id == 0) hits++; };
            eventInfo.AddEventHandler(match, handler);
            try
            {
                Contact(0, 0);
                Contact(0, 1);
                Assert.That(hits, Is.EqualTo(1));
            }
            finally { eventInfo.RemoveEventHandler(match, handler); }
        }

        private void CreateGround()
        {
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root.transform);
            ground.transform.position = new Vector3(1000f, -0.5f, 1000f);
            var collider = ground.AddComponent<BoxCollider>();
            collider.size = new Vector3(10f, 1f, 10f);
        }

        [Test]
        public void PlanarMove_OnGroundAdvancesWithoutChangingHeight()
        {
            CreateGround();
            var player = characters[0].gameObject;
            var controller = player.GetComponent<CharacterController>();
            controller.height = 1f; controller.radius = 0.28f; controller.center = new Vector3(0f, 0.5f, 0f);
            player.transform.position = new Vector3(1000f, controller.skinWidth, 1000f);
            hazards[0].enabled = false; hazards[1].enabled = false;
            Physics.SyncTransforms();
            var start = player.transform.position;
            var move = characterType.GetMethod("MoveOnPlane", Private);
            for (int i = 0; i < 10; i++) move.Invoke(characters[0], new object[] { new Vector3(0.05f, 0f, 0f) });
            Assert.That(player.transform.position.x - start.x, Is.GreaterThan(0.4f));
            Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(0.001f));
        }

        [Test]
        public void PlanarMove_LowObstacleDoesNotLiftOrLetCharacterCross()
        {
            CreateGround();
            var player = characters[0].gameObject;
            var controller = player.GetComponent<CharacterController>();
            controller.height = 1f; controller.radius = 0.28f; controller.center = new Vector3(0f, 0.5f, 0f);
            player.transform.position = new Vector3(1000f, controller.skinWidth, 1000f);
            hazards[0].transform.position = new Vector3(1001f, 0.1f, 1000f);
            hazards[0].transform.localScale = new Vector3(0.5f, 0.2f, 2f);
            hazards[1].enabled = false;
            Physics.SyncTransforms();
            var move = characterType.GetMethod("MoveOnPlane", Private);
            for (int i = 0; i < 30; i++) move.Invoke(characters[0], new object[] { new Vector3(0.05f, 0f, 0f) });
            Assert.That(player.transform.position.y, Is.EqualTo(controller.skinWidth).Within(0.001f));
            Assert.That(player.transform.position.x, Is.LessThan(1000.9f));
        }

        [Test]
        public void ArenaBoundObstacle_RespectsPauseAndRunningFlagWithoutLegacyManager()
        {
            var go = new GameObject("Arena spike");
            go.transform.SetParent(root.transform);
            var spikeType = Type.GetType("TimedSpikeTrap, Assembly-CSharp", true);
            var spike = go.AddComponent(spikeType);
            var obstacleType = spikeType.BaseType;
            obstacleType.GetMethod("BindArena").Invoke(spike, new object[] { match });
            var canRun = obstacleType.GetProperty("CanRun", Private);
            Assert.That(canRun.GetValue(spike), Is.EqualTo(true));
            match.GetType().GetMethod("Pause").Invoke(match, null);
            try { Assert.That(canRun.GetValue(spike), Is.EqualTo(false)); }
            finally { match.GetType().GetMethod("Resume").Invoke(match, null); }
            obstacleType.GetMethod("SetRunning").Invoke(spike, new object[] { false });
            Assert.That(canRun.GetValue(spike), Is.EqualTo(false));
        }

        [Test]
        public void SpikeHazard_IsRecognizedOnlyWhenActiveWithoutLegacyManager()
        {
            var go = new GameObject("Arena spike");
            go.transform.SetParent(root.transform);
            var spikeType = Type.GetType("TimedSpikeTrap, Assembly-CSharp", true);
            var spike = go.AddComponent(spikeType);
            var collider = go.AddComponent<BoxCollider>();
            var isHazard = match.GetType().GetMethod("IsHazard");
            spikeType.GetField("isVisible", Private).SetValue(spike, false);
            Assert.That(isHazard.Invoke(match, new object[] { collider }), Is.EqualTo(false));
            spikeType.GetField("isVisible", Private).SetValue(spike, true);
            Assert.That(isHazard.Invoke(match, new object[] { collider }), Is.EqualTo(true));
        }

        [Test]
        public void MultipleHazards_AreBlockedPerCharacterAndRequireNewContact()
        {
            Contact(0, 0);
            Contact(0, 1);
            Contact(1, 0);
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2));
            Assert.That(model.Participants[1].MaxHealth, Is.EqualTo(2));
            characterType.GetField("obstacleProtection", Private).SetValue(characters[0], 0f);
            Contact(0, 1);
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(2));
            Exit(0, 1);
            Contact(0, 1);
            Assert.That(model.Participants[0].MaxHealth, Is.EqualTo(1));
        }
    }
}
