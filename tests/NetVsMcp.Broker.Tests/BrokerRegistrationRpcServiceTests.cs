using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class BrokerRegistrationRpcServiceTests
{
    [Fact]
    public async Task RegisterAsync_AddsSessionToRegistry()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);

        var response = await service.RegisterAsync(
            CreateRegistration("vs-1", "NetVsMcp"),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(registry.ListSessions());
        Assert.Equal("NetVsMcp", registry.ListSessions().Single().SolutionName);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "OldName"), CancellationToken.None);

        var response = await service.UpdateAsync(
            new VsSessionUpdate(
                SessionId: "vs-1",
                SolutionName: "NewName",
                SolutionPath: @"C:\Code\NewName\NewName.slnx",
                ActiveDocument: "Services\\BrokerRuntime.cs",
                DebuggerMode: DebuggerMode.Break,
                IsActiveWindow: false,
                Capabilities: [VsCapability.Debugger]),
            CancellationToken.None);

        var session = registry.ListSessions().Single();
        Assert.True(response.Success);
        Assert.Equal("NewName", session.SolutionName);
        Assert.Equal(DebuggerMode.Break, session.DebuggerMode);
        Assert.Contains(VsCapability.Debugger, session.Capabilities);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.UpdateAsync(
            new VsSessionUpdate(
                SessionId: "missing",
                SolutionName: "Missing",
                SolutionPath: null,
                ActiveDocument: null,
                DebuggerMode: DebuggerMode.Unknown,
                IsActiveWindow: false),
            CancellationToken.None);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task HeartbeatAsync_RefreshesStaleSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);
        Assert.Equal(SessionHealth.Stale, registry.ListSessionStatuses(DateTimeOffset.UtcNow.AddMinutes(1)).Single().Health);

        var response = await service.HeartbeatAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(SessionHealth.Connected, registry.ListSessionStatuses().Single().Health);
    }

    [Fact]
    public async Task HeartbeatAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.HeartbeatAsync("missing", CancellationToken.None);

        Assert.False(response.Success);
    }

    [Fact]
    public async Task UnregisterAsync_RemovesExistingSession()
    {
        var registry = new SessionRegistry();
        var service = new BrokerRegistrationRpcService(registry);
        await service.RegisterAsync(CreateRegistration("vs-1", "NetVsMcp"), CancellationToken.None);

        var response = await service.UnregisterAsync("vs-1", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Empty(registry.ListSessions());
    }

    [Fact]
    public async Task UnregisterAsync_ReturnsFailureForUnknownSession()
    {
        var service = new BrokerRegistrationRpcService(new SessionRegistry());

        var response = await service.UnregisterAsync("missing", CancellationToken.None);

        Assert.False(response.Success);
    }

    private static VsSessionRegistration CreateRegistration(string sessionId, string solutionName)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: 1234,
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: $@"C:\Code\{solutionName}\{solutionName}.slnx",
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }
}
