using NTDLS.ReliableMessaging;

namespace Example.Shared
{
    public class MyNotificationForEvent
        : IRmNotification
    {
        public string Message { get; set; }

        public MyNotificationForEvent(string message)
        {
            Message = message;
        }
    }
}
