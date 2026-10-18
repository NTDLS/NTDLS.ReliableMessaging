using System.Net;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Creates the server's listener.
    /// </summary>
    internal static class RmNetwork
    {
        /// <summary>
        /// Creates a listener. When <paramref name="listenOnIPv6"/> is true (and the operating system has IPv6) it is a single dual-stack
        /// listener that accepts IPv6 and IPv4 clients. Otherwise it is IPv4 only, which is what the server has always been.
        /// </summary>
        public static TcpListener CreateListener(int port, bool listenOnIPv6)
        {
            if (listenOnIPv6 && Socket.OSSupportsIPv6)
            {
                var listener = new TcpListener(IPAddress.IPv6Any, port);
                listener.Server.DualMode = true;
                return listener;
            }

            return new TcpListener(IPAddress.Any, port);
        }

        /// <summary>
        /// Starts a listener. If IPv6 is installed but cannot be used (for example it is disabled on the network adapters) the
        /// listener is replaced with an IPv4 one. Returns the listener that is running.
        /// </summary>
        public static TcpListener StartListener(TcpListener listener, int port)
        {
            try
            {
                listener.Start();
                return listener;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported
                && listener.Server.AddressFamily == AddressFamily.InterNetworkV6)
            {
                try { listener.Stop(); } catch { }

                var ipv4 = new TcpListener(IPAddress.Any, port);
                ipv4.Start();
                return ipv4;
            }
        }
    }
}
