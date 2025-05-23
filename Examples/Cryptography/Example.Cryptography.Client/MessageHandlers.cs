using Example.Shared;
using NTDLS.ReliableMessaging;

namespace Example.Cryptography.Client
{
    internal class MessageHandlers
        : IRmMessageHandler
    {
        /// <summary>
        /// Just a simple notification to show that the server can send notifications to the client.
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
