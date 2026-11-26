using System.Collections.Generic;
using QFramework;

namespace EachOneCoin
{
    public sealed class MatchSessionModel : AbstractModel
    {
        public MatchModel Match { get; private set; }
        private StaminaModel[] stamina;
        public StaminaModel Stamina => stamina != null && stamina.Length > 0 ? stamina[0] : null;
        public StaminaModel StaminaAt(int participantId) => stamina[participantId];

        protected override void OnInit() { }

        public MatchModel CreateMatch(int width, int height, IReadOnlyList<GridPosition> spawns, int maxHealth, float maxStamina, int coinsToWin = 10, float duration = 60f)
        {
            var match = new MatchModel(width, height, spawns, maxHealth, coinsToWin, duration);
            var models = new StaminaModel[spawns.Count];
            for (int i = 0; i < models.Length; i++) models[i] = new StaminaModel(maxStamina);
            Match = match;
            stamina = models;
            return match;
        }

        public void ClearMatch(MatchModel match)
        {
            if (match == null || !ReferenceEquals(Match, match)) return;
            Match = null;
            stamina = null;
        }

        protected override void OnDeinit()
        {
            Match = null;
            stamina = null;
        }
    }
}
