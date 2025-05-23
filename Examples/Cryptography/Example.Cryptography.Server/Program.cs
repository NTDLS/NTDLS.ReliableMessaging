using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.Cryptography.Server
{
    internal class Program
    {
        static void Main()
        {
            var server = new RmServer();
            server.Start(ExampleConstants.PortNumber);

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            server.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Server exception: {ex.Message}");
            };

            server.AddHandler(new MessageHandlers());

            Console.WriteLine($"File transfer SERVER is now running on port {ExampleConstants.PortNumber}");

            Console.WriteLine("Press [enter] to shutdown...");
            Console.ReadLine();

            server.Stop();
        }
    }
}
