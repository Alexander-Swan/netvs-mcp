using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public class BrokerRegistrationOwnershipTests
{
    [Fact]
    public async Task ReplacedConnectionCannotPublishUnregisterOrRemoveItsSuccessor()
    {
        var sessions = new SessionRegistry();
        var connections = new VsSessionConnectionMap();
        var firstRpc = new FakeVisualStudioSessionRpc("first.cs");
        var nextRpc = new FakeVisualStudioSessionRpc("next.cs");
        var first = new BrokerRegistrationRpcService(sessions, connections, firstRpc);
        var next = new BrokerRegistrationRpcService(sessions, connections, nextRpc);
        var registration = new VsSessionRegistration("vs-1", 1, "18.0", "Community", "Fixture", "C:/Fixture.sln", null, DebuggerMode.Design, false, []);
        Assert.True((await first.RegisterAsync(registration, default)).Success);
        var oldEvents = new SessionEventBuffer("vs-1", epoch: "old");
        oldEvents.Publish("DebuggerState");
        Assert.True((await first.PublishEventsAsync(oldEvents.Snapshot(), default)).Success);
        var previousStream = sessions.Events.Find("vs-1")!;
        var waiting = previousStream.WaitAsync("old", 1, TimeSpan.FromSeconds(30), default);
        Assert.True((await next.RegisterAsync(registration, default)).Success);
        Assert.True((await waiting.WaitAsync(TimeSpan.FromSeconds(1))).RequiresReconciliation);
        var newEvents = new SessionEventBuffer("vs-1", epoch: "new");
        newEvents.Publish("DebuggerState");
        Assert.True((await next.PublishEventsAsync(newEvents.Snapshot(), default)).Success);
        Assert.False((await first.PublishEventsAsync(oldEvents.Snapshot(), default)).Success);
        Assert.False((await first.HeartbeatAsync("vs-1", default)).Success);
        Assert.False((await first.UnregisterAsync("vs-1", default)).Success);
        Assert.False((await first.RegisterAsync(registration, default)).Success);
        first.RemoveRegisteredConnections();
        Assert.Single(sessions.ListSessions());
        Assert.True(connections.TryGet("vs-1", out var current));
        Assert.Same(nextRpc, current);
        Assert.Equal("new", sessions.Events.Find("vs-1")!.Snapshot().Epoch);
        Assert.True((await next.UnregisterAsync("vs-1", default)).Success);
        Assert.Empty(sessions.ListSessions());
    }
}
