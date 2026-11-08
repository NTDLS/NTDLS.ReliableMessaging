using NTDLS.ReliableMessaging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Tests.Shared;

namespace Tests.Unit
{
    /// <summary>
    /// How frames from one connection are handed to handlers: notifications in order, queries at the same time, and
    /// replies without waiting behind either. These use their own server so that their settings do not affect other tests.
    /// </summary>
    public class TestFrameDispatch
    {
        private class Handlers : IRmMessageHandler
        {
            public ConcurrentQueue<int> Received { get; } = new();
            public int Concurrent;
            public int MaxConcurrent;
            public int NotificationsRunning;
            public int MaxNotificationsRunning;

            public void SequencedNotification(RmContext context, SequencedNotification notification)
            {
                var running = Interlocked.Increment(ref NotificationsRunning);
                InterlockedMax(ref MaxNotificationsRunning, running);
                try
                {
                    if (notification.DelayMilliseconds > 0)
                    {
                        Thread.Sleep(notification.DelayMilliseconds);
                    }
                    Received.Enqueue(notification.Sequence);
                }
                finally
                {
                    Interlocked.Decrement(ref NotificationsRunning);
                }
            }

            public async Task<MyAsyncQueryReply> MyAsyncQuery(RmContext context, MyAsyncQuery query)
            {
                var concurrent = Interlocked.Increment(ref Concurrent);
                InterlockedMax(ref MaxConcurrent, concurrent);
                try
                {
                    await Task.Delay(query.DelayMilliseconds);
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

        private static (RmServer server, RmClient client) Start(Handlers handlers, RmConfiguration? configuration = null)
        {
            var port = FreePort();
            var server = new RmServer(configuration ?? new RmConfiguration());
            server.AddHandler(handlers);
            server.Start(port);

            var client = new RmClient();
            client.Connect(Constants.HOST_NAME, port);
            return (server, client);
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutSeconds = 15)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                Thread.Sleep(10);
            }
            return condition();
        }

        [Fact(DisplayName = "Notifications from one connection are handled one at a time, in the order they were sent.")]
        public void NotificationsStayInOrder()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers);
            try
            {
                const int count = 5000;
                for (int i = 0; i < count; i++)
                {
                    client.Notify(new SequencedNotification(i));
                }

                Assert.True(WaitUntil(() => handlers.Received.Count == count), $"Received {handlers.Received.Count} of {count}.");
                Assert.Equal(Enumerable.Range(0, count), handlers.Received);
                Assert.Equal(1, handlers.MaxNotificationsRunning);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "Queries from one connection are handled at the same time.")]
        public async Task QueriesRunConcurrently()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers);
            try
            {
                var replies = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => client.QueryAsync(new MyAsyncQuery($"q{i}", 300))));
                Assert.Equal(20, replies.Length);
                Assert.Equal(20, handlers.MaxConcurrent);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "No more queries run at once than MaxConcurrentQueries, and the rest wait rather than fail.")]
        public async Task QueriesAreLimited()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers, new RmConfiguration { MaxConcurrentQueries = 3 });
            try
            {
                var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => client.QueryAsync(new MyAsyncQuery($"q{i}", 100))));
                Assert.Equal(12, replies.Length);
                Assert.Equal(3, handlers.MaxConcurrent);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "A slow notification handler does not hold up queries on the same connection.")]
        public async Task SlowNotificationsDoNotBlockQueries()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers);
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    client.Notify(new SequencedNotification(i, 400));
                }

                var stopwatch = Stopwatch.StartNew();
                var reply = await client.QueryAsync(new MyAsyncQuery("quick"));
                Assert.Equal("reply to quick", reply.Message);
                Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"The query took {stopwatch.ElapsedMilliseconds}ms behind 2 seconds of notifications.");

                Assert.True(WaitUntil(() => handlers.Received.Count == 5));
                Assert.Equal(Enumerable.Range(0, 5), handlers.Received);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "When the notification queue is full, the sender is slowed down and nothing is lost.")]
        public void FullNotificationQueueAppliesBackpressure()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers, new RmConfiguration { MaxPendingNotifications = 2 });
            try
            {
                const int count = 40;
                for (int i = 0; i < count; i++)
                {
                    client.Notify(new SequencedNotification(i, 20));
                }

                //The client sent faster than 20ms each; the queue of 2 means they could only be accepted as fast as they were handled.
                Assert.True(WaitUntil(() => handlers.Received.Count == count), $"Received {handlers.Received.Count} of {count}.");
                Assert.Equal(Enumerable.Range(0, count), handlers.Received);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }

        [Fact(DisplayName = "Notifications and queries can still run on the receive thread when configured to.")]
        public async Task SynchronousSettingsStillWork()
        {
            var handlers = new Handlers();
            var (server, client) = Start(handlers, new RmConfiguration { AsynchronousNotifications = false, AsynchronousQueryWaiting = false });
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    client.Notify(new SequencedNotification(i));
                }
                Assert.Equal("reply to sync", (await client.QueryAsync(new MyAsyncQuery("sync", 50))).Message);
                Assert.True(WaitUntil(() => handlers.Received.Count == 100));
                Assert.Equal(Enumerable.Range(0, 100), handlers.Received);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }
    }
}
