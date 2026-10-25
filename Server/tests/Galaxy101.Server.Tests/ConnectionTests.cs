using System.Net.WebSockets;
using Galaxy101.Server.Realtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace Galaxy101.Server.Tests;

public sealed class ConnectionTests
{
    [Fact]
    public void A_full_send_queue_closes_the_slow_client_instead_of_dropping_frames()
    {
        // Nothing drains the queue (no write loop): every frame past the limit must be refused, never silently lost.
        using var socket = new ClientWebSocket();
        var connection = new Connection(Guid.NewGuid(), socket, NullLogger.Instance);
        var accepted = 0;
        while (accepted < 1000 && connection.Send("match.state", new { n = accepted })) accepted++;
        Assert.Equal(512, accepted);
        Assert.True(connection.Closing.IsCancellationRequested);
    }
}
