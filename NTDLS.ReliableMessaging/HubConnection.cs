using NTDLS.StreamFraming.Payloads;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging
{
    internal class HubConnection
    {
        private readonly FrameBuffer _frameBuffer = new(4096);
        private readonly TcpClient _tcpclient; //The TCP/IP connection associated with this connection.
        private readonly Thread _dataPumpThread; //The thread that receives data for this connection.
        private readonly NetworkStream _stream; //The stream for the TCP/IP connection (used for reading and writing).
        private readonly IHub _hub;
        private bool _keepRunning;

        public Guid Id { get; private set; }

        public HubConnection(IHub hub, TcpClient tcpClient)
        {
            Id = Guid.NewGuid();
            _hub = hub;
            _tcpclient = tcpClient;
            _dataPumpThread = new Thread(DataPumpThreadProc);
            _keepRunning = true;
            _stream = tcpClient.GetStream();
        }

        public void SendNotification(IFramePayloadNotification notification)
            => _stream.SendNotificationFrame(notification);

        public Task<T> SendQuery<T>(IFramePayloadQuery query) where T : IFramePayloadQueryReply
            => _stream.SendQueryFrame<T>(query);

        public void RunAsync()
        {
            _dataPumpThread.Start();
        }

        internal void DataPumpThreadProc()
        {
            Thread.CurrentThread.Name = $"DataPumpThreadProc:{Thread.CurrentThread.ManagedThreadId}";

            try
            {
                while (_keepRunning && _stream.ReceiveAndProcessStreamFrames(_frameBuffer,
                    (payload) => _hub.InvokeOnNotificationReceived(Id, payload),
                    (payload) => _hub.InvokeOnQueryReceived(Id, payload)))
                {
                }
            }
            catch
            {
                //TODO: log this.
            }

            _hub.InvokeOnDisconnected(Id);
        }

        public void Disconnect(bool waitOnThread)
        {
            _keepRunning = false;
            try { _stream.Close(); } catch { }
            try { _tcpclient.Close(); } catch { }

            if (waitOnThread)
            {
                _dataPumpThread.Join();
            }
        }
    }
}
