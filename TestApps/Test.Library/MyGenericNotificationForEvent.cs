using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyGenericNotificationForEvent<T> : IRmNotification
    {
        public T? Message { get; set; }

        public MyGenericNotificationForEvent(T message)
        {
            Message = message;
        }
    }
}
