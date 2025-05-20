using System.IO.Compression;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Default compression provider that uses the Brotli compression algorithm.
    /// </summary>
    public class RmBrotliCompressionProvider : IRmCompressionProvider
    {
        /// <summary>
        /// Compression level to use when compressing the payload.
        /// </summary>
        public CompressionLevel CompressionLevel { get; private set; }

        /// <summary>
        /// Creates a new instance of the Brotli compression provider with the default compression level.
        /// </summary>
        public RmBrotliCompressionProvider()
        {
            CompressionLevel = CompressionLevel.Optimal;
        }

        /// <summary>
        /// creates a new instance of the Brotli compression provider with the specified compression level.
        /// </summary>
        public RmBrotliCompressionProvider(CompressionLevel compressionLevel)
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
            using (var brotli = new BrotliStream(mso, CompressionLevel))
            {
                msi.CopyTo(brotli);
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
            using (var brotli = new BrotliStream(msi, CompressionMode.Decompress))
            {
                brotli.CopyTo(mso);
            }
            return mso.ToArray();
        }
    }
}
