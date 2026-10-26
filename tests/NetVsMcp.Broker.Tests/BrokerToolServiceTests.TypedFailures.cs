using NetVsMcp.Contracts;
using StreamJsonRpc;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    private sealed class TypedRemoteFailure : RemoteRpcException
    {
        public TypedRemoteFailure(int code) : base("Explicit capability failure") { ErrorCode = (StreamJsonRpc.Protocol.JsonRpcErrorCode)code; }
    }

    [Fact]
    public async Task TestDebugDoesNotFallBackToAnOlderUnsafeAttachRpc()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            TestDebugOwnedHandler = (_, _) => throw MissingFrameInspectionMethod()
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var result = await runtime.Tools.TestDebug(projectName: "Tests", framework: "net10.0", filter: "Passing", sessionId: "vs-1");
        Assert.False(result.Success);
        Assert.Equal(ToolErrorCodes.UnsupportedByVsix, result.Metadata!["error_code"]);
        Assert.Null(session.LastTestDebugRequest);
    }

    [Theory]
    [InlineData(VsRpcErrorCodes.StaleDebuggerContext, ToolErrorCodes.StaleDebuggerContext)]
    [InlineData(VsRpcErrorCodes.StaleAction, ToolErrorCodes.StaleAction)]
    [InlineData(VsRpcErrorCodes.FeatureUnavailable, ToolErrorCodes.FeatureUnavailable)]
    [InlineData(VsRpcErrorCodes.InvalidRequest, ToolErrorCodes.InvalidRequest)]
    [InlineData(VsRpcErrorCodes.HistoryUnavailable, ToolErrorCodes.HistoryUnavailable)]
    public async Task TypedRpcFailuresPreserveTheirRecoveryContract(int rpcCode, string errorCode)
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs")
        {
            FrameInspectionHandler = (_, _) => throw new TypedRemoteFailure(rpcCode)
        });
        var result = await runtime.Tools.DebugExpand("value", "pause", 1, sessionId: "vs-1");
        Assert.False(result.Success);
        Assert.Equal(errorCode, result.Metadata!["error_code"]);
        Assert.Equal("vs-1", result.Metadata["sessionId"]);
        Assert.False(string.IsNullOrWhiteSpace(result.Metadata["nextActions"]));
    }
}
