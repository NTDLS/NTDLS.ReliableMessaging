namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Configuration for server/or client.
    /// </summary>
    public class RmConfiguration
    {
        /// <summary>
        /// No longer has any effect. Each connection's receive thread now decodes its own frames (cryptography, compression and
        /// serialization), completes replies directly, and hands notifications and queries on as AsynchronousNotifications and
        /// AsynchronousQueryWaiting describe. Handing every frame to the thread pool lost their order and had no limit.
        /// </summary>
        [Obsolete("Frames are now decoded on the connection's receive thread. Use AsynchronousNotifications and AsynchronousQueryWaiting.")]
        public bool AsynchronousFrameProcessing { get; set; } = true;

        /// <summary>
        /// Server only. When true, the server listens on IPv6 as well as IPv4 using a single dual-stack listener that accepts both.
        /// IPv4 clients then appear with IPv4-mapped addresses (::ffff:1.2.3.4). When false (the default) the server listens on
        /// IPv4 only. If the operating system has no IPv6, or it cannot be used, the server listens on IPv4 only regardless.
        /// The client is not affected: it can always connect to IPv6 addresses, and to host names that resolve to them.
        /// </summary>
        public bool ListenOnIPv6 { get; set; } = false;

        /// <summary>
        /// When true, notification handlers run off the receive thread, one at a time per connection and in the order that the
        /// notifications arrived, so that a slow handler does not hold up replies and queries. When false, they run on the
        /// receive thread, which reads nothing else from the connection until each handler returns.
        /// </summary>
        public bool AsynchronousNotifications { get; set; } = true;

        /// <summary>
        /// When true, query handlers run on the thread pool, so that several queries from one connection can be handled at once,
        /// up to MaxConcurrentQueries. When false, they run on the receive thread, which reads nothing else from the connection
        /// until each handler returns (an asynchronous handler that is still pending replies when it completes).
        /// </summary>
        public bool AsynchronousQueryWaiting { get; set; } = true;

        /// <summary>
        /// The most notifications from one connection that can be waiting for their handlers when AsynchronousNotifications is true.
        /// When this many are waiting, the connection stops reading until there is room, which makes TCP slow the sender down.
        /// A notification handler that sends a query on the same connection and waits for the reply can wait forever if this fills up.
        /// </summary>
        public int MaxPendingNotifications { get; set; } = 10000;

        /// <summary>
        /// The most query handlers that can run at once for one connection when AsynchronousQueryWaiting is true. When this
        /// many are running, the connection stops reading until one finishes, which makes TCP slow the sender down.
        /// A query handler that sends a query on the same connection and waits for the reply can wait forever if this fills up.
        /// </summary>
        public int MaxConcurrentQueries { get; set; } = 1000;

        /// <summary>
        /// When true (the default), each frame is sent as soon as it is written. When false, the operating system applies
        /// Nagle's algorithm: while earlier data is waiting to be acknowledged, it holds back small writes so it can combine them.
        /// With several queries, replies or notifications in flight on one connection, that holds a frame back until the peer
        /// acknowledges the one before it, which can wait on the peer's handler or its delayed acknowledgment timer.
        /// Each frame is written in a single call, so turning this on costs little more than some extra packet headers.
        /// </summary>
        public bool NoDelay { get; set; } = true;

        /// <summary>
        /// The default amount of time to wait for a query to reply before throwing a timeout exception.
        /// </summary>
        public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The initial size in bytes of the receive buffer.
        /// If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int InitialReceiveBufferSize { get; set; } = RmConstants.InitialBufferSize;

        /// <summary>
        ///The maximum size in bytes of the receive buffer.
        ///If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int MaxReceiveBufferSize { get; set; } = RmConstants.MaxBufferSize;

        /// <summary>
        ///The growth rate of the auto-resizing for the receive buffer.
        /// </summary>
        public double ReceiveBufferGrowthRate { get; set; } = RmConstants.BufferGrowthRate;

        /// <summary>
        /// Serialization provider, otherwise the default RmJsonSerializationProvider will be used.
        /// </summary>
        public IRmSerializationProvider? SerializationProvider = new RmJsonSerializationProvider();

        /// <summary>
        /// Compression provider, otherwise none will be used.
        /// </summary>
        public IRmCompressionProvider? CompressionProvider = null;

        /// <summary>
        /// Cryptography provider, otherwise none will be used.
        /// </summary>
        public IRmCryptographyProvider? CryptographyProvider = null;

        /// <summary>
        /// When true, the client will automatically attempt to reconnect after an unexpected disconnection.
        /// Has no effect on RmServer. Call Disconnect() to stop reconnection attempts.
        /// </summary>
        public bool AutoReconnect { get; set; } = false;

        /// <summary>
        /// The amount of time to wait between reconnection attempts when AutoReconnect is true.
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// A user settable object that can be accessed via the Context.Endpoint.Parameter Especially useful for convention based calls.
        /// </summary>
        public object? Parameter { get; set; } = null;
    }
}
