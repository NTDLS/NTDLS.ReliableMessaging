using NTDLS.ReliableMessaging;

namespace Example.Shared
{
    public class MyGenericNotificationForEvent<T>
        : IRmNotification
    {
        public T? Message { get; set; }

        public MyGenericNotificationForEvent(T message)
        {
            Message = message;
        }
    }
}
