using NetVsMcp.Broker.Services;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed class SessionRegistryTests
{
    [Fact]
    public void Resolve_ReturnsNoRegisteredSessions_WhenRegistryIsEmpty()
    {
        var registry = new SessionRegistry();

        var result = registry.Resolve(new RoutingTarget());

        Assert.False(result.Success);
        Assert.Equal(RouteFailureReason.NoRegisteredSessions, result.FailureReason);
    }

    [Fact]
    public void Resolve_UsesExplicitSessionIdBeforeOtherTargets()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.sln", isActive: true));
        registry.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.sln", isActive: false));

        var result = registry.Resolve(new RoutingTarget(SessionId: "vs-2", SolutionName: "Shared"));

        Assert.True(result.Success);
        Assert.Equal("vs-2", result.Session?.SessionId);
    }

    [Fact]
    public void Resolve_UsesSolutionPathBeforeSolutionName()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.sln", isActive: true));

        var result = registry.Resolve(new RoutingTarget(
            SolutionName: "Shared",
            SolutionPath: @"C:\Code\One\Shared.sln"));

        Assert.True(result.Success);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    [Fact]
    public void Resolve_MatchesNormalizedSolutionPath()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Shared", @"C:/Code/One/../One/Shared.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Other", @"C:\Code\Other\Other.sln", isActive: true));

        var result = registry.Resolve(new RoutingTarget(SolutionPath: @"c:\code\one\shared.sln"));

        Assert.True(result.Success);
        Assert.Equal("vs-1", result.Session?.SessionId);
        Assert.Equal(@"C:\Code\One\Shared.sln", result.Session?.SolutionPath);
    }

    [Fact]
    public void Update_StoresNormalizedSolutionPath()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Old", @"C:\Code\Old\Old.sln", isActive: true));

        var response = registry.Update(new VsSessionUpdate(
            SessionId: "vs-1",
            SolutionName: "New",
            SolutionPath: @"C:/Code/New/../New/New.slnx",
            ActiveDocument: "Program.cs",
            DebuggerMode: DebuggerMode.Design,
            IsActiveWindow: true));

        Assert.True(response.Success);
        Assert.Equal(@"C:\Code\New\New.slnx", registry.ListSessions().Single().SolutionPath);
    }

    [Fact]
    public void Resolve_UsesSolutionName_WhenPathIsNotProvided()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "One", @"C:\Code\One\One.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Two", @"C:\Code\Two\Two.sln", isActive: true));

        var result = registry.Resolve(new RoutingTarget(SolutionName: "one"));

        Assert.True(result.Success);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    [Fact]
    public void Resolve_UsesSingleActiveSession_WhenNoTargetIsProvided()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "One", @"C:\Code\One\One.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Two", @"C:\Code\Two\Two.sln", isActive: true));

        var result = registry.Resolve(null);

        Assert.True(result.Success);
        Assert.Equal("vs-2", result.Session?.SessionId);
    }

    [Fact]
    public void Resolve_UsesOnlyRegisteredSession_WhenNoTargetOrActiveSessionIsProvided()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "One", @"C:\Code\One\One.sln", isActive: false));

        var result = registry.Resolve(null);

        Assert.True(result.Success);
        Assert.Equal("vs-1", result.Session?.SessionId);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguous_WhenMultipleSessionsHaveNoSingleActiveWindow()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "One", @"C:\Code\One\One.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Two", @"C:\Code\Two\Two.sln", isActive: false));

        var result = registry.Resolve(null);

        Assert.False(result.Success);
        Assert.Equal(RouteFailureReason.Ambiguous, result.FailureReason);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguous_WhenMultipleSessionsMatchSolutionName()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "Shared", @"C:\Code\One\Shared.sln", isActive: false));
        registry.Register(CreateRegistration("vs-2", "Shared", @"C:\Code\Two\Shared.sln", isActive: false));

        var result = registry.Resolve(new RoutingTarget(SolutionName: "Shared"));

        Assert.False(result.Success);
        Assert.Equal(RouteFailureReason.Ambiguous, result.FailureReason);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void ListSessionStatuses_MarksOldSessionsAsStale()
    {
        var registry = new SessionRegistry();
        registry.Register(CreateRegistration("vs-1", "One", @"C:\Code\One\One.sln", isActive: true));

        var status = registry.ListSessionStatuses(DateTimeOffset.UtcNow.AddMinutes(1)).Single();

        Assert.Equal(SessionHealth.Stale, status.Health);
    }

    private static VsSessionRegistration CreateRegistration(
        string sessionId,
        string solutionName,
        string solutionPath,
        bool isActive)
    {
        return new VsSessionRegistration(
            SessionId: sessionId,
            ProcessId: Random.Shared.Next(1000, 9999),
            VisualStudioVersion: "18.0",
            Edition: "Enterprise",
            SolutionName: solutionName,
            SolutionPath: solutionPath,
            ActiveDocument: "Program.cs",
            DebuggerMode.Design,
            IsActiveWindow: isActive,
            Capabilities: [VsCapability.Editor, VsCapability.Navigation]);
    }
}
