﻿using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.ClientByGenericConvention
{
    /// <summary>
    /// In this example, we demonstrate how to use the generic convention for receiving notifications and queries.
    /// 
    /// Note that MyGenericNotification<> is a generic class that implements IRmNotification.
    /// Note that MyGenericQuery<> is a generic class that implements IRmQuery<MyGenericQueryReply<T>>.
    /// 
    /// See: Example.ServerByGenericConvention.MessageHandlers for the server-side implementation.
    /// </summary>
    internal class Program
    {
        static int messageNumber = 0;

        static void Main()
        {
            //Start a client and connect to the server.
            var client = new RmClient();

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            client.Connect("localhost", ExampleConstants.PortNumber);

            Console.WriteLine("Press [enter] to shutdown.");

            for (int i = 0; i < 10; i++)
            {
                client.Notify(new MyGenericNotification<int>(i));
                client.Notify(new MyGenericNotification<string>($"This is message {messageNumber++} from the client."));

                //Send a query to the server, specify which type of reply we expect.
                client.Query(new MyGenericQuery<string>($"This is query {messageNumber++} from the client.")).ContinueWith(x =>
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
            }

            //Cleanup.
            client.Disconnect();
        }
    }
}
