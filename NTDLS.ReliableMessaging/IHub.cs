using NTDLS.StreamFraming.Payloads;

namespace NTDLS.ReliableMessaging
{
    internal interface IHub
    {
        public void InvokeOnDisconnected(Guid connectionId);
        internal void InvokeOnNotificationReceived(Guid connectionId, IFramePayloadNotification payload);
        internal IFramePayloadQueryReply InvokeOnQueryReceived(Guid connectionId, IFramePayloadQuery payload);
    }
}
