using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyQueryForEvent
        : IRmQuery<MyQueryForEventReply>
    {
        public string Message { get; set; }

        public MyQueryForEvent(string message)
        {
            Message = message;
        }
    }

    public class MyQueryForEventReply
        : IRmQueryReply
    {
        public string Message { get; set; }

        public MyQueryForEventReply(string message)
        {
            Message = message;
        }
    }
}
