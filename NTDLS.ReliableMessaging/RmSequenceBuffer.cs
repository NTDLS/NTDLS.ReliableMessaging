namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Provides a buffer for out-of-order packets.
    /// This is used to ensure that packets are processed in the correct order.
    /// </summary>
    /// <typeparam name="T">Type of data in the packet</typeparam>
    public class RmSequenceBuffer<T>
    {
        private readonly Dictionary<long, T> _buffer = new();

        private long _lastConsumedSequence = -1;

        /// <summary>
        /// Represents a method that handles a buffer event by processing a sequence number and associated data.
        /// </summary>
        /// <param name="sequence">The sequence number of the buffer event.</param>
        /// <param name="data">The data associated with the buffer event.</param>
        public delegate void BufferSequenceHandler(long sequence, T data);

        /// <summary>
        /// Represents a method that handles a buffer event by processing a sequence number and associated data.
        /// </summary>
        /// <param name="data">The data associated with the buffer event.</param>
        public delegate void BufferHandler(T data);

        /// <summary>
        /// Clears all items from the buffer and resets the last consumed sequence.
        /// </summary>
        /// <remarks>This method removes all elements from the buffer and sets the last consumed sequence
        /// to -1. It is thread-safe and ensures that the operation is performed atomically.</remarks>
        public void Clear()
        {
            lock (_buffer)
            {
                _buffer.Clear();
                _lastConsumedSequence = -1;
            }
        }

        /// <summary>
        /// Processes a data packet by ensuring it is handled in the correct sequence order.
        /// </summary>
        /// <remarks>This method ensures that packets are processed in sequential order. If a packet
        /// arrives out of order, it is stored in an internal buffer until all preceding packets have been processed.
        /// Once the missing packets are received, the buffered packets are processed in sequence.</remarks>
        /// <param name="data">The data associated with the packet to be processed.</param>
        /// <param name="sequence">The sequence number of the data packet. Must be greater than or equal to the last processed sequence number.</param>
        /// <param name="handler">A delegate that handles the processing of a data packet. The delegate is invoked with the sequence number
        /// and data when the packet is ready to be processed in order.</param>
        public void Process(T data, long sequence, BufferSequenceHandler handler)
        {
            lock (_buffer)
            {
                //The next packet in the sequence is the next one that needs to be sent. Flush it to the stream.
                if (_lastConsumedSequence + 1 == sequence)
                {
                    handler(sequence, data);
                    _lastConsumedSequence = sequence;
                }
                else
                {
                    //We received out-of-order packets. Store them in the buffer.
                    _buffer.Add(sequence, data);
                }

                //Flush any packets that are now in order.
                while (_buffer.TryGetValue(_lastConsumedSequence + 1, out var bytes))
                {
                    handler(_lastConsumedSequence + 1, bytes);
                    _buffer.Remove(_lastConsumedSequence + 1);
                    _lastConsumedSequence++;
                }
            }
        }

        /// <summary>
        /// Processes a data packet by ensuring it is handled in the correct sequence order.
        /// </summary>
        /// <remarks>This method ensures that packets are processed in sequential order. If a packet
        /// arrives out of order, it is stored in an internal buffer until all preceding packets have been processed.
        /// Once the missing packets are received, the buffered packets are processed in sequence.</remarks>
        /// <param name="data">The data associated with the packet to be processed.</param>
        /// <param name="sequence">The sequence number of the data packet. Must be greater than or equal to the last processed sequence number.</param>
        /// <param name="handler">A delegate that handles the processing of a data packet. The delegate is invoked with
        /// data when the packet is ready to be processed in order.</param>
        public void Process(T data, long sequence, BufferHandler handler)
        {
            lock (_buffer)
            {
                //The next packet in the sequence is the next one that needs to be sent. Flush it to the stream.
                if (_lastConsumedSequence + 1 == sequence)
                {
                    handler(data);
                    _lastConsumedSequence = sequence;
                }
                else
                {
                    //We received out-of-order packets. Store them in the buffer.
                    _buffer.Add(sequence, data);
                }

                //Flush any packets that are now in order.
                while (_buffer.TryGetValue(_lastConsumedSequence + 1, out var bytes))
                {
                    handler(bytes);
                    _buffer.Remove(_lastConsumedSequence + 1);
                    _lastConsumedSequence++;
                }
            }
        }
    }
}
