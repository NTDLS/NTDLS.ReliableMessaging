using NTDLS.Helpers;
using NTDLS.ReliableMessaging.Internal.Payloads;
using NTDLS.Semaphore;
using System.Reflection;
using System.Text;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.Defaults;

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

        /// <summary>
        /// Callback to get the callback that is called to allow for manipulation of bytes before/after they are sent/received.
        /// </summary>
        /// <returns></returns>
        public delegate IRmCryptographyProvider? GetEncryptionProviderCallback();

        /// <summary>
        /// Callback to get the callback that is called to allow for manipulation of bytes before/after they are sent/received.
        /// </summary>
        /// <returns></returns>
        public delegate IRmCompressionProvider? GetCompressionProviderCallback();

        /// <summary>
        /// Callback to get the callback that is called to allow for custom serialization.
        /// </summary>
        /// <returns></returns>
        public delegate IRmSerializationProvider? GetSerializationProviderCallback();

        private static readonly PessimisticCriticalResource<Dictionary<string, MethodInfo>> _reflectionCache = new();

        /// <summary>
        /// When a connection is dropped, we need to make sure that we cancel any waiting queries.
        /// </summary>
        internal static void TerminateWaitingQueries(RmContext context, Guid connectionId)
        {
            context.QueriesAwaitingReplies.Use(o =>
            {
                foreach (var waitingQuery in o.Where(o => o.ConnectionId == connectionId))
                {
                    waitingQuery.Exception = new Exception("The connection was terminated.");
                    waitingQuery.ReplyPayload = null;
                    waitingQuery.WaitEvent.Set();
                }
            });
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
        /// <param name="processNotificationCallback">Optional callback to call when a notification frame is received.</param>
        /// <param name="processFrameQueryCallback">Optional callback to call when a query frame is received.</param>
        /// <param name="getSerializationProviderCallback">An optional callback to get the callback that is called to allow for custom serialization.</param>
        /// <param name="getCompressionProviderCallback">An optional callback to get the callback that is called to allow for manipulation of bytes after they are received.</param>/// 
        /// <param name="getEncryptionProviderCallback">An optional callback to get the callback that is called to allow for manipulation of bytes after they are received.</param>
        /// <returns>Returns true if the stream is healthy, returns false if disconnected.</returns>
        /// <exception cref="Exception"></exception>
        public static bool ReadAndProcessFrames(this Stream stream, RmContext context, RmEvents.ExceptionEvent? onException, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback? processNotificationCallback, ProcessFrameQueryCallback? processFrameQueryCallback,
            GetSerializationProviderCallback? getSerializationProviderCallback, GetCompressionProviderCallback? getCompressionProviderCallback,
            GetEncryptionProviderCallback? getEncryptionProviderCallback)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            if (frameBuffer.ReadStream(stream))
            {
                IRmSerializationProvider? serializationProvider = null;
                IRmCompressionProvider? compressionProvider = null;
                IRmCryptographyProvider? cryptographyProvider = null;

                if (getSerializationProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an serialization provider after we called ReadAndProcessFrames().
                    serializationProvider = getSerializationProviderCallback();
                }

                if (getCompressionProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an compression provider after we called ReadAndProcessFrames().
                    compressionProvider = getCompressionProviderCallback();
                }

                if (getEncryptionProviderCallback != null)
                {
                    //We use a callback because frameBuffer.ReadStream() blocks and we may have assigned an encryption provider after we called ReadAndProcessFrames().
                    cryptographyProvider = getEncryptionProviderCallback();
                }

                while (frameBuffer.GetNextFrame(context, onException, out var compressedFrameBodyBytes))
                {
                    if (context.Messenger.Configuration.MultiThreadedFrameProcessing)
                    {
                        Task.Run(() => stream.ProcessFrame(context, onException, compressedFrameBodyBytes, processNotificationCallback,
                            processFrameQueryCallback, serializationProvider, compressionProvider, cryptographyProvider));
                    }
                    else
                    {
                        stream.ProcessFrame(context, onException, compressedFrameBodyBytes, processNotificationCallback,
                            processFrameQueryCallback, serializationProvider, compressionProvider, cryptographyProvider);
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
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<T> WriteQueryFrameAsync<T>(this Stream stream, RmContext context,
            IRmQuery<T> framePayload, TimeSpan queryTimeout, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider) where T : IRmQueryReply
        {
            QueryAwaitingReply? queryAwaitingReply = null;

            try
            {
                if (stream == null)
                {
                    throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
                }

                var frameBody = new FrameBody(serializationProvider, framePayload, typeof(T));
                queryAwaitingReply = new QueryAwaitingReply(frameBody.Id, context.ConnectionId);

                context.QueriesAwaitingReplies.Use(o => o.Add(queryAwaitingReply));

                var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
                await stream.SafeWriteAsync(context, frameBytes);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (!queryAwaitingReply.WaitEvent.WaitOne(queryTimeout))
                {
                    context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));

                if (queryAwaitingReply.Exception != null)
                {
                    throw queryAwaitingReply.Exception;
                }

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("Reply payload was empty.");
                }

                if (queryAwaitingReply.ReplyPayload is FramePayloadQueryReplyException ex)
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
                    context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
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
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        /// <exception cref="Exception"></exception>
        public static Task<T> WriteQueryFrame<T>(this Stream stream, RmContext context,
            IRmQuery<T> framePayload, TimeSpan queryTimeout, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider) where T : IRmQueryReply
        {
            QueryAwaitingReply? queryAwaitingReply = null;

            try
            {
                if (stream == null)
                {
                    throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
                }

                var frameBody = new FrameBody(serializationProvider, framePayload, typeof(T));
                queryAwaitingReply = new QueryAwaitingReply(frameBody.Id, context.ConnectionId);

                context.QueriesAwaitingReplies.Use(o => o.Add(queryAwaitingReply));

                var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
                stream.SafeWrite(context, frameBytes);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (queryAwaitingReply.WaitEvent.WaitOne(queryTimeout) == false)
                {
                    context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));

                if (queryAwaitingReply.Exception != null)
                {
                    throw queryAwaitingReply.Exception;
                }

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("Reply payload was empty.");
                }

                if (queryAwaitingReply.ReplyPayload is FramePayloadQueryReplyException ex)
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
                    context.QueriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
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
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteReplyFrame(this Stream stream, RmContext context, FrameBody queryFrameBody,
            IRmQueryReply framePayload, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null.");
            }

            var frameBody = new FrameBody(serializationProvider, framePayload)
            {
                Id = queryFrameBody.Id
            };

            var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
            stream.SafeWrite(context, frameBytes);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification to the stream.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be written to the stream.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteNotificationFrame(this Stream stream, RmContext context,
            IRmNotification framePayload, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            var frameBody = new FrameBody(serializationProvider, framePayload);
            var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
            stream.SafeWrite(context, frameBytes);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification to the stream.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The notification payload that will be written to the stream.</param>
        /// <param name="serializationProvider">An optional callback that is called to allow for custom serialization.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public async static void WriteNotificationFrameAsync(this Stream stream, RmContext context,
            IRmNotification framePayload, IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            var frameBody = new FrameBody(serializationProvider, framePayload);
            var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
            await stream.SafeWriteAsync(context, frameBytes);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array payload. These are and handled in processNotificationCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="context">Contains information about the endpoint and the connection.</param>
        /// <param name="framePayload">The bytes will make up the body of the frame which is written to the stream.</param>
        /// <param name="compressionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <param name="cryptographyProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteBytesFrame(this Stream stream, RmContext context, byte[] framePayload, IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream can not be null.");
            }

            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(context, frameBody, compressionProvider, cryptographyProvider);
            stream.SafeWrite(context, frameBytes);
        }

        #endregion

        private static byte[] AssembleFrame(RmContext context, FrameBody frameBody,
            IRmCompressionProvider? compressionProvider, IRmCryptographyProvider? cryptographyProvider)
        {
            var frameBodyBytes = Serialization.SerializeToByteArray(frameBody);

            var compressedFrameBodyBytes = compressionProvider?.Compress(context, frameBodyBytes) ?? Compression.Compress(frameBodyBytes);

            if (cryptographyProvider != null)
            {
                compressedFrameBodyBytes = cryptographyProvider.Encrypt(context, compressedFrameBodyBytes);
            }

            var grossFrameSize = compressedFrameBodyBytes.Length + NtFrameDefaults.FRAME_HEADER_SIZE;
            var grossFrameBytes = new byte[grossFrameSize];
            var frameCrc = CRC16.ComputeChecksum(compressedFrameBodyBytes);

            Buffer.BlockCopy(BitConverter.GetBytes(NtFrameDefaults.FRAME_DELIMITER), 0, grossFrameBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(grossFrameSize), 0, grossFrameBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(frameCrc), 0, grossFrameBytes, 8, 2);
            Buffer.BlockCopy(compressedFrameBodyBytes, 0, grossFrameBytes, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes.Length);

            return grossFrameBytes;
        }

        private static void ProcessFrame(this Stream stream, RmContext context,
            RmEvents.ExceptionEvent? onException, byte[] compressedFrameBodyBytes,
            ProcessFrameNotificationCallback? processNotificationCallback,
            ProcessFrameQueryCallback? processFrameQueryCallback,
            IRmSerializationProvider? serializationProvider,
            IRmCompressionProvider? compressionProvider,
            IRmCryptographyProvider? cryptographyProvider)
        {
            try
            {
                if (cryptographyProvider != null)
                {
                    compressedFrameBodyBytes = cryptographyProvider.Decrypt(context, compressedFrameBodyBytes);
                }

                var frameBodyBytes = compressionProvider?.DeCompress(context, compressedFrameBodyBytes)
                    ?? Compression.Decompress(compressedFrameBodyBytes);

                var frameBody = Serialization.DeserializeToObject<FrameBody>(frameBodyBytes);

                var framePayload = ExtractFramePayload(serializationProvider, frameBody);

                if (framePayload is FramePayloadBytes frameNotificationBytes)
                {
                    if (processNotificationCallback == null)
                    {
                        throw new Exception("Notification handler was not supplied.");
                    }
                    processNotificationCallback(frameNotificationBytes);
                }
                else if (framePayload is IRmQueryReply reply)
                {
                    // A reply to a query was received, we need to find the waiting query - set the reply payload data and trigger the wait event.
                    var waitingQuery = context.QueriesAwaitingReplies.Use((o) =>
                        o.SingleOrDefault(o => o.FrameBodyId == frameBody.Id)
                        ?? throw new Exception($"No waiting query was found for the reply with id '{frameBody.Id}'. Possible query timeout."));

                    waitingQuery.ReplyPayload = reply;
                    waitingQuery.WaitEvent.Set();
                }
                else if (framePayload is IRmNotification notification)
                {
                    if (processNotificationCallback == null)
                    {
                        throw new Exception("Notification handler was not supplied.");
                    }
                    processNotificationCallback(notification);
                }
                else if (Reflection.ImplementsGenericInterfaceWithArgument(framePayload.GetType(), typeof(IRmQuery<>), typeof(IRmQueryReply)))
                {
                    if (processFrameQueryCallback == null)
                    {
                        throw new Exception("Query handler was not supplied.");
                    }

                    var replyPayload = processFrameQueryCallback(framePayload);
                    stream.WriteReplyFrame(context, frameBody, replyPayload, serializationProvider, compressionProvider, cryptographyProvider);
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
        /// Uses the "EnclosedPayloadType" to determine the type of the payload and then uses reflection
        /// to deserialize the json to that type. Deserialization is skipped when the type is byte[].
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static IRmPayload ExtractFramePayload(IRmSerializationProvider? serializationProvider, FrameBody frame)
        {
            if (frame.ObjectType == "byte[]")
            {
                return new FramePayloadBytes(frame.Bytes);
            }

            string cacheKey = $"{frame.ObjectType}";

            var genericToObjectMethod = _reflectionCache.Use((o) =>
            {
                if (o.TryGetValue(cacheKey, out var method))
                {
                    return method;
                }
                return null;
            });

            string json = Encoding.UTF8.GetString(frame.Bytes);

            if (genericToObjectMethod != null)
            {
                //Call the generic deserialization:
                return (IRmPayload?)genericToObjectMethod.Invoke(null, new object?[] { serializationProvider, json })
                    ?? throw new Exception($"Extraction payload can not be null.");
            }

            var genericType = Type.GetType(frame.ObjectType)
                ?? throw new Exception($"Unknown extraction payload type [{frame.ObjectType}].");

            var toObjectMethod = typeof(Serialization).GetMethod("RmDeserializeFramePayloadToObject")
                    ?? throw new Exception($"Could not resolve RmDeserializeFramePayloadToObject().");

            genericToObjectMethod = toObjectMethod.MakeGenericMethod(genericType);

            _reflectionCache.Use((o) => o.TryAdd(cacheKey, genericToObjectMethod));

            //Call the generic deserialization:
            return (IRmPayload?)genericToObjectMethod.Invoke(null, new object?[] { serializationProvider, json })
                ?? throw new Exception($"Extraction payload can not be null.");
        }
    }
}
