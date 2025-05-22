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
            server.OnException += Server_OnException;
            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;

            var client = new RmClient();
            client.Connect("localhost", _port);
            client.SetCompressionProvider(new RmDeflateCompressionProvider());
            client.OnException += Client_OnException;

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

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload payload)
        {
            if(payload is TestQuery)
            {
                Console.WriteLine($"Server received query: {payload.GetType().Name}");
                return new TestQueryReply();
            }
            throw new NotImplementedException();
        }

        private static void Server_OnException(RmContext? context, Exception ex, IRmPayload? payload)
        {
            Console.WriteLine(ex.Message);
        }

        private static void Client_OnException(RmContext? context, Exception ex, IRmPayload? payload)
        {
            Console.WriteLine(ex.Message);
        }

        private static void Server_OnNotificationReceived(RmContext context, IRmNotification payload)
        {
            Console.WriteLine($"Server received notification: {payload.GetType().Name}");
        }
    }
}
