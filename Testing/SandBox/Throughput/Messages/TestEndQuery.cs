using NTDLS.ReliableMessaging;

namespace Throughput.Messages
{
    public class TestEndQuery
        : IRmQuery<TestEndQueryReply>
    {
        public TestEndQuery()
        {
        }
    }

    public class TestEndQueryReply
        : IRmQueryReply
    {
    }
}
