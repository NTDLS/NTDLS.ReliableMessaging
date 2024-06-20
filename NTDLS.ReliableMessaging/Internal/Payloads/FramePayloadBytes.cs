namespace NTDLS.ReliableMessaging.Internal.Payloads
{
    /// <summary>
    /// Used to send a payload of a raw byte array. Used by WriteBytesFrame() and handled in processNotificationCallback().
    /// When a raw byte array is use, all json serilization is skipped and checks for this payload type are prioritized for performance.
    /// </summary>
    internal class FramePayloadBytes : IRmNotification
    {
        /// <summary>
        /// The payload bytes of the frame.
        /// </summary>
        public byte[] Bytes { get; set; }

        /// <summary>
        /// Instantiates a new frame payload from a byte array.
        /// </summary>
        /// <param name="bytes"></param>
        public FramePayloadBytes(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}
