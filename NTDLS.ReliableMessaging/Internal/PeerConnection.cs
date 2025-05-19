using NTDLS.Helpers;
using NTDLS.ReliableMessaging.Internal.Payloads;
using NTDLS.ReliableMessaging.Internal.StreamFraming;
using System.Net.Sockets;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.Framing;

namespace NTDLS.ReliableMessaging.Internal
{
    internal class PeerConnection
    {
        private readonly FrameBuffer _frameBuffer;
        private bool _keepRunning;
        public RmContext Context { get; private set; }
        private readonly RmConfiguration _configuration;

        public PeerConnection(IRmEndpoint endpoint, TcpClient tcpClient, RmConfiguration configuration,
            IRmSerializationProvider? serializationProvider, IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            Context = new RmContext(endpoint, tcpClient,
                serializationProvider, compressionProvider, cryptographyProvider,
                new Thread(DataPumpThreadProc), tcpClient.GetStream());

            _configuration = configuration;
            _frameBuffer = new FrameBuffer(_configuration.InitialReceiveBufferSize, _configuration.MaxReceiveBufferSize, _configuration.ReceiveBufferGrowthRate);
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
            Context.Endpoint.InvokeOnConnected(Context);

            while (_keepRunning)
            {
                try
                {
                    while (Context.Stream.ReadAndProcessFrames(Context, Context.Endpoint.InvokeOnException, _frameBuffer,
                        (payload) => OnNotificationReceived(payload),
                        (payload) => OnQueryReceived(payload),
                        Context.GetSerializationProvider,/*This is a delegate function call so that we can get the provider at the latest possible moment.*/
                        Context.GetCompressionProvider,/*This is a delegate function call so that we can get the provider at the latest possible moment.*/
                        Context.GetCryptographyProvider/*This is a delegate function call so that we can get the provider at the latest possible moment.*/))
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
                    Context.Endpoint.InvokeOnException(Context, ex.GetRoot() ?? ex, null);
                }
            }

            Disconnect(false);
            Context.Endpoint.InvokeOnDisconnected(Context);
        }

        private void OnNotificationReceived(IRmNotification payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (Context.Endpoint.ReflectionCache.RouteToNotificationHander(Context, payload))
                {
                    return; //Notification was handled by handler routing.
                }

                //Try to handle the query with a bound notification hander.
                Context.Endpoint.InvokeOnNotificationReceived(Context, payload);
            }
            catch (Exception ex)
            {
                Context.Endpoint.InvokeOnException(Context, ex.GetRoot() ?? ex, payload);
            }
        }

        private IRmQueryReply OnQueryReceived(IRmPayload payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (Context.Endpoint.ReflectionCache.RouteToQueryHander(Context, payload, out var invocationResult))
                {
                    //Query was handled by handler routing.
                    return invocationResult ?? throw new Exception("Query must return a valid instance of IRmQueryReply.");
                }

                //Try to handle the query with a bound event hander.
                return Context.Endpoint.InvokeOnQueryReceived(Context, payload);
            }
            catch (Exception ex)
            {
                var rootException = ex.GetBaseException();

                Context.Endpoint.InvokeOnException(Context, rootException, payload);
                return new FramePayloadQueryReplyException(rootException);
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
