using NTDLS.ReliableMessaging;
using System.Net;
using System.Net.Sockets;
using Tests.Shared;

namespace Tests.Unit
{
    /// <summary>
    /// Nagle's algorithm is off by default on both ends of a connection, and can be turned back on.
    /// </summary>
    public class TestNoDelay
    {
        private class Handlers : IRmMessageHandler
        {
            public async Task<MyAsyncQueryReply> MyAsyncQuery(RmContext context, MyAsyncQuery query)
            {
                await Task.Yield();
                return new MyAsyncQueryReply($"{context.TcpClient.NoDelay}");
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

        [Theory(DisplayName = "NoDelay is applied to both the server's and the client's end of the connection.")]
        [InlineData(null, true)]   //The default.
        [InlineData(true, true)]
        [InlineData(false, false)]
        public async Task NoDelayIsApplied(bool? noDelay, bool expected)
        {
            var configuration = new RmConfiguration();
            if (noDelay != null)
            {
                configuration.NoDelay = noDelay.Value;
            }

            var port = FreePort();
            var server = new RmServer(configuration);
            server.AddHandler(new Handlers());
            server.Start(port);

            bool? clientNoDelay = null;
            var client = new RmClient(configuration);
            client.OnConnected += (context, _) => clientNoDelay = context.TcpClient.NoDelay;
            client.Connect(Constants.HOST_NAME, port);
            try
            {
                //The server reports the setting on its end of the connection.
                var reply = await client.QueryAsync(new MyAsyncQuery("check"));
                Assert.Equal(expected.ToString(), reply.Message);
                Assert.Equal(expected, clientNoDelay);
            }
            finally
            {
                client.Disconnect();
                server.Stop();
            }
        }
    }
}
