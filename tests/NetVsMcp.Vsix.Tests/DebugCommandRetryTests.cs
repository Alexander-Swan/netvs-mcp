using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix.Tests;

public class DebugCommandRetryTests
{
    private static COMException Unavailable() => new("Unable to execute method at this time.", DebugCommandRetry.MethodUnavailable);
    private static Task NoDelay(CancellationToken _) => Task.CompletedTask;

    [Fact]
    public async Task TransientRejectionRetriesUntilAcceptedWithoutReplayingSuccess()
    {
        var attempts = 0;
        var delays = 0;
        await DebugCommandRetry.ExecuteAsync(_ =>
        {
            if (++attempts < 3) throw Unavailable();
            return Task.CompletedTask;
        }, CancellationToken.None, _ => { delays++; return Task.CompletedTask; });
        Assert.Equal(3, attempts);
        Assert.Equal(2, delays);
    }

    [Fact]
    public async Task PersistentRejectionHasAFiniteAttemptBudget()
    {
        var attempts = 0;
        var rejection = Unavailable();
        var failure = await Assert.ThrowsAsync<COMException>(() => DebugCommandRetry.ExecuteAsync(_ =>
        {
            attempts++;
            throw rejection;
        }, CancellationToken.None, NoDelay));
        Assert.Same(rejection, failure);
        Assert.Equal(DebugCommandRetry.MaxAttempts, attempts);
    }

    [Theory]
    [InlineData(unchecked((int)0x80004005))]
    [InlineData(unchecked((int)0x80004001))]
    public async Task UnrelatedComFailuresAreNotRetried(int errorCode)
    {
        var attempts = 0;
        var rejection = new COMException("unrelated", errorCode);
        var failure = await Assert.ThrowsAsync<COMException>(() => DebugCommandRetry.ExecuteAsync(_ =>
        {
            attempts++;
            throw rejection;
        }, CancellationToken.None, NoDelay));
        Assert.Same(rejection, failure);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task CancellationDuringReadinessWaitPreventsAnotherAttempt()
    {
        using var cancel = new CancellationTokenSource();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DebugCommandRetry.ExecuteAsync(_ =>
        {
            attempts++;
            throw Unavailable();
        }, cancel.Token, ct => { cancel.Cancel(); return Task.FromCanceled(ct); }));
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData("resume")]
    [InlineData("fastStep")]
    [InlineData("delayedResume")]
    [InlineData("newCommand")]
    public async Task ChangedContextPreventsReissuingARejectedCommand(string change)
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        tracker.Begin();
        var sequence = tracker.ObservedState().ExecutionSequence;
        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => DebugCommandRetry.ExecuteAsync(_ =>
        {
            if (!tracker.CanRetryCommand(sequence)) throw new InvalidOperationException("Context changed");
            attempts++;
            throw Unavailable();
        }, CancellationToken.None, _ =>
        {
            switch (change)
            {
                case "resume": tracker.Record("dbgRunMode"); break;
                case "fastStep": tracker.Record("dbgBreakMode", "stepCompleted"); break;
                case "delayedResume": tracker.Record("dbgRunMode", liveMode: "dbgBreakMode"); break;
                case "newCommand": tracker.Begin(); break;
            }
            return Task.CompletedTask;
        }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public void AbortedRetryAfterADelayedEventDoesNotLeaveTheCommandPending()
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        tracker.Begin();
        var sequence = tracker.ObservedState().ExecutionSequence;
        tracker.Record("dbgRunMode", liveMode: "dbgBreakMode");
        Assert.False(tracker.CanRetryCommand(sequence));
        tracker.RejectCommand("dbgBreakMode", sequence);
        Assert.Equal("unknown", tracker.ObservedState().Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AbortedRetryPreservesANewerCommandOrCompletedStop(bool newerCommand)
    {
        var tracker = new DebugExecutionTracker();
        tracker.Initialize("dbgBreakMode");
        tracker.Begin();
        var sequence = tracker.ObservedState().ExecutionSequence;
        if (newerCommand) tracker.Begin();
        else tracker.Record("dbgBreakMode", "stepCompleted");
        var before = tracker.ObservedState();
        tracker.RejectCommand("dbgBreakMode", sequence);
        var after = tracker.ObservedState();
        Assert.Equal(before.Outcome, after.Outcome);
        Assert.Equal(before.StopId, after.StopId);
        Assert.Equal(before.ExecutionSequence, after.ExecutionSequence);
    }
}
