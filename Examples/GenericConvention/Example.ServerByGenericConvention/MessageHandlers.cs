using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.ServerByGenericConvention
{
    /// <summary>
    /// Provides methods for handling generic notifications and queries received by the server.
    /// 
    /// Note that these functions contain the parameter "RmContext context", which is the context of the current request
    ///     but this parameter is not required as the convention based lookup handles functions with and without this parameter.
    /// </summary>
    internal class MessageHandlers : IRmMessageHandler
    {
        /// <summary>
        /// Processes a generic notification received by the server.
        /// </summary>
        /// <param name="context">The context of the current request, this can be used (in special cases) to communicate with the client directly.</param>
        /// <param name="notification">The notification object containing the message from the client.</param>
        public void MyGenericNotification(RmContext context, MyGenericNotification<string> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
        }

        /// <summary>
        /// Processes a generic notification received by the server.
        /// </summary>
        /// <param name="context">The context of the current request, this can be used (in special cases) to communicate with the client directly.</param>
        /// <param name="notification">The notification object containing the message from the client.</param>
        public void MyGenericNotification(RmContext context, MyGenericNotification<int> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
        }

        /// <summary>
        /// Processes a generic query and returns a reply containing a response message.
        /// </summary>
        /// <param name="context">The context of the current request, this can be used (in special cases) to communicate with the client directly.</param>
        /// <param name="query">The query to process, containing the message from the client.</param>
        public MyGenericQueryReply<string> MyGenericQuery(RmContext context, MyGenericQuery<string> query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");

            return new MyGenericQueryReply<string>("This is query reply from the server.");
        }
    }
}
