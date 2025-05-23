using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyNotification
        : IRmNotification
    {
        public string Message { get; set; }

        public MyNotification(string message)
        {
            Message = message;
        }
    }
}
