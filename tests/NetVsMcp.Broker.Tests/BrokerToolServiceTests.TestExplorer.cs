using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task ExplicitExplorerCannotSilentlyRunCliOnAnOlderExtension()
    {
        var runtime = CreateRuntime(); var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture")); runtime.Connections.AddOrUpdate("vs-1", rpc);
        var response = await runtime.Tools.TestRun("Fixture", sessionId: "vs-1", backend: "test-explorer");
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, response.Metadata!["error_code"]);
        Assert.Null(rpc.LastTestRunRequest);
    }

    [Fact]
    public async Task ExplorerCancellationUsesAnOwnedRecoveryRpcInsteadOfCancelingItsWait()
    {
        var runtime = CreateRuntime(); var entered = new TaskCompletionSource<bool>();
        var completed = new TaskCompletionSource<TestOperationResult>();
        string? id = null; var cancels = 0;
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            ExplorerRunHandler = (request, ct) => { Assert.False(ct.CanBeCanceled); id = request.RunId; entered.SetResult(true); return completed.Task; },
            ExplorerCancelHandler = (request, _) => { Assert.Equal(id, request.RunId); cancels++; completed.SetResult(new(true, "Canceled", [], [], "test-explorer", id, "canceled")); return Task.FromResult(true); }
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture")); runtime.Connections.AddOrUpdate("vs-1", rpc);
        using var cancel = new CancellationTokenSource();
        var running = runtime.Tools.TestRun("Fixture", sessionId: "vs-1", cancellationToken: cancel.Token, backend: "test-explorer");
        await entered.Task; cancel.Cancel();
        var response = await running;
        Assert.True(response.Success);
        Assert.Equal("canceled", response.Value!.Outcome);
        Assert.Equal(1, cancels);
        Assert.Null(rpc.LastTestRunRequest);
    }
}
