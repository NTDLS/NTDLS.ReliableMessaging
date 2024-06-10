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
        private IRmCryptographyProvider? _cryptographyProvider = null;
        private readonly RmConfiguration _configuration;

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
        public event ExceptionEvent? OnException;
        /// <summary>
        /// Event fired when a client connects to the server.
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
        /// Adds a class that contains notification and query handler functions.
        /// </summary>
        /// <param name="handlerClass"></param>
        public void AddHandler(IRmMessageHandler handlerClass)
        {
            ReflectionCache.AddInstance(handlerClass);
        }

        /// <summary>
        /// Sets the encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        /// <param name="provider"></param>
        public void SetCryptographyProvider(IRmCryptographyProvider? provider)
        {
            _activeConnections.Use((o) =>
            {
                _cryptographyProvider = provider;
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
                _cryptographyProvider = null;
                o.ForEach(c => c.Context.SetCryptographyProvider(null));
            });
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
            Utility.TryAndIgnore(() => _listener?.Stop());
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
                            var activeConnection = new PeerConnection(this, tcpClient, _configuration, _cryptographyProvider);
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
        /// Sends a query to the specified client and expects a reply.
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

            return await connection.Context.Query(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
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

            return await connection.Context.QueryAsync(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
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
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
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

        void IRmEndpoint.InvokeOnException(RmContext context, Exception ex, IRmPayload? payload)
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