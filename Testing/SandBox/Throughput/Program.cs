using NTDLS.ReliableMessaging;
using System.Diagnostics;
using Throughput.Messages;

namespace Throughput
{
    internal class Program
    {
        static readonly int _port = 36251;
        static readonly int _randSeed = 789654123;

        static void Main(string[] args)
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            int steps = 10;
            int chunkSize;

            #region SendTestMessageNotificationChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageNotificationChunks(chunkSize, 1000);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageNotificationChunks(chunkSize, 1000, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageNotificationChunks(chunkSize, 1000, new RmBrotliCompressionProvider());
                chunkSize *= 2;
            }

            #endregion

            #region SendTestMessageQueryChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageQueryChunks(chunkSize, 1000);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageQueryChunks(chunkSize, 1000, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestMessageQueryChunks(chunkSize, 1000, new RmBrotliCompressionProvider());
                chunkSize *= 2;
            }

            #endregion

            #region SendTestChaosQueryChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestChaosQueryChunks(chunkSize, 1000, 10);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestChaosQueryChunks(chunkSize, 1000, 10, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                SendTestChaosQueryChunks(chunkSize, 1000, 10, new RmBrotliCompressionProvider());
                chunkSize *= 2;
            }

            #endregion

            Console.WriteLine("Press [enter]] to exit.");
            Console.ReadLine();
        }

        private static byte[] GenerateRandomBytes(int size, int? seed = null)
        {
            seed ??= Environment.TickCount;

            var random = new Random(seed.Value);
            var buffer = new byte[size];
            random.NextBytes(buffer);
            return buffer;
        }

        private static void SendTestMessageNotificationChunks(int chunkSize, int chunkCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var server = new RmServer();
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient();
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            client.Query(new TestBeginQuery($"Notification {compressionName} {chunkSize}", chunkCount));

            for (int i = 0; i < chunkCount; i++)
            {
                client.Notify(new ChunkNotification(payload));
            }

            client.Query(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }

        private static void SendTestMessageQueryChunks(int chunkSize, int chunkCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var server = new RmServer();
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient();
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            client.Query(new TestBeginQuery($"Query {compressionName} {chunkSize}", chunkCount));

            for (int i = 0; i < chunkCount; i++)
            {
                client.Query(new ChunkQuery(payload));
            }

            client.Query(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }

        private static void SendTestChaosQueryChunks(int chunkSize, int chunkCount, int threadCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var server = new RmServer();
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient();
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            var tasks = new List<Task>();

            client.Query(new TestBeginQuery($"Chaos {compressionName} {chunkSize}", chunkCount * threadCount * 2));

            for (int t = 0; t < threadCount; t++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        client.Query(new ChunkQuery(payload));
                        client.Notify(new ChunkNotification(payload));
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            client.Query(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }
    }
}
