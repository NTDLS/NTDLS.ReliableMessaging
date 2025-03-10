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
        public string Message { get; set; } = string.Empty;

        /// The exception source that occurred while executing the query.
        public string? Source { get; set; }


        /// <summary>
        /// Instantiates an empty instance of the QueryException.
        /// </summary>
        public FramePayloadQueryReplyException()
        {
        }

        /// <summary>
        /// Instantiates an instance of the QueryException.
        /// </summary>
        /// <param name="ex"></param>
        public FramePayloadQueryReplyException(Exception ex)
        {
            Message = ex.Message;
            Source = ex.Source;
        }

        /// <summary>
        /// Returns an exception with the original exception message.
        /// </summary>
        /// <returns></returns>
        public Exception GetException()
        {
            return new Exception(Message)
            {
                Source = Source,
            };
        }
    }
}
