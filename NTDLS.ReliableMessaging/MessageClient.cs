using NTDLS.StreamFraming.Payloads;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    public class MessageClient : IMessageHub
    {
        public event NotificationReceivedEvent? OnNotificationReceived;
        public event QueryReceivedEvent? OnQueryReceived;
        public event DisconnectedEvent? OnDisconnected;

        public delegate void DisconnectedEvent(Guid connectionId);
        public delegate void NotificationReceivedEvent(Guid connectionId, IFrameNotification payload);
        public delegate IFrameQueryReply QueryReceivedEvent(Guid connectionId, IFrameQuery payload);

        private readonly TcpClient _client = new();
        private PeerConnection? _activeConnection;
        private bool _keepRunning;

        public void Connect(string hostName, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _client.Connect(hostName, port);
            _activeConnection = new PeerConnection(this, _client);
            _activeConnection.RunAsync();
        }

        public void Connect(IPAddress ipAddress, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _client.Connect(ipAddress, port);
            _activeConnection = new PeerConnection(this, _client);
            _activeConnection.RunAsync();
        }

        public void Disconnect()
        {
            _keepRunning = false;
            _activeConnection?.Disconnect(true);
        }

        public void SendNotification(IFrameNotification notification)
        {
            Utility.EnsureNotNull(_activeConnection);
            _activeConnection.SendNotification(notification);
        }

        public async Task<T?> SendQuery<T>(IFrameQuery query) where T : IFrameQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQuery<T>(query);
        }

        public void InvokeOnDisconnected(Guid connectionId)
        {
            _activeConnection = null;
            OnDisconnected?.Invoke(connectionId);
        }

        public void InvokeOnNotificationReceived(Guid connectionId, IFrameNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification hander event was not handled.");
            }
            OnNotificationReceived.Invoke(connectionId, payload);
        }

        public IFrameQueryReply InvokeOnQueryReceived(Guid connectionId, IFrameQuery payload)
        {
            if (OnQueryReceived == null)
            {
                throw new Exception("The query hander event was not handled.");
            }
            return OnQueryReceived.Invoke(connectionId, payload);
        }
    }
}
