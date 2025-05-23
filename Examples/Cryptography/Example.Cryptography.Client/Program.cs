using Example.Shared;
using Example.Shared.Sequencing;
using NTDLS.ReliableMessaging;

namespace Example.Cryptography.Client
{
    internal class Program
    {
        static readonly Random _random = new();

        static void Main()
        {
            var client = new RmClient();

            Console.WriteLine("Connecting...");
            client.Connect("localhost", ExampleConstants.PortNumber);

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            Console.WriteLine("Connected. Queuing file transfers...");

            //Start a few threads to send mock files to the server.
            var tasks = new List<Task>();
            for (int fileCount = 0; fileCount < 5; fileCount++)
            {
                tasks.Add(Task.Run(() => SendFile(client)));
            }

            Console.WriteLine("Waiting on file transfers to complete...");
            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Complete! Waiting on server to disconnect");
            while (client.IsConnected)
            {
                Thread.Sleep(100); //Keep the client alive until the server disconnects.
            }

            client.Disconnect();
        }

        private static void SendFile(RmClient client)
        {
            var fileId = Guid.NewGuid();
            int chunkSize = 1024 * 64;
            string fileName = Path.GetRandomFileName();
            long fileSize = _random.Next(10, 20) * 1024 * 1024;
            long bytesRemaining = fileSize;
            long sequence = 0;

            //Tell the server that we are about to start sending a mock-file.
            //We use a query here instead of a notification because we want to wait
            //  for the server to acknowledge that it is ready to receive the file.
            client.Query(new BeginFileTransferQuery(fileId, fileName, fileSize));

            //Send the mock-file bytes in chunks.
            while (bytesRemaining > 0)
            {
                if (bytesRemaining < chunkSize)
                {
                    chunkSize = (int)bytesRemaining;
                }

                var bytes = GenerateRandomBytes(chunkSize);

                client.Notify(new FileChunkNotification(fileId, bytes, sequence++));

                bytesRemaining -= chunkSize;
            }

            client.Query(new EndFileTransferQuery(fileId));
        }

        private static byte[] GenerateRandomBytes(int size)
        {
            var buffer = new byte[size];
            _random.NextBytes(buffer);
            return buffer;
        }
    }
}
