using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactSnapshotAndWaitPreservePauseGuards(bool wait)
    {
        var runtime = CreateRuntime();
        var state = new DebuggerStateInfo("dbgBreakMode", Outcome: "stopped", Generation: 2, StopId: "pause-compact");
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(state),
            FrameInspectionHandler = (_, _) => Task.FromResult(new DebugFrameInspectionResult(state, new(state, []), [], null, new(state, [], true)))
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture"));
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var result = wait
            ? await runtime.Tools.DebugWaitForBreak(include: ["callStack", "locals"], sessionId: "vs-1", expectedStopId: state.StopId, expectedGeneration: 2)
            : await runtime.Tools.DebugSnapshot(include: ["callStack", "locals"], sessionId: "vs-1", expectedStopId: state.StopId, expectedGeneration: 2);
        Assert.True(result.Success);
        Assert.Equal(state, result.Value!.State);
        Assert.Equal(state, result.Value.CallStack!.State);
        Assert.Equal(state, result.Value.Locals!.State);
        Assert.Equal(state.StopId, rpc.LastFrameInspectionRequest!.ExpectedStopId);
        Assert.Equal(2, rpc.LastFrameInspectionRequest.ExpectedGeneration);
    }
}
