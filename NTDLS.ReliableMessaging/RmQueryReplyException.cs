namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Used when an exception occurs during a query.
    /// </summary>
    public class RmQueryReplyException : IRmQueryReply
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
        public RmQueryReplyException()
        {
        }

        /// <summary>
        /// Instantiates an instance of the QueryException.
        /// </summary>
        /// <param name="ex"></param>
        public RmQueryReplyException(Exception ex)
        {
            Message = ex.Message;
            Source = ex.Source;
        }

        /// <summary>
        /// Returns an exception with the original exception message.
        /// </summary>
        public Exception GetException()
        {
            return new Exception(Message)
            {
                Source = Source,
            };
        }
    }
}
