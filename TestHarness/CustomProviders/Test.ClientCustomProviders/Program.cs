using Newtonsoft.Json;
using NTDLS.ReliableMessaging;
using TestHarness.Payloads;
using ZstdNet;

namespace Test.Client
{
    internal class Program
    {
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

        class CustomCompressionProvider : IRmCompressionProvider
        {
            public byte[] Compress(RmContext context, byte[] payload)
            {
                if (payload == null) return Array.Empty<byte>();
                using var compressor = new Compressor(new CompressionOptions(CompressionOptions.MaxCompressionLevel));
                return compressor.Wrap(payload);
            }

            public byte[] DeCompress(RmContext context, byte[] compressedPayload)
            {
                if (compressedPayload == null) return Array.Empty<byte>();
                using var decompressor = new Decompressor();
                return decompressor.Unwrap(compressedPayload);
            }
        }

        static void Main()
        {
            //Start a client and connect to the server.
            var client = new RmClient(new RmConfiguration()
            {
                SerializationProvider = new CustomSerializationProvider(),
                CompressionProvider = new CustomCompressionProvider(),
            });

            client.Connect("localhost", 45784);

            client.Notify(new MyNotification("This is message 001 from the client."));
            client.Notify(new MyNotification("This is message 002 from the client."));
            client.Notify(new MyNotification("This is message 003 from the client."));

            client.OnException += (RmContext? context, Exception ex, IRmPayload? payload) =>
            {
                // Handle the OnException event, otherwise the client will ignore any exceptions.
                Console.WriteLine($"RPC Client exception: {ex.Message}");
            };

            //Send a query to the server, specify which type of reply we expect.
            client.Query(new MyQuery("This is the query from the client.")).ContinueWith(x =>
            {
                //If we received a reply, print it to the console.
                if (x.IsCompletedSuccessfully && x.Result != null)
                {
                    Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
                }
            });

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            //Cleanup.
            client.Disconnect();
        }
    }
}
