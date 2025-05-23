using NTDLS.ReliableMessaging;

namespace Example.Shared.Cryptography
{
    public class PublicKeyExchangeQuery
        : IRmQuery<PublicKeyExchangeQueryReply>
    {
        /// <summary>
        /// Public key bytes from the client.
        /// </summary>
        public byte[] PublicKeyBytes { get; set; }

        public PublicKeyExchangeQuery(byte[] publicKeyBytes)
        {
            PublicKeyBytes = publicKeyBytes;
        }
    }

    public class PublicKeyExchangeQueryReply
        : IRmQueryReply
    {
        /// <summary>
        /// Public key bytes from the server.
        /// </summary>
        public byte[] PublicKeyBytes { get; set; }

        public PublicKeyExchangeQueryReply(byte[] publicKeyBytes)
        {
            PublicKeyBytes = publicKeyBytes;
        }
    }
}
