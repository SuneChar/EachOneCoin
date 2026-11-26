using Mirror;

namespace EachOneCoin.Networking
{
    public struct GridSnapshotMessage : NetworkMessage
    {
        public uint version;
        // Row-major by x, then y: 0 means empty; 1..4 identify the owner.
        public byte[] owners;
    }

    public struct GridChangeMessage : NetworkMessage
    {
        public uint version;
        public ushort x, y;
        public byte owner;
    }

    public struct GridSnapshotRequest : NetworkMessage { }
}
