using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyGenericNotification<T> : IRmNotification
    {
        public T Message { get; set; }

        public MyGenericNotification(T message)
        {
            Message = message;
        }
    }
}
