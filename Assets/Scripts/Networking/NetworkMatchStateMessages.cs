using Mirror;

namespace EachOneCoin.Networking
{
    public struct MatchStateMessage : NetworkMessage
    {
        public uint sequence;
        public byte matchStatus, participantMask;
        public float remaining;
        public byte status0, status1, status2, status3;
        public int score0, score1, score2, score3;
        public byte StatusAt(int slot) => slot == 0 ? status0 : slot == 1 ? status1 : slot == 2 ? status2 : status3;
        public int ScoreAt(int slot) => slot == 0 ? score0 : slot == 1 ? score1 : slot == 2 ? score2 : score3;
    }
}
