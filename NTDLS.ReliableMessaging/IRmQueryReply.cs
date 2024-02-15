namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// All query-reply payloads must inherit from this interface and be json serializable.
    /// </summary>
    public interface IRmQueryReply : IRmPayload
    {
    }
}
