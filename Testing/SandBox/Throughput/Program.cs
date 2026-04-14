using NTDLS.ReliableMessaging;
using System.Diagnostics;
using Throughput.Messages;

namespace Throughput
{
    internal class Program
    {
        static readonly int _port = 36251;
        static readonly int _randSeed = 789654123;

        static async Task Main()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

            int steps = 10;
            int chunkSize;

            #region SendTestMessageNotificationChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageNotificationChunks(chunkSize, 1000);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageNotificationChunks(chunkSize, 1000, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageNotificationChunks(chunkSize, 1000, new RmBrotliCompressionProvider());
                chunkSize *= 2;
            }

            #endregion

            #region SendTestMessageQueryChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageQueryChunks(chunkSize, 1000);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageQueryChunks(chunkSize, 1000, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestMessageQueryChunks(chunkSize, 1000, new RmBrotliCompressionProvider());
                chunkSize *= 2;
            }

            #endregion

            #region SendTestChaosQueryChunks.

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestChaosQueryChunks(chunkSize, 1000, 1);
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestChaosQueryChunks(chunkSize, 1000, 10, new RmDeflateCompressionProvider());
                chunkSize *= 2;
            }

            chunkSize = 1024;
            for (int i = 1; i < steps; i++)
            {
                await SendTestChaosQueryChunks(chunkSize, 1000, 10, new RmBrotliCompressionProvider());
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

        private static async Task SendTestMessageNotificationChunks(int chunkSize, int chunkCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var config = new RmConfiguration()
            {
                QueryTimeout = TimeSpan.FromHours(1)
            };

            var server = new RmServer(config);
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient(config);
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            await client.QueryAsync(new TestBeginQuery($"Method: Notification, Compression: {compressionName}, Chunk Size: {chunkSize:n0}", chunkCount));

            for (int i = 0; i < chunkCount; i++)
            {
                await client.NotifyAsync(new ChunkNotification(payload));
            }

            await client.QueryAsync(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }

        private static async Task SendTestMessageQueryChunks(int chunkSize, int chunkCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var config = new RmConfiguration()
            {
                QueryTimeout = TimeSpan.FromHours(1)
            };

            var server = new RmServer(config);
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient(config);
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            await client.QueryAsync(new TestBeginQuery($"Method: Query, Compression: {compressionName}, Chunk Size: {chunkSize:n0}", chunkCount));

            for (int i = 0; i < chunkCount; i++)
            {
                await client.QueryAsync(new ChunkQuery(payload));
            }

            await client.QueryAsync(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }

        private static async Task SendTestChaosQueryChunks(int chunkSize, int chunkCount, int threadCount, IRmCompressionProvider? compressionProvider = null)
        {
            var payload = GenerateRandomBytes(chunkSize, _randSeed);

            var config = new RmConfiguration()
            {
                QueryTimeout = TimeSpan.FromHours(1)
            };

            var server = new RmServer(config);
            if (compressionProvider != null)
            {
                server.SetCompressionProvider(compressionProvider);
            }
            server.Start(_port);
            server.AddHandler(new MessageHandlers());

            //Connect client.
            var client = new RmClient(config);
            if (compressionProvider != null)
            {
                client.SetCompressionProvider(compressionProvider);
            }
            client.Connect("127.0.0.1", _port);

            string compressionName = compressionProvider?.GetType().Name ?? "No-compression";

            var tasks = new List<Task>();

            await client.QueryAsync(new TestBeginQuery($"Method: Chaos, Compression: {compressionName}, Chunk Size: {chunkSize:n0}", chunkCount * 2));

            for (int t = 0; t < threadCount; t++)
            {
                tasks.Add(Task.Run( async () =>
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        await client.QueryAsync(new ChunkQuery(payload));
                        await client.NotifyAsync(new ChunkNotification(payload));
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            await client.QueryAsync(new TestEndQuery());

            client.Disconnect();
            server.Stop();
        }
    }
}
