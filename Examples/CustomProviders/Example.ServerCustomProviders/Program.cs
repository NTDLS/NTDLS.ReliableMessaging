using Example.Shared;
using Newtonsoft.Json;
using NTDLS.ReliableMessaging;

namespace Example.ServerCustomProviders
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
            var server = new RmServer(new RmConfiguration()
            {
                //Set the custom serialization provider at server creation time.
                SerializationProvider = new CustomSerializationProvider()
            });

            //The serialization provider can also be set after the server is created.
            //server.SetSerializationProvider(new CustomSerializationProvider());

            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;

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

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
        {
            if (query is MyQuery myQuery)
            {
                Console.WriteLine($"Server received query: '{myQuery.Message}'");
                return new MyQueryReply("This is the query reply from the server.");
            }
            else
            {
                throw new Exception("Payload type was not handled.");
            }
        }

        private static void Server_OnNotificationReceived(RmContext? context, IRmNotification notification)
        {
            if (notification is MyNotification myNotification)
            {
                Console.WriteLine($"Server received notification: {myNotification.Message}");
            }
            else
            {
                throw new Exception("Payload type was not handled.");
            }
        }
    }
}
