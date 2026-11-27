using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EachOneCoin.Tests
{
    public class MatchFeedEventTests
    {
        private static readonly Color GoldCoinAccent = new Color(0.98f, 0.78f, 0.18f, 1f);
        private static readonly Color KnockdownAccent = new Color(0.96f, 0.40f, 0.24f, 1f);
        private static readonly Color EliminatedAccent = new Color(0.88f, 0.24f, 0.28f, 1f);
        private static readonly Color VictoryGoldAccent = new Color(1.00f, 0.85f, 0.24f, 1f);
        private static readonly Color NearVictoryAccent = new Color(1.00f, 0.62f, 0.16f, 1f);
        private static readonly Color RespawnFallbackAccent = new Color(0.30f, 0.82f, 0.58f, 1f);

        private Type eventType;
        private Type eliminationReasonType;
        private MethodInfo createCoinSpawnedMethod;
        private MethodInfo createCoinCollectedMethod;
        private MethodInfo createPlayerKnockedDownMethod;
        private MethodInfo createPlayerEliminatedMethod;
        private MethodInfo createPlayerWonMethod;
        private MethodInfo createNearVictoryMethod;
        private MethodInfo createPlayerRespawnedMethod;
        private MethodInfo formatMethod;
        private MethodInfo resolveAccentColorMethod;
        private FieldInfo typeField;
        private FieldInfo primarySlotField;
        private FieldInfo secondarySlotField;
        private FieldInfo mineIndexField;
        private FieldInfo valueField;
        private FieldInfo reasonField;

        [SetUp]
        public void SetUp()
        {
            eventType = Type.GetType("EachOneCoin.Gameplay.MatchFeedEvent, Assembly-CSharp", true);
            eliminationReasonType = Type.GetType("EachOneCoin.Gameplay.EliminationReason, Assembly-CSharp", true);

            createCoinSpawnedMethod = eventType.GetMethod(
                "CreateCoinSpawned",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            createCoinCollectedMethod = eventType.GetMethod(
                "CreateCoinCollected",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            createPlayerKnockedDownMethod = eventType.GetMethod(
                "CreatePlayerKnockedDown",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int), typeof(int) },
                null);
            createPlayerEliminatedMethod = eventType.GetMethod(
                "CreatePlayerEliminated",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), eliminationReasonType },
                null);
            createPlayerWonMethod = eventType.GetMethod(
                "CreatePlayerWon",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            createNearVictoryMethod = eventType.GetMethod(
                "CreateNearVictory",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            createPlayerRespawnedMethod = eventType.GetMethod(
                "CreatePlayerRespawned",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null);
            formatMethod = eventType.GetMethod(
                "Format",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    eventType,
                    typeof(Func<int, string>),
                    typeof(Func<int, Color>),
                    typeof(Func<int, string>)
                },
                null);
            resolveAccentColorMethod = eventType.GetMethod(
                "ResolveAccentColor",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { eventType, typeof(Func<int, Color>) },
                null);

            typeField = eventType.GetField("Type", BindingFlags.Public | BindingFlags.Instance);
            primarySlotField = eventType.GetField("PrimarySlot", BindingFlags.Public | BindingFlags.Instance);
            secondarySlotField = eventType.GetField("SecondarySlot", BindingFlags.Public | BindingFlags.Instance);
            mineIndexField = eventType.GetField("MineIndex", BindingFlags.Public | BindingFlags.Instance);
            valueField = eventType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
            reasonField = eventType.GetField("Reason", BindingFlags.Public | BindingFlags.Instance);

            Assert.That(createCoinSpawnedMethod, Is.Not.Null);
            Assert.That(createCoinCollectedMethod, Is.Not.Null);
            Assert.That(createPlayerKnockedDownMethod, Is.Not.Null);
            Assert.That(createPlayerEliminatedMethod, Is.Not.Null);
            Assert.That(createPlayerWonMethod, Is.Not.Null);
            Assert.That(createNearVictoryMethod, Is.Not.Null);
            Assert.That(createPlayerRespawnedMethod, Is.Not.Null);
            Assert.That(formatMethod, Is.Not.Null);
            Assert.That(resolveAccentColorMethod, Is.Not.Null);
            Assert.That(typeField, Is.Not.Null);
            Assert.That(primarySlotField, Is.Not.Null);
            Assert.That(secondarySlotField, Is.Not.Null);
            Assert.That(mineIndexField, Is.Not.Null);
            Assert.That(valueField, Is.Not.Null);
            Assert.That(reasonField, Is.Not.Null);
        }

        private object CreateCoinSpawned(int mineIndex, int count)
        {
            return createCoinSpawnedMethod.Invoke(null, new object[] { mineIndex, count });
        }

        private object CreateCoinCollected(int slot, int amount = 1)
        {
            return createCoinCollectedMethod.Invoke(null, new object[] { slot, amount });
        }

        private object CreatePlayerKnockedDown(int victimSlot, int attackerSlot, int droppedCoins)
        {
            return createPlayerKnockedDownMethod.Invoke(null, new object[] { victimSlot, attackerSlot, droppedCoins });
        }

        private object CreatePlayerEliminated(int slot, string reasonName)
        {
            object reason = Enum.Parse(eliminationReasonType, reasonName);
            return createPlayerEliminatedMethod.Invoke(null, new object[] { slot, reason });
        }

        private object CreatePlayerWon(int slot, int finishOrder)
        {
            return createPlayerWonMethod.Invoke(null, new object[] { slot, finishOrder });
        }

        private object CreateNearVictory(int slot, int currentCoins)
        {
            return createNearVictoryMethod.Invoke(null, new object[] { slot, currentCoins });
        }

        private object CreatePlayerRespawned(int slot)
        {
            return createPlayerRespawnedMethod.Invoke(null, new object[] { slot });
        }

        private string FormatEvent(
            object evt,
            Func<int, string> nameResolver = null,
            Func<int, Color> colorResolver = null,
            Func<int, string> mineNameResolver = null)
        {
            return (string)formatMethod.Invoke(null, new object[] { evt, nameResolver, colorResolver, mineNameResolver });
        }

        private Color ResolveAccentColor(object evt, Func<int, Color> colorResolver = null)
        {
            return (Color)resolveAccentColorMethod.Invoke(null, new object[] { evt, colorResolver });
        }

        private string EventTypeName(object evt) => typeField.GetValue(evt).ToString();
        private int PrimarySlot(object evt) => (int)primarySlotField.GetValue(evt);
        private int SecondarySlot(object evt) => (int)secondarySlotField.GetValue(evt);
        private int MineIndex(object evt) => (int)mineIndexField.GetValue(evt);
        private int EventValue(object evt) => (int)valueField.GetValue(evt);
        private string EliminationReasonName(object evt) => reasonField.GetValue(evt).ToString();

        private static string ResolvePlayerName(int slot)
        {
            switch (slot)
            {
                case 0:
                    return "小红";
                case 1:
                    return "小蓝";
                case 2:
                    return "小绿";
                case 3:
                    return "小黄";
                default:
                    return null;
            }
        }

        private static Color ResolvePlayerColor(int slot)
        {
            switch (slot)
            {
                case 0:
                    return new Color(0.95f, 0.25f, 0.25f, 1f);
                case 1:
                    return new Color(0.20f, 0.60f, 0.95f, 1f);
                case 2:
                    return new Color(0.25f, 0.85f, 0.40f, 1f);
                case 3:
                    return new Color(0.95f, 0.80f, 0.20f, 1f);
                default:
                    return Color.white;
            }
        }

        private static string ExpectedPlayerTag(int slot)
        {
            string name = ResolvePlayerName(slot);
            string hex = ColorUtility.ToHtmlStringRGB(ResolvePlayerColor(slot));
            return "<color=#" + hex + ">" + name + "</color>";
        }

        private static void AssertColorEquals(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }

        [Test]
        public void CoinSpawned_FormatsMineDirectionAndCount()
        {
            object southEvent = CreateCoinSpawned(0, 3);

            Assert.That(EventTypeName(southEvent), Is.EqualTo("CoinSpawned"));
            Assert.That(PrimarySlot(southEvent), Is.EqualTo(-1));
            Assert.That(SecondarySlot(southEvent), Is.EqualTo(-1));
            Assert.That(MineIndex(southEvent), Is.EqualTo(0));
            Assert.That(EventValue(southEvent), Is.EqualTo(3));

            string defaultSouthText = FormatEvent(southEvent);
            Assert.That(defaultSouthText, Is.EqualTo("南侧刷新了 3 枚金币"));
            Assert.That(defaultSouthText, Does.Contain("南侧"));
            Assert.That(defaultSouthText, Does.Contain("3 枚金币"));

            Assert.That(FormatEvent(CreateCoinSpawned(1, 2)), Is.EqualTo("北侧刷新了 2 枚金币"));
            Assert.That(FormatEvent(CreateCoinSpawned(2, 4)), Is.EqualTo("西侧刷新了 4 枚金币"));
            Assert.That(FormatEvent(CreateCoinSpawned(3, 1)), Is.EqualTo("东侧刷新了 1 枚金币"));
            Assert.That(FormatEvent(CreateCoinSpawned(99, 2)), Is.EqualTo("矿点刷新了 2 枚金币"));

            string customDirectionText = FormatEvent(
                CreateCoinSpawned(1, 5),
                null,
                null,
                index => index == 1 ? "北侧高台矿点" : null);
            Assert.That(customDirectionText, Is.EqualTo("北侧高台矿点刷新了 5 枚金币"));
        }

        [Test]
        public void CoinCollected_FormatsColoredPlayerNameAndCount()
        {
            ParameterInfo[] parameters = createCoinCollectedMethod.GetParameters();
            Assert.That(parameters[1].HasDefaultValue, Is.True);
            Assert.That(parameters[1].DefaultValue, Is.EqualTo(1));

            object evt = CreateCoinCollected(1, 2);

            Assert.That(EventTypeName(evt), Is.EqualTo("CoinCollected"));
            Assert.That(PrimarySlot(evt), Is.EqualTo(1));
            Assert.That(SecondarySlot(evt), Is.EqualTo(-1));
            Assert.That(MineIndex(evt), Is.EqualTo(-1));
            Assert.That(EventValue(evt), Is.EqualTo(2));

            string text = FormatEvent(evt, ResolvePlayerName, ResolvePlayerColor);
            string expectedTag = ExpectedPlayerTag(1);

            Assert.That(text, Does.Contain(expectedTag));
            Assert.That(text, Does.Contain("获得了"));
            Assert.That(text, Does.Contain("2 枚金币"));
            Assert.That(text, Is.EqualTo(expectedTag + " 获得了 2 枚金币"));

            string fallbackText = FormatEvent(CreateCoinCollected(0, 1));
            Assert.That(fallbackText, Is.EqualTo("<color=#FFFFFF>玩家 1</color> 获得了 1 枚金币"));
        }

        [Test]
        public void PlayerKnockedDown_FormatsVictimAndAttackerWithKnockDownWording()
        {
            object evt = CreatePlayerKnockedDown(0, 1, 2);

            Assert.That(EventTypeName(evt), Is.EqualTo("PlayerKnockedDown"));
            Assert.That(PrimarySlot(evt), Is.EqualTo(0));
            Assert.That(SecondarySlot(evt), Is.EqualTo(1));
            Assert.That(MineIndex(evt), Is.EqualTo(-1));
            Assert.That(EventValue(evt), Is.EqualTo(2));

            string text = FormatEvent(evt, ResolvePlayerName, ResolvePlayerColor);
            string victimTag = ExpectedPlayerTag(0);
            string attackerTag = ExpectedPlayerTag(1);

            Assert.That(text, Is.EqualTo(victimTag + " 被 " + attackerTag + " 击倒了"));
            Assert.That(text, Does.Contain("击倒"));
            Assert.That(text, Does.Not.Contain("击杀"));
            Assert.That(text, Does.Not.Contain("淘汰"));
            Assert.That(
                text.IndexOf(victimTag, StringComparison.Ordinal),
                Is.LessThan(text.IndexOf(attackerTag, StringComparison.Ordinal)));
        }

        [Test]
        public void PlayerEliminated_DistinguishesObstacleAndForfeitReasons()
        {
            object obstacleEvent = CreatePlayerEliminated(0, "Obstacle");
            object forfeitEvent = CreatePlayerEliminated(2, "Forfeit");

            Assert.That(EventTypeName(obstacleEvent), Is.EqualTo("PlayerEliminated"));
            Assert.That(PrimarySlot(obstacleEvent), Is.EqualTo(0));
            Assert.That(SecondarySlot(obstacleEvent), Is.EqualTo(-1));
            Assert.That(EventValue(obstacleEvent), Is.EqualTo(0));
            Assert.That(EliminationReasonName(obstacleEvent), Is.EqualTo("Obstacle"));

            Assert.That(EventTypeName(forfeitEvent), Is.EqualTo("PlayerEliminated"));
            Assert.That(PrimarySlot(forfeitEvent), Is.EqualTo(2));
            Assert.That(SecondarySlot(forfeitEvent), Is.EqualTo(-1));
            Assert.That(EventValue(forfeitEvent), Is.EqualTo(0));
            Assert.That(EliminationReasonName(forfeitEvent), Is.EqualTo("Forfeit"));

            string obstacleText = FormatEvent(obstacleEvent, ResolvePlayerName, ResolvePlayerColor);
            string forfeitText = FormatEvent(forfeitEvent, ResolvePlayerName, ResolvePlayerColor);

            Assert.That(obstacleText, Is.EqualTo(ExpectedPlayerTag(0) + " 撞击障碍被淘汰了"));
            Assert.That(obstacleText, Does.Contain("障碍"));
            Assert.That(obstacleText, Does.Contain("淘汰"));
            Assert.That(obstacleText, Does.Not.Contain("退赛"));

            Assert.That(forfeitText, Is.EqualTo(ExpectedPlayerTag(2) + " 已退赛淘汰"));
            Assert.That(forfeitText, Does.Contain("退赛"));
            Assert.That(forfeitText, Does.Contain("淘汰"));
            Assert.That(forfeitText, Does.Not.Contain("障碍"));

            string sameSlotObstacle = FormatEvent(CreatePlayerEliminated(1, "Obstacle"), ResolvePlayerName, ResolvePlayerColor);
            string sameSlotForfeit = FormatEvent(CreatePlayerEliminated(1, "Forfeit"), ResolvePlayerName, ResolvePlayerColor);
            Assert.That(sameSlotObstacle, Is.Not.EqualTo(sameSlotForfeit));
        }

        [Test]
        public void PlayerWon_FormatsFinishOrderMedalRank()
        {
            for (int finishOrder = 1; finishOrder <= 3; finishOrder++)
            {
                int slot = finishOrder - 1;
                object evt = CreatePlayerWon(slot, finishOrder);

                Assert.That(EventTypeName(evt), Is.EqualTo("PlayerWon"));
                Assert.That(PrimarySlot(evt), Is.EqualTo(slot));
                Assert.That(SecondarySlot(evt), Is.EqualTo(-1));
                Assert.That(MineIndex(evt), Is.EqualTo(-1));
                Assert.That(EventValue(evt), Is.EqualTo(finishOrder));

                string text = FormatEvent(evt, ResolvePlayerName, ResolvePlayerColor);
                string expectedTag = ExpectedPlayerTag(slot);

                Assert.That(text, Is.EqualTo(expectedTag + " 集齐金币获胜（第 " + finishOrder + " 名）"));
                Assert.That(text, Does.Contain("获胜"));
                Assert.That(text, Does.Contain("第 " + finishOrder + " 名"));
            }
        }

        [Test]
        public void NearVictory_FormatsCurrentCoinsAndNearVictoryAlert()
        {
            object evt = CreateNearVictory(3, 4);

            Assert.That(EventTypeName(evt), Is.EqualTo("NearVictory"));
            Assert.That(PrimarySlot(evt), Is.EqualTo(3));
            Assert.That(SecondarySlot(evt), Is.EqualTo(-1));
            Assert.That(MineIndex(evt), Is.EqualTo(-1));
            Assert.That(EventValue(evt), Is.EqualTo(4));

            string text = FormatEvent(evt, ResolvePlayerName, ResolvePlayerColor);
            string expectedTag = ExpectedPlayerTag(3);

            Assert.That(text, Is.EqualTo(expectedTag + " 已获得 4 枚金币，即将获胜！"));
            Assert.That(text, Does.Contain(expectedTag));
            Assert.That(text, Does.Contain("已获得 4 枚金币"));
            Assert.That(text, Does.Contain("即将获胜"));
        }

        [Test]
        public void PlayerRespawned_FormatsRespawnMessage()
        {
            object evt = CreatePlayerRespawned(1);

            Assert.That(EventTypeName(evt), Is.EqualTo("PlayerRespawned"));
            Assert.That(PrimarySlot(evt), Is.EqualTo(1));
            Assert.That(SecondarySlot(evt), Is.EqualTo(-1));
            Assert.That(MineIndex(evt), Is.EqualTo(-1));
            Assert.That(EventValue(evt), Is.EqualTo(0));

            string text = FormatEvent(evt, ResolvePlayerName, ResolvePlayerColor);
            string expectedTag = ExpectedPlayerTag(1);

            Assert.That(text, Is.EqualTo(expectedTag + " 已复活重返战场"));
            Assert.That(text, Does.Contain(expectedTag));
            Assert.That(text, Does.Contain("复活"));
        }

        [Test]
        public void ResolveAccentColor_ReturnsPlayerColorOrGoldAccentAppropriately()
        {
            Color slot1Color = ResolvePlayerColor(1);
            Color slot2Color = ResolvePlayerColor(2);

            AssertColorEquals(
                ResolveAccentColor(CreateCoinSpawned(0, 2), ResolvePlayerColor),
                GoldCoinAccent);
            AssertColorEquals(
                ResolveAccentColor(CreatePlayerWon(0, 1), ResolvePlayerColor),
                VictoryGoldAccent);

            AssertColorEquals(
                ResolveAccentColor(CreateCoinCollected(1, 1), ResolvePlayerColor),
                slot1Color);
            AssertColorEquals(
                ResolveAccentColor(CreateCoinCollected(1, 1), null),
                GoldCoinAccent);
            AssertColorEquals(
                ResolveAccentColor(CreateCoinCollected(-1, 1), ResolvePlayerColor),
                GoldCoinAccent);
            AssertColorEquals(
                ResolveAccentColor(CreateCoinCollected(1, 1), _ => new Color(0.40f, 0.50f, 0.60f, 0f)),
                new Color(0.40f, 0.50f, 0.60f, 1f));

            AssertColorEquals(
                ResolveAccentColor(CreatePlayerRespawned(2), ResolvePlayerColor),
                slot2Color);
            AssertColorEquals(
                ResolveAccentColor(CreatePlayerRespawned(2), null),
                RespawnFallbackAccent);

            AssertColorEquals(
                ResolveAccentColor(CreatePlayerKnockedDown(0, 1, 1), ResolvePlayerColor),
                KnockdownAccent);
            AssertColorEquals(
                ResolveAccentColor(CreatePlayerEliminated(0, "Obstacle"), ResolvePlayerColor),
                EliminatedAccent);
            AssertColorEquals(
                ResolveAccentColor(CreatePlayerEliminated(0, "Forfeit"), ResolvePlayerColor),
                EliminatedAccent);
            AssertColorEquals(
                ResolveAccentColor(CreateNearVictory(3, 4), ResolvePlayerColor),
                NearVictoryAccent);
        }
    }
}
