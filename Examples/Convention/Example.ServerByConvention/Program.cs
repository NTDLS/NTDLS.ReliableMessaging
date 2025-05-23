using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.ServerByConvention
{
    internal class Program
    {
        static void Main()
        {
            var server = new RmServer();

            // The class HandlerMethods contains the functions that handle incoming queries and notifications.
            server.AddHandler(new MessageHandlers());

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

    }
}
