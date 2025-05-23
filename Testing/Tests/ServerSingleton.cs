using NTDLS.ReliableMessaging;
using Tests.Shared;

namespace Tests
{
    public class ServerSingleton
    {
        private static int _referenceCount = 0;
        private static readonly object _lock = new object();
        private static RmServer? _server;
        private static List<Exception> _exceptions = new();

        public static RmServer GetSingleInstance()
        {
            _referenceCount++;

            if (_server == null)
            {
                lock (_lock)
                {
                    _server ??= CreateNewInstance();
                }
            }

            return _server;
        }

        private static RmServer CreateNewInstance()
        {
            var server = new RmServer();
            server.OnConnected += Server_OnConnected;
            server.OnDisconnected += Server_OnDisconnected;
            server.OnNotificationReceived += Server_OnNotificationReceived;
            server.OnQueryReceived += Server_OnQueryReceived;

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            server.OnException += (context, ex, payload) =>
            {
                _exceptions.Add(ex);
                Console.WriteLine($"Server exception: {ex.Message}");
            };

            server.AddHandler(new MessageHandlers());

            server.Start(Constants.LISTEN_PORT);

            return server;
        }

        private static IRmQueryReply Server_OnQueryReceived(RmContext context, IRmPayload query)
        {
            if (query is MyQueryForEvent myQueryForEvent)
            {
                Console.WriteLine($"Server received query: '{myQueryForEvent.Message}'");
                Assert.Equal("query test", myQueryForEvent.Message);
                return new MyQueryForEventReply("query reply test");
            }
            else if (query is MyGenericQueryForEvent<string> myGenericQuery)
            {
                Console.WriteLine($"Server received query: '{myGenericQuery.Message}'");
                Assert.Equal("query test", myGenericQuery.Message);
                return new MyGenericQueryForEventReply<string>("query reply test");
            }
            throw new NotImplementedException();
        }

        private static void Server_OnNotificationReceived(RmContext context, IRmNotification notification)
        {
            if (notification is MyNotificationForEvent notificationForEvent)
            {
                Assert.Equal("test", notificationForEvent.Message);
            }
            else if (notification is MyGenericNotificationForEvent<string> myGenericNotificationForEvent)
            {
                Assert.Equal("test", myGenericNotificationForEvent.Message);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static void ThrowIfError()
        {
            if (_exceptions.Count != 0)
            {
                throw new AggregateException(_exceptions);
            }
        }

        private static void Server_OnDisconnected(RmContext context)
        {
            Console.WriteLine($"(server) Disconnected: {context.ConnectionId}");
        }

        private static void Server_OnConnected(RmContext context)
        {
            Console.WriteLine($"(server) Connected: {context.ConnectionId}");
        }

        public static void Dereference()
        {
            _referenceCount--;

            if (_referenceCount == 0)
            {
                lock (_lock)
                {
                    _server?.Stop();
                    _server = null;
                }
            }
        }
    }
}
