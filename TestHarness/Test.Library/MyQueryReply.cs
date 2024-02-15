using NTDLS.ReliableMessaging;

namespace TestHarness.Payloads
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
