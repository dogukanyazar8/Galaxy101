using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class RealtimeTests(ServerFixture server)
{
    [Fact]
    public async Task A_replaced_socket_gets_the_notice_and_closes_with_the_reason()
    {
        // The old device must learn it was replaced even when it misses the frame: the close carries "replaced" too.
        var (client, _) = await server.GuestAsync();
        await using var first = await RealtimeClient.ConnectAsync(server, client);
        await using var second = await RealtimeClient.ConnectAsync(server, client);
        await first.WaitAsync<object>(MessageTypes.Replaced);
        Assert.Equal("replaced", await first.ClosedAsync());
        Assert.False(second.Closed);
    }
}
