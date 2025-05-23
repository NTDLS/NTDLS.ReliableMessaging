using NTDLS.ReliableMessaging;

namespace Example.Shared
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
