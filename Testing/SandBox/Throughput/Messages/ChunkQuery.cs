using NTDLS.ReliableMessaging;

namespace Throughput.Messages
{
    public class ChunkQuery
        : IRmQuery<ChunkQueryReply>
    {
        public byte[] Bytes { get; set; }

        public ChunkQuery(byte[] bytes)
        {
            Bytes = bytes;
        }
    }

    public class ChunkQueryReply
        : IRmQueryReply
    {
    }
}
