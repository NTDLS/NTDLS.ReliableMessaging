using NTDLS.ReliableMessaging;

namespace Test.ServerByConvention
{
    internal class Program
    {
        static void Main()
        {
            var server = new RmServer();

            // The class HandlerMethods contains the functions that handle incoming queries and notifications.
            server.AddHandler(new HandlerMethods());

            server.OnException += (context, ex, payload) =>
            {
                // Handle the OnException event, otherwise the server will ignore any exceptions.
                Console.WriteLine($"RPC client exception: {ex.Message}");
            };

            server.Start(31254);

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            server.Stop();
        }

    }
}
