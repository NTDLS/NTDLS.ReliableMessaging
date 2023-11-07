using NTDLS.StreamFraming.Payloads;

namespace NTDLS.ReliableMessaging
{
    internal interface IMessageHub
    {
        internal void InvokeOnConnected(Guid connectionId);
        internal void InvokeOnDisconnected(Guid connectionId);
        internal void InvokeOnNotificationReceived(Guid connectionId, IFrameNotification payload);
        internal IFrameQueryReply InvokeOnQueryReceived(Guid connectionId, IFrameQuery payload);
    }
}
