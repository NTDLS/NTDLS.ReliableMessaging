using NTDLS.ReliableMessaging;

namespace Tests
{
    public class ClientFactory
    {
        public static RmClient CreateAndConnect()
        {
            var client = new RmClient();

            client.OnConnected += Client_OnConnected;
            client.OnDisconnected += Client_OnDisconnected;

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            client.Connect(Constants.HOST_NAME, Constants.LISTEN_PORT);

            return client;
        }

        private static void Client_OnDisconnected(RmContext context)
        {
            Console.WriteLine("Client disconnected.");
        }

        private static void Client_OnConnected(RmContext context)
        {
            Console.WriteLine("Client connected.");
        }
    }
}
