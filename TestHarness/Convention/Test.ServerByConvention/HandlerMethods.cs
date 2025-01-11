using NTDLS.ReliableMessaging;
using Test.Library;

namespace Test.ServerByConvention
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

            return new MyQueryReply("This is query reply from the server.");
        }
    }
}
