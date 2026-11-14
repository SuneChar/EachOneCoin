using QFramework;

namespace EachOneCoin
{
    public enum GameSessionState { Waiting, Playing, Won, Lost, Paused }

    public interface IGameModel : IModel
    {
        IReadonlyBindableProperty<int> Coins { get; }
        IReadonlyBindableProperty<int> Health { get; }
        IReadonlyBindableProperty<GameSessionState> State { get; }
        void ResetState();
        bool TryChangeState(GameSessionState next);
        int AddCoin();
        void ResetCoins();
        void ResetHealth(int maxHealth);
        int TakeDamage(int amount);
    }

    public sealed class GameModel : AbstractModel, IGameModel
    {
        private readonly BindableProperty<int> coins = new BindableProperty<int>(0);
        private readonly BindableProperty<int> health = new BindableProperty<int>(1);
        private readonly BindableProperty<GameSessionState> state = new BindableProperty<GameSessionState>(GameSessionState.Waiting);

        public IReadonlyBindableProperty<int> Coins => coins;
        public IReadonlyBindableProperty<int> Health => health;
        public IReadonlyBindableProperty<GameSessionState> State => state;

        public void ResetState()
        {
            state.Value = GameSessionState.Waiting;
        }

        public bool TryChangeState(GameSessionState next)
        {
            bool allowed =
                (state.Value == GameSessionState.Waiting && next == GameSessionState.Playing) ||
                (state.Value == GameSessionState.Playing &&
                    (next == GameSessionState.Paused || next == GameSessionState.Won || next == GameSessionState.Lost)) ||
                (state.Value == GameSessionState.Paused && next == GameSessionState.Playing);
            if (!allowed) return false;
            state.Value = next;
            return true;
        }

        public int AddCoin()
        {
            coins.Value++;
            return coins.Value;
        }

        public void ResetCoins()
        {
            coins.Value = 0;
        }

        public void ResetHealth(int maxHealth)
        {
            health.Value = System.Math.Max(1, maxHealth);
        }

        public int TakeDamage(int amount)
        {
            if (amount > 0)
                health.Value = amount >= health.Value ? 0 : health.Value - amount;
            return health.Value;
        }

        protected override void OnInit()
        {
        }
    }
}
