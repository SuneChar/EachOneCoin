using System.Collections.Generic;
using QFramework;

namespace EachOneCoin
{
    public sealed class MatchSessionModel : AbstractModel
    {
        public MatchModel Match { get; private set; }
        public StaminaModel Stamina { get; private set; }

        protected override void OnInit() { }

        public MatchModel CreateMatch(int width, int height, IReadOnlyList<GridPosition> spawns, int maxHealth, float maxStamina, int coinsToWin = 10, float duration = 60f)
        {
            var match = new MatchModel(width, height, spawns, maxHealth, coinsToWin, duration);
            var stamina = new StaminaModel(maxStamina);
            Match = match;
            Stamina = stamina;
            return match;
        }

        public void ClearMatch(MatchModel match)
        {
            if (match == null || !ReferenceEquals(Match, match)) return;
            Match = null;
            Stamina = null;
        }

        protected override void OnDeinit()
        {
            Match = null;
            Stamina = null;
        }
    }
}
