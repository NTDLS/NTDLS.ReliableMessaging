using NTDLS.ReliableMessaging;

namespace Example.Shared.Sequencing
{
    /// <summary>
    /// Query sent from the client to the server to let it know that a file transfer is about to begin.
    /// </summary>
    public class BeginFileTransferQuery
        : IRmQuery<BeginFileTransferQueryReply>
    {
        public Guid FileId { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }

        public BeginFileTransferQuery(Guid fileId, string fileName, long fileSize)
        {
            FileId = fileId;
            FileName = fileName;
            FileSize = fileSize;
        }
    }

    public class BeginFileTransferQueryReply
        : IRmQueryReply
    {
    }
}
