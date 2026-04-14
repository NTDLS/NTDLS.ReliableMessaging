namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    internal class RmQueryAwaitingReply
    {
        public Guid ConnectionId { get; set; }
        public Guid FrameBodyId { get; set; }
        public TaskCompletionSource<IRmQueryReply> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RmQueryAwaitingReply(Guid frameBodyId, Guid connectionId)
        {
            FrameBodyId = frameBodyId;
            ConnectionId = connectionId;
        }
    }
}
