using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task EventsRejectUnregisteredPublishersAndExposeReplayAfterWatermark()
    {
        var runtime = CreateRuntime();
        var registration = new BrokerRegistrationRpcService(runtime.Sessions);
        var publisher = new SessionEventBuffer("vs-1");
        Assert.False((await registration.PublishEventsAsync(publisher.Snapshot(), CancellationToken.None)).Success);
        await registration.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);
        await registration.PublishEventsAsync(publisher.Snapshot(), CancellationToken.None);
        var cursor = await runtime.Tools.VsEvents(sessionId: "vs-1");
        Assert.True(cursor.Success);
        publisher.Publish("debugger", new("dbgBreakMode", Outcome: "stopped", StopId: "stop"));
        await registration.PublishEventsAsync(publisher.Snapshot(), CancellationToken.None);
        var replay = await runtime.Tools.VsEvents(cursor.Value!.Epoch, cursor.Value.Watermark, sessionId: "vs-1");
        Assert.Equal("stop", Assert.Single(replay.Value!.Events).Debugger!.StopId);
        await registration.UnregisterAsync("vs-1", CancellationToken.None);
        Assert.Null(runtime.Sessions.Events.Find("vs-1"));
    }

    [Fact]
    public async Task EventWaitHandlesFastStopsWithoutRepeatedDebuggerRpc()
    {
        var runtime = CreateRuntime();
        var registration = new BrokerRegistrationRpcService(runtime.Sessions);
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => throw new InvalidOperationException("An event wait must not poll debugger status.")
        };
        await registration.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);
        var publisher = new SessionEventBuffer("vs-1");
        await registration.PublishEventsAsync(publisher.Snapshot(), CancellationToken.None);
        var routed = runtime.Sessions.ListSessions().Single();
        var cursor = runtime.DialogOperations.CaptureEventCursor(routed);
        publisher.Publish("debugger", new("dbgBreakMode", Outcome: "stopped", StopId: "fast", Generation: 1, ExecutionSequence: 2));
        await registration.PublishEventsAsync(publisher.Snapshot(), CancellationToken.None);
        var state = await runtime.DialogOperations.WaitForDebugStateAsync(routed, session,
            new("dbgRunMode", Outcome: "running", Generation: 1, ExecutionSequence: 2), cursor, CancellationToken.None);
        Assert.Equal("fast", state.StopId);
        Assert.Equal(publisher.Snapshot().Watermark, state.EventCursor!.Watermark);
        Assert.Equal(publisher.Snapshot().Epoch, state.EventCursor.Epoch);
        Assert.Equal(0, session.DebugExecutionCommands);
    }

    [Fact]
    public async Task EventsDoNotSilentlyClaimSupportForOldExtensions()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        Assert.False((await runtime.Tools.VsEvents(sessionId: "vs-1")).Success);
    }
}
