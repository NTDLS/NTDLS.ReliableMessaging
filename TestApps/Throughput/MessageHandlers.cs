using NTDLS.ReliableMessaging;
using Throughput.Messages;

namespace Throughput
{
    internal class MessageHandlers : IRmMessageHandler
    {
        private TestCase _testCase = new("n/a", 0);

        public TestBeginQueryReply TestBeginQuery(RmContext context, TestBeginQuery query)
        {
            _testCase = new TestCase(query.TestName, query.ChunkCount);
            return new TestBeginQueryReply();
        }

        public TestEndQueryReply TestEndQuery(RmContext context, TestEndQuery query)
        {
            var throughput = _testCase.GetThroughputMbPerSecond();
            Console.WriteLine($"{_testCase.Name} {throughput:n2} {_testCase.ChunksReceived:n0}");
            return new TestEndQueryReply();
        }

        public void FileTransferChunkNotification(RmContext context, ChunkNotification notification)
        {
            _testCase.TransferStartTime ??= DateTime.UtcNow;
            _testCase.AddBytes(notification.Bytes.Length);
        }

        public void FileTransferChunkNotification(RmContext context, RmBytesNotification payload)
        {
            _testCase.TransferStartTime ??= DateTime.UtcNow;
            _testCase.AddBytes(payload.Bytes.Length);
        }
    }
}
