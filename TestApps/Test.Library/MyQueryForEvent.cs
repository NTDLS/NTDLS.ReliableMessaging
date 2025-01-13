using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyQueryForEvent : IRmQuery<MyQueryForEventReply>
    {
        public string Message { get; set; }

        public MyQueryForEvent(string message)
        {
            Message = message;
        }
    }

    public class MyQueryForEventReply : IRmQueryReply
    {
        public string Message { get; set; }

        public MyQueryForEventReply(string message)
        {
            Message = message;
        }
    }
}
