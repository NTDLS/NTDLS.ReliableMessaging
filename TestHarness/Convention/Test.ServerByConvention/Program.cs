using NTDLS.ReliableMessaging;
using TestHarness;

namespace Test.Server
{
    internal class Program
    {
        static void Main()
        {
            var server = new RmServer();

            // The class HandlerMethods contains the functions that handle incomming queries and notifications.
            server.AddHandler(new HandlerMethods());

            server.OnException += (RmContext context, Exception ex, IRmPayload? payload) =>
            {
                // Handle the OnException event, otherwise the server will ignore any exceptions.
                Console.WriteLine($"RPC Client exception: {ex.Message}");
            };

            server.Start(45784);

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            server.Stop();
        }

    }
}
