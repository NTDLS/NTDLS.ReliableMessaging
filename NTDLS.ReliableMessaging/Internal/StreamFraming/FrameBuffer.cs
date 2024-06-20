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

        internal bool ReadStream(Stream stream)
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
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resizes the receive buffer by the given growth rate, up to the maxFrameBufferSize.
        /// </summary>
        private void AutoGrowReceiveBuffer()
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
    }
}
