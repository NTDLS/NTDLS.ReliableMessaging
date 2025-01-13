using NTDLS.ReliableMessaging;

namespace Tests
{
    public class ClientFactory
    {
        public static RmClient CreateAndConnect()
        {
            var client = new RmClient();

            client.OnException += Client_OnException;
            client.OnConnected += Client_OnConnected;
            client.OnDisconnected += Client_OnDisconnected;

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

        private static void Client_OnException(RmContext? context, Exception ex, IRmPayload? payload)
        {
            Console.WriteLine(ex.Message);
        }
    }
}
