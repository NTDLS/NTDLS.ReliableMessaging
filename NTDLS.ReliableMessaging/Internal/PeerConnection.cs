﻿using NTDLS.ReliableMessaging.Internal.Payloads;
using NTDLS.ReliableMessaging.Internal.StreamFraming;
using System.Net.Sockets;

namespace NTDLS.ReliableMessaging.Internal
{
    internal class PeerConnection
    {
        private readonly FrameBuffer _frameBuffer = new();
        private bool _keepRunning;
        private readonly RmContext _context;
        public PeerConnection(IRmEndpoint endpoint, TcpClient tcpClient)
        {
            _context = new RmContext(endpoint, tcpClient, new Thread(DataPumpThreadProc), tcpClient.GetStream());
            _keepRunning = true;
        }

        public Guid ConnectionId => _context.ConnectionId;

        public void SendNotification(IRmNotification notification)
            => _context.Stream.WriteNotificationFrame(notification);

        public Task<T> SendQueryAsync<T>(IRmQuery<T> query) where T : IRmQueryReply
            => _context.Stream.WriteQueryFrameAsync(query);

        public Task<T> SendQuery<T>(IRmQuery<T> query) where T : IRmQueryReply
            => _context.Stream.WriteQueryFrame(query);

        public Task<T> SendQueryAsync<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => _context.Stream.WriteQueryFrameAsync(query, queryTimeout);

        public Task<T> SendQuery<T>(IRmQuery<T> query, int queryTimeout) where T : IRmQueryReply
            => _context.Stream.WriteQueryFrame(query, queryTimeout);

        public void RunAsync() => _context.Thread.Start();

        public TcpClient GetClient() => _context.TcpClient;

        internal void DataPumpThreadProc()
        {
#if DEBUG
            Thread.CurrentThread.Name = $"ReliableMessaging:PeerConnection:{_context.ConnectionId}";
#endif
            _context.Endpoint.InvokeOnConnected(_context);

            while (_keepRunning)
            {
                try
                {
                    while (_context.Stream.ReadAndProcessFrames(_frameBuffer,
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
                    _context.Endpoint.InvokeOnException(_context, ex, null);
                }
            }

            _context.Endpoint.InvokeOnDisconnected(_context);
        }

        private void OnNotificationReceived(IRmNotification payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnNotificationReceived() event.
                if (_context.Endpoint.ReflectionCache.GetCachedMethod(payload.GetType(), out var cachedMethod))
                {
                    if (_context.Endpoint.ReflectionCache.GetCachedInstance(cachedMethod, out var cachedInstance))
                    {
                        switch (cachedMethod.MethodType)
                        {
                            case ReflectionCache.CachedMethodType.PayloadOnly:
                                cachedMethod.Method.Invoke(cachedInstance, new object[] { payload });
                                break;
                            case ReflectionCache.CachedMethodType.PayloadWithContext:
                                cachedMethod.Method.Invoke(cachedInstance, new object[] { _context, payload });
                                break;
                        }
                        return;
                    }
                }

                _context.Endpoint.InvokeOnNotificationReceived(_context, payload);
            }
            catch (Exception ex)
            {
                _context.Endpoint.InvokeOnException(_context, ex, payload);
            }
        }

        private IRmQueryReply OnQueryReceived(IRmPayload payload)
        {
            try
            {
                //First we try to invoke functions that match the signature, if that fails we will fall back to invoking the OnQueryReceived() event.
                if (_context.Endpoint.ReflectionCache.GetCachedMethod(payload.GetType(), out var cachedMethod))
                {
                    if (_context.Endpoint.ReflectionCache.GetCachedInstance(cachedMethod, out var cachedInstance))
                    {
                        IRmQueryReply? result = null;

                        switch (cachedMethod.MethodType)
                        {
                            case ReflectionCache.CachedMethodType.PayloadOnly:
                                result = cachedMethod.Method.Invoke(cachedInstance, new object[] { payload }) as IRmQueryReply;
                                break;
                            case ReflectionCache.CachedMethodType.PayloadWithContext:
                                result = cachedMethod.Method.Invoke(cachedInstance, new object[] { _context, payload }) as IRmQueryReply;
                                break;
                        }

                        return result ?? throw new Exception("The query must return a valid instance of IRmQueryReply.");
                    }
                }

                return _context.Endpoint.InvokeOnQueryReceived(_context, payload);
            }
            catch (Exception ex)
            {
                _context.Endpoint.InvokeOnException(_context, ex, payload);
                return new FramePayloadQueryReplyException(ex);
            }
        }

        public void Disconnect(bool waitOnThread)
        {
            if (_keepRunning)
            {
                _keepRunning = false;
                try { _context.Stream.Close(); } catch { }
                try { _context.Stream.Dispose(); } catch { }
                try { _context.TcpClient.Close(); } catch { }
                try { _context.TcpClient.Dispose(); } catch { }

                if (waitOnThread)
                {
                    _context.Thread.Join();
                }
            }
        }
    }
}
