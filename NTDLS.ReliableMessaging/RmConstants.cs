namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Constants used in the Reliable Messaging framework.
    /// </summary>
    public static class RmConstants
    {
        /// <summary>
        /// The frame header delimiter. Used to literally separate and detect the beginning of each packet.
        /// </summary>
        public const int FrameDelimiter = 948724593;

        /// <summary>
        /// The size of the total frame header.
        /// </summary>
        public const int GrossFrameHeaderSize = 10;

        /// <summary>
        /// The initial size in bytes of the buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxBufferSize.
        /// </summary>
        public const int InitialBufferSize = 16 * 1024;

        /// <summary>
        ///The maximum size in bytes of the buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxBufferSize.
        /// </summary>
        public const int MaxBufferSize = 1024 * 1024;

        /// <summary>
        ///The growth rate of the auto-resizing for the buffer in decimal percentages.
        /// </summary>
        public const double BufferGrowthRate = 0.2;
    }
}
