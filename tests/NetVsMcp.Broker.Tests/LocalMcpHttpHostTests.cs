using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed class LocalMcpHttpHostTests
{
    [Fact]
    public async Task StartAsync_StartsMcpHttpTransportOnLoopback()
    {
        var registry = new SessionRegistry();
        var runtime = new BrokerRuntime(
            new BrokerOptions("http://127.0.0.1:0", @"\\.\pipe\netvs-mcp-test"),
            registry);

        await runtime.StartAsync(CancellationToken.None);

        Assert.True(runtime.IsHttpEndpointRunning);

        await runtime.StopAsync();
        Assert.False(runtime.IsHttpEndpointRunning);
    }
}
