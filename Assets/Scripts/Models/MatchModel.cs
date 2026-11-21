using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EachOneCoin
{
    public enum MatchStatus { Waiting, Playing, Finished }

    public sealed class MatchModel
    {
        private readonly ParticipantState[] participants;
        private readonly ReadOnlyCollection<ParticipantState> participantView;

        public MatchStatus Status { get; private set; } = MatchStatus.Waiting;
        public float RemainingTime { get; private set; }
        public int CoinsToWin { get; }
        public GridModel Grid { get; }
        public IReadOnlyList<ParticipantState> Participants => participantView;

        public MatchModel(int width, int height, IReadOnlyList<GridPosition> spawns, int maxHealth, int coinsToWin = 10, float duration = 60f)
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

        public bool Start()
        {
            if (Status != MatchStatus.Waiting) return false;
            Status = MatchStatus.Playing;
            return true;
        }

        public void AdvanceTime(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (Status != MatchStatus.Playing) return;
            RemainingTime = Math.Max(0f, RemainingTime - elapsed);
            if (RemainingTime > 0f) return;
            foreach (var participant in participants)
                if (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning)
                    participant.Status = ParticipantStatus.Lost;
            Status = MatchStatus.Finished;
        }

        public bool TryPaint(int participantId, GridPosition position)
        {
            return IsActive(participantId) && Grid.TryPaint(participantId, position);
        }

        // The caller must confirm actual contact and atomically consume the coin after success.
        public bool TryCollectCoin(int participantId, GridPosition coinPosition)
        {
            if (!IsActive(participantId) || !Grid.IsConnected(participantId, coinPosition)) return false;
            var participant = participants[participantId];
            participant.Coins++;
            if (participant.Coins >= CoinsToWin)
            {
                participant.Status = ParticipantStatus.Won;
                FinishIfNoParticipantsRemain();
            }
            return true;
        }

        // dropCount is a provisional input; the caller creates the dropped coin objects.
        public bool TryDamage(int participantId, int damage, int dropCount, out int droppedCoins)
        {
            droppedCoins = 0;
            if (!IsActive(participantId) || damage <= 0 || dropCount < 0) return false;
            var participant = participants[participantId];
            participant.Health = Math.Max(0, participant.Health - damage);
            if (participant.Health == 0)
            {
                droppedCoins = Math.Min(participant.Coins, dropCount);
                participant.Coins -= droppedCoins;
                participant.Status = ParticipantStatus.Respawning;
            }
            return true;
        }

        // Called once per effective obstacle contact; contact debouncing belongs to the scene layer.
        public bool TryObstacleContact(int participantId)
        {
            if (!IsActive(participantId)) return false;
            var participant = participants[participantId];
            participant.MaxHealth--;
            participant.Health = Math.Min(participant.Health, participant.MaxHealth);
            if (participant.MaxHealth == 0)
            {
                participant.Status = ParticipantStatus.Lost;
                FinishIfNoParticipantsRemain();
            }
            return true;
        }

        public bool TryRespawn(int participantId)
        {
            if (Status != MatchStatus.Playing || participantId < 0 || participantId >= participants.Length) return false;
            var participant = participants[participantId];
            if (participant.Status != ParticipantStatus.Respawning) return false;
            participant.Health = participant.MaxHealth;
            participant.Status = ParticipantStatus.Active;
            return true;
        }

        private bool IsActive(int id) => Status == MatchStatus.Playing && id >= 0 && id < participants.Length && participants[id].Status == ParticipantStatus.Active;

        private void FinishIfNoParticipantsRemain()
        {
            foreach (var participant in participants)
                if (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning) return;
            Status = MatchStatus.Finished;
        }
    }
}
