using NTDLS.ReliableMessaging;

namespace Throughput.Messages
{
    public class TestBeginQuery
        : IRmQuery<TestBeginQueryReply>
    {
        public string TestName { get; set; }
        public int ChunkCount { get; set; }

        public TestBeginQuery(string testName, int chunkCount)
        {
            TestName = testName;
            ChunkCount = chunkCount;
        }
    }

    public class TestBeginQueryReply
        : IRmQueryReply
    {
    }
}
