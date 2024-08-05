namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    internal class QueryAwaitingReply
    {
        public Guid FrameBodyId { get; set; }
        public AutoResetEvent WaitEvent { get; set; } = new(false);
        public IRmQueryReply? ReplyPayload { get; set; }

        public QueryAwaitingReply(Guid frameBodyId)
        {
            FrameBodyId = frameBodyId;
        }
    }
}
