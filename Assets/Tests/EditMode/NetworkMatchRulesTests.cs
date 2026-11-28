using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class NetworkMatchRulesTests
    {
        private Type rulesType;
        private Type acknowledgementType;
        private Type protocolType;

        [SetUp]
        public void SetUp()
        {
            TestMatchArchitecture.Interface.Deinit();
            rulesType = Type.GetType("EachOneCoin.Networking.MatchRulesMessage, Assembly-CSharp", true);
            acknowledgementType = Type.GetType("EachOneCoin.Networking.MatchRulesAcknowledged, Assembly-CSharp", true);
            protocolType = Type.GetType("EachOneCoin.Networking.MatchRulesProtocol, Assembly-CSharp", true);
        }

        [TearDown]
        public void TearDown() => TestMatchArchitecture.Interface.Deinit();

        private static object Message(Type type, ushort version, int coins, float duration)
        {
            var value = Activator.CreateInstance(type);
            type.GetField("protocolVersion").SetValue(value, version);
            type.GetField("coinsToWin").SetValue(value, coins);
            type.GetField("durationSeconds").SetValue(value, duration);
            return value;
        }

        [TestCase(1, 10, 60f, true)]
        [TestCase(1, 6, 120f, true)]
        [TestCase(0, 10, 60f, false)]
        [TestCase(2, 10, 60f, false)]
        [TestCase(1, 0, 60f, false)]
        [TestCase(1, 10001, 60f, false)]
        [TestCase(1, 10, 0f, false)]
        [TestCase(1, 10, 86401f, false)]
        public void Protocol_ValidatesVersionAndBounds(int version, int coins, float duration, bool expected)
        {
            var rules = Message(rulesType, (ushort)version, coins, duration);
            Assert.That(protocolType.GetMethod("IsValid").Invoke(null, new[] { rules }), Is.EqualTo(expected));
        }

        [Test]
        public void Protocol_RejectsNonFiniteDurationAndMismatchedAcknowledgements()
        {
            var isValid = protocolType.GetMethod("IsValid");
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.That(isValid.Invoke(null, new[] { Message(rulesType, 1, 10, value) }), Is.EqualTo(false));
            var rules = Message(rulesType, 1, 6, 120f);
            var matches = protocolType.GetMethod("Matches");
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 1, 6, 120f) }), Is.EqualTo(true));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 1, 7, 120f) }), Is.EqualTo(false));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 1, 6, 60f) }), Is.EqualTo(false));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 2, 6, 120f) }), Is.EqualTo(false));
        }

        [Test]
        public void Handshake_RequiresMatchingOfferAndFreshConfirmationForReconnect()
        {
            var type = Type.GetType("EachOneCoin.Networking.MatchRulesHandshake, Assembly-CSharp", true);
            var handshake = Activator.CreateInstance(type);
            var offer = type.GetMethod("Offer");
            var confirm = type.GetMethod("Confirm");
            var pending = type.GetMethod("IsPending");
            var rules = Message(rulesType, 1, 6, 120f);
            var acknowledgement = Message(acknowledgementType, 1, 6, 120f);
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, acknowledgement }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)7, Message(rulesType, 2, 6, 120f) }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)7, rules }), Is.EqualTo(true));
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, Message(acknowledgementType, 1, 7, 120f) }), Is.EqualTo(false));
            Assert.That(pending.Invoke(handshake, new object[] { 7 }), Is.EqualTo(true));
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, acknowledgement }), Is.EqualTo(true));
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, acknowledgement }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)9, rules }), Is.EqualTo(true));
            type.GetMethod("Remove").Invoke(handshake, new object[] { 9 });
            Assert.That(confirm.Invoke(handshake, new[] { (object)9, acknowledgement }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)9, rules }), Is.EqualTo(true));
            Assert.That(confirm.Invoke(handshake, new[] { (object)9, acknowledgement }), Is.EqualTo(true));
        }

        [Test]
        public void ClientRules_GateStartAndRestoreLongMatchSnapshots()
        {
            var serverType = Type.GetType("Mirror.NetworkServer, Mirror", true);
            Assert.That(serverType.GetProperty("active", BindingFlags.Public | BindingFlags.Static).GetValue(null), Is.EqualTo(false));
            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            var model = session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
            var system = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            var coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            var coinsType = Type.GetType("EachOneCoin.Gameplay.CoinSpawner, Assembly-CSharp", true);
            var controllerType = Type.GetType("EachOneCoin.Networking.NetworkMatchController, Assembly-CSharp", true);
            var stateType = Type.GetType("EachOneCoin.Networking.MatchStateMessage, Assembly-CSharp", true);
            var mineType = Type.GetType("EachOneCoin.Networking.MineStatusMessage, Assembly-CSharp", true);
            const BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            var root = new GameObject("NetworkRulesClientTest");
            root.SetActive(false);
            try
            {
                var coordinator = root.AddComponent(coordinatorType);
                var coins = root.AddComponent(coinsType);
                var controller = root.AddComponent(controllerType);
                coordinatorType.GetProperty("Model").SetValue(coordinator, model);
                coordinatorType.GetField("<IsNetworkMovementTest>k__BackingField", privateInstance).SetValue(coordinator, true);
                coordinatorType.GetField("session", privateInstance).SetValue(coordinator, session);
                coordinatorType.GetField("matchSystem", privateInstance).SetValue(coordinator, system);
                coordinatorType.GetField("coins", privateInstance).SetValue(coordinator, coins);
                coinsType.GetField("match", privateInstance).SetValue(coins, coordinator);
                controllerType.GetField("match", privateInstance).SetValue(controller, coordinator);

                var state = Activator.CreateInstance(stateType);
                stateType.GetField("sequence").SetValue(state, 1u);
                stateType.GetField("matchStatus").SetValue(state, (byte)MatchStatus.Playing);
                stateType.GetField("participantMask").SetValue(state, (byte)1);
                stateType.GetField("remaining").SetValue(state, 100f);
                controllerType.GetMethod("ReceiveMatchState").Invoke(controller, new[] { state });
                controllerType.GetMethod("Begin").Invoke(controller, new object[] { (byte)1, false });
                Assert.That(controllerType.GetProperty("IsRunning").GetValue(controller), Is.EqualTo(false));
                Assert.That(controllerType.GetField("hasMatchState", privateInstance).GetValue(controller), Is.EqualTo(false));

                var receiveRules = controllerType.GetMethod("ReceiveAuthoritativeRules");
                Assert.That(receiveRules.Invoke(controller, new[] { Message(rulesType, 2, 6, 120f) }), Is.EqualTo(false));
                var rules = Message(rulesType, 1, 6, 120f);
                Assert.That(receiveRules.Invoke(controller, new[] { rules }), Is.EqualTo(true));
                Assert.That(receiveRules.Invoke(controller, new[] { rules }), Is.EqualTo(true));
                Assert.That(receiveRules.Invoke(controller, new[] { Message(rulesType, 1, 7, 120f) }), Is.EqualTo(false));
                Assert.That(model.CoinsToWin, Is.EqualTo(6));
                Assert.That(model.RemainingTime, Is.EqualTo(120f));
                Assert.That((double)coinsType.GetField("duration", privateInstance).GetValue(coins), Is.EqualTo(120d));

                controllerType.GetMethod("ReceiveMatchState").Invoke(controller, new[] { state });
                Assert.That(controllerType.GetProperty("RemainingTime").GetValue(controller), Is.EqualTo(100f));
                var mine = Activator.CreateInstance(mineType);
                mineType.GetField("sequence").SetValue(mine, 1u);
                mineType.GetField("elapsed").SetValue(mine, 100d);
                mineType.GetField("nextEvent").SetValue(mine, 13);
                mineType.GetField("warningMine").SetValue(mine, -1);
                mineType.GetField("warningX").SetValue(mine, Array.Empty<ushort>());
                mineType.GetField("warningY").SetValue(mine, Array.Empty<ushort>());
                controllerType.GetMethod("ReceiveMineStatus").Invoke(controller, new[] { mine });
                Assert.That(controllerType.GetField("hasMineStatus", privateInstance).GetValue(controller), Is.EqualTo(true));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PendingMatchRules_CanBeConfiguredBeforeStartButNotAfter()
        {
            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            var model = session.CreateMatch(2, 2, new[] { new GridPosition(0, 0) }, 3, 100f);
            var system = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            Assert.That(system.ConfigureRulesBeforeStart(6, 120f), Is.True);
            Assert.That(model.CoinsToWin, Is.EqualTo(6));
            Assert.That(model.RemainingTime, Is.EqualTo(120f));
            Assert.That(system.ConfigureRulesBeforeStart(0, 40f), Is.False);
            Assert.That(model.CoinsToWin, Is.EqualTo(6));
            Assert.That(model.RemainingTime, Is.EqualTo(120f));
            Assert.That(system.StartMatch(), Is.True);
            Assert.That(system.ConfigureRulesBeforeStart(2, 40f), Is.False);
            Assert.That(model.CoinsToWin, Is.EqualTo(6));
            Assert.That(model.RemainingTime, Is.EqualTo(120f));
        }
    }
}
