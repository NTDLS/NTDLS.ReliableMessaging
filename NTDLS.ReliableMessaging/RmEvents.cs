namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Public events
    /// </summary>
    public class RmEvents
    {
        /// <summary>
        /// Event fired when an exception occurs.
        /// </summary>
        /// <param name="context">Information about the connection, if any.</param>
        /// <param name="ex">The exception that was thrown.</param>
        /// <param name="payload">The payload which was involved in the exception, if any.</param>
        public delegate void ExceptionEvent(RmContext? context, Exception ex, IRmPayload? payload);
    }
}
