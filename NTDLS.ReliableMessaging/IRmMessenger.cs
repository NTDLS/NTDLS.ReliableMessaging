using NTDLS.ReliableMessaging.Internal;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// RPC Server or Client instance.
    /// </summary>
    public interface IRmMessenger
    {
        /// <summary>
        /// A user settable object that can be accessed via the Context.Endpoint.Parameter Especially useful for convention based calls.
        /// </summary>
        public object? Parameter { get; set; }

        /// <summary>
        /// Configuration that was used to initialize the endpoint.
        /// </summary>
        public RmConfiguration Configuration { get; }

        /// <summary>
        /// Adds a class that contains notification and query handler functions.
        /// </summary>
        /// <param name="handlerClass"></param>
        public void AddHandler(IRmMessageHandler handlerClass);

        internal RmReflectionCache ReflectionCache { get; }
        internal void InvokeOnException(RmContext? context, Exception ex, IRmPayload? payload);
        internal void InvokeOnConnected(RmContext context);
        internal void InvokeOnDisconnected(RmContext context);
        internal void InvokeOnNotificationReceived(RmContext context, IRmNotification payload);
        internal IRmQueryReply InvokeOnQueryReceived(RmContext context, IRmPayload payload);
    }
}
