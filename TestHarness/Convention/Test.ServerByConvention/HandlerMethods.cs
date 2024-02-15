using NTDLS.ReliableMessaging;
using TestHarness.Payloads;

namespace TestHarness
{
    internal class HandlerMethods : IRmMessageHandler
    {
        public void MyNotificationReceived(RmContext context, MyNotification notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
        }

        public MyQueryReply MyQueryReceived(RmContext context, MyQuery query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");
            return new MyQueryReply("This is the query reply from the server.");
        }
    }
}
