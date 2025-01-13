using NTDLS.ReliableMessaging;
using Test.Library;

namespace Test.ServerByGenericConvention
{
    internal class MessageHandlers : IRmMessageHandler
    {
        public void MyGenericNotification(RmContext context, MyGenericNotification<string> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
        }

        public void MyGenericNotification(RmContext context, MyGenericNotification<int> notification)
        {
            Console.WriteLine($"Server received notification: {notification.Message}");
        }

        public MyGenericQueryReply<string> MyGenericQuery(RmContext context, MyGenericQuery<string> query)
        {
            Console.WriteLine($"Server received query: '{query.Message}'");

            return new MyGenericQueryReply<string>("This is query reply from the server.");
        }
    }
}
