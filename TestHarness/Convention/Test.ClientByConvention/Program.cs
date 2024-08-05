using NTDLS.ReliableMessaging;
using TestHarness.Payloads;

namespace Test.Client
{
    internal class Program
    {
        static void Main()
        {
            //Start a client and connect to the server.
            var client = new RmClient();

            client.Connect("localhost", 45784);

            client.Notify(new MyNotification("This is message 001 from the client."));
            client.Notify(new MyNotification("This is message 002 from the client."));
            client.Notify(new MyNotification("This is message 003 from the client."));

            client.OnException += (RmContext? context, Exception ex, IRmPayload? payload) =>
            {
                Console.WriteLine($"RPC client exception: {ex.Message}");
            };

            //Send a query to the server, specify which type of reply we expect.
            client.Query(new MyQuery("This is the query from the client."), -1).ContinueWith(x =>
            {
                //If we received a reply, print it to the console.
                if (x.IsCompletedSuccessfully && x.Result != null)
                {
                    Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
                }
                else
                {
                    Console.WriteLine($"Exception: '{x.Exception?.GetBaseException()?.Message}'");
                }
            });

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            //Cleanup.
            client.Disconnect();
        }
    }
}
