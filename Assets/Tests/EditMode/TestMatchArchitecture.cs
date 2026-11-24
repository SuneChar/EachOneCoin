using QFramework;

namespace EachOneCoin.Tests
{
    public sealed class TestMatchArchitecture : Architecture<TestMatchArchitecture>
    {
        protected override void Init()
        {
            this.RegisterModel<MatchSessionModel>(new MatchSessionModel());
            this.RegisterSystem<MatchSystem>(new MatchSystem());
        }
    }
}
