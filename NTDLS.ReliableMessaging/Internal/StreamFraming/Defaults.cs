namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    internal static class Defaults
    {
        internal static class NtFrameDefaults
        {
            /// <summary>
            /// The frame header delimiter. Used to literally separate and detect the beginning of each packet.
            /// </summary>
            public const int FRAME_DELIMITER = 948724593;

            /// <summary>
            /// The size of the total frame header.
            /// </summary>
            public const int FRAME_HEADER_SIZE = 10;

            /// <summary>
            /// The initial size in bytes of the buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxBufferSize.
            /// </summary>
            public const int INITIAL_BUFFER_SIZE = 16 * 1024;

            /// <summary>
            ///The maximum size in bytes of the buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxBufferSize.
            /// </summary>
            public const int MAX_BUFFER_SIZE = 1024 * 1024;

            /// <summary>
            ///The growth rate of the auto-resizing for the buffer in decimal percentages.
            /// </summary>
            public const double BUFFER_GROWTH_RATE = 0.2;
        }
    }
}
