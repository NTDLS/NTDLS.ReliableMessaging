﻿using NTDLS.StreamFraming.Payloads;
using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Connects to a MessageServer then sends/received and processes notifications/queries.
    /// </summary>
    public class MessageClient : IMessageHub
    {
        private readonly TcpClient _tcpClient = new();
        private PeerConnection? _activeConnection;
        private bool _keepRunning;

        /// <summary>
        /// Returns true if the client is connected.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected => _tcpClient?.Connected ?? false;

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
        public delegate bool ExceptionEvent(MessageClient client, Guid connectionId, Exception ex, IFramePayload? payload);

        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        public event ConnectedEvent? OnConnected;
        /// <summary>
        /// Event fired when a client connects to the server.
        /// </summary>
        /// <param name="client">The instance of the client that is calling the event.</param>
        /// <param name="connectionId">The id of the client which was connected.</param>
        /// <param name="tcpClient">The underlying TCP client for the connection.</param>
        public delegate void ConnectedEvent(MessageClient client, Guid connectionId, TcpClient tcpClient);

        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        public event DisconnectedEvent? OnDisconnected;
        /// <summary>
        /// Event fired when a client is disconnected from the server.
        /// </summary>
        /// <param name="client">The instance of the client that is calling the event.</param>
        /// <param name="connectionId">The id of the client which was disconnected.</param>
        public delegate void DisconnectedEvent(MessageClient client, Guid connectionId);

        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        public event NotificationReceivedEvent? OnNotificationReceived;
        /// <summary>
        /// Event fired when a notification is received from a client.
        /// </summary>
        /// <param name="client">The instance of the client that is calling the event.</param>
        /// <param name="connectionId">The id of the client which send the notification.</param>
        /// <param name="payload"></param>
        public delegate void NotificationReceivedEvent(MessageClient client, Guid connectionId, IFramePayloadNotification payload);

        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        public event QueryReceivedEvent? OnQueryReceived;
        /// <summary>
        /// Event fired when a query is received from a client.
        /// </summary>
        /// <param name="client">The instance of the client that is calling the event.</param>
        /// <param name="connectionId">The id of the client which send the query.</param>
        /// <param name="payload"></param>
        /// <returns></returns>
        public delegate IFramePayloadQueryReply QueryReceivedEvent(MessageClient client, Guid connectionId, IFramePayloadQuery payload);

        #endregion

        /// <summary>
        /// Connects to a specified message server by its host name.
        /// </summary>
        /// <param name="hostName">The hostname of the message server.</param>
        /// <param name="port">The listenr port of the message server.</param>
        public void Connect(string hostName, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _tcpClient.Connect(hostName, port);
            _activeConnection = new PeerConnection(this, _tcpClient);
            _activeConnection.RunAsync();
        }

        /// <summary>
        /// Connects to a specified message server by its IP Address.
        /// </summary>
        /// <param name="ipAddress">The IP address of the message server.</param>
        /// <param name="port">The listenr port of the message server.</param>
        public void Connect(IPAddress ipAddress, int port)
        {
            if (_keepRunning)
            {
                return;
            }
            _keepRunning = true;

            _tcpClient.Connect(ipAddress, port);
            _activeConnection = new PeerConnection(this, _tcpClient);
            _activeConnection.RunAsync();
        }

        /// <summary>
        /// Disconnects the client from the server.
        /// </summary>
        public void Disconnect()
        {
            _keepRunning = false;
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
        public void Notify(IFramePayloadNotification notification)
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
        public async Task<T?> Query<T>(IFramePayloadQuery query) where T : IFramePayloadQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQuery<T>(query);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T?> QueryAsync<T>(IFramePayloadQuery query) where T : IFramePayloadQueryReply
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
        public async Task<T?> Query<T>(IFramePayloadQuery query, int queryTimeout) where T : IFramePayloadQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQuery<T>(query, queryTimeout);
        }

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<T?> QueryAsync<T>(IFramePayloadQuery query, int queryTimeout) where T : IFramePayloadQueryReply
        {
            Utility.EnsureNotNull(_activeConnection);
            return await _activeConnection.SendQueryAsync<T>(query, queryTimeout);
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
            _activeConnection = null;
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
