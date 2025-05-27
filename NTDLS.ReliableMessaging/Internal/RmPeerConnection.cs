using NTDLS.Helpers;
using NTDLS.ReliableMessaging.Internal.StreamFraming;
using System.Net.Sockets;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.RmFraming;

namespace NTDLS.ReliableMessaging.Internal
{
    internal class RmPeerConnection
    {
        private readonly RmFrameBuffer _frameBuffer;
        private bool _keepRunning;
        public RmContext Context { get; private set; }
        private readonly RmConfiguration _configuration;

        public RmPeerConnection(IRmMessenger messenger, TcpClient tcpClient, RmConfiguration configuration,
            IRmSerializationProvider? serializationProvider, IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            Context = new RmContext(messenger, tcpClient,
                serializationProvider, compressionProvider, cryptographyProvider,
                new Thread(DataPumpThreadProc), tcpClient.GetStream());

            _configuration = configuration;
            _frameBuffer = new RmFrameBuffer(_configuration.InitialReceiveBufferSize, _configuration.MaxReceiveBufferSize, _configuration.ReceiveBufferGrowthRate);
            _keepRunning = true;
        }

        public Guid ConnectionId
            => Context.ConnectionId;

        public void RunAsync()
            => Context.Thread.Start();

        public TcpClient GetClient()
            => Context.TcpClient;

        internal void DataPumpThreadProc()
        {
#if DEBUG
            Thread.CurrentThread.Name = $"ReliableMessaging:PeerConnection:{Context.ConnectionId}";
#endif
            Context.Messenger.InvokeOnConnected(Context);

            while (_keepRunning)
            {
                try
                {
                    while (Context.Stream.ReadAndProcessFrames(Context, Context.Messenger.InvokeOnException, _frameBuffer,
                        (payload) => OnNotificationReceived(payload),
                        (payload) => OnQueryReceived(payload)))
                    {
                        //the famous do nothing loop!
                    }
                    Disconnect(false);
                }
                catch (IOException)
                {
                    //Closing the connection.
                }
                catch (Exception ex)
                {
                    Context.Messenger.InvokeOnException(Context, ex.GetRoot() ?? ex, null);
                }
            }

            Disconnect(false);
            Context.Messenger.InvokeOnDisconnected(Context);
        }

        private void OnNotificationReceived(IRmNotification payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (Context.Messenger.ReflectionCache.RouteToNotificationHander(Context, payload))
                {
                    return; //Notification was handled by handler routing.
                }

                //Try to handle the query with a bound notification hander.
                Context.Messenger.InvokeOnNotificationReceived(Context, payload);
            }
            catch (Exception ex)
            {
                Context.Messenger.InvokeOnException(Context, ex.GetRoot() ?? ex, payload);
            }
        }

        private IRmQueryReply OnQueryReceived(IRmPayload payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (Context.Messenger.ReflectionCache.RouteToQueryHander(Context, payload, out var invocationResult))
                {
                    //Query was handled by handler routing.
                    return invocationResult ?? throw new Exception("Query must return a valid instance of IRmQueryReply.");
                }

                //Try to handle the query with a bound event hander.
                return Context.Messenger.InvokeOnQueryReceived(Context, payload);
            }
            catch (Exception ex)
            {
                var rootException = ex.GetBaseException();

                Context.Messenger.InvokeOnException(Context, rootException, payload);
                return new RmQueryReplyException(rootException);
            }
        }

        public void Disconnect(bool waitOnThread)
        {
            if (_keepRunning)
            {
                _keepRunning = false;

                Exceptions.Ignore(Context.Stream.Close);
                Exceptions.Ignore(Context.Stream.Dispose);
                Exceptions.Ignore(Context.TcpClient.Close);
                Exceptions.Ignore(Context.TcpClient.Dispose);

                if (waitOnThread && Environment.CurrentManagedThreadId != Context.Thread.ManagedThreadId)
                {
                    Context.Thread.Join();
                }
            }
        }
    }
}
