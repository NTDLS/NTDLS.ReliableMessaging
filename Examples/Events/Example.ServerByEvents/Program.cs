using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.ServerByEvents
{
    /// <summary>
    /// In this example, we demonstrate how to use events for receiving notifications and queries.
    /// 
    /// Note that MyNotification class that implements IRmNotification.
    /// Note that MyQuery is class that implements IRmQuery<MyQueryReply>.
    /// </summary>
    internal class Program
    {
        static void Main()
        {
            var server = new RmServer();

            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            server.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Server exception: {ex.Message}");
            };

            server.Start(ExampleConstants.PortNumber);

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            server.Stop();
        }

        /// <summary>
        /// Processes a notification received by the server.
        /// </summary>
        /// <param name="context">The context of the current request, this can be used (in special cases) to communicate with the client directly.</param>
        /// <param name="query">The notification object containing the message from the client.</param>
        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
        {
            //Use pattern matching to check the type of the payload and cast it to the appropriate type.
            if (query is MyQuery myQuery)
            {
                Console.WriteLine($"Server received query: '{myQuery.Message}'");
                return new MyQueryReply("This is the query reply from the server.");
            }
            else
            {
                throw new Exception("Payload type was not handled.");
            }
        }

        /// <summary>
        /// Processes a query and returns a reply containing a response message.
        /// </summary>
        /// <param name="context">The context of the current request, this can be used (in special cases) to communicate with the client directly.</param>
        /// <param name="notification">The query to process, containing the message from the client.</param>
        private static void Server_OnNotificationReceived(RmContext? context, IRmNotification notification)
        {
            if (notification is MyNotification myNotification)
            {
                Console.WriteLine($"Server received notification: {myNotification.Message}");
            }
            else
            {
                throw new Exception("Payload type was not handled.");
            }
        }
    }
}
