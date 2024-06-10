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
                    while (Context.Stream.ReadAndProcessFrames(Context, _frameBuffer,
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
                    Context.Endpoint.InvokeOnException(Context, Utility.GetBaseException(ex), null);
                }
            }

            Context.Endpoint.InvokeOnDisconnected(Context);
        }

        private void OnNotificationReceived(IRmNotification payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (Context.Endpoint.ReflectionCache.GetCachedMethod(payload.GetType(), out var cachedMethod))
                {
                    if (Context.Endpoint.ReflectionCache.GetCachedInstance(cachedMethod, out var cachedInstance))
                    {
                        switch (cachedMethod.MethodType)
                        {
                            case ReflectionCache.CachedMethodType.PayloadOnly:
                                cachedMethod.Method.Invoke(cachedInstance, new object[] { payload });
                                break;
                            case ReflectionCache.CachedMethodType.PayloadWithContext:
                                cachedMethod.Method.Invoke(cachedInstance, new object[] { Context, payload });
                                break;
                        }
                        return;
                    }
                }

                Context.Endpoint.InvokeOnNotificationReceived(Context, payload);
            }
            catch (Exception ex)
            {
                Context.Endpoint.InvokeOnException(Context, Utility.GetBaseException(ex), payload);
            }
        }

        private IRmQueryReply OnQueryReceived(IRmPayload payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnQueryReceived() event.
                if (Context.Endpoint.ReflectionCache.GetCachedMethod(payload.GetType(), out var cachedMethod))
                {
                    if (Context.Endpoint.ReflectionCache.GetCachedInstance(cachedMethod, out var cachedInstance))
                    {
                        IRmQueryReply? result = null;

                        switch (cachedMethod.MethodType)
                        {
                            case ReflectionCache.CachedMethodType.PayloadOnly:
                                result = cachedMethod.Method.Invoke(cachedInstance, new object[] { payload }) as IRmQueryReply;
                                break;
                            case ReflectionCache.CachedMethodType.PayloadWithContext:
                                result = cachedMethod.Method.Invoke(cachedInstance, new object[] { Context, payload }) as IRmQueryReply;
                                break;
                        }

                        return result ?? throw new Exception("The query must return a valid instance of IRmQueryReply.");
                    }
                }

                return Context.Endpoint.InvokeOnQueryReceived(Context, payload);
            }
            catch (Exception ex)
            {
                Context.Endpoint.InvokeOnException(Context, Utility.GetBaseException(ex), payload);
                return new FramePayloadQueryReplyException(Utility.GetBaseException(ex));
            }
        }

        public void Disconnect(bool waitOnThread)
        {
            if (_keepRunning)
            {
                _keepRunning = false;
                try { Context.Stream.Close(); } catch { }
                try { Context.Stream.Dispose(); } catch { }
                try { Context.TcpClient.Close(); } catch { }
                try { Context.TcpClient.Dispose(); } catch { }

                if (waitOnThread && Environment.CurrentManagedThreadId != Context.Thread.ManagedThreadId)
                {
                    Context.Thread.Join();
                }
            }
        }
    }
}
