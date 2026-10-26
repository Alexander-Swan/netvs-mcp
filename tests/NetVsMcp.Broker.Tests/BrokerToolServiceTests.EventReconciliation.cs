using NetVsMcp.Contracts;
using NetVsMcp.Broker.Services;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task TransportGapReconcilesWithAnAtomicSnapshotAndNeverReplaysExecution()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture"));
        var sender = new SessionEventBuffer("vs-1", capacity: 2, epoch: "publisher");
        var state = new DebuggerStateInfo("dbgBreakMode", StopId: "current", Generation: 3, ExecutionSequence: 7);
        for (var i = 0; i < 5; i++) sender.Publish("debugger", state);
        runtime.Sessions.Events.Accept(sender.Snapshot());
        var snapshots = 0;
        var rpc = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("A separate status read would race the watermark."),
            SessionSnapshotHandler = () =>
            {
                snapshots++;
                sender.Publish("snapshot", state);
                return Task.FromResult(new VsSessionEventSnapshot("vs-1", state, sender.Snapshot()));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", rpc);
        var cursor = new DialogAwareSessionDispatcher.EventCursor("publisher", 0);
        var observed = await runtime.DialogOperations.WaitForDebugStateAsync(runtime.Sessions.ListSessions().Single(), rpc,
            new("dbgRunMode"), cursor, default);
        Assert.Equal("current", observed.StopId);
        Assert.Equal(1, snapshots);
        Assert.Equal(6, cursor.Sequence);
        Assert.False(runtime.Sessions.Events.Find("vs-1")!.Read(cursor.Epoch, cursor.Sequence).RequiresReconciliation);
        Assert.Equal(0, rpc.DebugExecutionCommands);
    }

    [Fact]
    public async Task ReplacedConnectionCannotSatisfyAnExistingExecutionWait()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Fixture"));
        var old = new FakeVisualStudioSessionRpc("old.cs");
        runtime.Connections.AddOrUpdate("vs-1", old);
        var publisher = new SessionEventBuffer("vs-1", epoch: "old");
        runtime.Sessions.Events.Accept(publisher.Snapshot());
        var cursor = runtime.DialogOperations.CaptureEventCursor(runtime.Sessions.ListSessions().Single());
        var wait = runtime.DialogOperations.WaitForDebugStateAsync(runtime.Sessions.ListSessions().Single(), old, new("dbgRunMode"), cursor, default);
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("new.cs"));
        runtime.Sessions.Events.Remove("vs-1");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => wait);
        Assert.Contains("connection changed", error.Message);
        Assert.Equal(0, old.DebugExecutionCommands);
    }
}
