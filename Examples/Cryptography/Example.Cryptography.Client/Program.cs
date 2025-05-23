using Example.Shared;
using Example.Shared.Cryptography;
using NTDLS.ReliableMessaging;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

namespace Example.Cryptography.Client
{
    /// <summary>
    /// In this example, we are using the BouncyCastle library to perform a Diffie-Hellman key exchange with the server
    /// and then use it to instantiate an AES cryptography provider and use it to exchange encrypted messages with the server.
    /// </summary>
    internal class Program
    {
        static readonly Random _random = new();

        static void Main()
        {
            var client = new RmClient();

            Console.WriteLine("Connecting...");
            client.Connect("localhost", ExampleConstants.PortNumber);
            client.AddHandler(new MessageHandlers());

            // Handle the OnException event, otherwise the server will ignore any exceptions.
            client.OnException += (context, ex, payload) =>
            {
                Console.WriteLine($"Client exception: {ex.Message}");
            };

            //First we need to create a Diffie-Hellman key pair.
            #region BouncyCastle Diffie-Hellman key exchange (outside the scope of this example).

            var dhParams = DHStandardGroups.rfc3526_2048;
            var keyGen = GeneratorUtilities.GetKeyPairGenerator("DH");
            keyGen.Init(new DHKeyGenerationParameters(new SecureRandom(), dhParams));
            var keyPair = keyGen.GenerateKeyPair();

            //Create a new public key to send to the server.
            var publicKey = new DHPublicKeyParameters(((DHPublicKeyParameters)keyPair.Public).Y, dhParams);
            var publicKeyBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey).GetDerEncoded();

            #endregion

            //Send our public key to the server and wait for the reply.
            var aesCryptographyProvider = client.Query(new PublicKeyExchangeQuery(publicKeyBytes)).ContinueWith((reply) =>
            {
                //Use the reply containing the public key from the server to create a shared secret.
                #region BouncyCastle Diffie-Hellman key exchange (outside the scope of this example).

                var receivedKeyRaw = (DHPublicKeyParameters)PublicKeyFactory.CreateKey(reply.Result.PublicKeyBytes);
                var dhParams = DHStandardGroups.rfc3526_2048;
                var receivedKey = new DHPublicKeyParameters(receivedKeyRaw.Y, dhParams);

                var agreement = new DHBasicAgreement();
                agreement.Init(keyPair.Private);
                var sharedSecret = agreement.CalculateAgreement(receivedKey);
                var secretBytes = sharedSecret.ToByteArrayUnsigned();

                #endregion

                //We now have the shared secret, so we can create the AES key.
                var aesKey = SHA256.HashData(secretBytes);

                return new RmAesCryptographyProvider(aesKey);
            }).Result;

            //Now that we have sent out public key and received the server's public key, we are almost ready to apply the cryptography provider.
            //  The problem is that we need to let the server know we are ready, and we really shouldn't do that with a fire-and-forget notification
            //      because we'd end up sleeping the thread while we give the network and server sufficient time to process the notification.
            //
            // So lets use a query? Well, we can send the query, but then the server would reply with an encrypted reply.
            // Well, that's what the OnQueryPrepared overload for the Query() function is for!
            // It allows us to set the cryptography provider on the connection just before the query is sent. See below:

            client.Query(new ApplyCryptographyQuery(), () =>
            {
                //This is a special delegate on the Query function (named OnQueryPrepared) that is called once the query 
                //  frame is built, but before it is dispatched. This allows us to set the cryptography provider on the 
                //  connection just as the query is being sent so that the client can decrypt the query reply from the server.

                client.SetCryptographyProvider(aesCryptographyProvider);
            });

            client.Notify(new MyNotification("This message was sent from the Client to the Server, ENCRYPTED!"));

            Console.WriteLine("Complete! Waiting on server to disconnect");
            while (client.IsConnected)
            {
                Thread.Sleep(100); //Keep the client alive until the server disconnects.
            }

            client.Disconnect();
        }
    }
}
