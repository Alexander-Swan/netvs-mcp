using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix.Tests;

public class HotReloadBaselineTests
{
    private static Task NoDelayAsync(CancellationToken _) => Task.CompletedTask;

    [Fact]
    public async Task StartupReadinessFailuresDoNotAbandonTheBaseline()
    {
        var queries = 0;
        var accepted = 0;
        await HotReloadBaseline.InitializeAsync(_ =>
        {
            // The command can remain unavailable beyond the old two-second budget.
            return Task.FromResult(HotReloadBaseline.Attempt(() => true,
                () => ++queries <= 25 ? throw new COMException("Debugger starting") : true,
                () => accepted++));
        }, CancellationToken.None, NoDelayAsync);
        Assert.Equal(26, queries);
        Assert.Equal(1, accepted);
    }

    [Fact]
    public async Task AcceptedRequestIsNeverRepeated()
    {
        var attempts = 0;
        await HotReloadBaseline.InitializeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(HotReloadBaselineAttempt.Accepted);
        }, CancellationToken.None, _ => throw new Exception("Must not delay after acceptance"));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task UnsupportedEngineOrChangedSessionStopsInitialization()
    {
        var attempts = 0;
        await HotReloadBaseline.InitializeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(attempts == 1 ? HotReloadBaselineAttempt.NotReady : HotReloadBaselineAttempt.Unavailable);
        }, CancellationToken.None, NoDelayAsync);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task UnavailableCommandHasABoundedReadinessWait()
    {
        var attempts = 0;
        var delays = 0;
        await HotReloadBaseline.InitializeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(HotReloadBaselineAttempt.NotReady);
        }, CancellationToken.None, _ => { delays++; return Task.CompletedTask; });
        Assert.Equal(HotReloadBaseline.MaxAttempts, attempts);
        Assert.Equal(attempts - 1, delays);
    }

    [Fact]
    public async Task CancellationDoesNotIssueAnotherRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HotReloadBaseline.InitializeAsync(_ =>
        {
            attempts++;
            return Task.FromResult(HotReloadBaselineAttempt.NotReady);
        }, cancellation.Token, ct => { cancellation.Cancel(); return Task.FromCanceled(ct); }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public void OnlyExplicitPreExecutionRejectionsCanBeRetried()
    {
        Assert.True(HotReloadBaseline.IsRejectedBeforeExecution(new COMException("Unavailable", unchecked((int)0x89711007))));
        const string unavailable = "Command \"Debug.ApplyCodeChanges\" is not available.";
        Assert.True(HotReloadBaseline.IsRejectedBeforeExecution(new InvalidOperationException(unavailable)));
        Assert.True(HotReloadBaseline.IsRejectedBeforeExecution(new COMException(unavailable, unchecked((int)0x80004005))));
        Assert.False(HotReloadBaseline.IsRejectedBeforeExecution(new COMException("Operation failed", unchecked((int)0x80004005))));
        Assert.False(HotReloadBaseline.IsRejectedBeforeExecution(new InvalidOperationException("Operation failed")));
    }

    [Fact]
    public async Task RejectedReadinessRaceCanRecoverButUncertainIssueIsNotRepeated()
    {
        var issues = 0;
        await HotReloadBaseline.InitializeAsync(_ => Task.FromResult(HotReloadBaseline.Attempt(() => true,
            () => true, () =>
            {
                if (++issues == 1) throw new InvalidOperationException("Command \"Debug.ApplyCodeChanges\" is not available.");
                throw new COMException("May have accepted request", unchecked((int)0x80004005));
            })), CancellationToken.None, NoDelayAsync);
        Assert.Equal(2, issues);
    }

    [Fact]
    public void ChangedSessionDoesNotQueryOrIssueAgainstTheReplacement()
    {
        Assert.Equal(HotReloadBaselineAttempt.Unavailable, HotReloadBaseline.Attempt(() => false,
            () => throw new Exception("Must not query"), () => throw new Exception("Must not issue")));
    }
}
