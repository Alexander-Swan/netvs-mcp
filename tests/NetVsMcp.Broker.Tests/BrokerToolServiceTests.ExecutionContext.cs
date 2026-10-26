using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    [Fact]
    public async Task DebugEvaluate_ForwardsStopGuardToAtomicInspection()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs");
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugEvaluate("value", sessionId: "vs-1",
            expectedStopId: "pause-12", expectedGeneration: 3);
        Assert.True(response.Success);
        Assert.Equal("pause-12", session.LastFrameInspectionRequest!.ExpectedStopId);
        Assert.Equal(3, session.LastFrameInspectionRequest.ExpectedGeneration);
        Assert.Equal(0, session.LastFrameInspectionRequest.StackFrame); // Preserve selected frame.
        Assert.Equal(["value"], session.LastFrameInspectionRequest.Expressions);
    }

    [Fact]
    public async Task DebugSnapshot_PinsUntargetedLocalsToTheObservedPause()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(new DebuggerStateInfo("dbgBreakMode",
                Outcome: "stopped", Generation: 2, StopId: "pause-7"))
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugSnapshot(include: ["locals"], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("pause-7", session.LastFrameInspectionRequest!.ExpectedStopId);
        Assert.Equal(2, session.LastFrameInspectionRequest.ExpectedGeneration);
        Assert.True(session.LastFrameInspectionRequest.IncludeLocals);
    }

    [Fact]
    public async Task DebugSnapshot_StateOnlyChecksIdentityWithoutRequestingAFrame()
    {
        var runtime = CreateRuntime();
        var stopped = new DebuggerStateInfo("dbgBreakMode", Outcome: "stopped", Generation: 2, StopId: "native-stop");
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(stopped),
            FrameInspectionHandler = (request, _) =>
            {
                Assert.False(request.IncludeLocals);
                Assert.Null(request.CallStack);
                Assert.Null(request.Watch);
                Assert.Empty(request.Expressions);
                return Task.FromResult(new DebugFrameInspectionResult(stopped, null, [], null));
            }
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugSnapshot(include: [], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("native-stop", response.Value!.State.StopId);
        Assert.Null(response.Value.CallStack);
    }

    [Fact]
    public async Task DebugSnapshot_PendingOldBreakDoesNotReadLocals()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(new DebuggerStateInfo("dbgBreakMode", Outcome: "pending"))
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugSnapshot(include: ["locals", "callStack"], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("pending", response.Value!.State.Outcome);
        Assert.Null(response.Value.Locals);
        Assert.Null(response.Value.CallStack);
        Assert.Null(session.LastFrameInspectionRequest);
    }

    [Fact]
    public async Task DebugWaitForBreak_TimeoutPreservesObservedRunningMode()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs") { DebugStatusMode = "dbgRunMode" };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 1, include: ["locals"], sessionId: "vs-1");
        Assert.True(response.Success);
        Assert.Equal("timedOut", response.Value!.State.Outcome);
        Assert.Equal("dbgRunMode", response.Value.State.Mode);
        Assert.True(response.Value.TimedOut);
        Assert.Null(response.Value.Locals);
    }

    [Fact]
    public async Task DebugEvaluate_OlderVsixCannotSilentlyBypassAStopGuard()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            FrameInspectionHandler = (_, _) => throw MissingFrameInspectionMethod()
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugEvaluate("value", sessionId: "vs-1", expectedStopId: "old-pause");
        Assert.False(response.Success);
        Assert.Contains("guard was not bypassed", response.Message);
    }

    [Fact]
    public async Task DebugSnapshot_StaleGuardFailsBeforeIssuingAnAction()
    {
        var runtime = CreateRuntime();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            FrameInspectionHandler = (_, _) => throw new InvalidOperationException("Stale debugger context")
        };
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugSnapshot(action: DebugAdvanceAction.StepOver, sessionId: "vs-1",
            expectedStopId: "previous-pause");
        Assert.False(response.Success);
        Assert.Null(session.LastDebugStepRequest);
    }

    [Fact]
    public async Task DebugGetLocals_RejectsAnEmptyStopGuard()
    {
        var response = await CreateRuntime().Tools.DebugGetLocals(expectedStopId: " ");
        Assert.False(response.Success);
    }
}
