namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Use to provide custom payload serialization.
    /// </summary>
    public interface IRmSerializationProvider
    {
        /// <summary>
        /// Serialize the frame payload to text before it is compressed, encrypted and sent.
        /// </summary>
        /// <returns>Return the altered bytes.</returns>
        public string SerializeToText<T>(T obj);

        /// <summary>
        /// Deserialize the frame payload text to an object after it is received, decrypted and decompressed.
        /// </summary>
        /// <returns>Return the altered bytes.</returns>
        public T? DeserializeToObject<T>(string json);
    }
}
