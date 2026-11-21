namespace EachOneCoin
{
    public enum ParticipantStatus { Active, Respawning, Won, Lost }

    public sealed class ParticipantState
    {
        public int Id { get; }
        public GridPosition Spawn { get; }
        public int Coins { get; internal set; }
        public int Health { get; internal set; }
        public int MaxHealth { get; internal set; }
        public ParticipantStatus Status { get; internal set; }

        internal ParticipantState(int id, GridPosition spawn, int maxHealth)
        {
            Id = id;
            Spawn = spawn;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Status = ParticipantStatus.Active;
        }
    }
}
