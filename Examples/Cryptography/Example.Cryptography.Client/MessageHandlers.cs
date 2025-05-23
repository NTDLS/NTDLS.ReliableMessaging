using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.Cryptography.Client
{
    /// <summary>
    /// This is the client-side implementation of the message handlers for the cryptography example.
    /// 
    /// See: Example.Cryptography.Server.MessageHandlers for the server-side implementation.
    /// </summary>
    internal class MessageHandlers
        : IRmMessageHandler
    {
        /// <summary>
        /// Just a simple notification to show that the server can send encrypted notifications to the client.
        /// </summary>
        public void MyNotification(RmContext context, MyNotification notification)
        {
            if (context.GetCryptographyProvider() == null)
            {
                //Just to prove that the cryptography provider is set on the context.
                throw new Exception("Cryptography provider not set.");
            }

            Console.WriteLine($"Client received notification: \"{notification.Message}\"");
        }
    }
}
