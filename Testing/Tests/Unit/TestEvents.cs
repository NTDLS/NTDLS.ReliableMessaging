using Tests.Shared;

namespace Tests.Unit
{
    public class TestEvents(ServerFixture fixture) : IClassFixture<ServerFixture>
    {
        [Fact(DisplayName = "Test events.")]
        public async Task Test()
        {
            var client = ClientFactory.CreateAndConnect();

            client.Notify(new MyNotificationForEvent("test"));

            await client.Query(new MyQueryForEvent($"query test")).ContinueWith(x =>
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
