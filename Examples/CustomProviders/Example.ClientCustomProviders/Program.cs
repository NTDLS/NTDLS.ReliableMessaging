using Example.Shared;
using Newtonsoft.Json;
using NTDLS.ReliableMessaging;

namespace Example.ClientCustomProviders
{
    /// <summary>
    /// This example shows how to use a custom serialization provider.
    /// The approach is the same for compression and cryptography providers.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// This is the custom serialization provider, using Newtonsoft.Json.
        /// Typically, this would be in a separate library, but for this example it is included in both the client and server.
        /// </summary>
        class CustomSerializationProvider : IRmSerializationProvider
        {
            private static readonly JsonSerializerSettings _jsonSettings = new()
            {
                TypeNameHandling = TypeNameHandling.All
            };

            public T? DeserializeToObject<T>(string json)
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }

            public string SerializeToText<T>(T obj)
            {
                return JsonConvert.SerializeObject(obj, _jsonSettings);
            }
        }

        static void Main()
        {
            //Start a client and connect to the server.
            var client = new RmClient(new RmConfiguration()
            {
                //Set the custom serialization provider at client creation time.
                SerializationProvider = new CustomSerializationProvider()
            });

            //The serialization provider can also be set after the client is created.
            //client.SetSerializationProvider(new CustomSerializationProvider());

            client.Connect("localhost", ExampleConstants.PortNumber);

            client.Notify(new MyNotification("This is message 001 from the client."));
            client.Notify(new MyNotification("This is message 002 from the client."));
            client.Notify(new MyNotification("This is message 003 from the client."));

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            //Send a query to the server, specify which type of reply we expect.
            client.Query(new MyQuery("This is the query from the client.")).ContinueWith(x =>
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
