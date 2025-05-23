using NTDLS.ReliableMessaging;

namespace ExceptionPropagation
{
    internal class Program
    {
        const int _port = 4568;

        static void Main(string[] args)
        {
            var server = new RmServer();
            server.Start(_port);
            server.SetCompressionProvider(new RmDeflateCompressionProvider());
            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            server.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Server exception: {ex.Message}");
            };


            var client = new RmClient();
            client.Connect("localhost", _port);
            client.SetCompressionProvider(new RmDeflateCompressionProvider());

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            //client.Notify(new TestNotification());

            client.Query(new TestQuery()).ContinueWith((o) =>
            {
                if (o.IsFaulted)
                {
                    Console.WriteLine($"Client received exception: {o.Exception?.InnerException?.Message}");
                }
                else
                {
                    Console.WriteLine($"Client received reply: {o.Result.GetType().Name}");
                }
            });

            Console.WriteLine("Press [enter] to exit...");
            Console.ReadLine();

            client.Disconnect();
            server.Stop();
        }

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
        {
            if (query is TestQuery)
            {
                Console.WriteLine($"Server received query: {query.GetType().Name}");
                return new TestQueryReply();
            }
            throw new NotImplementedException();
        }
        private static void Server_OnNotificationReceived(RmContext context, IRmNotification notification)
        {
            Console.WriteLine($"Server received notification: {notification.GetType().Name}");
        }
    }
}
