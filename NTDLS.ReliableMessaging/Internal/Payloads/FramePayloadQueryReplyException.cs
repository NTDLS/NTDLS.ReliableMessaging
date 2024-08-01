namespace NTDLS.ReliableMessaging.Internal.Payloads
{
    /// <summary>
    /// Used when an exception occurs during a query.
    /// </summary>
    public class FramePayloadQueryReplyException : IRmQueryReply
    {
        /// <summary>
        /// The exception that occurred while executing the query.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Instantiates an empty instance of the QueryException.
        /// </summary>
        public FramePayloadQueryReplyException()
        {
            Exception = new Exception("Unhandled exception");
        }

        /// <summary>
        /// Instantiates an instance of the QueryException.
        /// </summary>
        /// <param name="ex"></param>
        public FramePayloadQueryReplyException(Exception ex)
        {
            Exception = ex;
        }
    }
}
