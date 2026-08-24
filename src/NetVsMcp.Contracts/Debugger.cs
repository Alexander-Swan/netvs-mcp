namespace NetVsMcp.Contracts;

/// <summary>Coarse-grained debugger state, mapped from DTE's <c>dbgDesignMode</c>/<c>dbgRunMode</c>/<c>dbgBreakMode</c>.</summary>
public enum DebuggerMode
{
    /// <summary>Session hasn't reported a mode yet, or the report failed.</summary>
    Unknown,
    Design,
    Run,
    Break
}

/// <summary>Which direction a single debugger step advances execution.</summary>
public enum DebugStepKind
{
    Into,
    Over,
    Out
}

/// <summary>Debugger actions that can be requested as part of a combined "advance" operation.</summary>
public enum DebugAdvanceAction
{
    StepInto,
    StepOver,
    StepOut,
    Continue,
    Break
}

/// <summary>Raw DTE debugger mode label (e.g. "Design", "Run", "Break").</summary>
public sealed record DebuggerStateInfo(string Mode);

public sealed record HotReloadApplyResult(
    bool Success,
    string Message,
    /// <summary>Compile errors that prevented the hot reload edit from applying, if any.</summary>
    IReadOnlyCollection<ErrorListItemInfo> Errors);

public sealed record DebuggedProcessInfo(
    int ProcessId,
    string? Name,
    /// <summary>Debugger transport the process is attached over, e.g. "Default", "SSH", "Docker".</summary>
    string? Transport,
    string? UserName);

public sealed record LocalProcessInfo(
    int ProcessId,
    string? Name,
    string? Transport,
    string? UserName,
    bool IsBeingDebugged);

public sealed record DebuggedProcessListResult(
    IReadOnlyCollection<DebuggedProcessInfo> Processes);

public sealed record LocalProcessListResult(
    IReadOnlyCollection<LocalProcessInfo> Processes);

public sealed class DebugAttachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }

    /// <summary>
    /// Name (or substring) of a Visual Studio debugger transport, e.g. "Default", "SSH", "Docker",
    /// "Windows Subsystem for Linux". When set, the process is looked up on that transport instead
    /// of the local machine. Leave null for a local attach.
    /// </summary>
    public string? Transport { get; set; }

    /// <summary>
    /// Transport-specific connection string (e.g. "host:port" for SSH/remote, a container id for
    /// Docker, or a distro name for WSL). Meaning depends on the selected transport.
    /// </summary>
    public string? TransportQualifier { get; set; }

    /// <summary>
    /// Optional debug engine name to force (e.g. "Managed", "Native"). When omitted, Visual Studio
    /// auto-detects the engine, which is not always reliable for remote attaches.
    /// </summary>
    public string? Engine { get; set; }
}

public sealed record DebugAttachResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process);

public sealed class ProcessDetachRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

public sealed record ProcessDetachResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process,
    DebuggerStateInfo State);

public sealed class ProcessTerminateRequest
{
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
}

public sealed record ProcessTerminateResult(
    bool Success,
    string? Message,
    DebuggedProcessInfo? Process,
    DebuggerStateInfo State);

public sealed class WatchAddRequest
{
    public string Expression { get; set; } = string.Empty;
}

public sealed class WatchRemoveRequest
{
    public string Expression { get; set; } = string.Empty;
}

public sealed record WatchOperationResult(
    /// <summary>False when the Watch window isn't accessible in the current debugger state.</summary>
    bool Supported,
    bool Success,
    string? Message,
    DebugExpressionInfo? Watch);

public sealed record WatchListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugExpressionInfo> Watches);

public sealed record DebugThreadInfo(
    int Id,
    string? Name,
    /// <summary>True if this is the thread the debugger is currently focused on.</summary>
    bool IsCurrent);

public sealed record DebugThreadListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugThreadInfo> Threads);

public sealed class ThreadSwitchRequest
{
    public int ThreadId { get; set; }
}

public sealed class DebugSetVariableRequest
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 5000;
}

public sealed record DebugSetVariableResult(
    bool Success,
    string? Message,
    /// <summary>The variable's value after the assignment, re-evaluated to confirm it took effect.</summary>
    EvaluateExpressionResult? Evaluation);

public sealed class ThreadSetFrozenRequest
{
    public int ThreadId { get; set; }
    /// <summary>True to freeze (suspend) the thread; false to thaw it.</summary>
    public bool Frozen { get; set; }
}

public sealed record ThreadSwitchResult(
    bool Supported,
    bool Success,
    string? Message,
    DebugThreadInfo? Thread);

public sealed record ThreadSetFrozenResult(
    bool Supported,
    bool Success,
    string? Message,
    DebugThreadInfo? Thread,
    bool Frozen);

public sealed class ThreadCallStackRequest
{
    public int ThreadId { get; set; }
}

public sealed record ThreadCallStackResult(
    bool Supported,
    string? Message,
    DebugThreadInfo? Thread,
    IReadOnlyCollection<CallStackFrameInfo> Frames);

public sealed record DebugModuleInfo(
    string? Name,
    string? Path);

public sealed record ModuleListResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugModuleInfo> Modules);

public sealed class ImmediateExecuteRequest
{
    /// <summary>Expression or statement to run in the Immediate window, e.g. "myList.Count".</summary>
    public string Statement { get; set; } = string.Empty;
}

public sealed record ImmediateExecuteResult(
    bool Supported,
    bool Success,
    string? Message,
    string? Output);

public sealed class ExceptionSettingsRequest
{
    /// <summary>Omit to get/set the default ("all exceptions") entry.</summary>
    public string? ExceptionName { get; set; }
    /// <summary>Omit for a get-only request; set to mutate the "break when thrown" flag.</summary>
    public bool? BreakOnThrown { get; set; }
}

public sealed record ExceptionSettingInfo(
    string? GroupName,
    string? Name,
    bool BreakWhenThrown,
    /// <summary>Break only when the exception is unhandled by user code (as opposed to any code).</summary>
    bool BreakWhenUserUnhandled,
    /// <summary>True if this entry was added by the user rather than being a built-in VS entry.</summary>
    bool UserDefined);

public sealed record ExceptionSettingsResult(
    bool Supported,
    bool Success,
    string? Message,
    IReadOnlyCollection<ExceptionSettingInfo>? Settings = null);

public sealed record ParallelStackFrameInfo(
    int ThreadId,
    string? ThreadName,
    string? FunctionName,
    string? File,
    int Line,
    int Column);

public sealed record ParallelStacksResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<ParallelStackFrameInfo> Frames);

public sealed record ParallelWatchResult(
    bool Supported,
    string? Message,
    IReadOnlyCollection<DebugExpressionInfo> Expressions);

public sealed class DebugStepRequest
{
    public DebugStepKind StepKind { get; set; } = DebugStepKind.Over;
}

public sealed class BreakpointSetRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; } = 1;

    /// <summary>Boolean expression; the breakpoint only fires when this evaluates true (or changes, depending on <see cref="HitCountType"/>-adjacent VS semantics).</summary>
    public string? Condition { get; set; }

    /// <summary>"Break" (default), "Print", or "Continue" — what the breakpoint does when hit.</summary>
    public string? Action { get; set; }

    /// <summary>Message template to print when <see cref="Action"/> is "Print"; supports VS's tracepoint substitution syntax.</summary>
    public string? ActionMessage { get; set; }

    /// <summary>When true and <see cref="Action"/> is "Print", execution continues automatically after printing (tracepoint behavior).</summary>
    public bool ContinueAfterAction { get; set; }

    /// <summary>Threshold used with <see cref="HitCountType"/> to decide when the breakpoint should actually break.</summary>
    public int? HitCount { get; set; }

    /// <summary>"equal", "greaterOrEqual", or "multiple" — how <see cref="HitCount"/> is compared against the running hit count.</summary>
    public string? HitCountType { get; set; }

    /// <summary>Name of another breakpoint that must be hit first before this one becomes active.</summary>
    public string? DependsOnBreakpointName { get; set; }

    public string? GroupName { get; set; }
}

public sealed class BreakpointRemoveRequest
{
    /// <summary>Remove by name; if set, takes precedence over the document/line pair.</summary>
    public string? Name { get; set; }

    public string? DocumentPath { get; set; }

    public int Line { get; set; }
}

public sealed record BreakpointRemoveResult(int Removed);

public sealed class BreakpointEnableRequest
{
    public string? Name { get; set; }

    public string? DocumentPath { get; set; }

    public int Line { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed record BreakpointEnableResult(
    int Updated,
    IReadOnlyCollection<BreakpointInfo> Breakpoints,
    DebuggerStateInfo? State = null);

public sealed record BreakpointListResult(
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointInfo(
    string? Name,
    string? File,
    int Line,
    int Column,
    string? FunctionName,
    string? Condition,
    bool Enabled,
    string? Action = null,
    string? ActionMessage = null,
    bool ContinueAfterAction = false,
    int? HitCount = null,
    string? HitCountType = null,
    string? DependsOnBreakpointName = null,
    string? GroupName = null);

public sealed record BreakpointGroupListResult(
    IReadOnlyCollection<string> Groups,
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointGroupOperationResult(
    string GroupName,
    /// <summary>Number of breakpoints found in the group.</summary>
    int Matched,
    /// <summary>Number of breakpoints whose state actually changed (may be less than <see cref="Matched"/> if some were already in the target state).</summary>
    int Updated,
    IReadOnlyCollection<BreakpointInfo> Breakpoints,
    DebuggerStateInfo? State = null);

public sealed record CallStackResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<CallStackFrameInfo> Frames);

public sealed record CallStackFrameInfo(
    string? FunctionName,
    string? File,
    int Line,
    int Column);

public sealed record LocalsResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<DebugExpressionInfo> Locals);

public sealed class EvaluateExpressionRequest
{
    public string Expression { get; set; } = string.Empty;

    public int TimeoutMilliseconds { get; set; } = 5000;
}

public sealed record EvaluateExpressionResult(
    DebuggerStateInfo State,
    DebugExpressionInfo Expression);

public sealed record DebugExpressionInfo(
    string? Name,
    string? Value,
    string? Type,
    /// <summary>False when evaluation failed (e.g. out of scope, threw) — <see cref="Value"/> then holds the error text instead.</summary>
    bool IsValidValue);

/// <summary>
/// A composite debugger snapshot; each section is null when not requested via the tool's include list
/// (see <see cref="UnrecognizedInclude"/> for names that didn't match a known section).
/// </summary>
public sealed record DebugSnapshotResult(
    DebuggerStateInfo State,
    CallStackResult? CallStack,
    LocalsResult? Locals,
    BreakpointListResult? Breakpoints,
    WatchListResult? Watch = null,
    DebugThreadListResult? Threads = null,
    ModuleListResult? Modules = null,
    ParallelStacksResult? ParallelStacks = null,
    ParallelWatchResult? ParallelWatch = null,
    /// <summary>Section names from the request's include list that weren't recognized.</summary>
    IReadOnlyCollection<string>? UnrecognizedInclude = null,
    /// <summary>True if snapshot collection was cut short by an internal time budget.</summary>
    bool TimedOut = false);

public sealed record DebugEvalManyResult(
    DebuggerStateInfo State,
    IReadOnlyCollection<EvaluateExpressionResult> Results);
