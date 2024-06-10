namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Use to manipulate the payload bytes after they are compressed but before they are framed.
    /// </summary>
    public interface IRmCryptographyProvider
    {
        /// <summary>
        /// Encrypt the frame payload before it is sent.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="payload">Contains the raw unencrypted data.</param>
        /// <returns>Return the alerted altered bytes.</returns>
        public byte[] Encrypt(RmContext context, byte[] payload);

        /// <summary>
        /// Decrypt the frame payload after it is received.
        /// </summary>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="encryptedPayload">Contains the encrypted data.</param>
        /// <returns>Return the alerted altered bytes.</returns>
        public byte[] Decrypt(RmContext context, byte[] encryptedPayload);
    }
}
