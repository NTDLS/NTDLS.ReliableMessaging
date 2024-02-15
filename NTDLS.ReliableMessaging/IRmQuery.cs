namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// All query payloads must inherit from this interface and be json serializable.
    /// </summary>
    public interface IRmQuery<ReplyType> : IRmPayload where ReplyType : IRmQueryReply
    {
    }
}
