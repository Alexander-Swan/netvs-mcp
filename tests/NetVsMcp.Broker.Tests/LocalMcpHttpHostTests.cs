using NetVsMcp.Broker.Services;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace NetVsMcp.Broker.Tests;

public sealed class LocalMcpHttpHostTests
{
    [Theory]
    [InlineData("http://127.0.0.1:0")]
    [InlineData("http://localhost:0")]
    public async Task StartAsync_StartsMcpHttpTransportOnLoopback(string endpoint)
    {
        var runtime = CreateRuntime(endpoint);

        await runtime.StartAsync(CancellationToken.None);

        Assert.True(runtime.IsHttpEndpointRunning);

        await runtime.StopAsync();
        Assert.False(runtime.IsHttpEndpointRunning);
    }

    [Fact]
    public async Task StartAsync_RejectsNonLoopbackEndpoint()
    {
        var runtime = CreateRuntime("http://192.0.2.10:5050");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.StartAsync(CancellationToken.None));

        Assert.Contains("loopback", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task McpEndpoint_InitializesAndListsBrokerTools()
    {
        var port = GetAvailablePort();
        var runtime = CreateRuntime($"http://127.0.0.1:{port}");

        await runtime.StartAsync(CancellationToken.None);

        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}")
            };

            using var initialize = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "NetVsMcp.Broker.Tests",
                        version = "1.0"
                    }
                }
            });
            initialize.EnsureSuccessStatusCode();

            using var listTools = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { }
            });
            listTools.EnsureSuccessStatusCode();

            var body = await listTools.Content.ReadAsStringAsync();
            Assert.Contains("vs_list_sessions", body);
            Assert.Contains("vs_get_status", body);
            Assert.Contains("vs_get_capabilities", body);
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    private static BrokerRuntime CreateRuntime(string endpoint)
    {
        return new BrokerRuntime(
            new BrokerOptions(endpoint, $@"\\.\pipe\netvs-mcp-test-{Guid.NewGuid():N}"),
            new SessionRegistry());
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(HttpClient http, object request)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var message = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = content
        };
        message.Headers.Accept.ParseAdd("application/json");
        message.Headers.Accept.ParseAdd("text/event-stream");

        return await http.SendAsync(message);
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
