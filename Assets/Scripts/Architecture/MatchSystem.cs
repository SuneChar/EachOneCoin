using System;
using QFramework;

namespace EachOneCoin
{
    public sealed class MatchSystem : AbstractSystem
    {
        private MatchSessionModel session;

        protected override void OnInit()
        {
            session = this.GetModel<MatchSessionModel>();
        }

        protected override void OnDeinit()
        {
            session = null;
        }

        private MatchModel CurrentMatch => session?.Match ?? throw new InvalidOperationException("No active match session is available.");

        public bool StartMatch()
        {
            var match = CurrentMatch;
            if (match.Status != MatchStatus.Waiting) return false;
            match.Status = MatchStatus.Playing;
            return true;
        }

        public void AdvanceTime(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            var match = CurrentMatch;
            if (match.Status != MatchStatus.Playing) return;
            match.RemainingTime = Math.Max(0f, match.RemainingTime - elapsed);
            if (match.RemainingTime > 0f) return;
            foreach (var participant in match.Participants)
                if (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning)
                    participant.Status = ParticipantStatus.Lost;
            match.Status = MatchStatus.Finished;
        }

        public bool TryPaint(int participantId, GridPosition position)
        {
            var match = CurrentMatch;
            var grid = match.Grid;
            if (!IsActive(match, participantId) || !grid.Contains(position)) return false;
            for (int i = 0; i < match.Participants.Count; i++)
                if (i != participantId && match.Participants[i].Spawn.Equals(position)) return false;
            if (grid.OwnerAt(position) == participantId) return false;
            grid.SetOwner(position, participantId);
            return true;
        }

        public void TickStamina(float deltaTime, bool active, bool moving, bool sprintHeld, float drainPerSecond, float recoveryPerSecond)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (float.IsNaN(drainPerSecond) || float.IsInfinity(drainPerSecond) || drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            if (float.IsNaN(recoveryPerSecond) || float.IsInfinity(recoveryPerSecond) || recoveryPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(recoveryPerSecond));
            var stamina = session?.Stamina ?? throw new InvalidOperationException("No active match session is available.");
            if (!sprintHeld) stamina.ExhaustedUntilRelease = false;
            stamina.IsSprinting = active && moving && sprintHeld && !stamina.ExhaustedUntilRelease && stamina.Current > 0f;
            if (!active) return;
            if (stamina.IsSprinting)
            {
                stamina.Current = Math.Max(0f, stamina.Current - drainPerSecond * deltaTime);
                if (stamina.Current <= 0f) { stamina.IsSprinting = false; stamina.ExhaustedUntilRelease = true; }
            }
            else stamina.Current = Math.Min(stamina.Maximum, stamina.Current + recoveryPerSecond * deltaTime);
        }

        // The caller must confirm actual contact and atomically consume the coin after success.
        public bool TryCollectCoin(int participantId, GridPosition position)
        {
            var match = CurrentMatch;
            if (!IsActive(match, participantId) || !match.Grid.IsConnected(participantId, position)) return false;
            var participant = match.Participants[participantId];
            participant.Coins++;
            if (participant.Coins >= match.CoinsToWin)
            {
                participant.Status = ParticipantStatus.Won;
                FinishIfNoParticipantsRemain(match);
            }
            return true;
        }

        // The caller creates dropped coin objects after a successful defeat.
        public bool TryDamage(int participantId, int damage, int dropCount, out int droppedCoins)
        {
            droppedCoins = 0;
            var match = CurrentMatch;
            if (!IsActive(match, participantId) || damage <= 0 || dropCount < 0) return false;
            var participant = match.Participants[participantId];
            participant.Health = Math.Max(0, participant.Health - damage);
            if (participant.Health == 0)
            {
                droppedCoins = Math.Min(participant.Coins, dropCount);
                participant.Coins -= droppedCoins;
                participant.Status = ParticipantStatus.Respawning;
            }
            return true;
        }

        public bool TryObstacleContact(int participantId)
        {
            var match = CurrentMatch;
            if (!IsActive(match, participantId)) return false;
            var participant = match.Participants[participantId];
            participant.MaxHealth--;
            participant.Health = Math.Min(participant.Health, participant.MaxHealth);
            if (participant.MaxHealth == 0)
            {
                participant.Status = ParticipantStatus.Lost;
                FinishIfNoParticipantsRemain(match);
            }
            return true;
        }

        public bool TryRespawn(int participantId)
        {
            var match = CurrentMatch;
            if (match.Status != MatchStatus.Playing || participantId < 0 || participantId >= match.Participants.Count) return false;
            var participant = match.Participants[participantId];
            if (participant.Status != ParticipantStatus.Respawning) return false;
            participant.Health = participant.MaxHealth;
            participant.Status = ParticipantStatus.Active;
            return true;
        }

        private static bool IsActive(MatchModel match, int id) => match.Status == MatchStatus.Playing && id >= 0 && id < match.Participants.Count && match.Participants[id].Status == ParticipantStatus.Active;

        private static void FinishIfNoParticipantsRemain(MatchModel match)
        {
            foreach (var participant in match.Participants)
                if (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning) return;
            match.Status = MatchStatus.Finished;
        }
    }
}
