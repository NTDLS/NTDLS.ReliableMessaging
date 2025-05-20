namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Used to send a payload of a raw byte array. Used by Notify() and handled in processNotificationCallback() or convention-based hander.
    /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
    /// </summary>
    public class RmBytesNotification : IRmNotification
    {
        /// <summary>
        /// The payload bytes of the frame.
        /// </summary>
        public byte[] Bytes { get; set; }

        /// <summary>
        /// Instantiates a new frame payload from a byte array.
        /// </summary>
        public RmBytesNotification(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}
