namespace NTDLS.ReliableMessaging.Internal.Payloads
{
    /// <summary>
    /// Used when an exception occurs durring a query.
    /// </summary>
    internal class FramePayloadQueryReplyException : IRmQueryReply
    {
        /// <summary>
        /// The exception that occured while executing the query.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Instanciates an empty instance of the QueryException.
        /// </summary>
        public FramePayloadQueryReplyException()
        {
            Exception = new Exception("Unhandled exception");
        }

        /// <summary>
        /// Instanciates an instance of the QueryException.
        /// </summary>
        /// <param name="ex"></param>
        public FramePayloadQueryReplyException(Exception ex)
        {
            Exception = ex;
        }
    }
}
