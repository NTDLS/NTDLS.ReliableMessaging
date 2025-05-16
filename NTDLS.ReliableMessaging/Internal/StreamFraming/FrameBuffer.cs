using System.Diagnostics.CodeAnalysis;
using static NTDLS.ReliableMessaging.Internal.StreamFraming.Defaults;

namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    /// <summary>
    /// Auto-resizing frame buffer for stream receiving and stream frame reassembly.
    /// </summary>
    internal class FrameBuffer
    {
        /// <summary>
        /// The initial size of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int InitialReceiveBufferSize { get; private set; } = NtFrameDefaults.INITIAL_BUFFER_SIZE;

        /// <summary>
        ///The maximum size of the receive buffer. If the buffer ever gets full while receiving data it will be automatically resized up to MaxReceiveBufferSize.
        /// </summary>
        public int MaxReceiveBufferSize { get; set; } = NtFrameDefaults.MAX_BUFFER_SIZE;

        /// <summary>
        ///The growth rate of the auto-resizing for the receive buffer.
        /// </summary>
        public double ReceiveBufferGrowthRate { get; set; } = NtFrameDefaults.BUFFER_GROWTH_RATE;

        /// <summary>
        /// The number of bytes in the current receive buffer.
        /// </summary>
        public int ReceiveBufferUsed = 0;
        /// <summary>
        /// The current receive buffer. May be more than one frame or even a partial frame.
        /// </summary>
        public byte[] ReceiveBuffer;

        /// <summary>
        /// The buffer used to build a full message from the frame. This will be automatically resized if its too small.
        /// </summary>
        public byte[] FrameBuilder;

        /// <summary>
        /// The length of the data currently contained in the PayloadBuilder.
        /// </summary>
        public int FrameBuilderLength = 0;

        /// <summary>
        /// Instantiates a new frame buffer with a pre-defined size.
        /// </summary>
        /// <param name="initialReceiveBufferSize"></param>
        /// <param name="maxReceiveBufferSize"></param>
        /// <param name="receiveBufferGrowthRate"></param>
        public FrameBuffer(int initialReceiveBufferSize, int maxReceiveBufferSize, double receiveBufferGrowthRate = 0.2)
        {
            InitialReceiveBufferSize = initialReceiveBufferSize;
            MaxReceiveBufferSize = maxReceiveBufferSize;
            ReceiveBufferGrowthRate = receiveBufferGrowthRate;

            ReceiveBuffer = new byte[initialReceiveBufferSize];
            FrameBuilder = new byte[initialReceiveBufferSize];
        }

        /// <summary>
        /// Instantiates a new frame buffer with a default initial size of 16KB, a max size of 1MB and a growth rate of 0.1 (10%).
        /// </summary>
        public FrameBuffer()
        {
            ReceiveBuffer = new byte[InitialReceiveBufferSize];
            FrameBuilder = new byte[InitialReceiveBufferSize];
        }

        /// <summary>
        /// Reads the stream into the receive buffer. If the buffer is full, it will be resized to accommodate more data.
        /// </summary>
        /// <returns>Returns false if the stream is closed.</returns>
        internal bool ReadStream(Stream stream)
        {
            lock (this)
            {
                try
                {
                    ReceiveBufferUsed = stream.Read(ReceiveBuffer, 0, ReceiveBuffer.Length);
                    if (ReceiveBufferUsed == 0)
                    {
                        return false; //Graceful stream disconnect.
                    }
                    if (ReceiveBufferUsed == ReceiveBuffer.Length && ReceiveBufferUsed < MaxReceiveBufferSize)
                    {
                        AutoGrowReceiveBuffer();
                    }

                    if (FrameBuilderLength + ReceiveBufferUsed >= FrameBuilder.Length)
                    {
                        Array.Resize(ref FrameBuilder, FrameBuilderLength + ReceiveBufferUsed);
                    }

                    Buffer.BlockCopy(ReceiveBuffer, 0, FrameBuilder, FrameBuilderLength, ReceiveBufferUsed);

                    FrameBuilderLength += ReceiveBufferUsed;
                }
                catch (IOException)
                {
                    return false;
                }

                return true;
            }
        }

        public bool GetNextFrame(RmContext context, RmEvents.ExceptionEvent? onException, [NotNullWhen(true)] out byte[]? compressedFrameBodyBytes)
        {
            lock (this)
            {
                try
                {
                    if (FrameBuilderLength > NtFrameDefaults.FRAME_HEADER_SIZE) //[FrameSize] and [CRC16]
                    {
                        var frameDelimiterBytes = new byte[4];
                        var frameSizeBytes = new byte[4];
                        var expectedCRC16Bytes = new byte[2];

                        Buffer.BlockCopy(FrameBuilder, 0, frameDelimiterBytes, 0, frameDelimiterBytes.Length);
                        Buffer.BlockCopy(FrameBuilder, 4, frameSizeBytes, 0, frameSizeBytes.Length);
                        Buffer.BlockCopy(FrameBuilder, 8, expectedCRC16Bytes, 0, expectedCRC16Bytes.Length);

                        var frameDelimiter = BitConverter.ToInt32(frameDelimiterBytes, 0);
                        var grossFrameSize = BitConverter.ToInt32(frameSizeBytes, 0);
                        var expectedCRC16 = BitConverter.ToUInt16(expectedCRC16Bytes, 0);

                        if (frameDelimiter != NtFrameDefaults.FRAME_DELIMITER || grossFrameSize < 0)
                        {
                            throw new Exception("Frame was corrupted.");
                        }

                        if (FrameBuilderLength < grossFrameSize)
                        {
                            //We have data in the buffer, but it's not enough to make up
                            //  the entire message so we will break and wait on more data.
                            compressedFrameBodyBytes = null;
                            return false;
                        }

                        if (CRC16.ComputeChecksum(FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE) != expectedCRC16)
                        {
                            throw new Exception("Frame was corrupted (size discrepancy).");
                        }

                        var netFrameSize = grossFrameSize - NtFrameDefaults.FRAME_HEADER_SIZE;
                        compressedFrameBodyBytes = new byte[netFrameSize];

                        //Copy the frame body (one packet) to a new array:
                        Buffer.BlockCopy(FrameBuilder, NtFrameDefaults.FRAME_HEADER_SIZE, compressedFrameBodyBytes, 0, netFrameSize);

                        //Remove the frame from the buffer:
                        Buffer.BlockCopy(FrameBuilder, grossFrameSize, FrameBuilder, 0, FrameBuilderLength - grossFrameSize);
                        FrameBuilderLength -= grossFrameSize;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    onException?.Invoke(context, ex, null);
                }

                compressedFrameBodyBytes = null;
                return false;
            }
        }

        public void SkipFrame(RmContext context, RmEvents.ExceptionEvent? onException)
        {
            lock (this)
            {
                try
                {
                    var frameDelimiterBytes = new byte[4];

                    for (int offset = 1; offset < FrameBuilderLength - frameDelimiterBytes.Length; offset++)
                    {
                        Buffer.BlockCopy(FrameBuilder, offset, frameDelimiterBytes, 0, frameDelimiterBytes.Length);

                        var value = BitConverter.ToInt32(frameDelimiterBytes, 0);

                        if (value == NtFrameDefaults.FRAME_DELIMITER)
                        {
                            Buffer.BlockCopy(FrameBuilder, offset, FrameBuilder, 0, FrameBuilderLength - offset);
                            FrameBuilderLength -= offset;
                            return;
                        }
                    }
                    Array.Clear(FrameBuilder, 0, FrameBuilder.Length);
                    FrameBuilderLength = 0;
                }
                catch (Exception ex)
                {
                    onException?.Invoke(context, ex, null);
                }
            }
        }

        /// <summary>
        /// Resizes the receive buffer by the given growth rate, up to the maxFrameBufferSize.
        /// </summary>
        private void AutoGrowReceiveBuffer()
        {
            lock (this)
            {
                if (ReceiveBuffer.Length < MaxReceiveBufferSize)
                {
                    int newSize = (int)(ReceiveBuffer.Length + ReceiveBuffer.Length * ReceiveBufferGrowthRate);
                    if (newSize > MaxReceiveBufferSize)
                    {
                        newSize = MaxReceiveBufferSize;
                    }
                    Array.Resize(ref ReceiveBuffer, newSize);
                }
            }
        }
    }
}
