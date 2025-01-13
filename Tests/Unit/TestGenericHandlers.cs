using Test.Library;

namespace Tests.Unit
{
    public class TestGenericHandlers(ServerFixture fixture) : IClassFixture<ServerFixture>
    {
        [Fact(DisplayName = "Test generic handlers.")]
        public async Task TestGenerics()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyGenericNotification<int>(1234));
            client.Notify(new MyGenericNotification<string>("test"));

            await client.Query(new MyGenericQuery<string>($"query test")).ContinueWith(x =>
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

            await client.Query(new MyGenericQueryDifferentReturn<string, int>($"query test")).ContinueWith(x =>
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
