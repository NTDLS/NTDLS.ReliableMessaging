using System.Security.Cryptography;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Provides AES-based encryption and decryption functionality.
    /// </summary>
    /// <remarks>This class implements the <see cref="IRmCryptographyProvider"/> interface to provide
    /// cryptographic operations using the AES algorithm in CBC mode with PKCS7 padding. It supports encrypting and
    /// decrypting byte arrays while ensuring that the initialization vector (IV) is handled appropriately for each
    /// operation.</remarks>
    public class RmAesCryptographyProvider : IRmCryptographyProvider
    {
        private readonly byte[] _aesKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmAesCryptographyProvider"/> class with the specified AES key.
        /// </summary>
        /// <param name="aesKey"></param>
        public RmAesCryptographyProvider(byte[] aesKey)
        {
            _aesKey = aesKey;
        }

        /// <summary>
        /// Decrypts the specified encrypted byte array using the provided <see cref="RmContext"/>.
        /// </summary>
        /// <remarks>The method uses AES encryption in CBC mode with PKCS7 padding. The first 16 bytes of
        /// the input array are  interpreted as the initialization vector (IV), and the remaining bytes are treated as
        /// the ciphertext. Ensure that the provided <paramref name="encryptedBytes"/> array is correctly
        /// formatted.</remarks>
        /// <param name="context">The <see cref="RmContext"/> instance that provides the necessary context for decryption.  This parameter is
        /// currently unused but reserved for future functionality.</param>
        /// <param name="encryptedBytes">A byte array containing the encrypted data. The first 16 bytes must represent the initialization vector
        /// (IV), followed by the ciphertext.</param>
        /// <returns>A byte array containing the decrypted data.</returns>
        public byte[] Decrypt(RmContext context, byte[] encryptedBytes)
        {
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract IV
            var iv = new byte[16];
            var ciphertext = new byte[encryptedBytes.Length - 16];
            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, 16);
            Buffer.BlockCopy(encryptedBytes, 16, ciphertext, 0, ciphertext.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }

        /// <summary>
        /// Encrypts the specified plaintext using AES encryption with a randomly generated initialization vector (IV).
        /// </summary>
        /// <remarks>The returned byte array includes the initialization vector (IV) as the first segment,
        /// followed by the encrypted ciphertext. The IV is required for decryption and must be extracted and used when
        /// decrypting the data. (which is handled by Decrypt()</remarks>
        /// <param name="context">The <see cref="RmContext"/> instance providing the encryption context. This parameter is reserved for future
        /// use and is not currently utilized.</param>
        /// <param name="plaintextBytes">The plaintext data to encrypt, represented as a byte array. Cannot be null or empty.</param>
        /// <returns>A byte array containing the encrypted data, with the IV prepended to the ciphertext.</returns>
        public byte[] Encrypt(RmContext context, byte[] plaintextBytes)
        {
            using var aes = Aes.Create();
            aes.Key = _aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Prepend IV to ciphertext
            var result = new byte[aes.IV.Length + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

            return result;
        }
    }
}
