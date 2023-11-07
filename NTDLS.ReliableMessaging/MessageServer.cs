using NTDLS.Semaphore;
using NTDLS.StreamFraming.Payloads;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    public class MessageServer : IMessageHub
    {
        public event NotificationReceivedEvent? OnNotificationReceived;
        public event QueryReceivedEvent? OnQueryReceived;
        public event DisconnectedEvent? OnDisconnected;

        public delegate void DisconnectedEvent(Guid connectionId);
        public delegate void NotificationReceivedEvent(Guid connectionId, IFrameNotification payload);
        public delegate IFrameQueryReply QueryReceivedEvent(Guid connectionId, IFrameQuery payload);

        private TcpListener? _listener;
        private readonly CriticalResource<List<PeerConnection>> _activeConnections = new();
        private readonly Thread? _listenerThreadProc;
        private bool _keepRunning;

        public MessageServer()
        {
            _listenerThreadProc = new Thread(ListenerThreadProc);
        }

        /// <summary>
        /// Starts the message server.
        /// </summary>
        public void Start(int listenPort)
        {
            if (_keepRunning)
            {
                return;
            }
            _listener = new TcpListener(IPAddress.Any, listenPort);
            _keepRunning = true;
            _listenerThreadProc?.Start();
        }

        /// <summary>
        /// Stops the message server.
        /// </summary>
        public void Stop()
        {
            _keepRunning = false;
            _listenerThreadProc?.Join();

            _activeConnections.Use((o) => o.ForEach(c => c.Disconnect(true)));
        }

        void ListenerThreadProc()
        {
            Utility.EnsureNotNull(_listener);

            Thread.CurrentThread.Name = $"ListenerThreadProc:{Thread.CurrentThread.ManagedThreadId}";

            _listener.Start();

            while (_keepRunning)
            {
                var tcpClient = _listener.AcceptTcpClient(); //Wait for an inbound connection.

                if (tcpClient.Connected)
                {
                    if (_keepRunning) //Check again, we may have received a connection while shutting down.
                    {
                        var activeConnection = new PeerConnection(this, tcpClient);
                        _activeConnections.Use((o) => o.Add(activeConnection));
                        activeConnection.RunAsync();
                    }
                }
            }
        }

        public void SendNotification(Guid connectionId, IFrameNotification notification)
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            connection.SendNotification(notification);
        }

        public void InvokeOnNotificationReceived(Guid connectionId, IFrameNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification hander event was not handled.");
            }
            OnNotificationReceived.Invoke(connectionId, payload);
        }

        public void InvokeOnDisconnected(Guid connectionId)
        {
            _activeConnections.Use((o) => o.RemoveAll(o => o.Id == connectionId));
            OnDisconnected?.Invoke(connectionId);
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
