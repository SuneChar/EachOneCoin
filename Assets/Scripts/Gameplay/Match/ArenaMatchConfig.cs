using UnityEngine;

namespace EachOneCoin.Gameplay
{
    [CreateAssetMenu(menuName = "EachOneCoin/Match/Match Rules", fileName = "ArenaMatchConfig")]
    public sealed class ArenaMatchConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float durationSeconds = MatchModel.DefaultDurationSeconds;
        [SerializeField, Min(1)] private int coinsToWin = MatchModel.DefaultCoinsToWin;

        public bool TryGetRules(out int targetCoins, out float duration)
        {
            targetCoins = coinsToWin;
            duration = durationSeconds;
            return coinsToWin > 0 && !float.IsNaN(durationSeconds) && !float.IsInfinity(durationSeconds) && durationSeconds > 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!TryGetRules(out int target, out float duration))
            {
                Debug.LogWarning("对局配置无效：时长必须为有限正数，获胜金币数必须大于零；单机将回退默认值，联机将拒绝开始。", this);
                return;
            }

            if (target > EachOneCoin.Networking.MatchRulesProtocol.MaximumCoinsToWin || duration > EachOneCoin.Networking.MatchRulesProtocol.MaximumDurationSeconds)
                Debug.LogWarning("该规则超出联机协议允许范围；单机仍可使用，联机将拒绝开始。", this);

            int maxScheduledCoins = 0;
            for (int i = 0; i < CoinSpawnSchedule.EventCount; i++)
                if (CoinSpawnSchedule.EventTime(i) < duration) maxScheduledCoins += CoinSpawnSchedule.CoinsPerEvent;
            if (target > maxScheduledCoins)
                Debug.LogWarning($"对局配置的获胜目标为 {target} 枚，但该时长结束前固定时间表最多生成 {maxScheduledCoins} 枚；部分事件还可能因无安全位置而跳过。", this);
            if (duration > MatchModel.DefaultDurationSeconds)
                Debug.LogWarning("对局超过 60 秒；当前固定金币时间表在第 58 秒后不会继续生成金币。", this);
        }
#endif
    }
}
