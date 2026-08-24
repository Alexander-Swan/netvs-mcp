using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using StreamJsonRpc;

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
            Assert.Contains("netvs_get_best_practices", body);

            using var listResources = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "resources/list",
                @params = new { }
            });
            listResources.EnsureSuccessStatusCode();

            var resourcesBody = await listResources.Content.ReadAsStringAsync();
            Assert.Contains("guide://netvsmcp/manage-visual-studio.md", resourcesBody);
            Assert.Contains("guide://netvsmcp/build-visual-studio.md", resourcesBody);

            using var readResource = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 4,
                method = "resources/read",
                @params = new
                {
                    uri = "guide://netvsmcp/manage-visual-studio.md"
                }
            });
            readResource.EnsureSuccessStatusCode();

            var readBody = await readResource.Content.ReadAsStringAsync();
            Assert.Contains("Visual Studio", readBody);
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    [Fact]
    public async Task WebAutomationTools_AreOnlyServedFromTheOptInEndpoint()
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

            var defaultEndpointTools = await ListToolNamesAsync(http, "/mcp");
            Assert.Contains("vs_list_sessions", defaultEndpointTools);
            Assert.Contains("console_get_info", defaultEndpointTools);
            Assert.DoesNotContain(defaultEndpointTools, name => McpEndpointRouting.IsWebAutomationTool(name));

            var webAutomationEndpointTools = await ListToolNamesAsync(http, "/mcp-wu");
            Assert.Contains("ui_capture_region", webAutomationEndpointTools);
            Assert.Contains("web_connect", webAutomationEndpointTools);
            Assert.All(webAutomationEndpointTools, name => Assert.True(McpEndpointRouting.IsWebAutomationTool(name)));
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    [Fact]
    public async Task McpToolCall_MissingRequiredArguments_ReturnsSpecificToolValidation()
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

            using var defaultInitialize = await InitializeMcpAsync(http, "/mcp", 1);
            defaultInitialize.EnsureSuccessStatusCode();

            using var openRelevantFiles = await PostMcpAsync(http, "/mcp", new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "open_relevant_files",
                    arguments = new
                    {
                        path = "src/Program.cs"
                    }
                }
            });
            openRelevantFiles.EnsureSuccessStatusCode();

            var openRelevantFilesResponse = await ReadMcpToolResponseAsync<OpenRelevantFilesResult>(openRelevantFiles);
            Assert.False(openRelevantFilesResponse.Success);
            Assert.Equal("At least one path is required.", openRelevantFilesResponse.Message);

            using var webAutomationInitialize = await InitializeMcpAsync(http, "/mcp-wu", 3);
            webAutomationInitialize.EnsureSuccessStatusCode();

            using var uiFindElements = await PostMcpAsync(http, "/mcp-wu", new
            {
                jsonrpc = "2.0",
                id = 4,
                method = "tools/call",
                @params = new
                {
                    name = "ui_find_elements",
                    arguments = new
                    {
                        query = "Button"
                    }
                }
            });
            uiFindElements.EnsureSuccessStatusCode();

            var uiFindElementsResponse = await ReadMcpToolResponseAsync<AutomationResult>(uiFindElements);
            Assert.False(uiFindElementsResponse.Success);
            Assert.Equal("Selector is required.", uiFindElementsResponse.Message);
            Assert.Equal(ToolErrorCodes.InvalidRequest, uiFindElementsResponse.Metadata!["error_code"]);
        }
        finally
        {
            await runtime.StopAsync();
        }
    }

    [Fact]
    public async Task McpToolCall_RoundTripsThroughHttpBrokerPipeAndVsixRpc()
    {
        var port = GetAvailablePort();
        var pipeName = $"netvs-mcp-test-{Guid.NewGuid():N}";
        var runtime = CreateRuntime($"http://127.0.0.1:{port}", pipeName);

        await runtime.StartAsync(CancellationToken.None);

        await using var clientPipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await clientPipe.ConnectAsync(5000);

        using var jsonRpc = new JsonRpc(clientPipe);
        jsonRpc.AddLocalRpcTarget(new FakeVisualStudioSessionRpc(@"C:\Code\NetVsMcp\Program.cs"));
        var registration = jsonRpc.Attach<IBrokerRegistrationRpc>();
        jsonRpc.StartListening();

        using var http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}")
        };

        try
        {
            var registerResponse = await registration.RegisterAsync(
                CreateRegistration("vs-e2e", "NetVsMcp"),
                CancellationToken.None);
            Assert.True(registerResponse.Success);

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

            using var toolCall = await PostMcpAsync(http, new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "document_active",
                    arguments = new
                    {
                        sessionId = "vs-e2e"
                    }
                }
            });
            toolCall.EnsureSuccessStatusCode();

            var toolResponse = await ReadMcpToolResponseAsync<string?>(toolCall);
            Assert.True(toolResponse.Success);
            Assert.Equal(@"C:\Code\NetVsMcp\Program.cs", toolResponse.Value);
        }
        finally
        {
            jsonRpc.Dispose();
            await runtime.StopAsync();
        }
    }

    private static async Task<HashSet<string>> ListToolNamesAsync(HttpClient http, string path)
    {
        using var initialize = await InitializeMcpAsync(http, path, 1);
        initialize.EnsureSuccessStatusCode();

        using var listTools = await PostMcpAsync(http, path, new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        });
        listTools.EnsureSuccessStatusCode();

        var body = await listTools.Content.ReadAsStringAsync();
        var jsonStart = body.IndexOf('{');
        using var document = JsonDocument.Parse(body[jsonStart..]);
        var tools = document.RootElement.GetProperty("result").GetProperty("tools");
        return tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).ToHashSet();
    }

    private static Task<HttpResponseMessage> InitializeMcpAsync(HttpClient http, string path, int id) =>
        PostMcpAsync(http, path, new
        {
            jsonrpc = "2.0",
            id,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-11-25",
                capabilities = new { },
                clientInfo = new { name = "NetVsMcp.Broker.Tests", version = "1.0" }
            }
        });

    private static BrokerRuntime CreateRuntime(string endpoint)
    {
        return CreateRuntime(endpoint, $"netvs-mcp-test-{Guid.NewGuid():N}");
    }

    private static BrokerRuntime CreateRuntime(string endpoint, string pipeName)
    {
        return new BrokerRuntime(
            new BrokerOptions(endpoint, $@"\\.\pipe\{pipeName}"),
            new SessionRegistry());
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: 1234,
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: $@"C:\Code\{solutionName}\{solutionName}.slnx",
            ActiveDocument: @"C:\Code\NetVsMcp\Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private static Task<HttpResponseMessage> PostMcpAsync(HttpClient http, object request) =>
        PostMcpAsync(http, "/mcp", request);

    private static async Task<HttpResponseMessage> PostMcpAsync(HttpClient http, string path, object request)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = content
        };
        message.Headers.Accept.ParseAdd("application/json");
        message.Headers.Accept.ParseAdd("text/event-stream");

        return await http.SendAsync(message);
    }

    private static async Task<ToolResponse<T>> ReadMcpToolResponseAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var jsonStart = body.IndexOf('{');
        Assert.True(jsonStart >= 0, $"Expected JSON response body. Body: {body}");

        using var document = JsonDocument.Parse(body[jsonStart..]);
        var text = document.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(text));
        return JsonSerializer.Deserialize<ToolResponse<T>>(text, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class FakeVisualStudioSessionRpc
    {
        private readonly string _activeDocument;

        public FakeVisualStudioSessionRpc(string activeDocument)
        {
            _activeDocument = activeDocument;
        }

        public Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<string?>.Ok(_activeDocument));
        }
    }
}
