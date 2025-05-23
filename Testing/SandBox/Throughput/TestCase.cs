namespace Throughput
{
    public class TestCase
    {
        public string Name { get; set; }
        public DateTime? TransferStartTime { get; set; } = null;
        public long TotalBytesReceived = 0;
        public int ChunksReceived = 0;
        public int ChunkCount { get; set; }

        public TestCase(string name, int chunkCount)
        {
            Name = name;
            ChunkCount = chunkCount;
        }

        public void AddBytes(long count)
        {
            Interlocked.Add(ref ChunksReceived, 1);
            Interlocked.Add(ref TotalBytesReceived, count);
        }

        public double GetThroughputMbPerSecond()
        {
            while (ChunksReceived != ChunkCount)
            {
                Thread.Sleep(1); //Wait to receive all chunks
            }

            if (TransferStartTime != null)
            {
                var elapsedSeconds = (DateTime.UtcNow - TransferStartTime.Value).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    return (TotalBytesReceived / (1024.0 * 1024.0)) / elapsedSeconds;
                }
            }

            return 0;
        }
    }
}
