using NTDLS.ReliableMessaging;
using Throughput.Messages;

namespace Throughput
{
    internal class MessageHandlers : IRmMessageHandler
    {
        private TestCase _testCase = new("n/a", 0);

        public TestBeginQueryReply TestBeginQuery(TestBeginQuery query)
        {
            _testCase = new TestCase(query.TestName, query.ChunkCount);
            return new TestBeginQueryReply();
        }

        public TestEndQueryReply TestEndQuery(TestEndQuery query)
        {
            var throughput = _testCase.GetThroughputMbPerSecond();
            Console.WriteLine($"{_testCase.Name} {throughput:n2} {_testCase.ChunksReceived:n0}");
            return new TestEndQueryReply();
        }

        public void ChunkNotification(ChunkNotification notification)
        {
            _testCase.TransferStartTime ??= DateTime.UtcNow;
            _testCase.AddBytes(notification.Bytes.Length);
        }

        public ChunkQueryReply ChunkQuery(ChunkQuery payload)
        {
            _testCase.TransferStartTime ??= DateTime.UtcNow;
            _testCase.AddBytes(payload.Bytes.Length);
            return new ChunkQueryReply();
        }
    }
}
