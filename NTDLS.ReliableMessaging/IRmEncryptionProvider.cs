namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Use to manipulate the payload bytes after they are compressed but before they are framed.
    /// </summary>
    public interface IRmEncryptionProvider
    {
        /// <summary>
        /// Encrypt the frame payload before it is sent.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        public byte[] Encrypt(byte[] payload);

        /// <summary>
        /// Decrypt the frame payload after it is received.
        /// </summary>
        /// <param name="encryptedPayload"></param>
        /// <returns></returns>
        public byte[] Decrypt(byte[] encryptedPayload);
    }
}
