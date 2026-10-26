using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix.Tests;

public class DebugExecutionTrackerTests
{
    private static DebugExecutionTracker Paused()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        return tracker;
    }

    [Fact]
    public async Task OldBreakModeCannotCompleteAStep()
    {
        var tracker = Paused();
        var old = tracker.State("dbgBreakMode");
        var before = tracker.Begin();
        using var cancel = new CancellationTokenSource();
        var waiting = tracker.WaitAsync(before, true, TimeSpan.FromSeconds(10), cancel.Token);
        Assert.False(waiting.IsCompleted);
        var pending = tracker.State("dbgBreakMode", "stepCompleted");
        Assert.Equal("pending", pending.Outcome);
        Assert.Null(pending.StopId);
        Assert.Null(pending.BreakReason);
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", old.StopId, old.Generation));
        tracker.Record("dbgBreakMode", "stepCompleted"); // Fast step: no sampled run mode.
        Assert.True(await waiting);
        var landed = tracker.State("dbgBreakMode");
        Assert.Equal("stopped", landed.Outcome);
        Assert.NotEqual(old.StopId, landed.StopId);
        Assert.Equal(old.Generation, landed.Generation);
    }

    [Fact]
    public async Task StopDeliveredBeforeWaitIsStillObserved()
    {
        var tracker = Paused();
        var before = tracker.Begin();
        tracker.Record("dbgRunMode");
        tracker.Record("dbgBreakMode", "breakpoint");
        Assert.True(await tracker.WaitAsync(before, true, TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.Equal("breakpoint", tracker.State("dbgBreakMode").BreakReason);
    }

    [Fact]
    public async Task ExitCompletesAWaitingStep()
    {
        var tracker = Paused();
        var before = tracker.Begin();
        tracker.Record("dbgDesignMode");
        Assert.True(await tracker.WaitAsync(before, true, TimeSpan.FromSeconds(1), CancellationToken.None));
        var state = tracker.State("dbgDesignMode");
        Assert.Equal("exited", state.Outcome);
        Assert.Null(state.StopId);
    }

    [Fact]
    public async Task TimeoutDoesNotClaimTheProgramStopped()
    {
        var tracker = Paused();
        var before = tracker.Begin();
        tracker.Record("dbgRunMode");
        Assert.False(await tracker.WaitAsync(before, true, TimeSpan.FromMilliseconds(20), CancellationToken.None));
        var state = tracker.State("dbgRunMode", outcome: "timedOut");
        Assert.Equal("dbgRunMode", state.Mode);
        Assert.Equal("timedOut", state.Outcome);
        Assert.Null(state.StopId);
        tracker.Record("dbgBreakMode", "stepCompleted");
        Assert.Equal("stopped", tracker.State("dbgBreakMode").Outcome);
    }

    [Fact]
    public async Task CancelingWaitDoesNotCancelOrForgetTheCommand()
    {
        var tracker = Paused();
        var before = tracker.Begin();
        using var cancel = new CancellationTokenSource();
        var waiting = tracker.WaitAsync(before, true, TimeSpan.FromSeconds(10), cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal("pending", tracker.State("dbgBreakMode").Outcome);
        tracker.Record("dbgBreakMode", "stepCompleted");
        Assert.True(await tracker.WaitAsync(before, true, TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Fact]
    public void ManualResumeAndPauseInvalidateOldFrameContext()
    {
        var tracker = Paused();
        var old = tracker.State("dbgBreakMode");
        tracker.Record("dbgRunMode");
        tracker.Record("dbgBreakMode", "manualPause");
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", old.StopId, old.Generation));
        var current = tracker.State("dbgBreakMode");
        tracker.Validate("dbgBreakMode", current.StopId, current.Generation);
        Assert.NotEqual(old.StopId, current.StopId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RestartAndReattachRejectPreviousGeneration(bool passesThroughDesign)
    {
        var tracker = Paused();
        var old = tracker.State("dbgBreakMode");
        if (passesThroughDesign) tracker.Record("dbgDesignMode");
        tracker.Begin(newSession: true);
        tracker.Record("dbgBreakMode", "breakpoint");
        var current = tracker.State("dbgBreakMode");
        Assert.True(current.Generation > old.Generation);
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", null, old.Generation));
        Assert.NotEqual(old.StopId, current.StopId);
    }

    [Fact]
    public void DuplicateNotificationDoesNotInvalidateReturnedStop()
    {
        var tracker = Paused();
        tracker.Record("dbgBreakMode", "breakpoint");
        var current = tracker.State("dbgBreakMode");
        tracker.Record("dbgBreakMode", "breakpoint");
        tracker.Validate("dbgBreakMode", current.StopId, current.Generation);
    }

    [Fact]
    public void DelayedRunNotificationInvalidatesContextButStatusCanRefreshWithoutResuming()
    {
        var tracker = Paused();
        tracker.Begin();
        tracker.Record("dbgBreakMode", "stepCompleted");
        var current = tracker.State("dbgBreakMode");
        tracker.Record("dbgRunMode", liveMode: "dbgBreakMode");
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", current.StopId, current.Generation));
        Assert.Equal("unknown", tracker.State("dbgBreakMode").Outcome);
        var refreshed = tracker.State("dbgBreakMode", refresh: true);
        Assert.Equal("stopped", refreshed.Outcome);
        tracker.Validate("dbgBreakMode", refreshed.StopId, refreshed.Generation);
        Assert.NotEqual(current.StopId, refreshed.StopId);
    }

    [Fact]
    public void FastManualResumeAndIdenticalRebreakCannotReuseStopId()
    {
        var tracker = Paused();
        tracker.Record("dbgBreakMode", "breakpoint");
        var old = tracker.State("dbgBreakMode");
        tracker.Record("dbgRunMode", liveMode: "dbgBreakMode");
        tracker.Record("dbgBreakMode", "breakpoint", liveMode: "dbgBreakMode");
        Assert.NotEqual(old.StopId, tracker.State("dbgBreakMode").StopId);
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", old.StopId, old.Generation));
    }

    [Fact]
    public void RejectedCommandDoesNotInventACompletedPause()
    {
        var tracker = Paused();
        var old = tracker.State("dbgBreakMode");
        tracker.Begin();
        tracker.RejectCommand("dbgBreakMode");
        var current = tracker.State("dbgBreakMode");
        Assert.Equal("unknown", current.Outcome);
        Assert.Null(current.StopId);
        Assert.Throws<StaleDebugContextException>(() => tracker.Validate("dbgBreakMode", old.StopId, old.Generation));
    }

    [Fact]
    public void StopIdsCannotBeReusedAcrossVsixLifetimes()
    {
        var first = Paused().State("dbgBreakMode");
        var second = Paused();
        Assert.Throws<StaleDebugContextException>(() => second.Validate("dbgBreakMode", first.StopId, first.Generation));
    }
}
