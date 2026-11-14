using QFramework;

namespace EachOneCoin
{
    public interface IGameModel : IModel
    {
        IReadonlyBindableProperty<int> Coins { get; }
        int AddCoin();
        void ResetCoins();
    }

    public sealed class GameModel : AbstractModel, IGameModel
    {
        private readonly BindableProperty<int> coins = new BindableProperty<int>(0);

        public IReadonlyBindableProperty<int> Coins => coins;

        public int AddCoin()
        {
            coins.Value++;
            return coins.Value;
        }

        public void ResetCoins()
        {
            coins.Value = 0;
        }

        protected override void OnInit()
        {
        }
    }
}
