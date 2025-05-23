using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyQuery
        : IRmQuery<MyQueryReply>
    {
        public string Message { get; set; }

        public MyQuery(string message)
        {
            Message = message;
        }
    }

    public class MyQueryReply
        : IRmQueryReply
    {
        public string Message { get; set; }

        public MyQueryReply(string message)
        {
            Message = message;
        }
    }
}
