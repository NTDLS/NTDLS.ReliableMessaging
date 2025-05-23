using Example.Shared;
using Example.Shared.Cryptography;
using NTDLS.ReliableMessaging;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Example.Cryptography.Server
{
    /// <summary>
    /// This is the server-side implementation of the message handlers for the cryptography example.
    /// 
    /// See: Example.Cryptography.Client.MessageHandlers for the client-side implementation.
    /// </summary>
    internal class MessageHandlers
        : IRmMessageHandler
    {
        ConcurrentDictionary<Guid, RmAesCryptographyProvider> _cryptographyProviders = new();

        public PublicKeyExchangeQueryReply PublicKeyExchangeQuery(RmContext context, PublicKeyExchangeQuery query)
        {
            #region BouncyCastle Diffie-Hellman key exchange (outside the scope of this example).

            var dhParams = DHStandardGroups.rfc3526_2048;
            var keyGen = GeneratorUtilities.GetKeyPairGenerator("DH");
            keyGen.Init(new DHKeyGenerationParameters(new SecureRandom(), dhParams));

            //Deserialize client public key (without parameters)
            var receivedKeyRaw = (DHPublicKeyParameters)PublicKeyFactory.CreateKey(query.PublicKeyBytes);

            //Force the known parameters onto it.
            var receivedKey = new DHPublicKeyParameters(receivedKeyRaw.Y, dhParams);

            var keyPair = keyGen.GenerateKeyPair();

            //Proceed with agreement
            var agreement = new DHBasicAgreement();
            agreement.Init(keyPair.Private);
            var sharedSecret = agreement.CalculateAgreement(receivedKey);
            var secretBytes = sharedSecret.ToByteArrayUnsigned();

            //Create a new public key to send back to the client.
            var publicKey = new DHPublicKeyParameters(((DHPublicKeyParameters)keyPair.Public).Y, dhParams);
            var publicKeyBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded();

            #endregion

            //We now have the shared secret, so we can create the AES key.
            var aesKey = SHA256.HashData(secretBytes);

            //We cant apply the cryptography provider yet, because we need to send the public key back to the client first.
            //So we store it in a dictionary for later use. The client will let us know when it is ready to apply the cryptography provider.
            _cryptographyProviders.TryAdd(context.ConnectionId, new RmAesCryptographyProvider(aesKey));

            return new PublicKeyExchangeQueryReply(publicKeyBytes);
        }

        /// <summary>
        /// The client is letting us know that it is ready to apply the cryptography provider.
        /// </summary>
        public ApplyCryptographyQueryReply ApplyCryptographyQuery(RmContext context, ApplyCryptographyQuery query)
        {
            //Get the previously stored cryptography provider and set it on the context.
            //Yes, TryRemove gets the value and removes it from the dictionary.
            if (_cryptographyProviders.TryRemove(context.ConnectionId, out var cryptographyProvider))
            {
                context.SetCryptographyProvider(cryptographyProvider);
            }
            else
            {
                throw new Exception("Cryptography provider not found.");
            }
            return new ApplyCryptographyQueryReply();
        }

        /// <summary>
        /// Just a simple notification to show that the client can send encrypted notifications to the server.
        /// </summary>
        public void MyNotification(RmContext context, MyNotification notification)
        {
            if (context.GetCryptographyProvider() == null)
            {
                //Just to prove that the cryptography provider is set on the context.
                throw new Exception("Cryptography provider not set.");
            }

            context.Notify(new MyNotification("This message was sent from the Server to the Client, ENCRYPTED!"));
            Console.WriteLine($"Server received notification: \"{notification.Message}\"");
        }
    }
}
