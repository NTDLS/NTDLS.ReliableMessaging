using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyGenericNotification<T>
        : IRmNotification
    {
        public T Message { get; set; }

        public MyGenericNotification(T message)
        {
            Message = message;
        }
    }
}
