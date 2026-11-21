using QFramework;

namespace EachOneCoin
{
    public sealed class CollectCoinCommand : AbstractCommand<int>
    {
        protected override int OnExecute()
        {
            return this.GetModel<IGameModel>().AddCoin();
        }
    }
}
