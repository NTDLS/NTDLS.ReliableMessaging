using NTDLS.ReliableMessaging;

namespace TestHarness.Payloads
{
    public class MyNotification : IRmNotification
    {
        public string Message { get; set; }

        public MyNotification(string message)
        {
            Message = message;
        }
    }
}
