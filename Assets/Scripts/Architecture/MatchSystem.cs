using System;
using System.Collections.Generic;
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

        public void ConfigureParticipants(byte mask)
        {
            var match = CurrentMatch;
            if (match.Status != MatchStatus.Waiting) throw new InvalidOperationException("Participants must be configured before starting.");
            int validMask = (1 << match.Participants.Count) - 1;
            if ((mask & validMask) == 0 || (mask & ~validMask) != 0) throw new ArgumentOutOfRangeException(nameof(mask));
            for (int i = 0; i < match.Participants.Count; i++) match.Participants[i].IsParticipating = (mask & (1 << i)) != 0;
        }

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
                if (participant.IsParticipating && (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning))
                    participant.Status = ParticipantStatus.Lost;
            match.Status = MatchStatus.Finished;
        }

        public void ConfigureMines(IReadOnlyList<GridPosition> positions)
        {
            var match = CurrentMatch;
            if (positions == null || positions.Count == 0) throw new ArgumentException("Mine positions are required.", nameof(positions));
            if (match.Mines.Count != 0) throw new InvalidOperationException("Mines have already been configured.");
            var seen = new HashSet<GridPosition>();
            foreach (var position in positions)
            {
                if (!match.Grid.Contains(position) || !seen.Add(position)) throw new ArgumentException("Mine positions must be unique and inside the grid.", nameof(positions));
                foreach (var participant in match.Participants)
                    if (participant.Spawn.Equals(position)) throw new ArgumentException("A mine cannot occupy a spawn.", nameof(positions));
            }
            var mines = new GridPosition[positions.Count];
            for (int i = 0; i < mines.Length; i++) mines[i] = positions[i];
            foreach (var position in mines) match.Grid.SetBlocked(position, true);
            match.Mines = Array.AsReadOnly(mines);
        }

        public bool TryPaint(int participantId, GridPosition position)
        {
            var match = CurrentMatch;
            var grid = match.Grid;
            if (!IsActive(match, participantId) || !grid.Contains(position) || grid.IsBlocked(position)) return false;
            for (int i = 0; i < match.Participants.Count; i++)
                if (i != participantId && match.Participants[i].Spawn.Equals(position)) return false;
            if (grid.OwnerAt(position) == participantId) return false;
            grid.SetOwner(position, participantId);
            return true;
        }

        public void TickStamina(float deltaTime, bool active, bool moving, bool sprintHeld, float drainPerSecond, float recoveryPerSecond)
            => TickStamina(0, deltaTime, active, moving, sprintHeld, drainPerSecond, recoveryPerSecond);

        public void TickStamina(int participantId, float deltaTime, bool active, bool moving, bool sprintHeld, float drainPerSecond, float recoveryPerSecond)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (float.IsNaN(drainPerSecond) || float.IsInfinity(drainPerSecond) || drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            if (float.IsNaN(recoveryPerSecond) || float.IsInfinity(recoveryPerSecond) || recoveryPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(recoveryPerSecond));
            var match = CurrentMatch;
            if (participantId < 0 || participantId >= match.Participants.Count) throw new ArgumentOutOfRangeException(nameof(participantId));
            var stamina = session.StaminaAt(participantId);
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
            return AwardCoin(match, participantId);
        }

        private static bool AwardCoin(MatchModel match, int participantId)
        {
            var participant = match.Participants[participantId];
            participant.Coins++;
            if (participant.Coins >= match.CoinsToWin)
            {
                int order = 1;
                foreach (var other in match.Participants)
                    if (other.FinishOrder >= order) order = other.FinishOrder + 1;
                participant.FinishOrder = order;
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
            if (match.Status != MatchStatus.Playing || participantId < 0 || participantId >= match.Participants.Count || !match.Participants[participantId].IsParticipating) return false;
            var participant = match.Participants[participantId];
            if (participant.Status != ParticipantStatus.Respawning) return false;
            participant.Health = participant.MaxHealth;
            participant.Status = ParticipantStatus.Active;
            return true;
        }

        public bool Forfeit(int participantId)
        {
            var match = CurrentMatch;
            if (match.Status != MatchStatus.Playing || participantId < 0 || participantId >= match.Participants.Count) return false;
            var participant = match.Participants[participantId];
            if (!participant.IsParticipating || (participant.Status != ParticipantStatus.Active && participant.Status != ParticipantStatus.Respawning)) return false;
            participant.Status = ParticipantStatus.Lost;
            FinishIfNoParticipantsRemain(match);
            return true;
        }

        private static bool IsActive(MatchModel match, int id) => match.Status == MatchStatus.Playing && id >= 0 && id < match.Participants.Count && match.Participants[id].IsParticipating && match.Participants[id].Status == ParticipantStatus.Active;

        private static void FinishIfNoParticipantsRemain(MatchModel match)
        {
            foreach (var participant in match.Participants)
                if (participant.IsParticipating && (participant.Status == ParticipantStatus.Active || participant.Status == ParticipantStatus.Respawning)) return;
            match.Status = MatchStatus.Finished;
        }
    }
}
