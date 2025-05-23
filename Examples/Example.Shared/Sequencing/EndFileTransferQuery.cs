using NTDLS.ReliableMessaging;

namespace Example.Shared.Sequencing
{
    /// <summary>
    /// Query sent from the client to the server to let it know that the client has finished sending a file.
    /// </summary>
    public class EndFileTransferQuery
        : IRmQuery<EndFileTransferQueryReply>
    {
        public Guid FileId { get; set; }

        public EndFileTransferQuery(Guid fileId)
        {
            FileId = fileId;
        }
    }

    public class EndFileTransferQueryReply
        : IRmQueryReply
    {
    }
}
