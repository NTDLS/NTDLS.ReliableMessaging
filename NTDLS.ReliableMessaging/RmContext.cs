﻿using NTDLS.ReliableMessaging.Internal.StreamFraming;
using System.Collections.Concurrent;
using System.Net.Sockets;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.RmFraming;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Contains information about the messenger endpoint, connection and various interaction functions.
    /// </summary>
    public class RmContext
    {
        /// <summary>
        /// callback that is called after the frame has been built but before the query is dispatched. This is useful when establishing encrypted connections, where we need to tell a peer that encryption is being initialized but we need to tell the peer before setting the provider.
        /// </summary>
        public delegate void OnQueryPrepared();

        private IRmSerializationProvider? _serializationProvider = null;
        private IRmCompressionProvider? _compressionProvider = null;
        private IRmCryptographyProvider? _cryptographyProvider = null;

        /// <summary>
        /// Gets or sets the collection of queries that are awaiting replies, identified by their FrameBody.Id.
        /// </summary>
        internal ConcurrentDictionary<Guid, RmQueryAwaitingReply> QueriesAwaitingReplies { get; set; } = new();

        #region Public Properties.

        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public IRmMessenger Messenger { get; private set; }

        /// <summary>
        /// The ID of the connection.
        /// </summary>
        public Guid ConnectionId { get; private set; }

        /// <summary>
        /// The TCP/IP connection associated with this connection.
        /// </summary>
        public TcpClient TcpClient { get; private set; }

        /// <summary>
        /// //The thread that receives data for this connection.
        /// </summary>
        public Thread Thread { get; private set; }

        /// <summary>
        /// //The stream for the TCP/IP connection (used for reading and writing).
        /// </summary>
        public NetworkStream Stream { get; private set; }

        #endregion

        /// <summary>
        /// Creates a new ReliableMessagingContext instance.
        /// </summary>
        public RmContext(IRmMessenger messenger, TcpClient tcpClient, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider, Thread thread, NetworkStream stream)
        {
            _serializationProvider = serializationProvider;
            _compressionProvider = compressionProvider;
            _cryptographyProvider = cryptographyProvider;
            Messenger = messenger;
            ConnectionId = Guid.NewGuid();
            TcpClient = tcpClient;
            Thread = thread;
            Stream = stream;
        }

        /// <summary>
        /// Disconnects the connection to this endpoint.
        /// </summary>
        public void Disconnect()
        {
            if (Messenger is RmClient client)
            {
                client.Disconnect();
            }
            else if (Messenger is RmServer server)
            {
                server.Disconnect(ConnectionId);
            }
        }

        #region IRmSerializationProvider.

        /// <summary>
        /// Sets the custom serialization provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearSerializationProvider().
        /// </summary>
        public void SetSerializationProvider(IRmSerializationProvider? provider)
            => _serializationProvider = provider;

        /// Removes the custom serialization provider set by a previous call to SetSerializationProvider().
        public void ClearSerializationProvider()
            => _serializationProvider = null;

        /// <summary>
        /// Gets the current custom serialization provider, if any.
        /// </summary>
        public IRmSerializationProvider? GetSerializationProvider()
            => _serializationProvider;

        #endregion

        #region IRmCompressionProvider.

        /// <summary>
        /// Sets the custom compression provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearCompressionProvider().
        /// </summary>
        public void SetCompressionProvider(IRmCompressionProvider? provider)
            => _compressionProvider = provider;

        /// Removes the custom compression provider set by a previous call to SetCompressionProvider().
        public void ClearCompressionProvider()
            => _compressionProvider = null;

        /// <summary>
        /// Gets the current custom compression provider, if any.
        /// </summary>
        public IRmCompressionProvider? GetCompressionProvider()
            => _compressionProvider;

        #endregion

        #region IRmCryptographyProvider.

        /// <summary>
        /// Sets the custom encryption provider that this client should use when sending/receiving data.
        /// Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        public void SetCryptographyProvider(IRmCryptographyProvider? provider)
            => _cryptographyProvider = provider;

        /// <summary>
        /// Removes the custom encryption provider set by a previous call to SetCryptographyProvider().
        /// </summary>
        public void ClearCryptographyProvider()
            => _cryptographyProvider = null;

        /// <summary>
        /// Gets the current custom cryptography provider, if any.
        /// </summary>
        public IRmCryptographyProvider? GetCryptographyProvider()
            => _cryptographyProvider;

        #endregion

        #region Stream Interactions.

        /// <summary>
        /// Dispatches a one way notification to the connected server.
        /// </summary>
        /// <param name="notification">The notification message to send.</param>
        public void Notify(IRmNotification notification)
            => Stream.WriteNotificationFrame(this, notification);

        /// <summary>
        /// Dispatches a one way notification to the connected server.
        /// </summary>
        /// <param name="notification">The notification message to send.</param>
        public async Task NotifyAsync(IRmNotification notification)
            => await Stream.WriteNotificationFrameAsync(this, notification);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query, TimeSpan queryTimeout) where T : IRmQueryReply
            => await Stream.WriteQueryFrameAsync(this, query, queryTimeout, null);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query, TimeSpan queryTimeout) where T : IRmQueryReply
            => Stream.WriteQueryFrame(this, query, queryTimeout, null);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="onQueryPrepared">Optional callback that is called after the frame has been built but before the query is dispatched. This is useful when establishing encrypted connections, where we need to tell a peer that encryption is being initialized but we need to tell the peer before setting the provider.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public async Task<T> QueryAsync<T>(IRmQuery<T> query, OnQueryPrepared onQueryPrepared, TimeSpan queryTimeout) where T : IRmQueryReply
            => await Stream.WriteQueryFrameAsync(this, query, queryTimeout, onQueryPrepared);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="onQueryPrepared">Optional callback that is called after the frame has been built but before the query is dispatched. This is useful when establishing encrypted connections, where we need to tell a peer that encryption is being initialized but we need to tell the peer before setting the provider.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query, OnQueryPrepared onQueryPrepared, TimeSpan queryTimeout) where T : IRmQueryReply
            => Stream.WriteQueryFrame(this, query, queryTimeout, onQueryPrepared);

        #endregion
    }
}
