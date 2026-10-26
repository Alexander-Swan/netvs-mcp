using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task ExpansionRequiresValidBoundsAndForwardsTheStopGuardAndEvaluationPolicy()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        Assert.False((await runtime.Tools.DebugExpand("value", "stop", 3, maxNodes: 1001, sessionId: "vs-1")).Success);
        Assert.Null(rpc.LastFrameInspectionRequest);
        await runtime.Tools.DebugExpand("value", "stop", 3, maxChildren: 10, stackFrame: 2, sessionId: "vs-1");
        Assert.Equal("stop", rpc.LastFrameInspectionRequest!.ExpectedStopId);
        Assert.Equal(3, rpc.LastFrameInspectionRequest.ExpectedGeneration);
        Assert.Equal(2, rpc.LastFrameInspectionRequest.StackFrame);
        Assert.False(rpc.LastFrameInspectionRequest.Expansion!.AllowFunctionEvaluation);
        Assert.Equal(10, rpc.LastFrameInspectionRequest.Expansion.MaxChildren);
    }
}
