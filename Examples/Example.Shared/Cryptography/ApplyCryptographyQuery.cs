using NTDLS.ReliableMessaging;

namespace Example.Shared.Cryptography
{
    public class ApplyCryptographyQuery
        : IRmQuery<ApplyCryptographyQueryReply>
    {
        public ApplyCryptographyQuery()
        {
        }
    }

    public class ApplyCryptographyQueryReply
        : IRmQueryReply
    {
        public ApplyCryptographyQueryReply()
        {
        }
    }
}
