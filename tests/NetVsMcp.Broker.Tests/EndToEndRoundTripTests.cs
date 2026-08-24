using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Tests;

/// <summary>
/// Smoke test proving a full MCP tool call actually works end-to-end through the real transports:
/// HTTP -> broker dispatch -> named pipe -> (fake) VS-side RPC target -> response back through HTTP
/// as MCP. The closest existing coverage,
/// <see cref="VsixRegistrationPipeListenerTests"/>, exercises the real pipe/JSON-RPC transport but
/// never drives a call through the real HTTP MCP endpoint, and <see cref="LocalMcpHttpHostTests"/>
/// exercises the real HTTP transport but never has a live VSIX connection behind it. This test
/// starts a real <see cref="BrokerRuntime"/> (both transports) and connects a fake VSIX over the
/// real named pipe, then drives <c>document_active</c> through the real HTTP endpoint.
/// </summary>
public sealed class EndToEndRoundTripTests
{
    [Fact]
    public async Task DocumentActive_RoundTripsThroughRealHttpAndPipeTransports()
    {
        var pipeName = $"netvs-mcp-e2e-{Guid.NewGuid():N}";
        var port = GetAvailablePort();
        var runtime = new BrokerRuntime(
            new BrokerOptions($"http://127.0.0.1:{port}", $@"\\.\pipe\{pipeName}"),
            new SessionRegistry());

        await runtime.StartAsync(CancellationToken.None);
        try
        {
            // Connect a fake VSIX instance over the real named pipe, exactly as a real VS process would.
            await using var clientPipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await clientPipe.ConnectAsync(5000);

            using var jsonRpc = new JsonRpc(clientPipe);
            jsonRpc.AddLocalRpcTarget<IVisualStudioSessionRpc>(
                new FakeVisualStudioSessionRpc("Program.cs"),
                options: null);
            var registration = jsonRpc.Attach<IBrokerRegistrationRpc>();
            jsonRpc.StartListening();

            var registerResponse = await registration.RegisterAsync(
                new VsSessionRegistration(
                    SessionId: "vs-e2e-1",
                    ProcessId: 4321,
                    VisualStudioVersion: "18.0",
                    Edition: "Enterprise",
                    SolutionName: "NetVsMcp",
                    SolutionPath: @"C:\Code\NetVsMcp\NetVsMcp.slnx",
                    ActiveDocument: "Program.cs",
                    DebuggerMode: DebuggerMode.Design,
                    IsActiveWindow: true,
                    Capabilities: [VsCapability.Editor, VsCapability.Navigation]),
                CancellationToken.None);
            Assert.True(registerResponse.Success);

            // Now drive a real MCP tool call through the real HTTP endpoint. With exactly one
            // registered session, the broker auto-selects it -- no explicit sessionId needed.
            using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            using var initialize = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new { name = "NetVsMcp.Broker.Tests", version = "1.0" }
                }
            });
            initialize.EnsureSuccessStatusCode();

            using var callTool = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "document_active",
                    arguments = new { }
                }
            });
            callTool.EnsureSuccessStatusCode();

            var body = await callTool.Content.ReadAsStringAsync();
            var jsonStart = body.IndexOf('{');
            using var document = JsonDocument.Parse(body[jsonStart..]);
            var result = document.RootElement.GetProperty("result");

            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());

            var content = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.NotNull(content);
            // The tool result is the JSON-serialized ToolResponse<string?> carrying the value the
            // fake VSIX returned over the pipe -- proving the round trip actually reached it.
            Assert.Contains("Program.cs", content);
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    private static async Task<HttpResponseMessage> PostMcpAsync(HttpClient http, object request)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var message = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
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
