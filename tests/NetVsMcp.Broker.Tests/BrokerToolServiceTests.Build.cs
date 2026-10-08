using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task BuildSolution_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildSolution(
            waitForBuildToFinish: true,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.Equal("Done", response.Value!.Status.State);
        Assert.Equal(0, response.Value.LastBuildInfo);
    }

    [Fact]
    public async Task BuildAndGetErrors_BuildsAndReturnsDiagnostics()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.BuildAndGetErrors(includeWarnings: false, maxItems: 10, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(session.LastBuildSolutionRequest!.WaitForBuildToFinish);
        Assert.False(session.LastErrorListRequest!.IncludeWarnings);
        Assert.Equal(10, session.LastErrorListRequest.MaxItems);
        Assert.Single(response.Value!.Errors.Items);
    }

    [Fact]
    public async Task BuildStatus_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", new FakeVisualStudioSessionRpc("Editor.cs"));

        var response = await runtime.Tools.BuildStatus(solutionName: "NetVsMcp");

        Assert.True(response.Success);
        Assert.Equal("Idle", response.Value!.State);
    }

    [Fact]
    public async Task OutputRead_RoutesToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OutputRead(
            paneName: "Build",
            maxChars: 100,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("Build", session.LastOutputReadRequest!.PaneName);
        Assert.Equal(100, session.LastOutputReadRequest.MaxChars);
        Assert.Equal("Build output", response.Value!.Text);
    }

    [Fact]
    public async Task OutputWrite_RoutesTextToConnectedSession()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);

        var response = await runtime.Tools.OutputWrite("hello", paneName: "NetVsMcp", activate: true, sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.Equal("NetVsMcp", session.LastOutputWriteRequest!.PaneName);
        Assert.True(session.LastOutputWriteRequest.Activate);
        Assert.Contains("hello", response.Value!.Text);
    }

    [Fact]
    public async Task BuildStatus_ReturnsMissingConnectionFailure()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.BuildStatus(sessionId: "vs-1");

        Assert.False(response.Success);
        Assert.Equal("MissingConnection", response.Metadata!["failureReason"]);
        Assert.Equal("vs-1", response.Metadata["sessionId"]);
    }

    [Fact]
    public void EventsList_ReturnsQueuedEventsForRoutedSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Events.Publish(new BrokerEventNotification(
            "vs-1",
            BrokerEventTypes.BuildCompleted,
            "Build completed.",
            new Dictionary<string, string> { ["succeeded"] = "True" }));

        var response = runtime.Tools.EventsList(
            eventTypes: [BrokerEventTypes.BuildCompleted],
            sessionId: "vs-1");

        Assert.True(response.Success);
        var evt = Assert.Single(response.Value!.Events);
        Assert.Equal(BrokerEventTypes.BuildCompleted, evt.Type);
        Assert.Equal("True", evt.Data!["succeeded"]);
        Assert.False(response.Value.TimedOut);
    }

    [Fact]
    public async Task EventsWait_ReturnsQueuedEventWithoutPolling()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var wait = runtime.Tools.EventsWait(
            eventTypes: [BrokerEventTypes.BuildCompleted],
            timeoutSeconds: 5,
            sessionId: "vs-1");

        runtime.Events.Publish(new BrokerEventNotification(
            "vs-1",
            BrokerEventTypes.BuildCompleted,
            "Build completed.",
            null));

        var response = await wait;

        Assert.True(response.Success);
        Assert.False(response.Value!.TimedOut);
        Assert.Equal(BrokerEventTypes.BuildCompleted, Assert.Single(response.Value.Events).Type);
    }

    [Fact]
    public async Task EventsWait_TimesOutWhenNoMatchingEventArrives()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));

        var response = await runtime.Tools.EventsWait(
            eventTypes: [BrokerEventTypes.BuildCompleted],
            timeoutSeconds: 0,
            sessionId: "vs-1");

        Assert.True(response.Success);
        Assert.True(response.Value!.TimedOut);
        Assert.Empty(response.Value.Events);
    }

    [Fact]
    public async Task EventsWait_ReturnsRoutingFailureForAmbiguousSession()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.slnx", isActive: false));
        runtime.Sessions.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.slnx", isActive: false));

        var response = await runtime.Tools.EventsWait(timeoutSeconds: 0, solutionName: "Shared");

        Assert.False(response.Success);
        Assert.Equal("Ambiguous", response.Metadata!["failureReason"]);
        Assert.Contains("sessionId", response.Metadata["nextActions"]);
    }
}
