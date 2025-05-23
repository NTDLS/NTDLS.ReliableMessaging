using NTDLS.ReliableMessaging;

namespace Example.Shared.Sequencing
{
    public class FileChunkNotification
        : IRmNotification
    {
        public Guid FileId { get; set; }
        public long Sequence { get; set; }
        public byte[] Bytes { get; set; }

        public FileChunkNotification(Guid fileId, byte[] bytes, long sequence)
        {
            FileId = fileId;
            Bytes = bytes;
            Sequence = sequence;
        }
    }
}
