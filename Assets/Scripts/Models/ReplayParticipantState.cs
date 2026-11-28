namespace EachOneCoin
{
    // Plain rule-state projection used when viewing a recorded match.
    public struct ReplayParticipantState
    {
        public bool participating;
        public int health, maxHealth, coins, finishOrder, status;
        public float stamina;
    }
}
