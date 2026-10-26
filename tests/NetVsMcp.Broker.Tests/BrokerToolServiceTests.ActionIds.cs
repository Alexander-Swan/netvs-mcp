using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task CodeActionPreviewUsesVerifiedRpcWithoutApplying()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        await runtime.Tools.CodeActionsPreview("Editor.cs", 1, 1, "original", sessionId: "vs-1");
        Assert.True(rpc.LastCodeActionsApplyRequest!.PreviewOnly);
        Assert.Equal("original", rpc.LastCodeActionsApplyRequest.ActionId);
    }

    [Fact]
    public async Task CodeActionIdentityIsForwardedToSelectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        await runtime.Tools.CodeActionsApply("Editor.cs", 1, 1, 0, sessionId: "vs-1", actionId: "opaque-original");
        Assert.Equal("opaque-original", rpc.LastCodeActionsApplyRequest!.ActionId);
    }
}
