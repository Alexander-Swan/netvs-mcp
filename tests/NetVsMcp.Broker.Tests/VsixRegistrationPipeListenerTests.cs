using System.IO.Pipes;
using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Tests;

public sealed class VsixRegistrationPipeListenerTests
{
    [Fact]
    public async Task RegisteredPipeSession_IsAddedToConnectionMapAndRemovedOnDisconnect()
    {
        var pipeName = $"netvs-mcp-test-{Guid.NewGuid():N}";
        var registry = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        await using var listener = new VsixRegistrationPipeListener(
            new BrokerOptions("http://127.0.0.1:0", $@"\\.\pipe\{pipeName}"),
            registry,
            connections);

        await listener.StartAsync(CancellationToken.None);

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

        var response = await registration.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(connections.TryGet("vs-1", out _));

        var dispatcher = new VsSessionDispatcher(registry, connections);
        var dispatch = await dispatcher.DispatchAsync(
            new RoutingTarget(SessionId: "vs-1"),
            static async (connection, cancellationToken) =>
                (await connection.GetActiveDocumentAsync(cancellationToken)).Value,
            CancellationToken.None);

        Assert.True(dispatch.Success);
        Assert.Equal("Program.cs", dispatch.Value);

        jsonRpc.Dispose();
        clientPipe.Dispose();

        await WaitForAsync(() => !connections.TryGet("vs-1", out _));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            await Task.Delay(25, cancellation.Token);
        }
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
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }

    private sealed class FakeVisualStudioSessionRpc : IVisualStudioSessionRpc
    {
        private readonly string _activeDocument;

        public FakeVisualStudioSessionRpc(string activeDocument)
        {
            _activeDocument = activeDocument;
        }

        public Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ToolResponse<string?>.Ok(_activeDocument));
        }

        public Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
            string documentPath,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
