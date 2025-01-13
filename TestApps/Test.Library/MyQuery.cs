﻿using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyQuery : IRmQuery<MyQueryReply>
    {
        public string Message { get; set; }

        public MyQuery(string message)
        {
            Message = message;
        }
    }

    public class MyQueryReply : IRmQueryReply
    {
        public string Message { get; set; }

        public MyQueryReply(string message)
        {
            Message = message;
        }
    }
}