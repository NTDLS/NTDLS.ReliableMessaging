﻿using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyGenericQuery<T> : IRmQuery<MyGenericQueryReply<T>>
    {
        public T Message { get; set; }

        public MyGenericQuery(T message)
        {
            Message = message;
        }
    }

    public class MyGenericQueryReply<T> : IRmQueryReply
    {
        public T Message { get; set; }

        public MyGenericQueryReply(T message)
        {
            Message = message;
        }
    }
}
