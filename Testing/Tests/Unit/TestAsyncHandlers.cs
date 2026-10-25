using NTDLS.ReliableMessaging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Tests.Shared;

namespace Tests.Unit
{
    /// <summary>
    /// Query handlers may return Task&lt;TReply&gt;. These tests use their own server (on a free port) rather than the shared one,
    /// because some of them intentionally throw from the handler and the shared server records every exception as a failure.
    /// </summary>
    public class TestAsyncHandlers
    {
        private class AsyncMessageHandlers : IRmMessageHandler
        {
            public int Concurrent;
            public int MaxConcurrent;

            public async Task<MyAsyncQueryReply> MyAsyncQuery(RmContext context, MyAsyncQuery query)
            {
                var concurrent = Interlocked.Increment(ref Concurrent);
                InterlockedMax(ref MaxConcurrent, concurrent);
                try
                {
                    await Task.Delay(query.DelayMilliseconds);

                    if (query.Throw)
                    {
                        throw new InvalidOperationException($"failed {query.Message}");
                    }

                    return new MyAsyncQueryReply($"reply to {query.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref Concurrent);
                }
            }

            private static void InterlockedMax(ref int target, int value)
            {
                int current;
                while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current)
                {
                }
            }
        }

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static (RmServer server, RmClient client) Start(AsyncMessageHandlers handlers, RmConfiguration? configuration = null)
        {
            var port = FreePort();
            var server = configuration == null ? new RmServer() : new RmServer(configuration);
            server.AddHandler(handlers);
            server.Start(port);

            var client = new RmClient();
            client.Connect(Constants.HOST_NAME, port);
            return (server, client);
        }

        [Fact(DisplayName = "Async query handler replies.")]
        public async Task Replies()
        {
            var (server, client) = Start(new AsyncMessageHandlers());
            try
            {
                Assert.Equal("reply to now", (await client.QueryAsync(new MyAsyncQuery("now"))).Message);
                Assert.Equal("reply to later", (await client.QueryAsync(new MyAsyncQuery("later", 100))).Message);
                Assert.Equal("reply to sync", client.Query(new MyAsyncQuery("sync", 50)).Message);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "Async query handler exceptions are returned to the caller.")]
        public async Task Throws()
        {
            var serverExceptions = new List<Exception>();
            var (server, client) = Start(new AsyncMessageHandlers());
            server.OnException += (context, ex, payload) => { lock (serverExceptions) serverExceptions.Add(ex); };
            try
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => client.QueryAsync(new MyAsyncQuery("boom", 50, @throw: true)));
                Assert.Contains("failed boom", ex.GetBaseException().Message);

                //The connection must remain usable after a handler failure.
                Assert.Equal("reply to after", (await client.QueryAsync(new MyAsyncQuery("after"))).Message);

                lock (serverExceptions) Assert.Contains(serverExceptions, o => o.Message.Contains("failed boom"));
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        /// <summary>
        /// Pending async handlers must not hold threads. If they did, many concurrent slow handlers would each tie up a thread-pool
        /// thread and the pool (which only adds a thread or two per second once exhausted) would serialize them.
        /// </summary>
        [Theory(DisplayName = "Pending async query handlers do not block threads.")]
        [InlineData(true, false)]  //Default: frames are processed on the thread-pool.
        [InlineData(false, true)]  //Frames processed inline, queries waited on via the thread-pool.
        [InlineData(false, false)] //Everything inline on the connection's receive thread.
        public async Task DoesNotBlock(bool asynchronousFrameProcessing, bool asynchronousQueryWaiting)
        {
            const int queryCount = 500;
            const int delayMs = 1000;

            var handlers = new AsyncMessageHandlers();
            var (server, client) = Start(handlers, new RmConfiguration
            {
                AsynchronousFrameProcessing = asynchronousFrameProcessing,
                AsynchronousQueryWaiting = asynchronousQueryWaiting
            });

            try
            {
                var sw = Stopwatch.StartNew();
                var replies = await Task.WhenAll(Enumerable.Range(0, queryCount)
                    .Select(i => client.QueryAsync(new MyAsyncQuery($"{i}", delayMs))));
                sw.Stop();

                for (int i = 0; i < queryCount; i++)
                {
                    Assert.Equal($"reply to {i}", replies[i].Message);
                }

                Assert.True(handlers.MaxConcurrent > queryCount / 2, $"Only {handlers.MaxConcurrent} of {queryCount} handlers ran concurrently.");
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"{queryCount} concurrent {delayMs}ms handlers took {sw.Elapsed.TotalSeconds:n1}s.");
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }
    }
}
