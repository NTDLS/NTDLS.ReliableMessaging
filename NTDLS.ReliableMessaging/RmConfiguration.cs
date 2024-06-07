using static NTDLS.ReliableMessaging.Internal.StreamFraming.Defaults;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Configuration for server/or client.
    /// </summary>
    public class RmConfiguration
    {
        /// <summary>
        /// The initial size in bytes of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int InitialReceiveBufferSize { get; set; } = NtFrameDefaults.INITIAL_BUFFER_SIZE;

        /// <summary>
        ///The maximum size in bytes of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int MaxReceiveBufferSize { get; set; } = NtFrameDefaults.MAX_BUFFER_SIZE;

        /// <summary>
        ///The growth rate of the auto-resizing for the receive buffer.
        /// </summary>
        public double ReceiveBufferGrowthRate { get; set; } = NtFrameDefaults.BUFFER_GROWTH_RATE;
    }
}
