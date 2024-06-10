﻿using NTDLS.ReliableMessaging.Internal.StreamFraming;
using System.Net.Sockets;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.Framing;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Contains information about the endpoint, the connection and various interaction functions.
    /// </summary>
    public class RmContext
    {
        private IRmCryptographyProvider? _cryptographyProvider = null;

        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public IRmEndpoint Endpoint { get; private set; }

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

        /// <summary>
        /// Creates a new ReliableMessagingContext instance.
        /// </summary>
        public RmContext(IRmEndpoint endpoint, TcpClient tcpClient, IRmCryptographyProvider? cryptographyProvider, Thread thread, NetworkStream stream)
        {
            _cryptographyProvider = cryptographyProvider;
            Endpoint = endpoint;
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
            if (Endpoint is RmClient client)
            {
                client.Disconnect();
            }
            else if (Endpoint is RmServer server)
            {
                server.Disconnect(ConnectionId);
            }
        }

        /// <summary>
        /// Sets the encryption provider that this client should use when sending/receiving data. Can be cleared by passing null or calling ClearCryptographyProvider().
        /// </summary>
        public void SetCryptographyProvider(IRmCryptographyProvider? provider)
            => _cryptographyProvider = provider;

        /// Removes the encryption provider set by a previous call to SetCryptographyProvider().
        public void ClearCryptographyProvider()
            => _cryptographyProvider = null;

        /// <summary>
        /// Gets the current Cryptography Provider, if any.
        /// </summary>
        public IRmCryptographyProvider? GetCryptographyProvider()
            => _cryptographyProvider;

        /// <summary>
        /// Dispatches a one way notification to the connected server.
        /// </summary>
        /// <param name="notification">The notification message to send.</param>
        public void Notify(IRmNotification notification)
            => Stream.WriteNotificationFrame(this, notification, _cryptographyProvider);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> QueryAsync<T>(IRmQuery<T> query) where T : IRmQueryReply
            => Stream.WriteQueryFrameAsync(this, query, -1, _cryptographyProvider);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query) where T : IRmQueryReply
            => Stream.WriteQueryFrame(this, query, -1, _cryptographyProvider);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> QueryAsync<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => Stream.WriteQueryFrameAsync(this, query, queryTimeout, _cryptographyProvider);

        /// <summary>
        /// Sends a query to the specified client and expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of reply that is expected.</typeparam>
        /// <param name="query">The query message to send.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <returns>Returns the result of the query.</returns>
        public Task<T> Query<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => Stream.WriteQueryFrame(this, query, queryTimeout, _cryptographyProvider);
    }
}
