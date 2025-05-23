using Example.Cryptography.Server;
using Example.Shared.Sequencing;
using NTDLS.ReliableMessaging;
using System.Collections.Concurrent;

namespace Example.Cryptography.Server
{
    internal class MessageHandlers : IRmMessageHandler
    {
        /// <summary>
        /// Used to store the file transfer sessions.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, FileTransferSession> _sessions = new();

        /// <summary>
        /// Client is about to start sending a file.
        /// </summary>
        public BeginFileTransferQueryReply BeginFileTransferQuery(BeginFileTransferQuery notification)
        {
            Console.WriteLine($"Received file transfer request: [{notification.FileName}] ({notification.FileSize} bytes)");

            _sessions.TryAdd(notification.FileId, new FileTransferSession(notification.FileName, notification.FileSize));
            return new BeginFileTransferQueryReply();
        }

        /// <summary>
        /// Client is sending a chunk of the file.
        /// </summary>
        /// <param name="notification"></param>
        public void FileChunkNotification(FileChunkNotification notification)
        {
            if (_sessions.TryGetValue(notification.FileId, out var session))
            {
                //We use a RmSequenceBuffer because it ensures that packets that are
                //  received out of order are buffered and processed in the proper order.
                session.SequenceBuffer.Process(notification.Bytes, notification.Sequence, (data) =>
                {
                    //If this weren't a mock process, this is where we would write the data to a file.
                    session.BytesReceived += data.Length;
                });
            }
            else
            {
                Console.WriteLine($"No session found for file ID: {notification.FileId}");
            }
        }

        /// <summary>
        /// Client is done sending the file.
        /// </summary>
        public EndFileTransferQueryReply EndFileTransferQuery(EndFileTransferQuery notification)
        {
            if (_sessions.TryGetValue(notification.FileId, out var session))
            {
                //Wait for the last packet to be received.
                while (session.BytesReceived != session.FileSize)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine($"File [{session.FileName}] received successfully.");
            }
            else
            {
                Console.WriteLine($"No session found for file ID: {notification.FileId}");
            }

            return new EndFileTransferQueryReply();
        }
    }
}
