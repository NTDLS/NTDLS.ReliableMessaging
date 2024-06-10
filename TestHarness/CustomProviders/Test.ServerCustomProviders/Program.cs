﻿using Newtonsoft.Json;
using NTDLS.ReliableMessaging;
using TestHarness.Payloads;
using ZstdNet;

namespace Test.Server
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
            var server = new RmServer();

            server.SetSerializationProvider(new CustomSerializationProvider());
            server.SetCompressionProvider(new CustomCompressionProvider());

            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;
            server.OnException += Server_OnException; // Handle the OnException event, otherwise the server will ignore any exceptions.

            server.Start(45784);

            Console.WriteLine("Press [enter] to shutdown.");
            Console.ReadLine();

            server.Stop();
        }

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload payload)
        {
            if (payload is MyQuery myQuery)
            {
                Console.WriteLine($"Server received query: '{myQuery.Message}'");
                return new MyQueryReply("This is the query reply from the server.");
            }
            else
            {
                throw new Exception("The payload type was not handled.");
            }
        }

        private static void Server_OnNotificationReceived(RmContext? context, IRmNotification payload)
        {
            if (payload is MyNotification myNotification)
            {
                Console.WriteLine($"Server received notification: {myNotification.Message}");
            }
            else
            {
                throw new Exception("The payload type was not handled.");
            }
        }

        private static void Server_OnException(RmContext? context, Exception ex, IRmPayload? payload)
        {
            Console.WriteLine($"RPC Client exception: {ex.Message}");
        }
    }
}
