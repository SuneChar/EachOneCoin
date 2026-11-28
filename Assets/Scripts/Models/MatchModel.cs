using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EachOneCoin
{
    public enum MatchStatus { Waiting, Playing, Finished }

    public sealed class MatchModel
    {
        public const int DefaultCoinsToWin = 10;
        public const float DefaultDurationSeconds = 60f;

        private readonly ParticipantState[] participants;
        private readonly ReadOnlyCollection<ParticipantState> participantView;

        public MatchStatus Status { get; internal set; } = MatchStatus.Waiting;
        public float RemainingTime { get; internal set; }
        public int CoinsToWin { get; internal set; }
        public GridModel Grid { get; }
        public IReadOnlyList<GridPosition> Mines { get; internal set; } = Array.AsReadOnly(Array.Empty<GridPosition>());
        public IReadOnlyList<ParticipantState> Participants => participantView;

        public MatchModel(int width, int height, IReadOnlyList<GridPosition> spawns, int maxHealth, int coinsToWin = DefaultCoinsToWin, float duration = DefaultDurationSeconds)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (coinsToWin <= 0) throw new ArgumentOutOfRangeException(nameof(coinsToWin));
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            Grid = new GridModel(width, height, spawns);
            participants = new ParticipantState[spawns.Count];
            for (int i = 0; i < participants.Length; i++) participants[i] = new ParticipantState(i, spawns[i], maxHealth);
            participantView = Array.AsReadOnly(participants);
            CoinsToWin = coinsToWin;
            RemainingTime = duration;
        }

    }
}
