using System.Threading.Channels;

namespace NTDLS.ReliableMessaging.Internal
{
    /// <summary>
    /// Runs a connection's notification handlers one at a time, in the order that the notifications arrived.
    /// The queue is bounded: when it is full, the receive thread waits, which stops reading from the socket so that
    /// TCP slows the sender down, rather than letting unhandled notifications fill memory.
    /// </summary>
    internal class RmNotificationDispatcher
    {
        private readonly Channel<Action> _channel;

        public RmNotificationDispatcher(int capacity)
        {
            _channel = Channel.CreateBounded<Action>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            //A pool thread is only held while a handler is running, not while waiting for the next notification.
            _ = Task.Run(ProcessAsync);
        }

        /// <summary>
        /// Queues a handler. Called from the receive thread, which this blocks while the queue is full.
        /// </summary>
        public void Enqueue(Action handler)
        {
            if (_channel.Writer.TryWrite(handler) == false)
            {
                _channel.Writer.WriteAsync(handler).AsTask().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Stops accepting notifications. Those already queued are still handled.
        /// </summary>
        public void Complete()
            => _channel.Writer.TryComplete();

        private async Task ProcessAsync()
        {
            await foreach (var handler in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                handler(); //Handlers catch and report their own exceptions.
            }
        }
    }
}
