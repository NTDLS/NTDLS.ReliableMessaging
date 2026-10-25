using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class MyAsyncQuery
        : IRmQuery<MyAsyncQueryReply>
    {
        public string Message { get; set; }
        public int DelayMilliseconds { get; set; }
        public bool Throw { get; set; }

        public MyAsyncQuery(string message, int delayMilliseconds = 0, bool @throw = false)
        {
            Message = message;
            DelayMilliseconds = delayMilliseconds;
            Throw = @throw;
        }
    }

    public class MyAsyncQueryReply
        : IRmQueryReply
    {
        public string Message { get; set; }

        public MyAsyncQueryReply(string message)
        {
            Message = message;
        }
    }
}
