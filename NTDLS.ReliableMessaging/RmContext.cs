using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Contains information about the endpoint and the connection.
    /// </summary>
    public class RmContext
    {
        /// <summary>
        /// This is the RPC server or client instance.
        /// </summary>
        public IRmEndpoint Endpoint { get; set; }

        /// <summary>
        /// The ID of the connection.
        /// </summary>
        public Guid ConnectionId { get; set; }

        /// <summary>
        /// The TCP/IP connection associated with this connection.
        /// </summary>
        public TcpClient TcpClient { get; set; }

        /// <summary>
        /// //The thread that receives data for this connection.
        /// </summary>
        public Thread Thread { get; set; }

        /// <summary>
        /// //The stream for the TCP/IP connection (used for reading and writing).
        /// </summary>
        public NetworkStream Stream { get; set; }

        /// <summary>
        /// Disconnects the connection to this endpoint.
        /// </summary>
        public void Disconnect()
        {
            if (Endpoint is RmClient client)
            {
                client.Disconnect();
            }
            else if (Endpoint is RmServer server)
            {
                server.Disconnect(ConnectionId);
            }
        }

        /// <summary>
        /// Creates a new ReliableMessagingContext instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="tcpClient"></param>
        /// <param name="thread"></param>
        /// <param name="stream"></param>
        public RmContext(IRmEndpoint endpoint, TcpClient tcpClient, Thread thread, NetworkStream stream)
        {
            Endpoint = endpoint;
            ConnectionId = Guid.NewGuid();
            TcpClient = tcpClient;
            Thread = thread;
            Stream = stream;
        }
    }
}
