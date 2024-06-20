using NTDLS.Helpers;
using NTDLS.ReliableMessaging.Internal;
using NTDLS.Semaphore;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Listens for connections from Median RPC Clients and processes the incoming notifications/queries.
    /// </summary>
    public class RmServer : IRmEndpoint
    {
        private TcpListener? _listener;
        private readonly PessimisticCriticalResource<List<PeerConnection>> _activeConnections = new();
        private Thread? _listenerThreadProc;
        private bool _keepRunning;
        private readonly RmConfiguration _configuration;

        /// <summary>
        /// A user settable object that can be accessed via the Context.Endpoint.Parameter Especially useful for convention based calls.
        /// </summary>
        public object? Parameter { get => _configuration.Parameter; set => _configuration.Parameter = value; }

        /// <summary>
        /// Cache of class instances and method reflection information for message handlers.
        /// </summary>
        public ReflectionCache ReflectionCache { get; private set; } = new();

        /// <summary>
        /// Creates a new instance of RmServer with the default configuration.
        /// </summary>
        public RmServer()
        {
            _configuration = new();
        }

        /// <summary>
        /// Creates a new instance of RmServer with the given configuration.
        /// </summary>
        public RmServer(RmConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region Events.

        /// <summary>
        /// Event fired when an exception occurs.
        /// </summary>
        public event RmEvents.ExceptionEvent? OnException;

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

        #region IRmSerializationProvider.

        /// <summary>
        /// Sets the custom serialization provider. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetSerializationProvider(IRmSerializationProvider? provider)
        {
            _activeConnections.Use((o) =>
            {
                _configuration.SerializationProvider = provider;
                o.ForEach(c => c.Context.SetSerializationProvider(provider));
            });
        }

        /// <summary>
        /// Removes the custom serialization provider set by a previous call to SetSerializationProvider().
        /// </summary>
        public void ClearSerializationProvider()
        {
            _activeConnections.Use((o) =>
            {
                _configuration.SerializationProvider = null;
                o.ForEach(c => c.Context.SetSerializationProvider(null));
            });
        }

        #endregion

        #region IRmCompressionProvider.

        /// <summary>
        /// Sets the compression provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCompressionProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCompressionProvider(IRmCompressionProvider? provider)
        {
            _activeConnections.Use((o) =>
            {
                _configuration.CompressionProvider = provider;
                o.ForEach(c => c.Context.SetCompressionProvider(provider));
            });
        }

        /// <summary>
        /// Removes the compression provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCompressionProvider()
        {
            _activeConnections.Use((o) =>
            {
                _configuration.CompressionProvider = null;
                o.ForEach(c => c.Context.SetCompressionProvider(null));
            });
        }

        #endregion

        #region IRmCryptographyProvider.

        /// <summary>
        /// Sets the encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCryptographyProvider(IRmCryptographyProvider? provider)
        {
            _activeConnections.Use((o) =>
            {
                _configuration.CryptographyProvider = provider;
                o.ForEach(c => c.Context.SetCryptographyProvider(provider));
            });
        }

        /// <summary>
        /// Removes the encryption provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCryptographyProvider()
        {
            _activeConnections.Use((o) =>
            {
                _configuration.CryptographyProvider = null;
                o.ForEach(c => c.Context.SetCryptographyProvider(null));
            });
        }

        #endregion

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

            _listener.Start();

            _keepRunning = true;
            _listenerThreadProc = new Thread(ListenerThreadProc);
            _listenerThreadProc.Start();
        }

        /// <summary>
        /// Stops the message server.
        /// </summary>
        public void Stop()
        {
            if (_keepRunning == false)
            {
                return;
            }
            _keepRunning = false;
            Exceptions.Ignore(() => _listener?.Stop());
            _listenerThreadProc?.Join();
            _activeConnections.Use((o) =>
            {
                o.ForEach(c => c.Disconnect(true));
                o.Clear();
            });
        }

        /// <summary>
        /// Gets the connection context.
        /// </summary>
        /// <param name="connectionId">The connection to get the context for.</param>
        public RmContext? GetContext(Guid connectionId)
        {
            return _activeConnections.Use((o) =>
            {
                return o.Where(c => c.ConnectionId == connectionId).FirstOrDefault()?.Context;
            });
        }

        /// <summary>
        /// Disconnects the connection with the given connection id. Does not wait on the thread to exit.
        /// </summary>
        /// <param name="connectionId">The connection to disconnect.</param>
        public void Disconnect(Guid connectionId)
        {
            _activeConnections.Use((o) =>
            {
                o.Where(c => c.ConnectionId == connectionId).FirstOrDefault()?.Disconnect(false);
            });
        }

        /// <summary>
        /// Disconnects the connection with the given connection id.
        /// </summary>
        /// <param name="connectionId">The connection to disconnect.</param>
        /// <param name="waitOnThreadToExit">Whether or not the server should wait on the client thread to exit before returning.</param>
        public void Disconnect(Guid connectionId, bool waitOnThreadToExit)
        {
            _activeConnections.Use((o) =>
            {
                o.Where(c => c.ConnectionId == connectionId).FirstOrDefault()?.Disconnect(waitOnThreadToExit);
            });
        }

        void ListenerThreadProc()
        {
            while (_keepRunning)
            {
                try
                {
                    Thread.CurrentThread.Name = $"ListenerThreadProc:{Environment.CurrentManagedThreadId}";

                    var tcpClient = _listener.EnsureNotNull().AcceptTcpClient(); //Wait for an inbound connection.

                    if (tcpClient.Connected)
                    {
                        if (_keepRunning) //Check again, we may have received a connection while shutting down.
                        {
                            var activeConnection = new PeerConnection(this, tcpClient, _configuration,
                                _configuration.SerializationProvider, _configuration.CompressionProvider, _configuration.CryptographyProvider);
                            _activeConnections.Use((o) => o.Add(activeConnection));
                            activeConnection.RunAsync();
                        }
                    }

                    Thread.Sleep(1);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.Interrupted
                        || ex.SocketErrorCode == SocketError.Shutdown)
                    {
                        //These are expected exceptions.
                    }
                    else
                    {
                        OnException?.Invoke(null, ex, null);
                        Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    OnException?.Invoke(null, ex, null);
                    Thread.Sleep(10);
                }
            }
        }

        /// <summary>
        /// Dispatches a one way notification to the specified connection.
        /// </summary>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="notification">The notification message to send.</param>
        /// <exception cref="Exception"></exception>
        public void Notify(Guid connectionId, IRmNotification notification)
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.ConnectionId == connectionId).FirstOrDefault())
                ?? throw new Exception($"The connection with id {connectionId} was not found.");

            connection.Context.Notify(notification);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply, using the default timeout.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> Query<T>(Guid connectionId, IRmQuery<T> query) where T : IRmQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.ConnectionId == connectionId).FirstOrDefault())
                ?? throw new Exception($"The connection with id {connectionId} was not found.");

            return await connection.Context.Query(query, _configuration.QueryResponseTimeoutMs);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply, using the default timeout.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> QueryAsync<T>(Guid connectionId, IRmQuery<T> query) where T : IRmQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.ConnectionId == connectionId).FirstOrDefault())
                ?? throw new Exception($"The connection with id {connectionId} was not found.");

            return await connection.Context.QueryAsync(query, _configuration.QueryResponseTimeoutMs);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query (-1 = infinite).</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> Query<T>(Guid connectionId, IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.ConnectionId == connectionId).FirstOrDefault())
                ?? throw new Exception($"The connection with id {connectionId} was not found.");

            return await connection.Context.Query(query, queryTimeout);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query (-1 = infinite).</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T> QueryAsync<T>(Guid connectionId, IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.ConnectionId == connectionId).FirstOrDefault())
                ?? throw new Exception($"The connection with id {connectionId} was not found.");

            return await connection.Context.QueryAsync(query, queryTimeout);
        }

        void IRmEndpoint.InvokeOnConnected(RmContext context)
        {
            OnConnected?.Invoke(context);
        }

        void IRmEndpoint.InvokeOnException(RmContext? context, Exception ex, IRmPayload? payload)
        {
            OnException?.Invoke(context, ex, payload);
        }

        void IRmEndpoint.InvokeOnDisconnected(RmContext context)
        {
            if (_keepRunning) //Avoid a race condition with the client thread waiting on a lock on _activeConnections that is held by Server.Stop().
            {
                _activeConnections.Use((o) => o.RemoveAll(o => o.ConnectionId == context.ConnectionId));
            }
            OnDisconnected?.Invoke(context);
        }

        void IRmEndpoint.InvokeOnNotificationReceived(RmContext context, IRmNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification hander event was not handled.");
            }
            OnNotificationReceived.Invoke(context, payload);
        }

        IRmQueryReply IRmEndpoint.InvokeOnQueryReceived(RmContext context, IRmPayload payload)
        {
            if (OnQueryReceived == null)
            {
                throw new Exception("The query hander event was not handled.");
            }
            return OnQueryReceived.Invoke(context, payload);
        }
    }
}