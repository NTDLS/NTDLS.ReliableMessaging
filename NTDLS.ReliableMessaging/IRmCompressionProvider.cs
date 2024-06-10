namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Use to manipulate the payload bytes before they are optionally encrypted and then framed.
    /// </summary>
    public interface IRmCompressionProvider
    {
        /// <summary>
        /// Compress the frame payload before it is sent.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="payload">Contains the raw uncompressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] Compress(RmContext context, byte[] payload);

        /// <summary>
        /// Encrypt the frame payload after it is received.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="encryptedPayload">Contains the compressed data.</param>
        /// <returns>Return the altered bytes.</returns>
        public byte[] DeCompress(RmContext context, byte[] encryptedPayload);
    }
}
