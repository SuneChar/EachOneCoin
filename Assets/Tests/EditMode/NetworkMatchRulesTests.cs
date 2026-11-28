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

        [TestCase(2, 10, 60f, true)]
        [TestCase(2, 6, 120f, true)]
        [TestCase(0, 10, 60f, false)]
        [TestCase(1, 10, 60f, false)]
        [TestCase(3, 10, 60f, false)]
        [TestCase(2, 0, 60f, false)]
        [TestCase(2, 10001, 60f, false)]
        [TestCase(2, 10, 0f, false)]
        [TestCase(2, 10, 86401f, false)]
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
                Assert.That(isValid.Invoke(null, new[] { Message(rulesType, 2, 10, value) }), Is.EqualTo(false));
            var rules = Message(rulesType, 2, 6, 120f);
            var matches = protocolType.GetMethod("Matches");
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 2, 6, 120f) }), Is.EqualTo(true));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 2, 7, 120f) }), Is.EqualTo(false));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 2, 6, 60f) }), Is.EqualTo(false));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 1, 6, 120f) }), Is.EqualTo(false));
            Assert.That(matches.Invoke(null, new[] { rules, Message(acknowledgementType, 3, 6, 120f) }), Is.EqualTo(false));
        }

        [Test]
        public void Handshake_RequiresMatchingOfferAndFreshConfirmationForReconnect()
        {
            var type = Type.GetType("EachOneCoin.Networking.MatchRulesHandshake, Assembly-CSharp", true);
            var handshake = Activator.CreateInstance(type);
            var offer = type.GetMethod("Offer");
            var confirm = type.GetMethod("Confirm");
            var pending = type.GetMethod("IsPending");
            var rules = Message(rulesType, 2, 6, 120f);
            var acknowledgement = Message(acknowledgementType, 2, 6, 120f);
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, acknowledgement }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)7, Message(rulesType, 1, 6, 120f) }), Is.EqualTo(false));
            Assert.That(offer.Invoke(handshake, new[] { (object)7, rules }), Is.EqualTo(true));
            Assert.That(confirm.Invoke(handshake, new[] { (object)7, Message(acknowledgementType, 2, 7, 120f) }), Is.EqualTo(false));
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
                Assert.That(receiveRules.Invoke(controller, new[] { Message(rulesType, 1, 6, 120f) }), Is.EqualTo(false));
                var rules = Message(rulesType, 2, 6, 120f);
                Assert.That(receiveRules.Invoke(controller, new[] { rules }), Is.EqualTo(true));
                Assert.That(receiveRules.Invoke(controller, new[] { rules }), Is.EqualTo(true));
                Assert.That(receiveRules.Invoke(controller, new[] { Message(rulesType, 2, 7, 120f) }), Is.EqualTo(false));
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

        [Test]
        public void AuthoritativeAppearances_SanitizeAndApplyOnBegin_WithFivePresetsIncludingNone()
        {
            var serverType = Type.GetType("Mirror.NetworkServer, Mirror", true);
            Assert.That(serverType.GetProperty("active", BindingFlags.Public | BindingFlags.Static).GetValue(null), Is.EqualTo(false));

            var beginMsgType = Type.GetType("EachOneCoin.Networking.MovementBegin, Assembly-CSharp", true);
            var beginMsg = Activator.CreateInstance(beginMsgType);
            beginMsgType.GetField("appearance0").SetValue(beginMsg, (byte)2);
            beginMsgType.GetField("appearance1").SetValue(beginMsg, (byte)4);
            beginMsgType.GetField("appearance2").SetValue(beginMsg, (byte)0);
            beginMsgType.GetField("appearance3").SetValue(beginMsg, (byte)99);
            var beginAppearanceAt = beginMsgType.GetMethod("AppearanceAt");
            Assert.That(beginAppearanceAt.Invoke(beginMsg, new object[] { 0 }), Is.EqualTo((byte)2));
            Assert.That(beginAppearanceAt.Invoke(beginMsg, new object[] { 1 }), Is.EqualTo((byte)4));
            Assert.That(beginAppearanceAt.Invoke(beginMsg, new object[] { 2 }), Is.EqualTo((byte)0));
            Assert.That(beginAppearanceAt.Invoke(beginMsg, new object[] { 3 }), Is.EqualTo((byte)99));

            var snapshotType = Type.GetType("EachOneCoin.Networking.RoomSnapshot, Assembly-CSharp", true);
            var snapshot = Activator.CreateInstance(snapshotType);
            snapshotType.GetField("appearance0").SetValue(snapshot, (byte)4);
            snapshotType.GetField("appearance1").SetValue(snapshot, (byte)255);
            Assert.That(snapshotType.GetMethod("AppearanceAt").Invoke(snapshot, new object[] { 0 }), Is.EqualTo((byte)4));
            Assert.That(snapshotType.GetMethod("AppearanceAt").Invoke(snapshot, new object[] { 1 }), Is.EqualTo((byte)255));

            var selectReqType = Type.GetType("EachOneCoin.Networking.SelectAppearanceRequest, Assembly-CSharp", true);
            Assert.That(selectReqType.GetField("appearanceIndex"), Is.Not.Null);

            var session = TestMatchArchitecture.Interface.GetModel<MatchSessionModel>();
            var spawns = new[] { new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(0, 1), new GridPosition(1, 1) };
            var model = session.CreateMatch(2, 2, spawns, 3, 100f);
            var system = TestMatchArchitecture.Interface.GetSystem<MatchSystem>();
            var coordinatorType = Type.GetType("EachOneCoin.Gameplay.MatchCoordinator, Assembly-CSharp", true);
            var coinsType = Type.GetType("EachOneCoin.Gameplay.CoinSpawner, Assembly-CSharp", true);
            var controllerType = Type.GetType("EachOneCoin.Networking.NetworkMatchController, Assembly-CSharp", true);
            var characterType = Type.GetType("EachOneCoin.Gameplay.ArenaCharacter, Assembly-CSharp", true);
            var profileType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceProfile, Assembly-CSharp", true);
            var catalogType = Type.GetType("EachOneCoin.Gameplay.CharacterAppearanceCatalog, Assembly-CSharp", true);
            const BindingFlags privateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

            var root = new GameObject("NetworkAppearanceBeginTest");
            root.SetActive(false);
            var createdAssets = new System.Collections.Generic.List<UnityEngine.Object>();
            var createdObjects = new System.Collections.Generic.List<GameObject> { root };
            try
            {
                var profiles = Array.CreateInstance(profileType, 5);
                for (int i = 0; i < 5; i++)
                {
                    var profile = ScriptableObject.CreateInstance(profileType);
                    createdAssets.Add(profile);
                    profileType.GetField("appearanceId", privateInstance).SetValue(profile, i < 4 ? "slot_" + i : "none");
                    profileType.GetField("accessorySlotName", privateInstance).SetValue(profile, i < 4 ? "Slot " + i : string.Empty);
                    profileType.GetField("displayName", privateInstance).SetValue(profile, i < 4 ? "套装" + i : "无配饰");
                    profiles.SetValue(profile, i);
                }

                var catalog = ScriptableObject.CreateInstance(catalogType);
                createdAssets.Add(catalog);
                catalogType.GetField("profiles", privateInstance).SetValue(catalog, profiles);

                var coordinator = root.AddComponent(coordinatorType);
                var coins = root.AddComponent(coinsType);
                var controller = root.AddComponent(controllerType);
                var characters = Array.CreateInstance(characterType, 4);
                for (int i = 0; i < 4; i++)
                {
                    var charObj = new GameObject("Char_" + i, typeof(CharacterController));
                    charObj.SetActive(false);
                    createdObjects.Add(charObj);
                    characters.SetValue(charObj.AddComponent(characterType), i);
                }

                coordinatorType.GetProperty("Model").SetValue(coordinator, model);
                coordinatorType.GetProperty("Characters").SetValue(coordinator, characters);
                coordinatorType.GetField("<IsNetworkMovementTest>k__BackingField", privateInstance).SetValue(coordinator, true);
                coordinatorType.GetField("session", privateInstance).SetValue(coordinator, session);
                coordinatorType.GetField("matchSystem", privateInstance).SetValue(coordinator, system);
                coordinatorType.GetField("coins", privateInstance).SetValue(coordinator, coins);
                coordinatorType.GetField("appearanceCatalog", privateInstance).SetValue(coordinator, catalog);
                coinsType.GetField("match", privateInstance).SetValue(coins, coordinator);
                controllerType.GetField("match", privateInstance).SetValue(controller, coordinator);

                Assert.That(controllerType.GetProperty("AppearanceCatalogCount").GetValue(controller), Is.EqualTo(5));
                Assert.That(controllerType.GetMethod("ReceiveAuthoritativeRules").Invoke(controller, new[] { Message(rulesType, 2, 10, 60f) }), Is.EqualTo(true));

                var changedSlots = new System.Collections.Generic.List<int>();
                Action<int> onChanged = slot => changedSlots.Add(slot);
                var changedEvent = coordinatorType.GetEvent("ParticipantAppearanceChanged");
                changedEvent.AddEventHandler(coordinator, onChanged);
                try
                {
                    controllerType.GetMethod("SetAuthoritativeAppearances").Invoke(controller, new object[] { (byte)2, (byte)4, (byte)0, (byte)99 });
                    var indexAt = controllerType.GetMethod("AppearanceIndexAt");
                    Assert.That(indexAt.Invoke(controller, new object[] { 0 }), Is.EqualTo(2));
                    Assert.That(indexAt.Invoke(controller, new object[] { 1 }), Is.EqualTo(4));
                    Assert.That(indexAt.Invoke(controller, new object[] { 2 }), Is.EqualTo(0));
                    Assert.That(indexAt.Invoke(controller, new object[] { 3 }), Is.EqualTo(3));

                    var beginMethod = controllerType.GetMethod("Begin");
                    Assert.That(beginMethod, Is.Not.Null);
                    Assert.That(beginMethod.GetParameters().Length, Is.EqualTo(2));
                    beginMethod.Invoke(controller, new object[] { (byte)3, true });

                    Assert.That(controllerType.GetProperty("IsRunning").GetValue(controller), Is.EqualTo(true));
                    var getIndex = coordinatorType.GetMethod("GetParticipantAppearanceIndex");
                    var getId = coordinatorType.GetMethod("GetParticipantAppearanceId");
                    Assert.That(getIndex.Invoke(coordinator, new object[] { 0 }), Is.EqualTo(2));
                    Assert.That(getIndex.Invoke(coordinator, new object[] { 1 }), Is.EqualTo(4));
                    Assert.That(getIndex.Invoke(coordinator, new object[] { 2 }), Is.EqualTo(0));
                    Assert.That(getIndex.Invoke(coordinator, new object[] { 3 }), Is.EqualTo(3));
                    Assert.That(getId.Invoke(coordinator, new object[] { 1 }), Is.EqualTo("none"));
                    Assert.That(changedSlots, Is.EqualTo(new[] { 0, 1, 2, 3 }));
                }
                finally
                {
                    changedEvent.RemoveEventHandler(coordinator, onChanged);
                }
            }
            finally
            {
                for (int i = 0; i < createdObjects.Count; i++)
                    if (createdObjects[i] != null) UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                for (int i = 0; i < createdAssets.Count; i++)
                    if (createdAssets[i] != null) UnityEngine.Object.DestroyImmediate(createdAssets[i]);
            }
        }
    }
}
