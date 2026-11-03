using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix.Tests;

public class DebugCommandExecutionTests
{
    private static Task EnterAsync(CancellationToken _, Action action) { action(); return Task.CompletedTask; }
    private static Task NoDelayAsync(CancellationToken _) => Task.CompletedTask;
    private static COMException Unavailable() => new("Unable to execute method at this time.", DebugCommandRetry.MethodUnavailable);

    [Fact]
    public async Task ModeValidationIssueAndRejectionRunInsideTheRequiredContext()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var inside = false;
        var validations = 0;
        await Assert.ThrowsAsync<COMException>(() => DebugCommandExecution.IssueAsync(tracker,
            async (_, action) =>
            {
                await Task.Yield();
                inside = true;
                try { action(); }
                finally { inside = false; }
            }, () => { Assert.True(inside); return "dbgBreakMode"; },
            () => { Assert.True(inside); throw new COMException("Uncertain", unchecked((int)0x80004005)); },
            false, CancellationToken.None,
            () => { Assert.True(inside); validations++; }));
        Assert.Equal(2, validations);
        Assert.Equal("unknown", tracker.ObservedState().Outcome);
    }

    [Fact]
    public async Task ReadinessRetryCorrelatesOneCommandWithItsFastTargetStop()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var original = tracker.ObservedState();
        var attempts = 0;
        var before = await DebugCommandExecution.IssueAsync(tracker, EnterAsync, () => "dbgBreakMode", () =>
        {
            if (++attempts < 3) throw Unavailable();
            tracker.Record("dbgRunMode");
            tracker.Record("dbgBreakMode", "breakpoint");
        }, false, CancellationToken.None, delay: NoDelayAsync);

        Assert.Equal(3, attempts);
        Assert.Equal(original.ExecutionSequence + 1, tracker.ObservedState().ExecutionSequence);
        Assert.NotEqual(original.StopId, tracker.ObservedState().StopId);
        Assert.True(await tracker.WaitAsync(before, true, TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Theory]
    [InlineData("notification")]
    [InlineData("newCommand")]
    [InlineData("mode")]
    [InlineData("target")]
    public async Task ChangedExecutionOrTargetContextPreventsAnotherIssue(string change)
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var mode = "dbgBreakMode";
        var targetValid = true;
        var attempts = 0;
        DebuggerStateInfo? newer = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => DebugCommandExecution.IssueAsync(
            tracker, EnterAsync, () => mode, () => { attempts++; throw Unavailable(); }, false,
            CancellationToken.None, () =>
            {
                if (!targetValid) throw new InvalidOperationException("Target changed");
            }, _ =>
            {
                switch (change)
                {
                    case "notification": tracker.Record("dbgBreakMode", "stepCompleted"); break;
                    case "newCommand": tracker.Begin(); break;
                    case "mode": mode = "dbgRunMode"; break;
                    case "target": targetValid = false; break;
                }
                newer = tracker.ObservedState();
                return Task.CompletedTask;
            }));
        Assert.Equal(1, attempts);
        if (change is "notification" or "newCommand")
        {
            Assert.Equal(newer!.StopId, tracker.ObservedState().StopId);
            Assert.Equal(newer.Outcome, tracker.ObservedState().Outcome);
            Assert.Equal(newer.ExecutionSequence, tracker.ObservedState().ExecutionSequence);
        }
        else Assert.NotEqual("pending", tracker.ObservedState().Outcome);
    }

    [Fact]
    public async Task ExhaustedRejectionCanBeReconciledWithoutRestoringAnOldStop()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var original = tracker.ObservedState();
        var attempts = 0;
        await Assert.ThrowsAsync<COMException>(() => DebugCommandExecution.IssueAsync(tracker, EnterAsync,
            () => "dbgBreakMode", () => { attempts++; throw Unavailable(); }, false,
            CancellationToken.None, delay: NoDelayAsync));
        Assert.Equal(DebugCommandRetry.MaxAttempts, attempts);
        Assert.Null(tracker.ObservedState().StopId);
        Assert.Equal("unknown", tracker.ObservedState().Outcome);
        var refreshed = tracker.State("dbgBreakMode", refresh: true);
        Assert.NotNull(refreshed.StopId);
        Assert.NotEqual(original.StopId, refreshed.StopId);
        tracker.Validate("dbgBreakMode", refreshed.StopId, refreshed.Generation);
    }

    [Fact]
    public async Task CancellationDuringReadinessWaitClearsOnlyItsPendingCommand()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DebugCommandExecution.IssueAsync(
            tracker, EnterAsync, () => "dbgBreakMode", () => { attempts++; throw Unavailable(); }, false,
            cancellation.Token, delay: ct => { cancellation.Cancel(); return Task.FromCanceled(ct); }));
        Assert.Equal(1, attempts);
        Assert.Equal("unknown", tracker.ObservedState().Outcome);
    }

    [Fact]
    public async Task InvalidInitialTargetLeavesTheOriginalStopAvailable()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var original = tracker.ObservedState();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DebugCommandExecution.IssueAsync(
            tracker, EnterAsync, () => "dbgBreakMode", () => throw new Exception("Must not issue"), false,
            CancellationToken.None, () => throw new InvalidOperationException("Target changed")));
        Assert.Equal(original.StopId, tracker.ObservedState().StopId);
        Assert.Equal(original.ExecutionSequence, tracker.ObservedState().ExecutionSequence);
    }

    [Fact]
    public async Task UncertainExecutionPreservesNewStopAndCleansOnlyOwnedBreakpoints()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        var scope = new OwnedBreakpointScope<object>();
        var owned = new object();
        var edited = new object();
        scope.Add(owned, null, "temporary");
        scope.ConfirmTag(owned);
        scope.Add(edited, null, "temporary");
        scope.ConfirmTag(edited);
        var removed = new System.Collections.Generic.List<object>();
        string? stop = null;
        var attempts = 0;
        try
        {
            await Assert.ThrowsAsync<COMException>(() => DebugCommandExecution.IssueAsync(tracker, EnterAsync,
                () => "dbgBreakMode", () =>
                {
                    attempts++;
                    tracker.Record("dbgRunMode");
                    tracker.Record("dbgBreakMode", "breakpoint");
                    stop = tracker.ObservedState().StopId;
                    throw new COMException("Execution may have resumed", unchecked((int)0x80004005));
                }, false, CancellationToken.None, delay: NoDelayAsync));
        }
        finally
        {
            var cleanup = scope.Cleanup(value => ReferenceEquals(value, edited) ? "user" : "temporary",
                value => removed.Add(value), true);
            Assert.Single(removed);
            Assert.Same(owned, removed[0]);
        }
        Assert.Equal(1, attempts);
        Assert.NotNull(stop);
        Assert.Equal(stop, tracker.ObservedState().StopId);
    }
}
