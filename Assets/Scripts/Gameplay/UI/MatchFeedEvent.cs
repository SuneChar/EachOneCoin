using System;
using UnityEngine;

namespace EachOneCoin.Gameplay
{
    public enum MatchFeedEventType : byte
    {
        CoinSpawned = 0,
        CoinCollected = 1,
        PlayerKnockedDown = 2,
        PlayerEliminated = 3,
        PlayerWon = 4,
        NearVictory = 5,
        PlayerRespawned = 6
    }

    public enum EliminationReason : byte
    {
        Obstacle = 0,
        Forfeit = 1
    }

    [Serializable]
    public struct MatchFeedEvent
    {
        private static readonly string[] DefaultMineNames = { "南侧", "北侧", "西侧", "东侧" };
        private static readonly Color DefaultPlayerColor = Color.white;
        private static readonly Color CoinSpawnedAccentColor = new Color(0.98f, 0.78f, 0.18f, 1f);
        private static readonly Color KnockdownAccentColor = new Color(0.96f, 0.40f, 0.24f, 1f);
        private static readonly Color EliminatedAccentColor = new Color(0.88f, 0.24f, 0.28f, 1f);
        private static readonly Color VictoryAccentColor = new Color(1.00f, 0.85f, 0.24f, 1f);
        private static readonly Color NearVictoryAccentColor = new Color(1.00f, 0.62f, 0.16f, 1f);
        private static readonly Color RespawnAccentColor = new Color(0.30f, 0.82f, 0.58f, 1f);

        public MatchFeedEventType Type;
        public int PrimarySlot;
        public int SecondarySlot;
        public int MineIndex;
        public int Value;
        public EliminationReason Reason;

        public static MatchFeedEvent CreateCoinSpawned(int mineIndex, int count)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.CoinSpawned,
                PrimarySlot = -1,
                SecondarySlot = -1,
                MineIndex = mineIndex,
                Value = count,
                Reason = EliminationReason.Obstacle
            };
        }

        public static MatchFeedEvent CreateCoinCollected(int slot, int amount = 1)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.CoinCollected,
                PrimarySlot = slot,
                SecondarySlot = -1,
                MineIndex = -1,
                Value = amount,
                Reason = EliminationReason.Obstacle
            };
        }

        public static MatchFeedEvent CreatePlayerKnockedDown(int victimSlot, int attackerSlot, int droppedCoins)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.PlayerKnockedDown,
                PrimarySlot = victimSlot,
                SecondarySlot = attackerSlot,
                MineIndex = -1,
                Value = droppedCoins,
                Reason = EliminationReason.Obstacle
            };
        }

        public static MatchFeedEvent CreatePlayerEliminated(int slot, EliminationReason reason)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.PlayerEliminated,
                PrimarySlot = slot,
                SecondarySlot = -1,
                MineIndex = -1,
                Value = 0,
                Reason = reason
            };
        }

        public static MatchFeedEvent CreatePlayerWon(int slot, int finishOrder)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.PlayerWon,
                PrimarySlot = slot,
                SecondarySlot = -1,
                MineIndex = -1,
                Value = finishOrder,
                Reason = EliminationReason.Obstacle
            };
        }

        public static MatchFeedEvent CreateNearVictory(int slot, int currentCoins)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.NearVictory,
                PrimarySlot = slot,
                SecondarySlot = -1,
                MineIndex = -1,
                Value = currentCoins,
                Reason = EliminationReason.Obstacle
            };
        }

        public static MatchFeedEvent CreatePlayerRespawned(int slot)
        {
            return new MatchFeedEvent
            {
                Type = MatchFeedEventType.PlayerRespawned,
                PrimarySlot = slot,
                SecondarySlot = -1,
                MineIndex = -1,
                Value = 0,
                Reason = EliminationReason.Obstacle
            };
        }

        public static string Format(
            MatchFeedEvent evt,
            Func<int, string> nameResolver,
            Func<int, Color> colorResolver,
            Func<int, string> mineNameResolver)
        {
            switch (evt.Type)
            {
                case MatchFeedEventType.CoinSpawned:
                    return ResolveMineName(evt.MineIndex, mineNameResolver) + "刷新了 " + evt.Value + " 枚金币";

                case MatchFeedEventType.CoinCollected:
                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 获得了 " + evt.Value + " 枚金币";

                case MatchFeedEventType.PlayerKnockedDown:
                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver)
                        + " 被 "
                        + FormatPlayer(evt.SecondarySlot, nameResolver, colorResolver)
                        + " 击倒了";

                case MatchFeedEventType.PlayerEliminated:
                    if (evt.Reason == EliminationReason.Forfeit)
                    {
                        return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 已退赛淘汰";
                    }

                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 撞击障碍被淘汰了";

                case MatchFeedEventType.PlayerWon:
                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 集齐金币获胜（第 " + evt.Value + " 名）";

                case MatchFeedEventType.NearVictory:
                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 已获得 " + evt.Value + " 枚金币，即将获胜！";

                case MatchFeedEventType.PlayerRespawned:
                    return FormatPlayer(evt.PrimarySlot, nameResolver, colorResolver) + " 已复活重返战场";

                default:
                    return string.Empty;
            }
        }

        public static Color ResolveAccentColor(MatchFeedEvent evt, Func<int, Color> colorResolver)
        {
            switch (evt.Type)
            {
                case MatchFeedEventType.CoinSpawned:
                    return CoinSpawnedAccentColor;

                case MatchFeedEventType.CoinCollected:
                    return ResolveSlotColor(evt.PrimarySlot, colorResolver, CoinSpawnedAccentColor);

                case MatchFeedEventType.PlayerKnockedDown:
                    return KnockdownAccentColor;

                case MatchFeedEventType.PlayerEliminated:
                    return EliminatedAccentColor;

                case MatchFeedEventType.PlayerWon:
                    return VictoryAccentColor;

                case MatchFeedEventType.NearVictory:
                    return NearVictoryAccentColor;

                case MatchFeedEventType.PlayerRespawned:
                    return ResolveSlotColor(evt.PrimarySlot, colorResolver, RespawnAccentColor);

                default:
                    return CoinSpawnedAccentColor;
            }
        }

        private static string FormatPlayer(
            int slot,
            Func<int, string> nameResolver,
            Func<int, Color> colorResolver)
        {
            string name = nameResolver != null ? nameResolver(slot) : null;
            if (string.IsNullOrEmpty(name))
            {
                name = slot >= 0 ? "玩家 " + (slot + 1) : "未知玩家";
            }

            Color color = colorResolver != null ? colorResolver(slot) : DefaultPlayerColor;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return "<color=#" + hex + ">" + name + "</color>";
        }

        private static string ResolveMineName(int mineIndex, Func<int, string> mineNameResolver)
        {
            if (mineNameResolver != null)
            {
                string resolved = mineNameResolver(mineIndex);
                if (!string.IsNullOrEmpty(resolved))
                {
                    return resolved;
                }
            }

            if (mineIndex >= 0 && mineIndex < DefaultMineNames.Length)
            {
                return DefaultMineNames[mineIndex];
            }

            return "矿点";
        }

        private static Color ResolveSlotColor(int slot, Func<int, Color> colorResolver, Color fallback)
        {
            if (colorResolver != null && slot >= 0)
            {
                Color resolved = colorResolver(slot);
                if (resolved.a <= 0f)
                {
                    resolved.a = 1f;
                }

                return resolved;
            }

            return fallback;
        }
    }
}
