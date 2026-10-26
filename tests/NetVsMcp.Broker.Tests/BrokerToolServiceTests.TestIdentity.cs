using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task CompositeRetrievesTheRunJustStartedInsteadOfAnOlderRequestedId()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            TestRunReport = new(true, "completed", [], [], Backend: "dotnet-cli", RunId: "new-run", Outcome: "noTests", RunnerExitCode: 0)
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var response = await runtime.Tools.TestRunAndGetResults(runId: "older-run", sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("new-run", rpc.LastTestResultsRequest!.RunId);
        Assert.Equal("noTests", response.Value!.Run.Outcome);
    }
}
