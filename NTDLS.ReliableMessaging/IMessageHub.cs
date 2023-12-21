using NTDLS.StreamFraming.Payloads;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    internal interface IMessageHub
    {
        internal void InvokeOnException(Guid connectionId, Exception ex);
        internal void InvokeOnConnected(Guid connectionId, TcpClient tcpClient);
        internal void InvokeOnDisconnected(Guid connectionId);
        internal void InvokeOnNotificationReceived(Guid connectionId, IFramePayloadNotification payload);
        internal IFramePayloadQueryReply InvokeOnQueryReceived(Guid connectionId, IFramePayloadQuery payload);
    }
}
