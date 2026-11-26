using Mirror;

namespace EachOneCoin.Networking
{
    public struct CoinEntry
    {
        public uint id;
        public byte kind;
        public ushort x, y;
        public bool revealed;
    }
    public struct CoinSnapshotMessage : NetworkMessage
    {
        public uint version;
        public CoinEntry[] entries;
        public byte scatteredMines;
        public int score0, score1, score2, score3;
    }
    // Operation: 0 spawn, 1 remove, 2 update reveal, 3 mine state. Scores are authoritative after the operation.
    public struct CoinChangeMessage : NetworkMessage
    {
        public uint version;
        public byte operation;
        public CoinEntry entry;
        public byte scatteredMines;
        public int score0, score1, score2, score3;
    }
    public struct CoinSnapshotRequest : NetworkMessage { }
    public struct MineStatusMessage : NetworkMessage
    {
        public float[] progress;
        public double[] elapsed;
        public double testElapsed;
        public uint connectedMask;
        public byte scatteredMines;
    }
}
