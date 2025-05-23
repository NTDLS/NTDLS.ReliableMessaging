using NTDLS.ReliableMessaging;

namespace Throughput.Messages
{
    public class ChunkNotification
        : IRmNotification
    {
        public byte[] Bytes { get; set; }

        public ChunkNotification(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}
