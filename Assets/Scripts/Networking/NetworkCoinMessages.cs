using Mirror;

namespace EachOneCoin.Networking
{
    public struct CoinEntry
    {
        public uint id;
        public byte kind;
        public ushort x, y;
    }
    public struct CoinSnapshotMessage : NetworkMessage
    {
        public uint version;
        public CoinEntry[] entries;
        public int score0, score1, score2, score3;
    }
    // Operation: 0 spawn, 1 remove. Scores are authoritative after the operation.
    public struct CoinChangeMessage : NetworkMessage
    {
        public uint version;
        public byte operation;
        public CoinEntry entry;
        public int score0, score1, score2, score3;
    }
    public struct CoinSnapshotRequest : NetworkMessage { }
    public struct MineStatusMessage : NetworkMessage
    {
        public uint sequence;
        public double elapsed;
        public int nextEvent;
        public int warningMine;
        public ushort[] warningX, warningY;
    }
}
