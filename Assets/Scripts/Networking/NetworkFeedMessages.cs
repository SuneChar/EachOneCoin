using Mirror;

namespace EachOneCoin.Networking
{
    public struct MatchFeedEventMessage : NetworkMessage
    {
        public byte eventType;
        public int primarySlot;
        public int secondarySlot;
        public int mineIndex;
        public int value;
        public byte reason;
    }
}
