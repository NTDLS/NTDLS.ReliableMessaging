namespace Example.Shared.Cryptography
{
    /// <summary>
    /// Just a plain ol' public and private key pair in byte arrays.
    /// </summary>
    public class PublicPrivateKeyPair
    {
        public byte[] PublicRsaKey { get; private set; }
        public byte[] PrivateRsaKey { get; private set; }

        public PublicPrivateKeyPair(byte[] publicRsaKey, byte[] privateRsaKey)
        {
            PublicRsaKey = publicRsaKey;
            PrivateRsaKey = privateRsaKey;
        }
    }
}
