using System;
using System.Threading.Tasks;

// Pure event-source tests: these tasks have no JoinableTaskFactory dependency.
#pragma warning disable VSTHRD003

namespace NetVsMcp.Vsix.Tests;

public class BuildExecutionTrackerTests
{
    [Fact]
    public async Task OldCompletionCannotCompleteANewBuild()
    {
        var tracker = new BuildExecutionTracker();
        var ticket = tracker.Begin("Debug", "Any CPU", null, "solution", "build");
        tracker.ObserveDone("solution", "build", 9);
        Assert.False(ticket.Completion.Task.IsCompleted);
        tracker.ObserveBegin("solution", "build");
        tracker.ObserveDone("solution", "build", 2);
        Assert.Equal(2, await ticket.Completion.Task);
        Assert.Equal(ticket.Id, tracker.Current!.Id);
        Assert.Equal("Debug", ticket.Configuration);
        Assert.NotNull(ticket.Completed);
    }

    [Fact]
    public void InFlightBuildRejectsDuplicateCommands()
    {
        var tracker = new BuildExecutionTracker();
        var ticket = tracker.Begin("Debug", "Any CPU", "project", "project", "build");
        Assert.Throws<InvalidOperationException>(() => tracker.Begin("Release", "x64", null, "solution", "build"));
        tracker.Abort(ticket);
        Assert.True(ticket.Completion.Task.IsCanceled);
        tracker.Begin("Release", "x64", null, "solution", "build");
    }

    [Fact]
    public async Task InterferingBuildDoesNotBorrowTheRequestedIdentity()
    {
        var tracker = new BuildExecutionTracker();
        var ticket = tracker.Begin("Debug", "Any CPU", null, "solution", "build");
        tracker.ObserveBegin("solution", "clean");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ticket.Completion.Task);
        Assert.Null(tracker.Current);
    }

    [Fact]
    public void DeveloperBuildInvalidatesAnEarlierCompletedStatusIdentity()
    {
        var tracker = new BuildExecutionTracker();
        tracker.Begin("Debug", "Any CPU", null, "solution", "build");
        tracker.ObserveBegin("solution", "build");
        tracker.ObserveDone("solution", "build", 0);
        Assert.NotNull(tracker.Current);
        tracker.ObserveBegin("solution", "build");
        Assert.Null(tracker.Current);
    }
}
