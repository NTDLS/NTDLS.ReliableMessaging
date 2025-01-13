using NTDLS.ReliableMessaging;

namespace Test.Library
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
