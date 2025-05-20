using System.IO.Compression;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Default compression provider that uses the Deflate compression algorithm.
    /// </summary>
    public class RmDeflateCompressionProvider : IRmCompressionProvider
    {
        /// <summary>
        /// Compression level to use when compressing the payload.
        /// </summary>
        public CompressionLevel CompressionLevel { get; private set; }

        /// <summary>
        /// Creates a new instance of the Deflate compression provider with the default compression level.
        /// </summary>
        public RmDeflateCompressionProvider()
        {
            CompressionLevel = CompressionLevel.Optimal;
        }

        /// <summary>
        /// creates a new instance of the Deflate compression provider with the specified compression level.
        /// </summary>
        public RmDeflateCompressionProvider(CompressionLevel compressionLevel)
        {
            CompressionLevel = CompressionLevel;
        }

        /// <summary>
        /// Compressed the payload using deflate.
        /// </summary>
        public byte[] Compress(RmContext context, byte[] payload)
        {
            if (payload == null)
                return Array.Empty<byte>();

            using var msi = new MemoryStream(payload);
            using var mso = new MemoryStream();
            using (var deflate = new DeflateStream(mso, CompressionLevel))
            {
                msi.CopyTo(deflate);
            }
            return mso.ToArray();
        }

        /// <summary>
        /// Decompress the payload using deflate.
        /// </summary>
        public byte[] DeCompress(RmContext context, byte[] compressedPayload)
        {
            using var msi = new MemoryStream(compressedPayload);
            using var mso = new MemoryStream();
            using (var deflate = new DeflateStream(msi, CompressionMode.Decompress))
            {
                deflate.CopyTo(mso);
            }
            return mso.ToArray();
        }
    }
}
