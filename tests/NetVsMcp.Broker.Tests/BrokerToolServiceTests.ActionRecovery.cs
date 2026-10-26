using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task ActionIdDoesNotRequireADummyIndexAndOmittedSelectionNeverAppliesIndexZero()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var unspecified = await runtime.Tools.CodeActionsApply("Editor.cs", 1, 1, sessionId: "vs-1");
        Assert.False(unspecified.Success);
        Assert.Null(rpc.LastCodeActionsApplyRequest);
        Assert.True((await runtime.Tools.CodeActionsApply("Editor.cs", 1, 1, actionId: "original", sessionId: "vs-1")).Success);
        Assert.Equal("original", rpc.LastCodeActionsApplyRequest!.ActionId);
    }

    [Fact]
    public async Task PartialActionFailurePreservesReceiptAndDoesNotReportToolSuccess()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            ActionApplicationReport = new(false, "Observed some changes", "Fix", [], "failedWithChanges", true, "receipt")
        });
        var response = await runtime.Tools.CodeActionsApply("Editor.cs", 1, 1, 0, actionId: "action", sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal("partial_application", response.Metadata!["error_code"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
        Assert.Equal("receipt", response.Value!.RecoveryId);
        Assert.Contains("Do not repeat the original apply", response.Metadata["nextActions"]);
    }

    [Fact]
    public async Task RecoveryRoutesTheReceiptAndExplainsConflictingDeveloperEdits()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            ActionRecoveryHandler = _ => throw new TypedRemoteFailure(VsRpcErrorCodes.StaleAction)
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var response = await runtime.Tools.CodeActionsRevert("receipt", sessionId: "vs-1");
        Assert.False(response.Success);
        Assert.Equal("receipt", rpc.LastCodeActionRecoveryRequest!.RecoveryId);
        Assert.Equal(ToolErrorCodes.StaleAction, response.Metadata!["error_code"]);
        Assert.Contains("Do not replay recovery", response.Metadata["nextActions"]);
    }
}
