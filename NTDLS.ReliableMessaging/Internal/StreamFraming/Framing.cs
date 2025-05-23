using NTDLS.Helpers;
using System.Collections.Concurrent;
using System.Text;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.Defaults;
using static NTDLS.ReliableMessaging.RmContext;

namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    /// <summary>
    /// Stream packets (especially TCP/IP) can be fragmented or combined. Framing rebuilds what was
    /// originally written to the stream while also providing compression, CRC checking and optional encryption.
    /// </summary>
    internal static class Framing
    {
        private static readonly SemaphoreSlim _streamWriteLock = new(1, 1);

        /// <summary>
        /// The callback that is used to notify of the receipt of a notification frame.
        /// </summary>
        /// <param name="notification">The notification payload.</param>
        public delegate void ProcessFrameNotificationCallback(IRmNotification notification);

        /// <summary>
        /// The callback that is used to notify of the receipt of a query frame. A return of type IFrameQueryReply
        ///  is  expected and will be routed to the originator and the appropriate waiting asynchronous task.
        /// </summary>
        /// <param name="query">The query payload</param>
        /// <returns>The reply payload to return to the originator.</returns>
        public delegate IRmQueryReply ProcessFrameQueryCallback(IRmPayload query);

        private static readonly ConcurrentDictionary<string, Func<IRmSerializationProvider?, string, IRmPayload>> _deserializationCache = new();

        /// <summary>
        /// When a connection is dropped, we need to make sure that we cancel any waiting queries.
        /// </summary>
        internal static void TerminateWaitingQueries(RmContext context, Guid connectionId)
        {
            foreach (var kvp in context.QueriesAwaitingReplies.ToArray())
            {
                if (kvp.Value.ConnectionId == connectionId)
                {
                    kvp.Value.Exception = new Exception("The connection was terminated.");
                    kvp.Value.ReplyPayload = null;
                    kvp.Value.WaitEvent.Set();
                }
            }
        }

        #region Extension methods.

        /// <summary>
        /// Writes a byte array to the stream.
        /// This is a thread-safe method that uses a semaphore to ensure that only one thread can write to the stream at a time.
        /// </summary>
        public async static Task SafeWriteAsync(this Stream stream, RmContext context, byte[] buffer)
        {
            await _streamWriteLock.WaitAsync();
            try
            {
                if (context.TcpClient.Connected)
                {
                    if (!stream.CanWrite)
                    {
                        throw new Exception("Peer is connected but stream is unwritable.");
                    }
                    await stream.WriteAsync(buffer);
                }

            }
            finally
            {
                _streamWriteLock.Release();
            }
        }

        /// <summary>
        /// Writes a byte array to the stream.
        /// This is a thread-safe method that uses a semaphore to ensure that only one thread can write to the stream at a time.
        /// </summary>
        public static void SafeWrite(this Stream stream, RmContext context, byte[] buffer)
        {
            _streamWriteLock.Wait();
            try
            {
                if (context.TcpClient.Connected)
                {
                    if (!stream.CanWrite)
                    {
                        throw new Exception("Peer is connected but stream is unwritable.");
                    }
                    stream.Write(buffer);
                }
            }
            finally
            {
                _streamWriteLock.Release();
            }
        }

        /// <summary>
        /// Waits on bytes to become available on the stream, reads those bytes then parses the available frames (if any) and calls the appropriate callbacks.
        /// </summary>
        /// <param name="stream">The open stream that should be read from</param>
        /// <param name="frameBuffer">The frame buffer that will be used to receive bytes from the stream.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="onException">Delegate to call on exception.</param>
        /// <param name="processNotificationCallback">Callback to call when a notification frame is received.</param>
        /// <param name="processFrameQueryCallback">Callback to call when a query frame is received.</param>
        /// <returns>Returns true if the stream is healthy, returns false if disconnected.</returns>
        public static bool ReadAndProcessFrames(this Stream stream, RmContext context, RmEvents.ExceptionEvent? onException, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback processNotificationCallback, ProcessFrameQueryCallback processFrameQueryCallback)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            if (frameBuffer.ReadStream(stream))
            {
                while (frameBuffer.GetNextFrame(context, onException, out var frameBodyBytes))
                {
                    if (context.Messenger.Configuration.AsynchronousFrameProcessing)
                    {
                        Task.Run(() => stream.ProcessFrame(context, onException, frameBodyBytes, processNotificationCallback, processFrameQueryCallback));
                    }
                    else
                    {
                        stream.ProcessFrame(context, onException, frameBodyBytes, processNotificationCallback, processFrameQueryCallback);
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Writes a query to the stream, expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of the expected reply payload.</typeparam>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The query payload that will be written to the stream.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <param name="onQueryPrepared">Optional callback that is called after the frame has been built but before the query is dispatched. This is useful when establishing encrypted connections, where we need to tell a peer that encryption is being initialized but we need to tell the peer before setting the provider.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        public static async Task<T> WriteQueryFrameAsync<T>(this Stream stream, RmContext context,
            IRmQuery<T> framePayload, TimeSpan queryTimeout, OnQueryPrepared? onQueryPrepared) where T : IRmQueryReply
        {
            QueryAwaitingReply? queryAwaitingReply = null;

            try
            {
                if (stream == null)
                {
                    throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
                }

                var frameBody = new FrameBody(context.GetSerializationProvider(), framePayload, typeof(T));
                queryAwaitingReply = new QueryAwaitingReply(frameBody.Id, context.ConnectionId);

                context.QueriesAwaitingReplies.TryAdd(frameBody.Id, queryAwaitingReply);

                var frameBytes = AssembleFrame(context, frameBody);

                onQueryPrepared?.Invoke();
                await stream.SafeWriteAsync(context, frameBytes);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (!queryAwaitingReply.WaitEvent.WaitOne(queryTimeout))
                {
                    context.QueriesAwaitingReplies.TryRemove(frameBody.Id, out _);
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                context.QueriesAwaitingReplies.TryRemove(frameBody.Id, out _);

                if (queryAwaitingReply.Exception != null)
                {
                    throw queryAwaitingReply.Exception;
                }

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("Reply payload was empty.");
                }

                if (queryAwaitingReply.ReplyPayload is RmQueryReplyException ex)
                {
                    throw ex.GetException();
                }

                if (queryAwaitingReply.ReplyPayload is T t)
                {
                    return t;
                }

                throw new Exception($"Query expected a reply of type '{typeof(T).Name}', received '{queryAwaitingReply.ReplyPayload.GetType().Name}'.");
            }
            catch (Exception ex)
            {
                if (queryAwaitingReply != null)
                {
                    context.QueriesAwaitingReplies.TryRemove(queryAwaitingReply.FrameBodyId, out _);
                }
                return await Task.FromException<T>(ex);
            }
        }

        /// <summary>
        /// Writes a query to the stream, expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of the expected reply payload.</typeparam>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The query payload that will be written to the stream.</param>
        /// <param name="queryTimeout">The amount of time to wait on a reply to the query.</param>
        /// <param name="onQueryPrepared">Optional callback that is called after the frame has been built but before the query is dispatched. This is useful when establishing encrypted connections, where we need to tell a peer that encryption is being initialized but we need to tell the peer before setting the provider.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        public static Task<T> WriteQueryFrame<T>(this Stream stream, RmContext context,
            IRmQuery<T> framePayload, TimeSpan queryTimeout, OnQueryPrepared? onQueryPrepared) where T : IRmQueryReply
        {
            QueryAwaitingReply? queryAwaitingReply = null;

            try
            {
                if (stream == null)
                {
                    throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
                }

                var frameBody = new FrameBody(context.GetSerializationProvider(), framePayload, typeof(T));
                queryAwaitingReply = new QueryAwaitingReply(frameBody.Id, context.ConnectionId);

                context.QueriesAwaitingReplies.TryAdd(frameBody.Id, queryAwaitingReply);

                var frameBytes = AssembleFrame(context, frameBody);

                onQueryPrepared?.Invoke();
                stream.SafeWrite(context, frameBytes);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (queryAwaitingReply.WaitEvent.WaitOne(queryTimeout) == false)
                {
                    context.QueriesAwaitingReplies.TryRemove(frameBody.Id, out _);
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                context.QueriesAwaitingReplies.TryRemove(frameBody.Id, out _);

                if (queryAwaitingReply.Exception != null)
                {
                    throw queryAwaitingReply.Exception;
                }

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("Reply payload was empty.");
                }

                if (queryAwaitingReply.ReplyPayload is RmQueryReplyException ex)
                {
                    throw ex.GetException();
                }

                if (queryAwaitingReply.ReplyPayload is T t)
                {
                    return Task.FromResult(t);
                }

                throw new Exception($"Query expected a reply of type '{typeof(T).Name}', received '{queryAwaitingReply.ReplyPayload.GetType().Name}'.");
            }
            catch (Exception ex)
            {
                if (queryAwaitingReply != null)
                {
                    context.QueriesAwaitingReplies.TryRemove(queryAwaitingReply.FrameBodyId, out _);
                }

                return Task.FromException<T>(ex);
            }
        }

        /// <summary>
        /// Writes a reply to the stream, in reply to a stream query.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="queryFrameBody">The query frame that was received and that we are responding to.</param>
        /// <param name="framePayload">The query reply payload.</param>
        public static void WriteReplyFrame(this Stream stream, RmContext context, FrameBody queryFrameBody,
            IRmQueryReply framePayload)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");
            }

            var frameBody = new FrameBody(context.GetSerializationProvider(), framePayload)
            {
                Id = queryFrameBody.Id
            };

            var frameBytes = AssembleFrame(context, frameBody);
            stream.SafeWrite(context, frameBytes);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification to the stream.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be written to the stream.</param>
        public static void WriteNotificationFrame(this Stream stream, RmContext context, IRmNotification framePayload)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            var frameBody = new FrameBody(context.GetSerializationProvider(), framePayload);
            var frameBytes = AssembleFrame(context, frameBody);
            stream.SafeWrite(context, frameBytes);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification to the stream.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be written to the stream.</param>
        public async static Task WriteNotificationFrameAsync(this Stream stream, RmContext context, IRmNotification framePayload)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            var frameBody = new FrameBody(context.GetSerializationProvider(), framePayload);
            var frameBytes = AssembleFrame(context, frameBody);
            await stream.SafeWriteAsync(context, frameBytes);
        }

        #endregion

        private static byte[] AssembleFrame(RmContext context, FrameBody frameBody)
        {
            var frameBodyBytes = Serialization.SerializeToByteArray(frameBody);

            frameBodyBytes = context.GetCompressionProvider()?.Compress(context, frameBodyBytes) ?? frameBodyBytes;
            frameBodyBytes = context.GetCryptographyProvider()?.Encrypt(context, frameBodyBytes) ?? frameBodyBytes;

            var grossFrameSize = frameBodyBytes.Length + NtFrameDefaults.FRAME_HEADER_SIZE;
            var grossFrameBytes = new byte[grossFrameSize];
            var frameCrc = CRC16.ComputeChecksum(frameBodyBytes);

            Buffer.BlockCopy(BitConverter.GetBytes(NtFrameDefaults.FRAME_DELIMITER), 0, grossFrameBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, grossFrameBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(frameCrc), 0, grossFrameBytes, 8, 2);
            Buffer.BlockCopy(frameBodyBytes, 0, grossFrameBytes, NtFrameDefaults.FRAME_HEADER_SIZE, frameBodyBytes.Length);

            return grossFrameBytes;
        }

        private static void ProcessFrame(this Stream stream, RmContext context,
            RmEvents.ExceptionEvent? onException, byte[] frameBodyBytes,
            ProcessFrameNotificationCallback processNotificationCallback,
            ProcessFrameQueryCallback processFrameQueryCallback)
        {
            try
            {
                frameBodyBytes = context.GetCryptographyProvider()?.Decrypt(context, frameBodyBytes) ?? frameBodyBytes;
                frameBodyBytes = context.GetCompressionProvider()?.DeCompress(context, frameBodyBytes) ?? frameBodyBytes;

                var frameBody = Serialization.DeserializeToObject<FrameBody>(frameBodyBytes);

                var framePayload = ExtractFramePayload(context.GetSerializationProvider(), frameBody);

                if (framePayload is IRmQueryReply reply)
                {
                    // A reply to a query was received, we need to find the waiting query, set the reply payload data then trigger its wait event.
                    if (!context.QueriesAwaitingReplies.TryGetValue(frameBody.Id, out var waitingQuery))
                    {
                        throw new Exception($"No waiting query was found for the reply with id '{frameBody.Id}'. Possible query timeout.");
                    }

                    waitingQuery.ReplyPayload = reply;
                    waitingQuery.WaitEvent.Set();
                }
                else if (framePayload is IRmNotification notification)
                {
                    if (!context.Messenger.Configuration.AsynchronousFrameProcessing
                        && context.Messenger.Configuration.AsynchronousNotifications)
                    {
                        //Keep a reference to the frame payload that we are going to perform an async wait on.
                        var asynchronousNotificationReference = notification;
                        Task.Run(() => //We do not wait on this task, we just fire and forget it.
                        {
                            try
                            {
                                processNotificationCallback(asynchronousNotificationReference);
                            }
                            catch (Exception ex)
                            {
                                onException?.Invoke(context, ex.GetRoot() ?? ex, notification);
                            }
                        });
                    }
                    else
                    {
                        try
                        {
                            processNotificationCallback(notification);
                        }
                        catch (Exception ex)
                        {
                            onException?.Invoke(context, ex.GetRoot() ?? ex, notification);
                        }
                    }
                }
                else if (Reflection.ImplementsGenericInterfaceWithArgument(framePayload.GetType(), typeof(IRmQuery<>), typeof(IRmQueryReply)))
                {
                    if (!context.Messenger.Configuration.AsynchronousFrameProcessing
                        && context.Messenger.Configuration.AsynchronousQueryWaiting)
                    {
                        //Keep a reference to the frame payload that we are going to perform an async wait on.
                        var asynchronousFramePayloadReference = framePayload;
                        Task.Run(() => //We do not wait on this task, we just fire and forget it.
                        {
                            try
                            {
                                var replyPayload = processFrameQueryCallback(asynchronousFramePayloadReference);
                                stream.WriteReplyFrame(context, frameBody, replyPayload);
                            }
                            catch (Exception ex)
                            {
                                onException?.Invoke(context, ex.GetRoot() ?? ex, asynchronousFramePayloadReference);
                            }
                        });
                    }
                    else
                    {
                        try
                        {
                            var replyPayload = processFrameQueryCallback(framePayload);
                            stream.WriteReplyFrame(context, frameBody, replyPayload);
                        }
                        catch (Exception ex)
                        {
                            onException?.Invoke(context, ex.GetRoot() ?? ex, framePayload);
                        }
                    }
                }
                else
                {
                    throw new Exception($"Undefined frame payload type: '{framePayload.GetType()?.Name}'.");
                }
            }
            catch (Exception ex)
            {
                onException?.Invoke(context, ex.GetRoot() ?? ex, null);
            }
        }

        /// <summary>
        /// Uses the "EnclosedPayloadType" to determine the type of the payload and deserialize the json to that type.
        /// </summary>
        private static IRmPayload ExtractFramePayload(IRmSerializationProvider? serializationProvider, FrameBody frame)
        {
            if (!_deserializationCache.TryGetValue(frame.ObjectType, out var deserializeMethod))
            {
                var genericType = Type.GetType(frame.ObjectType)
                    ?? throw new Exception($"Unknown extraction payload type [{frame.ObjectType}].");

                var methodInfo = typeof(Serialization).GetMethod(nameof(Serialization.RmDeserializeFramePayloadToObject))
                    ?? throw new Exception("Could not resolve RmDeserializeFramePayloadToObject().");

                var genericMethod = methodInfo.MakeGenericMethod(genericType);

                // Create a delegate for the deserialization method.
                deserializeMethod = (Func<IRmSerializationProvider?, string, IRmPayload>)
                    Delegate.CreateDelegate(typeof(Func<IRmSerializationProvider?, string, IRmPayload>), genericMethod);

                _deserializationCache.TryAdd(frame.ObjectType, deserializeMethod);
            }

            var json = Encoding.UTF8.GetString(frame.Bytes);

            return deserializeMethod(serializationProvider, json)
                ?? throw new Exception("Extraction payload cannot be null.");
        }
    }
}
