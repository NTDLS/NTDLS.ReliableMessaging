using NTDLS.ReliableMessaging;

namespace Example.Sequencing.Server
{
    internal class FileTransferSession
    {
        /// <summary>
        /// Name of the file that is being transferred.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Size of the file in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// The total number of bytes received so far.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// We use a RmSequenceBuffer because it ensures that packets that are
        //  received out of order are buffered and processed in the proper order.
        /// </summary>
        public RmSequenceBuffer<byte[]> SequenceBuffer = new();

        public FileTransferSession(string fileName, long fileSize)
        {
            FileName = fileName;
            FileSize = fileSize;
        }
    }
}
