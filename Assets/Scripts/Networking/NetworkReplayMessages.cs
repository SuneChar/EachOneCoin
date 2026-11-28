using Mirror;

namespace EachOneCoin.Networking
{
    public struct ReplayTransferBegin : NetworkMessage
    {
        public int length;
        public string sha256;
    }
    public struct ReplayTransferChunk : NetworkMessage
    {
        public int offset;
        public byte[] bytes;
    }
    public struct ReplayTransferEnd : NetworkMessage { }
}
