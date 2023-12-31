﻿using NTDLS.Semaphore;
using NTDLS.StreamFraming.Payloads;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Listens for connections from MessageClients and processes the incoming notifications/queries.
    /// </summary>
    public class MessageServer : IMessageHub
    {
        private TcpListener? _listener;
        private readonly PessimisticSemaphore<List<PeerConnection>> _activeConnections = new();
        private Thread? _listenerThreadProc;
        private bool _keepRunning;

        #region Events.

        /// <summary>
        /// Event fired when an excaption occurs.
        /// </summary>
        public event ExceptionEvent? OnException;
        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        /// <param name="client">The instance of the client that is calling the event.</param>
        /// <param name="connectionId">The id of the client which was connected.</param>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="payload">The payload which was involved in the exception.</param>
        public delegate void ExceptionEvent(MessageServer client, Guid connectionId, Exception ex, IFramePayload? payload);

        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        public event ConnectedEvent? OnConnected;
        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        /// <param name="server">The instance of the server that is calling the event.</param>
        /// <param name="connectionId">The id of the client which was connected.</param>
        /// <param name="tcpClient">The underlying TCP client for the connection.</param>
        public delegate void ConnectedEvent(MessageServer server, Guid connectionId, TcpClient tcpClient);

        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        public event DisconnectedEvent? OnDisconnected;
        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        /// <param name="server">The instance of the server that is calling the event.</param>
        /// <param name="connectionId">The id of the client which was disconnected.</param>
        public delegate void DisconnectedEvent(MessageServer server, Guid connectionId);

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedEvent? OnNotificationReceived;
        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="server">The instance of the server that is calling the event.</param>
        /// <param name="connectionId">The id of the client which send the notification.</param>
        /// <param name="payload"></param>
        public delegate void NotificationReceivedEvent(MessageServer server, Guid connectionId, IFramePayloadNotification payload);

        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        public event QueryReceivedEvent? OnQueryReceived;
        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        /// <param name="server">The instance of the server that is calling the event.</param>
        /// <param name="connectionId">The id of the client which send the query.</param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public delegate IFramePayloadQueryReply QueryReceivedEvent(MessageServer server, Guid connectionId, IFramePayloadQuery payload);

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
        /// Gets the underlying TcpClient for the given connection id.
        /// </summary>
        /// <param name="connectionId">The connection to get the TcpClient for.</param>
        public TcpClient? GetClient(Guid connectionId)
        {
            return _activeConnections.Use((o) =>
            {
                return o.Where(c => c.Id == connectionId).FirstOrDefault()?.GetClient();
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
                o.Where(c => c.Id == connectionId).FirstOrDefault()?.Disconnect(false);
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
                o.Where(c => c.Id == connectionId).FirstOrDefault()?.Disconnect(waitOnThreadToExit);
            });
        }

        void ListenerThreadProc()
        {
            try
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
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.Interrupted
                    && ex.SocketErrorCode != SocketError.Shutdown)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Dispatches a one way notification to the specified connection.
        /// </summary>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="notification">The notification message to send.</param>
        /// <exception cref="Exception"></exception>
        public void Notify(Guid connectionId, IFramePayloadNotification notification)
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            connection.SendNotification(notification);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T?> Query<T>(Guid connectionId, IFramePayloadQuery query) where T : IFramePayloadQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            return await connection.SendQuery<T>(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="connectionId">The connection id of the client</param>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T?> QueryAsync<T>(Guid connectionId, IFramePayloadQuery query) where T : IFramePayloadQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            return await connection.SendQueryAsync<T>(query);
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
        public async Task<T?> Query<T>(Guid connectionId, IFramePayloadQuery query, int queryTimeout) where T : IFramePayloadQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            return await connection.SendQuery<T>(query, queryTimeout);
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
        public async Task<T?> QueryAsync<T>(Guid connectionId, IFramePayloadQuery query, int queryTimeout) where T : IFramePayloadQueryReply
        {
            var connection = _activeConnections.Use((o) => o.Where(c => c.Id == connectionId).FirstOrDefault());
            if (connection == null)
            {
                throw new Exception($"The connection with id {connectionId} was not found.");
            }

            return await connection.SendQueryAsync<T>(query, queryTimeout);
        }

        void IMessageHub.InvokeOnConnected(Guid connectionId, TcpClient tcpClient)
        {
            OnConnected?.Invoke(this, connectionId, tcpClient);
        }

        void IMessageHub.InvokeOnException(Guid connectionId, Exception ex, IFramePayload? payload)
        {
            OnException?.Invoke(this, connectionId, ex, payload);
        }

        void IMessageHub.InvokeOnDisconnected(Guid connectionId)
        {
            if (_keepRunning) //Avoid a race condition with the client thread waiting on a lock on _activeConnections that is held by Server.Stop().
            {
                _activeConnections.Use((o) => o.RemoveAll(o => o.Id == connectionId));
            }
            OnDisconnected?.Invoke(this, connectionId);
        }

        void IMessageHub.InvokeOnNotificationReceived(Guid connectionId, IFramePayloadNotification payload)
        {
            if (OnNotificationReceived == null)
            {
                throw new Exception("The notification hander event was not handled.");
            }
            OnNotificationReceived.Invoke(this, connectionId, payload);
        }

        IFramePayloadQueryReply IMessageHub.InvokeOnQueryReceived(Guid connectionId, IFramePayloadQuery payload)
        {
            if (OnQueryReceived == null)
            {
                throw new Exception("The query hander event was not handled.");
            }
            return OnQueryReceived.Invoke(this, connectionId, payload);
        }
    }
}
