using NTDLS.ReliableMessaging;

namespace Tests
{
    public class ServerFixture : IDisposable
    {
        public RmServer Server { get; private set; }

        public ServerFixture()
        {
            Server = ServerSingleton.GetSingleInstance();
        }


        public void ThrowIfError()
        {
            ServerSingleton.ThrowIfError();
        }

        public void Dispose()
        {
            ServerSingleton.Dereference();
            GC.SuppressFinalize(this);
        }
    }
}
