using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task SessionPauseRejectsMutationsWithStructuredRecoveryAndPreservesReads()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));
        Assert.True(runtime.Tools.SessionAutomationControl("vs-1", paused: true).Value!.Paused);
        var rejected = await runtime.Tools.DocumentWrite("Editor.cs", "new text", sessionId: "vs-1");
        Assert.False(rejected.Success);
        Assert.Equal(ToolErrorCodes.MutationConflict, rejected.Metadata!["error_code"]);
        Assert.Contains("No new command was sent", rejected.Metadata["nextActions"]);
        Assert.True((await runtime.Tools.DocumentRead("Editor.cs", sessionId: "vs-1")).Success);
        runtime.Tools.SessionAutomationControl("vs-1", paused: false);
        Assert.True((await runtime.Tools.DocumentWrite("Editor.cs", "new text", sessionId: "vs-1")).Success);
    }
}
