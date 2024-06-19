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
        private readonly RmConfiguration _configuration;

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// A user settable object that can be accessed via the Context.Endpoint.Parameter Especially useful for convention based calls.
        /// </summary>
        public object? Parameter { get => _configuration.Parameter; set => _configuration.Parameter = value; }

        /// <summary>
        /// Returns true if the client is connected.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected => _tcpClient?.Connected == true;

        #region Events.

        /// <summary>
        /// Event fired when an exception occurs.
        /// </summary>
        public event ExceptionEvent? OnException;
        /// <summary>
        /// Event fired when an exception occurs.
        /// </summary>
        /// <param name="context">Information about the connection, if any.</param>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="payload">The payload which was involved in the exception, if any.</param>
        public delegate void ExceptionEvent(RmContext? context, Exception ex, IRmPayload? payload);

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
        /// Creates a new instance of RmClient with the default configuration.
        /// </summary>
        public RmClient()
        {
            _configuration = new();
        }

        /// Creates a new instance of RmClient with the given configuration.
        public RmClient(RmConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Adds a class that contains notification and query handler functions.
        /// </summary>
        /// <param name="handlerClass"></param>
        public void AddHandler(IRmMessageHandler handlerClass)
            => ReflectionCache.AddInstance(handlerClass);

        #region IRmSerializationProvider.

        /// <summary>
        /// Sets the custom serialization provider. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetSerializationProvider(IRmSerializationProvider? provider)
        {
            _configuration.SerializationProvider = provider;
            _activeConnection?.Context.SetSerializationProvider(provider);
        }

        /// <summary>
        /// Removes the serialization provider set by a previous call to SetSerializationProvider().
        /// </summary>
        public void ClearSerializationProvider()
        {
            _configuration.SerializationProvider = null;
            _activeConnection?.Context.SetSerializationProvider(null);
        }

        #endregion

        #region IRmCompressionProvider.

        /// <summary>
        /// Sets the compression provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCompressionProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCompressionProvider(IRmCompressionProvider? provider)
        {
            _configuration.CompressionProvider = provider;
            _activeConnection?.Context.SetCompressionProvider(provider);
        }

        /// <summary>
        /// Removes the compression provider set by a previous call to SetCompressionProvider().
        /// </summary>
        public void ClearCompressionProvider()
        {
            _configuration.CompressionProvider = null;
            _activeConnection?.Context.SetCryptographyProvider(null);
        }

        #endregion

        #region IRmCryptographyProvider.

        /// <summary>
        /// Sets the encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCryptographyProvider(IRmCryptographyProvider? provider)
        {
            _configuration.CryptographyProvider = provider;
            _activeConnection?.Context.SetCryptographyProvider(provider);
        }

        /// <summary>
        /// Removes the encryption provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCryptographyProvider()
        {
            _configuration.CryptographyProvider = null;
            _activeConnection?.Context.SetCryptographyProvider(null);
        }

        #endregion

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
            _activeConnection = new PeerConnection(this, _tcpClient, _configuration,
                _configuration.SerializationProvider, _configuration.CompressionProvider, _configuration.CryptographyProvider);
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
            _activeConnection = new PeerConnection(this, _tcpClient, _configuration,
                                _configuration.SerializationProvider, _configuration.CompressionProvider, _configuration.CryptographyProvider);
            _activeConnection.RunAsync();
        }

        /// <summary>
        /// Disconnects the client from the server.
        /// </summary>
        public void Disconnect()
            => _activeConnection?.Disconnect(true);

        /// <summary>
        /// Gets the connection context.
        /// </summary>
        public RmContext? GetClient()
            => _activeConnection?.Context;

        /// <summary>
        /// Dispatches a one way notification to the connected server.
        /// </summary>
        /// <param name="notification">The notification message to send.</param>
        public void Notify(IRmNotification notification)
            => _activeConnection.EnsureNotNull().Context.Notify(notification);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query) where T : IRmQueryReply
            => _activeConnection.EnsureNotNull().Context.Query<T>(query);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns>Returns the result of the query.</returns>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query) where T : IRmQueryReply
            => await _activeConnection.EnsureNotNull().Context.QueryAsync<T>(query);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => _activeConnection.EnsureNotNull().Context.Query<T>(query, queryTimeout);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => await _activeConnection.EnsureNotNull().Context.QueryAsync<T>(query, queryTimeout);

        void IRmEndpoint.InvokeOnConnected(RmContext context)
            => OnConnected?.Invoke(context);

        void IRmEndpoint.InvokeOnException(RmContext? context, Exception ex, IRmPayload? payload)
            => OnException?.Invoke(context, ex, payload);

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
