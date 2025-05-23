using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.ClientByConvention
{
    /// <summary>
    /// In this example, we demonstrate how to use convention for receiving notifications and queries.
    /// 
    /// Note that MyNotification class that implements IRmNotification.
    /// Note that MyQuery is class that implements IRmQuery<MyQueryReply>.
    /// 
    /// See: Example.ServerByConvention.MessageHandlers for the server-side implementation.
    /// </summary>
    internal class Program
    {
        static int messageNumber = 0;

        static void Main()
        {
            //Start a client and connect to the server.
            var client = new RmClient();

            client.Connect("localhost", ExampleConstants.PortNumber);

            Console.WriteLine("Press [enter] to shutdown.");

            while (true)
            {
                client.Notify(new MyNotification($"This is message {messageNumber++} from the client."));
                client.Notify(new MyNotification($"This is message {messageNumber++} from the client."));
                client.Notify(new MyNotification($"This is message {messageNumber++} from the client."));

                // Handle the OnException event, otherwise the server will ignore any exceptions.
                client.OnException += (context, ex, payload) =>
                {
                    Console.WriteLine($"Client exception: {ex.Message}");
                };

                //Send a query to the server, specify which type of reply we expect.
                client.Query(new MyQuery($"This is query {messageNumber++} from the client.")).ContinueWith(x =>
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

                if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                {
                    break;
                }
            }

            //Cleanup.
            client.Disconnect();
        }
    }
}
