using NTDLS.ReliableMessaging;
using Tests.Shared;

namespace Tests
{
    internal class MessageHandlers : IRmMessageHandler
    {
        public void MyGenericNotification(RmContext context, MyGenericNotification<string> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
            Assert.Equal("test", notification.Message);
        }

        public void MyGenericNotification(RmContext context, MyGenericNotification<int> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
            Assert.Equal(1234, notification.Message);
        }

        public MyGenericQueryReply<string> MyGenericQuery(RmContext context, MyGenericQuery<string> query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");
            Assert.Equal("query test", query.Message);

            return new MyGenericQueryReply<string>("query reply test");
        }

        public MyQueryReply MyQuery(RmContext context, MyQuery query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");
            Assert.Equal("query test", query.Message);

            return new MyQueryReply("query reply test");
        }

        public void MyNotification(RmContext context, MyNotification notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
            Assert.Equal("test", notification.Message);
        }

        public MyGenericQueryDifferentReturnReply<int> MyGenericQueryDifferentReturn(RmContext context, MyGenericQueryDifferentReturn<string, int> query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");
            Assert.Equal("query test", query.Message);

            return new MyGenericQueryDifferentReturnReply<int>(4321);
        }
    }
}
