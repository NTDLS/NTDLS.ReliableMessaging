using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyGenericQueryDifferentReturn<Q, R>
        : IRmQuery<MyGenericQueryDifferentReturnReply<R>>
    {
        public Q Message { get; set; }

        public MyGenericQueryDifferentReturn(Q message)
        {
            Message = message;
        }
    }

    public class MyGenericQueryDifferentReturnReply<T>
        : IRmQueryReply
    {
        public T Message { get; set; }

        public MyGenericQueryDifferentReturnReply(T message)
        {
            Message = message;
        }
    }
}
