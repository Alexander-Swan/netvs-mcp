using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task ExactTestResultPagesForwardIdentityAndRejectInvalidBounds()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            TestResultsPageHandler = r => new(r.RunId, "dotnet-cli", "testsFailed", 1, r.Offset, 10, 10,
                r.Offset + r.MaxItems, false, DateTimeOffset.UtcNow, [])
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        Assert.False((await runtime.Tools.TestResultsPage("run", maxItems: 101, sessionId: "vs-1")).Success);
        Assert.Null(rpc.LastTestResultsPageRequest);
        var page = await runtime.Tools.TestResultsPage("older-run", offset: 2, maxItems: 3, sessionId: "vs-1");
        Assert.True(page.Success);
        Assert.Equal("older-run", rpc.LastTestResultsPageRequest!.RunId);
        Assert.Equal(2, rpc.LastTestResultsPageRequest.Offset);
        Assert.Equal("testsFailed", page.Value!.Outcome);
    }

    [Fact]
    public async Task CompositeDoesNotFetchMonolithicResultsAfterAPagedRun()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            TestRunReport = new(true, "Completed", [], [], Backend: "dotnet-cli", RunId: "paged", Outcome: "completed",
                RunnerExitCode: 0, TotalResults: 100, RetainedResults: 100, NextOffset: 25)
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var result = await runtime.Tools.TestRunAndGetResults(sessionId: "vs-1");
        Assert.True(result.Success);
        Assert.Equal(25, result.Value!.Results.NextOffset);
        Assert.Equal("paged", result.Value.Results.RunId);
        Assert.Null(rpc.LastTestResultsRequest);
    }
}
