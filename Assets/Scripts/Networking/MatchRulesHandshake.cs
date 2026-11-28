using System.Collections.Generic;

namespace EachOneCoin.Networking
{
    public sealed class MatchRulesHandshake
    {
        private readonly Dictionary<int, MatchRulesMessage> pending = new Dictionary<int, MatchRulesMessage>();

        public bool Offer(int connectionId, MatchRulesMessage rules)
        {
            if (connectionId < 0 || !MatchRulesProtocol.IsValid(rules)) return false;
            pending[connectionId] = rules;
            return true;
        }

        public bool IsPending(int connectionId) => pending.ContainsKey(connectionId);

        public bool Confirm(int connectionId, MatchRulesAcknowledged acknowledgement)
        {
            if (!pending.TryGetValue(connectionId, out var rules) || !MatchRulesProtocol.Matches(rules, acknowledgement)) return false;
            pending.Remove(connectionId);
            return true;
        }

        public void Remove(int connectionId) => pending.Remove(connectionId);
        public void Clear() => pending.Clear();
    }
}
