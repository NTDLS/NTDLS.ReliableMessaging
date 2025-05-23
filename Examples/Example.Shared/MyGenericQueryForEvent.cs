using NTDLS.ReliableMessaging;

namespace Example.Shared
{
    public class MyGenericQueryForEvent<T>
        : IRmQuery<MyGenericQueryForEventReply<T>>
    {
        public T Message { get; set; }

        public MyGenericQueryForEvent(T message)
        {
            Message = message;
        }
    }

    public class MyGenericQueryForEventReply<T>
        : IRmQueryReply
    {
        public T Message { get; set; }

        public MyGenericQueryForEventReply(T message)
        {
            Message = message;
        }
    }
}
