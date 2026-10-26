using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Tests;

public sealed partial class BrokerToolServiceTests
{
    private static DebuggerStateInfo ReadyPause(string id = "pause") => new("dbgBreakMode", Outcome: "stopped", Generation: 1, StopId: id);

    [Fact]
    public async Task WaitRetriesOnlyFrameReadsUntilInspectionBecomesAvailable()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var reads = 0;
        var state = ReadyPause();
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(state),
            FrameInspectionHandler = (request, _) =>
            {
                Assert.Equal("pause", request.ExpectedStopId);
                if (++reads < 3) throw new TypedRemoteFailure(VsRpcErrorCodes.InspectionUnavailable);
                return Task.FromResult(new DebugFrameInspectionResult(state, null, [], null,
                    new CallStackResult(state, [new("Fixture.Test", "Test.cs", 10, 1)], false)));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 2, sessionId: "vs-1");
        Assert.True(response.Success, response.Message);
        Assert.False(response.Value!.TimedOut);
        Assert.Equal("Fixture.Test", Assert.Single(response.Value.CallStack!.Frames).FunctionName);
        Assert.Equal(3, reads);
        Assert.Equal(0, session.DebugExecutionCommands);
    }

    [Fact]
    public async Task WaitDeadlinePreservesPausedStateAndExplicitUnavailableInspection()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(ReadyPause()),
            FrameInspectionHandler = (_, _) => throw new TypedRemoteFailure(VsRpcErrorCodes.InspectionUnavailable)
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 1, sessionId: "vs-1");
        Assert.True(response.Success, response.Message);
        Assert.True(response.Value!.TimedOut);
        Assert.Equal("dbgBreakMode", response.Value.State.Mode);
        Assert.Equal("pause", response.Value.State.StopId);
        Assert.Equal("unavailable", response.Value.InspectionStatus);
        Assert.NotEmpty(response.Value.InspectionMessage!);
        Assert.Null(response.Value.CallStack);
        Assert.Equal(0, session.DebugExecutionCommands);
    }

    [Fact]
    public async Task WaitReadinessDoesNotRefreshAnExplicitCallerPauseGuard()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var reads = 0;
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(ReadyPause(reads == 0 ? "pause" : "new-pause")),
            FrameInspectionHandler = (request, _) =>
            {
                Assert.Equal("pause", request.ExpectedStopId);
                throw new TypedRemoteFailure(++reads == 1 ? VsRpcErrorCodes.InspectionUnavailable : VsRpcErrorCodes.StaleDebuggerContext);
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 2, sessionId: "vs-1", expectedStopId: "pause", expectedGeneration: 1);
        Assert.False(response.Success);
        Assert.Equal(ToolErrorCodes.StaleDebuggerContext, response.Metadata!["error_code"]);
        Assert.Equal(2, reads);
        Assert.Equal(0, session.DebugExecutionCommands);
    }

    [Fact]
    public async Task ReadinessWaitCancellationNeverReplaysExecution()
    {
        using var cancellation = new CancellationTokenSource();
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(ReadyPause()),
            FrameInspectionHandler = (_, _) =>
            {
                cancellation.Cancel();
                throw new TypedRemoteFailure(VsRpcErrorCodes.InspectionUnavailable);
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.Tools.DebugWaitForBreak(sessionId: "vs-1", cancellationToken: cancellation.Token));
        Assert.Equal(0, session.DebugExecutionCommands);
    }

    [Fact]
    public async Task UnguardedWaitReconcilesAPauseChangedDuringReadOnlyCollection()
    {
        var runtime = CreateRuntime();
        runtime.Sessions.Register(CreateRegistration("vs-1", "NetVsMcp"));
        var reads = 0;
        var session = new FakeVisualStudioSessionRpc("Editor.cs")
        {
            DebugStatusHandler = _ => Task.FromResult(ReadyPause(reads == 0 ? "pause" : "new-pause")),
            FrameInspectionHandler = (request, _) =>
            {
                if (++reads == 1) throw new TypedRemoteFailure(VsRpcErrorCodes.StaleDebuggerContext);
                Assert.Equal("new-pause", request.ExpectedStopId);
                var state = ReadyPause("new-pause");
                return Task.FromResult(new DebugFrameInspectionResult(state, null, [], null,
                    new CallStackResult(state, [new("Fixture.Test", "Test.cs", 10, 1)], false)));
            }
        };
        runtime.Connections.AddOrUpdate("vs-1", session);
        var response = await runtime.Tools.DebugWaitForBreak(timeoutSeconds: 2, sessionId: "vs-1");
        Assert.True(response.Success, response.Message);
        Assert.Equal("new-pause", response.Value!.State.StopId);
        Assert.Equal(2, reads);
        Assert.Equal(0, session.DebugExecutionCommands);
    }
}
