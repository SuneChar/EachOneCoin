using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EachOneCoin.Tests
{
    public class LocalDemoCombatTests
    {
        private GameObject root;
        private Component demo;
        private Type demoType;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private object Field(string name) => demoType.GetField(name, Private).GetValue(demo);
        private object Call(string name, params object[] args) => demoType.GetMethod(name, Private).Invoke(demo, args);
        private void SetPawnPosition(int id, Vector2 position)
        {
            var pawns = (Array)Field("pawns");
            var pawn = pawns.GetValue(id);
            pawn.GetType().GetMethod("SetPosition").Invoke(pawn, new object[] { position });
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            demoType = Type.GetType("EachOneCoin.Demo.LocalMatchDemo, Assembly-CSharp", true);
            root = new GameObject("LocalDemoCombatTests");
            demo = root.AddComponent(demoType);
            yield return null;
            ((Behaviour)demo).enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attack_DropsHeldCoinAndRespawnsAtOwnSpawn()
        {
            var match = (MatchModel)Field("match");
            Assert.That(match.TryCollectCoin(1, match.Participants[1].Spawn), Is.True);
            SetPawnPosition(0, new Vector2(5f, 5f));
            SetPawnPosition(1, new Vector2(5f, 5f));
            var timers = (float[])Field("attackTimers");
            for (int i = 0; i < 3; i++) { timers[0] = 0; Call("TryAttack", 0); }
            Assert.That(match.Participants[1].Status, Is.EqualTo(ParticipantStatus.Respawning));
            Assert.That(match.Participants[1].Coins, Is.Zero);
            var coins = (IEnumerable)Field("coins");
            int dropped = 0;
            foreach (var coin in coins)
            {
                var t = coin.GetType();
                if ((bool)t.GetField("IsDropped").GetValue(coin) && (GameObject)t.GetField("Visual").GetValue(coin) != null) dropped++;
            }
            Assert.That(dropped, Is.EqualTo(1));
            Call("UpdateRespawns", 3f);
            Assert.That(match.Participants[1].Status, Is.EqualTo(ParticipantStatus.Active));
            Assert.That(match.Participants[1].Health, Is.EqualTo(match.Participants[1].MaxHealth));
            var pawns = (Array)Field("pawns");
            var position = (Vector2)pawns.GetValue(1).GetType().GetProperty("Position").GetValue(pawns.GetValue(1));
            Assert.That(position, Is.EqualTo(new Vector2(12.5f, 0f)));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Npc_MovesFromSpawnAndPaintsNeighbouringCell()
        {
            var match = (MatchModel)Field("match");
            SetPawnPosition(1, new Vector2(12.5f, 0f));
            var before = (Vector2)((Array)Field("pawns")).GetValue(1).GetType().GetProperty("Position").GetValue(((Array)Field("pawns")).GetValue(1));
            for (int i = 0; i < 8; i++) Call("UpdateNpc", 1, 0.1f);
            var pawn = ((Array)Field("pawns")).GetValue(1);
            var after = (Vector2)pawn.GetType().GetProperty("Position").GetValue(pawn);
            Assert.That(after, Is.Not.EqualTo(before));
            var cell = new GridPosition(Mathf.RoundToInt(after.x / 1.25f), Mathf.RoundToInt(after.y / 1.25f));
            Assert.That(match.Grid.OwnerAt(cell), Is.EqualTo(1));
            Assert.That(match.Grid.IsConnected(1, cell), Is.True);
            yield return null;
        }
    }
}
