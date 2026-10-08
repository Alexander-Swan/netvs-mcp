namespace NetVsMcp.Contracts;

public static class BrokerEventTypes
{
    public const string BuildStarted = "build_started";
    public const string BuildCompleted = "build_completed";
    public const string DebuggerModeChanged = "debugger_mode_changed";
    public const string DebuggerBreak = "debugger_break";
    public const string DebuggerStopped = "debugger_stopped";
    public const string TestRunStarted = "test_run_started";
    public const string TestRunCompleted = "test_run_completed";
    public const string BreakpointChanged = "breakpoint_changed";
}

public sealed record BrokerEvent(
    long Sequence,
    string SessionId,
    string Type,
    DateTimeOffset TimestampUtc,
    string? Summary,
    IReadOnlyDictionary<string, string>? Data);

public sealed record BrokerEventNotification(
    string SessionId,
    string Type,
    string? Summary,
    IReadOnlyDictionary<string, string>? Data,
    DateTimeOffset? TimestampUtc = null);

public sealed record BrokerEventsResult(
    IReadOnlyCollection<BrokerEvent> Events,
    bool TimedOut,
    long NextSequence);
