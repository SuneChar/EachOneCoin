using System;
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

    internal static class TestSessionGuard
    {
        public static void EnsureAvailable(MatchSessionModel session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Match != null)
                throw new InvalidOperationException("Arena integration tests require an empty GameArchitecture session; stop the active match before running them.");
        }
    }
}
