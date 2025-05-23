using NTDLS.ReliableMessaging;

namespace Tests.Shared
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
