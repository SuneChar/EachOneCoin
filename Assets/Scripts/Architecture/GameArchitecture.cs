using QFramework;

namespace EachOneCoin
{
    public sealed class GameArchitecture : Architecture<GameArchitecture>
    {
        protected override void Init()
        {
            this.RegisterModel<MatchSessionModel>(new MatchSessionModel());
            this.RegisterSystem<MatchSystem>(new MatchSystem());
        }
    }
}
