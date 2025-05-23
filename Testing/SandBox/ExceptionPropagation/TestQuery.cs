using NTDLS.ReliableMessaging;

namespace ExceptionPropagation
{
    internal class TestQuery : IRmQuery<TestQueryReply>
    {
    }

    public class TestQueryReply : IRmQueryReply
    {
    }
}