using NTDLS.ReliableMessaging;
using System.Net;
using System.Net.Sockets;
using Tests.Shared;

namespace Tests.Unit
{
    /// <summary>
    /// The server listens on IPv6 as well as IPv4 and the client can connect over either. These tests use their own servers
    /// (on free ports) rather than the shared one, because they change the server configuration.
    /// </summary>
    public class TestIPv6
    {
        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static RmServer StartServer(int port, RmConfiguration? configuration = null)
        {
            var server = configuration == null ? new RmServer() : new RmServer(configuration);
            server.OnQueryReceived += (context, query) =>
                query is MyQueryForEvent q ? new MyQueryForEventReply($"reply to {q.Message}") : throw new NotImplementedException();
            server.Start(port);
            return server;
        }

        private static void AssertRoundTrip(RmClient client)
        {
            var reply = client.Query(new MyQueryForEvent("hello"));
            Assert.Equal("reply to hello", reply.Message);
        }

        [Fact(DisplayName = "Server listens dual-stack and accepts IPv4 and IPv6 clients.")]
        public void DualStack()
        {
            if (!Socket.OSSupportsIPv6)
            {
                return; //Nothing to test on a machine without IPv6.
            }

            var port = FreePort();
            var server = StartServer(port, new RmConfiguration { ListenOnIPv6 = true });

            try
            {
                foreach (var host in new[] { "127.0.0.1", "::1", "localhost" })
                {
                    var client = new RmClient();
                    client.Connect(host, port);
                    AssertRoundTrip(client);
                    client.Disconnect();
                }

                var byAddress = new RmClient();
                byAddress.Connect(IPAddress.IPv6Loopback, port);
                AssertRoundTrip(byAddress);
                byAddress.Disconnect();

                var byAddressV4 = new RmClient();
                byAddressV4.Connect(IPAddress.Loopback, port);
                AssertRoundTrip(byAddressV4);
                byAddressV4.Disconnect();

                var withCancellation = new RmClient();
                withCancellation.Connect("::1", port, CancellationToken.None);
                AssertRoundTrip(withCancellation);
                withCancellation.Disconnect();
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact(DisplayName = "By default the server is IPv4 only (as it always was), and ListenOnIPv6 is off.")]
        public void ServerIPv4OnlyByDefault()
        {
            if (!Socket.OSSupportsIPv6)
            {
                return;
            }

            var port = FreePort();
            Assert.False(new RmConfiguration().ListenOnIPv6);
            var server = StartServer(port);

            try
            {
                var v4 = new RmClient();
                v4.Connect("127.0.0.1", port);
                AssertRoundTrip(v4);
                v4.Disconnect();

                Assert.ThrowsAny<Exception>(() => new RmClient().Connect("::1", port));
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact(DisplayName = "A client that connected over IPv6 reconnects over IPv6.")]
        public void ReconnectOverIPv6()
        {
            if (!Socket.OSSupportsIPv6)
            {
                return;
            }

            var port = FreePort();
            var server = StartServer(port, new RmConfiguration { ListenOnIPv6 = true });
            var configuration = new RmConfiguration { AutoReconnect = true, ReconnectDelay = TimeSpan.FromMilliseconds(200) };
            var client = new RmClient(configuration);

            try
            {
                client.Connect("::1", port);
                AssertRoundTrip(client);

                server.Stop();
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (client.IsConnected && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(50);
                }
                Assert.False(client.IsConnected);

                server = StartServer(port, new RmConfiguration { ListenOnIPv6 = true });
                deadline = DateTime.UtcNow.AddSeconds(10);
                while (!client.IsConnected && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(50);
                }
                Assert.True(client.IsConnected);
                Thread.Sleep(200);
                AssertRoundTrip(client);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }
    }
}
