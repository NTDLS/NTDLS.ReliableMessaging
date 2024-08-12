namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    internal class QueryAwaitingReply
    {
        public Exception? Exception { get; set; }
        public Guid ConnectionId { get; set; }
        public Guid FrameBodyId { get; set; }
        public AutoResetEvent WaitEvent { get; set; } = new(false);
        public IRmQueryReply? ReplyPayload { get; set; }

        public QueryAwaitingReply(Guid frameBodyId, Guid connectionId)
        {
            FrameBodyId = frameBodyId;
            ConnectionId = connectionId;
        }
    }
}
