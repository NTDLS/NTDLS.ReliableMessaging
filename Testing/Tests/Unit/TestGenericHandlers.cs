using Tests.Shared;

namespace Tests.Unit
{
    public class TestGenericHandlers(ServerFixture fixture) : IClassFixture<ServerFixture>
    {
        [Fact(DisplayName = "Test generic handlers.")]
        public void Test()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyGenericNotification<int>(1234));
            client.Notify(new MyGenericNotification<string>("test"));

            var reply1 = client.Query(new MyGenericQuery<string>($"query test"));
            Console.WriteLine($"Client received query reply: '{reply1.Message}'");
            Assert.Equal("query reply test", reply1.Message);

            var reply2 = client.Query(new MyGenericQueryDifferentReturn<string, int>($"query test"));
            Console.WriteLine($"Client received query reply: '{reply2.Message}'");
            Assert.Equal(4321, reply2.Message);

            Thread.Sleep(500);
            fixture.ThrowIfError();

            client.Disconnect();
        }

        [Fact(DisplayName = "Test generic handlers (async).")]
        public async Task TestAsync()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyGenericNotification<int>(1234));
            client.Notify(new MyGenericNotification<string>("test"));

            await client.QueryAsync(new MyGenericQuery<string>($"query test")).ContinueWith(x =>
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

            await client.QueryAsync(new MyGenericQueryDifferentReturn<string, int>($"query test")).ContinueWith(x =>
            {
                if (x.IsCompletedSuccessfully && x.Result != null)
                {
                    Console.WriteLine($"Client received query reply: '{x.Result.Message}'");
                    Assert.Equal(4321, x.Result.Message);
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
