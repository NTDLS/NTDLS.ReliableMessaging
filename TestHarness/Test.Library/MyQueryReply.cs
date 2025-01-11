using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyQueryReply : IRmQueryReply
    {
        public string Message { get; set; }

        public MyQueryReply(string message)
        {
            Message = message;
        }
    }
}
