using QFramework;

namespace EachOneCoin
{
    public sealed class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            this.RegisterModel<IGameModel>(new GameModel());
        }
    }
}
