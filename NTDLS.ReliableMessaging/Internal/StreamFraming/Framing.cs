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
        /// <summary>
        /// The callback that is used to notify of the receipt of a notification frame.
        /// </summary>
        /// <param name="payload">The notification payload.</param>
        public delegate void ProcessFrameNotificationCallback(IRmNotification payload);

        /// <summary>
        /// The callback that is used to notify of the receipt of a query frame. A return of type IFrameQueryReply
        ///  is  expected and will be routed to the originator and the appropriate waiting asynchronous task.
        /// </summary>
        /// <param name="payload">The query payload</param>
        /// <returns>The reply payload to return to the originator.</returns>
        public delegate IRmQueryReply ProcessFrameQueryCallback(IRmPayload payload);

        private static readonly PessimisticCriticalResource<Dictionary<string, MethodInfo>> _reflectionCache = new();
        private static readonly PessimisticCriticalResource<List<QueryAwaitingReply>> _queriesAwaitingReplies = new();


        #region Extension methods.

        /// <summary>
        /// Waits on bytes to become available on the stream, reads those bytes then parses the available frames (if any) and calls the appropriate callbacks.
        /// </summary>
        /// <param name="stream">The open stream that should be read from</param>
        /// <param name="frameBuffer">The frame buffer that will be used to receive bytes from the stream.</param>
        /// <param name="processNotificationCallback">Optional callback to call when a notification frame is received.</param>
        /// <param name="processFrameQueryCallback">Optional callback to call when a query frame is received.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes after they are received.</param>
        /// <returns>Returns true if the stream is healthy, returns false if disconnected.</returns>
        /// <exception cref="Exception"></exception>
        public static bool ReadAndProcessFrames(this Stream stream, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback? processNotificationCallback = null, ProcessFrameQueryCallback? processFrameQueryCallback = null,
            IRmEncryptionProvider? encryptionProvider = null)
        {
            if (stream == null)
            {
                throw new Exception("ReceiveAndProcessStreamFrames: stream can not be null.");
            }

            if (frameBuffer.ReadStream(stream))
            {
                stream.ProcessFrameBuffer(frameBuffer, processNotificationCallback, processFrameQueryCallback, encryptionProvider);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Writes a query to the stream, expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of the expected reply payload.</typeparam>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="framePayload">The query payload that will be written to the stream.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<T> WriteQueryFrameAsync<T>(this Stream stream,
            IRmQuery<T> framePayload, int queryTimeout = -1, IRmEncryptionProvider? encryptionProvider = null) where T : IRmQueryReply
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var FrameBody = new FrameBody(framePayload, typeof(T));

            var queryAwaitingReply = new QueryAwaitingReply(FrameBody.Id);

            _queriesAwaitingReplies.Use(o => o.Add(queryAwaitingReply));

            return await Task.Run(() =>
            {
                var frameBytes = AssembleFrame(FrameBody, encryptionProvider);
                stream.Write(frameBytes, 0, frameBytes.Length);

                //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
                //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
                if (queryAwaitingReply.WaitEvent.WaitOne(queryTimeout) == false)
                {
                    _queriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
                    throw new Exception("Query timeout expired while waiting on reply.");
                }

                _queriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));

                if (queryAwaitingReply.ReplyPayload == null)
                {
                    throw new Exception("The reply payload can not be null.");
                }

                if (queryAwaitingReply.ReplyPayload is FramePayloadQueryReplyException ex)
                {
                    throw ex.Exception;
                }

                if (queryAwaitingReply.ReplyPayload is T)
                {
                    return (T)queryAwaitingReply.ReplyPayload;
                }

                throw new Exception($"The query expected a reply of type '{typeof(T).Name}'.");
            });
        }

        /// <summary>
        /// Writes a query to the stream, expects a reply.
        /// </summary>
        /// <typeparam name="T">The type of the expected reply payload.</typeparam>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="framePayload">The query payload that will be written to the stream.</param>
        /// <param name="queryTimeout">The number of milliseconds to wait on a reply to the query.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <returns>Returns the reply payload that is written to the stream from the recipient of the query.</returns>
        /// <exception cref="Exception"></exception>
        public static Task<T> WriteQueryFrame<T>(this Stream stream,
            IRmQuery<T> framePayload, int queryTimeout = -1, IRmEncryptionProvider? encryptionProvider = null) where T : IRmQueryReply
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var FrameBody = new FrameBody(framePayload, typeof(T));

            var queryAwaitingReply = new QueryAwaitingReply(FrameBody.Id);

            _queriesAwaitingReplies.Use(o => o.Add(queryAwaitingReply));

            var frameBytes = AssembleFrame(FrameBody, encryptionProvider);
            stream.Write(frameBytes, 0, frameBytes.Length);

            //Wait for a reply. When a reply is received, it will be routed to the correct query via ApplyQueryReply().
            //ApplyQueryReply() will apply the payload data to queryAwaitingReply and trigger the wait event.
            if (queryAwaitingReply.WaitEvent.WaitOne(queryTimeout) == false)
            {
                _queriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));
                throw new Exception("Query timeout expired while waiting on reply.");
            }

            _queriesAwaitingReplies.Use(o => o.Remove(queryAwaitingReply));

            if (queryAwaitingReply.ReplyPayload == null)
            {
                throw new Exception("The reply payload can not be null.");
            }

            if (queryAwaitingReply.ReplyPayload is FramePayloadQueryReplyException ex)
            {
                throw ex.Exception;
            }

            if (queryAwaitingReply.ReplyPayload is T)
            {
                return Task.FromResult((T)queryAwaitingReply.ReplyPayload);
            }

            throw new Exception($"The query expected a reply of type '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Writes a reply to the stream, in reply to a stream query.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="queryFrameBody">The query frame that was received and that we are responding to.</param>
        /// <param name="framePayload">The query reply payload.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteReplyFrame(this Stream stream, FrameBody queryFrameBody,
            IRmQueryReply framePayload, IRmEncryptionProvider? encryptionProvider = null)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var frameBody = new FrameBody(framePayload)
            {
                Id = queryFrameBody.Id
            };

            var frameBytes = AssembleFrame(frameBody, encryptionProvider);
            stream.Write(frameBytes, 0, frameBytes.Length);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget notification to the stream.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="framePayload">The notification payload that will be written to the stream.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteNotificationFrame(this Stream stream,
            IRmNotification framePayload, IRmEncryptionProvider? encryptionProvider = null)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var frameBody = new FrameBody(framePayload);

            var frameBytes = AssembleFrame(frameBody, encryptionProvider);
            stream.Write(frameBytes, 0, frameBytes.Length);
        }

        /// <summary>
        /// Sends a one-time fire-and-forget byte array payload. These are and handled in processNotificationCallback().
        /// When a raw byte array is use, all json serialization is skipped and checks for this payload type are prioritized for performance.
        /// </summary>
        /// <param name="stream">The open stream that will be written to.</param>
        /// <param name="framePayload">The bytes will make up the body of the frame which is written to the stream.</param>
        /// <param name="encryptionProvider">An optional callback that is called to allow for manipulation of bytes before they are framed.</param>
        /// <exception cref="Exception"></exception>
        public static void WriteBytesFrame(this Stream stream, byte[] framePayload, IRmEncryptionProvider? encryptionProvider = null)
        {
            if (stream == null)
            {
                throw new Exception("SendStreamFramePayload stream can not be null.");
            }

            var frameBody = new FrameBody(framePayload);
            var frameBytes = AssembleFrame(frameBody, encryptionProvider);
            stream.Write(frameBytes, 0, frameBytes.Length);
        }

        #endregion

        private static byte[] AssembleFrame(FrameBody frameBody, IRmEncryptionProvider? encryptionProvider)
        {
            var FrameBodyBytes = Utility.SerializeToByteArray(frameBody);
            var compressedFrameBodyBytes = Utility.Compress(FrameBodyBytes);

            if (encryptionProvider != null)
            {
                compressedFrameBodyBytes = encryptionProvider.Encrypt(compressedFrameBodyBytes);
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

        private static void SkipFrame(ref FrameBuffer frameBuffer)
        {
            var frameDelimiterBytes = new byte[4];

            for (int offset = 1; offset < frameBuffer.FrameBuilderLength - frameDelimiterBytes.Length; offset++)
            {
                Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameDelimiterBytes, 0, frameDelimiterBytes.Length);

                var value = BitConverter.ToInt32(frameDelimiterBytes, 0);

                if (value == NtFrameDefaults.FRAME_DELIMITER)
                {
                    Buffer.BlockCopy(frameBuffer.FrameBuilder, offset, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - offset);
                    frameBuffer.FrameBuilderLength -= offset;
                    return;
                }
            }
            Array.Clear(frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilder.Length);
            frameBuffer.FrameBuilderLength = 0;
        }

        private static void ProcessFrameBuffer(this Stream stream, FrameBuffer frameBuffer,
            ProcessFrameNotificationCallback? processNotificationCallback,
            ProcessFrameQueryCallback? processFrameQueryCallback,
            IRmEncryptionProvider? encryptionProvider = null)
        {
            if (frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed >= frameBuffer.FrameBuilder.Length)
            {
                Array.Resize(ref frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength + frameBuffer.ReceiveBufferUsed);
            }

            Buffer.BlockCopy(frameBuffer.ReceiveBuffer, 0, frameBuffer.FrameBuilder, frameBuffer.FrameBuilderLength, frameBuffer.ReceiveBufferUsed);

            frameBuffer.FrameBuilderLength += frameBuffer.ReceiveBufferUsed;

            while (frameBuffer.FrameBuilderLength > NtFrameDefaults.FRAME_HEADER_SIZE) //[FrameSize] and [CRC16]
            {
                var frameDelimiterBytes = new byte[4];
                var frameSizeBytes = new byte[4];
                var expectedCRC16Bytes = new byte[2];

                Buffer.BlockCopy(frameBuffer.FrameBuilder, 0, frameDelimiterBytes, 0, frameDelimiterBytes.Length);
                Buffer.BlockCopy(frameBuffer.FrameBuilder, 4, frameSizeBytes, 0, frameSizeBytes.Length);
                Buffer.BlockCopy(frameBuffer.FrameBuilder, 8, expectedCRC16Bytes, 0, expectedCRC16Bytes.Length);

                var frameDelimiter = BitConverter.ToInt32(frameDelimiterBytes, 0);
                var grossFrameSize = BitConverter.ToInt32(frameSizeBytes, 0);
                var expectedCRC16 = BitConverter.ToUInt16(expectedCRC16Bytes, 0);

                if (frameDelimiter != NtFrameDefaults.FRAME_DELIMITER || grossFrameSize < 0)
                {
                    //Possible corrupt frame.
                    SkipFrame(ref frameBuffer);
                    continue;
                }

                if (frameBuffer.FrameBuilderLength < grossFrameSize)
                {
                    //We have data in the buffer, but it's not enough to make up
                    //  the entire message so we will break and wait on more data.
                    break;
                }

                if (CRC16.ComputeChecksum(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE) != expectedCRC16)
                {
                    //Corrupt frame.
                    SkipFrame(ref frameBuffer);
                    continue;
                }

                var netFrameSize = grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE;
                var compressedFrameBodyBytes = new byte[netFrameSize];
                Buffer.BlockCopy(frameBuffer.FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes, 0, netFrameSize);

                if (encryptionProvider != null)
                {
                    compressedFrameBodyBytes = encryptionProvider.Decrypt(compressedFrameBodyBytes);
                }

                var FrameBodyBytes = Utility.Decompress(compressedFrameBodyBytes);
                var frameBody = Utility.DeserializeToObject<FrameBody>(FrameBodyBytes);

                //Zero out the consumed portion of the frame buffer - more for fun than anything else.
                Array.Clear(frameBuffer.FrameBuilder, 0, grossFrameSize);

                Buffer.BlockCopy(frameBuffer.FrameBuilder, grossFrameSize, frameBuffer.FrameBuilder, 0, frameBuffer.FrameBuilderLength - grossFrameSize);
                frameBuffer.FrameBuilderLength -= grossFrameSize;

                var framePayload = ExtractFramePayload(frameBody);

                if (framePayload is FramePayloadBytes frameNotificationBytes)
                {
                    if (processNotificationCallback == null)
                    {
                        throw new Exception("ProcessFrameBuffer: A notification handler was not supplied.");
                    }
                    processNotificationCallback(frameNotificationBytes);
                }
                else if (framePayload is IRmQueryReply reply)
                {
                    // A reply to a query was received, we need to find the waiting query - set the reply payload data and trigger the wait event.
                    var waitingQuery = _queriesAwaitingReplies.Use(o => o.Where(o => o.FrameBodyId == frameBody.Id).Single());
                    waitingQuery.ReplyPayload = reply;
                    waitingQuery.WaitEvent.Set();
                }
                else if (framePayload is IRmNotification notification)
                {
                    if (processNotificationCallback == null)
                    {
                        throw new Exception("ProcessFrameBuffer: A notification handler was not supplied.");
                    }
                    processNotificationCallback(notification);
                }
                else if (Utility.ImplementsGenericInterfaceWithArgument(framePayload.GetType(), typeof(IRmQuery<>), typeof(IRmQueryReply)))
                {
                    if (processFrameQueryCallback == null)
                    {
                        throw new Exception("ProcessFrameBuffer: A query handler was not supplied.");
                    }
                    var replyPayload = processFrameQueryCallback(framePayload);
                    stream.WriteReplyFrame(frameBody, replyPayload);
                }
                else
                {
                    throw new Exception("ProcessFrameBuffer: Encountered undefined frame payload type.");
                }
            }
        }

        /// <summary>
        /// Uses the "EnclosedPayloadType" to determine the type of the payload and then uses reflection
        /// to deserialize the json to that type. Deserialization is skipped when the type is byte[].
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static IRmPayload ExtractFramePayload(FrameBody frame)
        {
            if (frame.ObjectType == "byte[]")
            {
                return new FramePayloadBytes(frame.Bytes);
            }

            var genericToObjectMethod = _reflectionCache.Use((o) =>
            {
                if (o.TryGetValue(frame.ObjectType, out var method))
                {
                    return method;
                }
                return null;
            });

            string json = Encoding.UTF8.GetString(frame.Bytes);

            if (genericToObjectMethod != null)
            {
                return (IRmPayload?)genericToObjectMethod.Invoke(null, new object[] { json })
                    ?? throw new Exception($"ExtractFramePayload: Payload can not be null.");
            }

            var genericType = Type.GetType(frame.ObjectType)
                ?? throw new Exception($"ExtractFramePayload: Unknown payload type {frame.ObjectType}.");

            var toObjectMethod = typeof(Utility).GetMethod("JsonDeserializeToObject")
                ?? throw new Exception($"ExtractFramePayload: Could not find JsonDeserializeToObject().");

            genericToObjectMethod = toObjectMethod.MakeGenericMethod(genericType);

            _reflectionCache.Use((o) => o.TryAdd(frame.ObjectType, genericToObjectMethod));

            return (IRmPayload?)genericToObjectMethod.Invoke(null, new object[] { json })
                ?? throw new Exception($"ExtractFramePayload: Payload can not be null.");
        }
    }
}
