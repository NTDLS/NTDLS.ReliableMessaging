using NTDLS.ReliableMessaging;

namespace Test.Library
{
    public class MyNotificationForEvent : IRmNotification
    {
        public string Message { get; set; }

        public MyNotificationForEvent(string message)
        {
            Message = message;
        }
    }
}
