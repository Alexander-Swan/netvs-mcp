using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DebugSnapshotImportsItsWatermarkBeforeTheNextCommand()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var publisher = new SessionEventBuffer("vs-1", epoch: "epoch");
        var state = new DebuggerStateInfo("dbgBreakMode", StopId: "pause", Generation: 2);
        publisher.Publish("snapshot", state);
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("State and watermark must come from one capture."),
            SessionSnapshotHandler = () => Task.FromResult(new VsSessionEventSnapshot("vs-1", state, publisher.Snapshot()))
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var snapshot = await runtime.Tools.DebugSnapshot(include: [], sessionId: "vs-1");
        Assert.True(snapshot.Success);
        Assert.Equal("pause", snapshot.Value!.State.StopId);
        Assert.Equal("epoch", snapshot.Value.State.EventCursor!.Epoch);
        Assert.Equal("vs-1", snapshot.Value.State.EventCursor.SessionId);
        var following = await runtime.Tools.VsEvents("epoch", snapshot.Value!.State.EventCursor!.Watermark, sessionId: "vs-1");
        Assert.True(following.Success);
        Assert.False(following.Value!.RequiresReconciliation);
        Assert.Empty(following.Value.Events);
    }

    [Fact]
    public async Task SupersededSnapshotCannotReplaceTheCurrentStream()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var publisher = new SessionEventBuffer("vs-1", epoch: "old");
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            SessionSnapshotHandler = () =>
            {
                runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("replacement.cs"));
                return Task.FromResult(new VsSessionEventSnapshot("vs-1", new DebuggerStateInfo("dbgDesignMode"), publisher.Snapshot()));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        Assert.False((await runtime.Tools.DebugSnapshot(include: [], sessionId: "vs-1")).Success);
        Assert.Null(runtime.Sessions.Events.Find("vs-1"));
    }
    [Fact]
    public async Task DebugStatusUsesTheSameAtomicEventCapture()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var publisher = new SessionEventBuffer("vs-1", epoch: "epoch");
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("A separate status read would race."),
            SessionSnapshotHandler = () =>
            {
                var state = new DebuggerStateInfo("dbgRunMode", Generation: 3, ExecutionSequence: 4);
                publisher.Publish("snapshot", state);
                return Task.FromResult(new VsSessionEventSnapshot("vs-1", state, publisher.Snapshot()));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var status = await runtime.Tools.DebugStatus(sessionId: "vs-1");
        Assert.True(status.Success);
        Assert.Equal("dbgRunMode", status.Value!.Mode);
        Assert.Equal(1, status.Value.EventCursor!.Watermark);
        Assert.Equal(0, rpc.DebugExecutionCommands);
        var context = await runtime.Tools.VsContextSnapshot(sessionId: "vs-1");
        Assert.True(context.Success);
        Assert.Equal("dbgRunMode", context.Value!.Debugger!.Mode);
        Assert.Equal(2, context.Value.Debugger.EventCursor!.Watermark);
    }

    [Fact]
    public async Task LegacySnapshotKeepsItsStateWithoutInventingAnEventCursor()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));
        var snapshot = await runtime.Tools.DebugSnapshot(include: [], sessionId: "vs-1");
        Assert.True(snapshot.Success);
        Assert.Null(snapshot.Value!.State.EventCursor);
        Assert.Null(runtime.Sessions.Events.Find("vs-1"));
    }

    [Fact]
    public async Task ActionSnapshotCapturesCursorWithoutRepeatingExecution()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var publisher = new SessionEventBuffer("vs-1", epoch: "epoch");
        var state = new DebuggerStateInfo("dbgRunMode", Generation: 3, ExecutionSequence: 4);
        publisher.Publish("snapshot", state);
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("Use the atomic capture."),
            SessionSnapshotHandler = () => Task.FromResult(new VsSessionEventSnapshot("vs-1", state, publisher.Snapshot()))
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var snapshot = await runtime.Tools.DebugSnapshot(action: DebugAdvanceAction.Continue,
            include: [], settleTimeoutMilliseconds: 0, sessionId: "vs-1");
        Assert.True(snapshot.Success);
        Assert.Equal("dbgRunMode", snapshot.Value!.State.Mode);
        Assert.Equal(1, snapshot.Value.State.EventCursor!.Watermark);
        Assert.Equal(1, rpc.DebugExecutionCommands);
    }

}
