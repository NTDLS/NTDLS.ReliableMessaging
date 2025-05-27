namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Configuration for server/or client.
    /// </summary>
    public class RmConfiguration
    {
        /// <summary>
        /// Whether to use a multiple thread for processing frames (serialization, compression, cryptography, and query/notification handlers).
        /// </summary>
        public bool AsynchronousFrameProcessing { get; set; } = true;

        /// <summary>
        /// When true, notification handlers are executed on a separate thread.
        /// This setting is ignored when MultiThreadedFrameProcessing is true because the entire frame (serialization, compression, cryptography, and the handler) is executed in a separate thread.
        /// </summary>
        public bool AsynchronousNotifications { get; set; } = true;

        /// <summary>
        /// When true, query handlers are executed on a separate thread.
        /// This setting is ignored when MultiThreadedFrameProcessing is true because the entire frame (serialization, compression, cryptography, and the handler) is executed in a separate thread.
        /// </summary>
        public bool AsynchronousQueryWaiting { get; set; } = true;

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
        /// Custom serialization provider. Otherwise the default will be used.
        /// </summary>
        public IRmSerializationProvider? SerializationProvider = null;

        /// <summary>
        /// Custom compression provider. Otherwise the default will be used.
        /// </summary>
        public IRmCompressionProvider? CompressionProvider = null;

        /// <summary>
        /// Custom encryption provider. Otherwise none will be used.
        /// </summary>
        public IRmCryptographyProvider? CryptographyProvider = null;

        /// <summary>
        /// A user settable object that can be accessed via the Context.Endpoint.Parameter Especially useful for convention based calls.
        /// </summary>
        public object? Parameter { get; set; } = null;
    }
}
