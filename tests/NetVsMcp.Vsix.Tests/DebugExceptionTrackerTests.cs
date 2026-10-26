namespace NetVsMcp.Vsix.Tests;

public class DebugExceptionTrackerTests
{
    private static DebuggerStateInfo State(string? stop = "pause", long generation = 1, long command = 2, string reason = "exception") =>
        new("dbgBreakMode", reason, "stopped", generation, stop, command);

    [Fact]
    public void NotificationBelongsOnlyToTheExactExceptionPause()
    {
        var tracker = new DebugExceptionTracker();
        tracker.Capture("CLR", "InvalidOperationException", "Controlled failure", -1, false, 10, 20, State(null));
        tracker.Bind(State());
        var result = tracker.Read(State());
        Assert.NotNull(result);
        Assert.Equal("InvalidOperationException", result!.TypeName);
        Assert.Equal("0xFFFFFFFF", result.Code);
        Assert.Null(result.Handled);
        Assert.Equal(10, result.ProcessId);
        Assert.Null(tracker.Read(State("other-pause")));
        Assert.Null(tracker.Read(State(generation: 2)));
        tracker.Clear();
        Assert.Null(tracker.Read(State()));
    }

    [Theory]
    [InlineData(2, 2, "exception")]
    [InlineData(1, 3, "exception")]
    [InlineData(1, 2, "breakpoint")]
    public void UnrelatedPausesNeverAdoptPendingEvidence(long generation, long command, string reason)
    {
        var tracker = new DebugExceptionTracker();
        tracker.Capture("CLR", "Type", "Message", 1, false, null, null, State(null));
        var next = State(generation: generation, command: command, reason: reason);
        tracker.Bind(next);
        Assert.Null(tracker.Read(next));
        tracker.Bind(State());
        Assert.Null(tracker.Read(State()));
    }

    [Fact]
    public void UnhandledNotificationOverridesThrownWithoutGuessingFutureHandling()
    {
        var tracker = new DebugExceptionTracker();
        tracker.Capture("CLR", "Type", "Message", 1, false, null, null, State(null));
        tracker.Capture("CLR", "Type", "Message", 1, true, null, null, State(null));
        tracker.Bind(State());
        Assert.False(tracker.Read(State())!.Handled);
        Assert.Equal("notHandled", tracker.Read(State())!.NotificationKind);
        tracker.Bind(State("next"));
        Assert.Null(tracker.Read(State("next")));
    }

    [Fact]
    public void NotificationEvidenceHasExplicitSizeLimits()
    {
        var tracker = new DebugExceptionTracker();
        tracker.Capture(new string('c', 1000), new string('t', 2000), new string('m', 10000), 1, false, null, null, State(null), message: new string('m', 10000));
        tracker.Bind(State());
        var evidence = tracker.Read(State())!;
        Assert.True(evidence.EvidenceTruncated);
        Assert.Equal(256, evidence.Category!.Length);
        Assert.Equal(1024, evidence.TypeName!.Length);
        Assert.Equal(4096, evidence.Description!.Length);
        Assert.Equal(4096, evidence.Message!.Length);
    }
}
