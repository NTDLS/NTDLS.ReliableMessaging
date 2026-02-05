using System.Security.Cryptography;
using System.Text;

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
        /// Initializes a new instance of the DmAesCryptographyProvider class using the specified AES key.
        /// </summary>
        /// <param name="aesKey">A byte array containing the AES key to use for cryptographic operations. The array must be 16, 24, or 32
        /// bytes in length, corresponding to 128, 192, or 256-bit key sizes.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="aesKey"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="aesKey"/> is not 16, 24, or 32 bytes in length.</exception>
        public RmAesCryptographyProvider(byte[] aesKey)
        {
            if (aesKey == null)
                throw new ArgumentNullException(nameof(aesKey));

            if (aesKey.Length != 16 && aesKey.Length != 24 && aesKey.Length != 32)
                throw new ArgumentException("AES key must be 16, 24, or 32 bytes.", nameof(aesKey));

            _aesKey = aesKey;
        }

        /// <summary>
        /// Initializes a new instance of the RmAesCryptographyProvider class using a password and a specified AES key
        /// size.
        /// </summary>
        /// <remarks>This constructor derives an AES key from the provided password using the specified
        /// key size. For stronger security, use a complex password and a larger key size when possible.</remarks>
        /// <param name="password">The password used to derive the cryptographic key for AES encryption and decryption. Cannot be null or
        /// empty.</param>
        /// <param name="aesKeySize">The size of the AES key to use for encryption and decryption. Defaults to Aes128 if not specified.</param>
        public RmAesCryptographyProvider(string password, RmAesKeySize aesKeySize = RmAesKeySize.Aes128)
            : this(DeriveKey(password, aesKeySize))
        {
        }

        /// <summary>
        /// Specifies the supported key sizes, in bytes, for AES encryption operations.
        /// </summary>
        /// <remarks>Use this enumeration to select the appropriate AES key size when configuring
        /// cryptographic algorithms. The values correspond to the standard AES key lengths: 128, 192, and 256
        /// bits.</remarks>
        public enum RmAesKeySize
        {
            /// <summary>
            /// Specifies the AES encryption algorithm with a 128-bit key size.
            /// </summary>
            /// <remarks>Use this value to indicate that cryptographic operations should employ AES
            /// with a 128-bit key. AES-128 provides a balance of security and performance suitable for most
            /// applications.</remarks>
            Aes128 = 16,
            /// <summary>
            /// Specifies the Advanced Encryption Standard (AES) algorithm with a 192-bit key size.
            /// </summary>
            /// <remarks>Use this value to select AES encryption with a 192-bit key, which provides a
            /// balance between performance and security.</remarks>
            Aes192 = 24,
            /// <summary>
            /// Specifies a key size of 256 bits for AES encryption.
            /// </summary>
            /// <remarks>Use this value when configuring cryptographic operations that require AES-256
            /// encryption. AES-256 provides a high level of security and is commonly used for protecting sensitive data.</remarks>
            Aes256 = 32
        }

        private static byte[] DeriveKey(string password, RmAesKeySize aesKeySize = RmAesKeySize.Aes128)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));

            byte[] salt = Encoding.UTF8.GetBytes("NTDLS.DatagramMessaging.AES");

            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                (int)aesKeySize
            );
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
