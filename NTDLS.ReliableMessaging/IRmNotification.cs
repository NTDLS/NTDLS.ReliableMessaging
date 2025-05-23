namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// All notification payloads must inherit from this interface and be json serializable.
    /// </summary>
    public interface IRmNotification
        : IRmPayload
    {
    }
}
