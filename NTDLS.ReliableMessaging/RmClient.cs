using NTDLS.ReliableMessaging.Internal;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Connects to a Median RPC Server then sends/received and processes notifications/queries.
    /// </summary>
    public class RmClient : IRmEndpoint
    {
        private TcpClient? _tcpClient;
        private PeerConnection? _activeConnection;
        //private bool _keepRunning;

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// Returns true if the client is connected.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected
            => _tcpClient?.Connected == true;

        #region Events.

        /// <summary>
        /// Event fired when an exception occurs.
        /// </summary>
        public event ExceptionEvent? OnException;
        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="payload">The payload which was involved in the exception.</param>
        public delegate void ExceptionEvent(RmContext context, Exception ex, IRmPayload? payload);

        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        public event ConnectedEvent? OnConnected;
        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        public delegate void ConnectedEvent(RmContext context);

        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        public event DisconnectedEvent? OnDisconnected;
        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        public delegate void DisconnectedEvent(RmContext context);

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedEvent? OnNotificationReceived;
        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="payload"></param>
        public delegate void NotificationReceivedEvent(RmContext context, IRmNotification payload);

        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        public event QueryReceivedEvent? OnQueryReceived;
        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        /// <param name="context">Information about the connection.</param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public delegate IRmQueryReply QueryReceivedEvent(RmContext context, IRmPayload payload);

        #endregion

        /// <summary>
        /// Adds a class that contains notification and query handler functions.
        /// </summary>
        /// <param name="handlerClass"></param>
        public void AddHandler(IRmMessageHandler handlerClass)
        {
            ReflectionCache.AddInstance(handlerClass);
        }

        /// <summary>
        /// Connects to a specified message server by its host name.
        /// </summary>
        /// <param name="hostName">The hostname of the message server.</param>
        /// <param name="port">The listener port of the message server.</param>
        public void Connect(string hostName, int port)
        {
            if (IsConnected)
            {
                throw new Exception("The client is already connected.");
            }

            _tcpClient = new TcpClient(hostName, port);
            _activeConnection = new PeerConnection(this, _tcpClient);
            _activeConnection.RunAsync();
        }

        /// <summary>
        /// Connects to a specified message server by its IP Address.
        /// </summary>
        /// <param name="ipAddress">The IP address of the message server.</param>
        /// <param name="port">The listener port of the message server.</param>
        public void Connect(IPAddress ipAddress, int port)
        {
            if (IsConnected)
            {
                throw new Exception("The client is already connected.");
            }

            _tcpClient = new TcpClient();
            _tcpClient.Connect(ipAddress, port);
            _activeConnection = new PeerConnection(this, _tcpClient);
            _activeConnection.RunAsync();
        }

        /// <summary>
        /// Disconnects the client from the server.
        /// </summary>
        public void Disconnect()
        {
            _activeConnection?.Disconnect(true);
        }

        /// <summary>
        /// Gets the underlying TcpClient.
        /// </summary>
        public TcpClient? GetClient()
        {
            return _activeConnection?.GetClient();
        }

        /// <summary>
        /// Dispatches a one way notification to the connected server.
        /// </summary>
        /// <param name="notification">The notification message to send.</param>
        public void Notify(IRmNotification notification)
        {
            Utility.EnsureNotNull(_activeConnection);
            _activeConnection.SendNotification(notification);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Task<T> Query<T>(IRmQuery<T> query) where T : IRmQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return _activeConnection.SendQuery<T>(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query) where T : IRmQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQueryAsync<T>(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Task<T> Query<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return _activeConnection.SendQuery<T>(query, queryTimeout);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQueryAsync<T>(query, queryTimeout);
        }

        void IRmEndpoint.InvokeOnConnected(RmContext context)
        {
            OnConnected?.Invoke(context);
        }

        void IRmEndpoint.InvokeOnException(RmContext context, Exception ex, IRmPayload? payload)
        {
            OnException?.Invoke(context, ex, payload);
        }

        void IRmEndpoint.InvokeOnDisconnected(RmContext context)
        {
            _activeConnection = null;
            OnDisconnected?.Invoke(context);
        }

        void IRmEndpoint.InvokeOnNotificationReceived(RmContext context, IRmNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification event was not handled and no acceptable handler was able to intercept the notification.");
            }
            OnNotificationReceived.Invoke(context, payload);
        }

        IRmQueryReply IRmEndpoint.InvokeOnQueryReceived(RmContext context, IRmPayload payload)
        {
            if (OnQueryReceived == null)
            {
                throw new Exception("The query event was not handled and no acceptable handler was able to intercept the query.");
            }
            return OnQueryReceived.Invoke(context, payload);
        }
    }
}
