using NTDLS.ReliableMessaging;

namespace Tests.Shared
{
    public class SequencedNotification
        : IRmNotification
    {
        public int Sequence { get; set; }
        public int DelayMilliseconds { get; set; }

        public SequencedNotification(int sequence, int delayMilliseconds = 0)
        {
            Sequence = sequence;
            DelayMilliseconds = delayMilliseconds;
        }
    }
}
