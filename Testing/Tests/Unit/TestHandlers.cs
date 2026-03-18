using Tests.Shared;

namespace Tests.Unit
{
    public class TestHandlers(ServerFixture fixture) : IClassFixture<ServerFixture>
    {
        [Fact(DisplayName = "Test handlers.")]
        public void Test()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyNotification("test"));

            var reply = client.Query(new MyQuery($"query test"));
            Console.WriteLine($"Client received query reply: '{reply.Message}'");
            Assert.Equal("query reply test", reply.Message);

            Thread.Sleep(500);
            fixture.ThrowIfError();

            client.Disconnect();
        }

        [Fact(DisplayName = "Test handlers (async).")]
        public async Task TestAsync()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyNotification("test"));

            await client.QueryAsync(new MyQuery($"query test")).ContinueWith(x =>
            {
                if (x.IsCompletedSuccessfully && x.Result != null)
                {
                    Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
                    Assert.Equal("query reply test", x.Result.Message);
                }
                else
                {
                    Assert.Fail(x.Exception?.GetBaseException()?.Message);
                }
            });

            Thread.Sleep(500);
            fixture.ThrowIfError();

            client.Disconnect();
        }
    }
}
