namespace NetVsMcp.Contracts;

public enum VsCapability
{
    Editor,
    Navigation,
    Build,
    Debugger,
    Diagnostics,
    Tests,
    ProjectSystem
}

public enum DebuggerMode
{
    Unknown,
    Design,
    Run,
    Break
}

public enum DebugStepKind
{
    Into,
    Over,
    Out
}

public enum SessionHealth
{
    Unknown,
    Connected,
    Stale,
    Disconnected
}

public enum RouteFailureReason
{
    None,
    NoRegisteredSessions,
    SessionNotFound,
    SolutionPathNotFound,
    SolutionNameNotFound,
    Ambiguous
}

public sealed record RoutingTarget(
    string? SessionId = null,
    string? SolutionName = null,
    string? SolutionPath = null);

public sealed record VsSessionInfo(
    string SessionId,
    int ProcessId,
    string? VisualStudioVersion,
    string? Edition,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    DateTimeOffset LastSeenUtc,
    IReadOnlyCollection<VsCapability> Capabilities);

public sealed record VsSessionRegistration(
    string SessionId,
    int ProcessId,
    string? VisualStudioVersion,
    string? Edition,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    IReadOnlyCollection<VsCapability> Capabilities);

public sealed record VsSessionUpdate(
    string SessionId,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    IReadOnlyCollection<VsCapability>? Capabilities = null);

public sealed record VsSessionStatus(
    VsSessionInfo Session,
    SessionHealth Health,
    TimeSpan Age);

public sealed class BuildSolutionRequest
{
    public bool WaitForBuildToFinish { get; set; }
}

public sealed record BuildSolutionResult(
    BuildStatusInfo Status,
    int LastBuildInfo);

public sealed record BuildStatusInfo(
    string State,
    int LastBuildInfo);

public sealed class ErrorListRequest
{
    public bool IncludeWarnings { get; set; } = true;

    public int MaxItems { get; set; } = 200;
}

public sealed record ErrorListResult(
    IReadOnlyCollection<ErrorListItemInfo> Items);

public sealed record ErrorListItemInfo(
    string? Description,
    string? File,
    int Line,
    int Column,
    string Level,
    string? Project);

public sealed class OutputReadRequest
{
    public string? PaneName { get; set; }

    public int MaxChars { get; set; } = 20000;
}

public sealed record OutputReadResult(
    string? PaneName,
    string Text,
    bool Truncated);

public sealed record DebuggerStateInfo(string Mode);

public sealed class DebugStepRequest
{
    public DebugStepKind StepKind { get; set; } = DebugStepKind.Over;
}

public sealed class BreakpointSetRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; } = 1;

    public string? Condition { get; set; }
}

public sealed class BreakpointRemoveRequest
{
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
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointListResult(
    IReadOnlyCollection<BreakpointInfo> Breakpoints);

public sealed record BreakpointInfo(
    string? Name,
    string? File,
    int Line,
    int Column,
    string? FunctionName,
    string? Condition,
    bool Enabled);

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
    bool IsValidValue);

public sealed record BrokerStatus(
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    DateTimeOffset StartedUtc,
    string Version,
    IReadOnlyCollection<VsSessionStatus> Sessions);

public sealed record BrokerToolDescriptor(
    string Name,
    string Description,
    bool RequiresVisualStudioSession);

public sealed record BrokerCapabilities(
    string McpEndpoint,
    IReadOnlyCollection<BrokerToolDescriptor> Tools,
    IReadOnlyCollection<VsCapability> VisualStudioCapabilities);

public sealed record RouteResult(
    bool Success,
    VsSessionInfo? Session,
    RouteFailureReason FailureReason,
    string? Message,
    IReadOnlyCollection<VsSessionInfo> Candidates)
{
    public static RouteResult Found(VsSessionInfo session) =>
        new(true, session, RouteFailureReason.None, null, Array.Empty<VsSessionInfo>());

    public static RouteResult Failed(
        RouteFailureReason reason,
        string message,
        IReadOnlyCollection<VsSessionInfo>? candidates = null) =>
        new(false, null, reason, message, candidates ?? Array.Empty<VsSessionInfo>());
}

public sealed record ToolResponse(
    bool Success,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse Ok(string? message = null) => new(true, message);

    public static ToolResponse Fail(string message) => new(false, message);
}

public sealed record ToolResponse<T>(
    bool Success,
    T? Value,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse<T> Ok(T value, string? message = null) => new(true, value, message);

    public static ToolResponse<T> Fail(string message) => new(false, default, message);
}

public interface IBrokerRegistrationRpc
{
    Task<ToolResponse> RegisterAsync(VsSessionRegistration registration, CancellationToken cancellationToken);

    Task<ToolResponse> UpdateAsync(VsSessionUpdate update, CancellationToken cancellationToken);

    Task<ToolResponse> HeartbeatAsync(string sessionId, CancellationToken cancellationToken);

    Task<ToolResponse> UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}

public interface IVisualStudioSessionRpc
{
    Task<ToolResponse<VsSessionInfo>> GetStatusAsync(CancellationToken cancellationToken);

    Task<ToolResponse<string?>> GetActiveDocumentAsync(CancellationToken cancellationToken);

    Task<ToolResponse<IReadOnlyCollection<string>>> ListDocumentSymbolsAsync(
        string documentPath,
        CancellationToken cancellationToken);

    Task<BuildSolutionResult> BuildSolutionAsync(
        BuildSolutionRequest request,
        CancellationToken cancellationToken);

    Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken);

    Task<ErrorListResult> ErrorsListAsync(
        ErrorListRequest request,
        CancellationToken cancellationToken);

    Task<OutputReadResult> OutputReadAsync(
        OutputReadRequest request,
        CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken);

    Task<DebuggerStateInfo> DebugStepAsync(
        DebugStepRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointInfo> BreakpointSetAsync(
        BreakpointSetRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken);

    Task<BreakpointRemoveResult> BreakpointRemoveAsync(
        BreakpointRemoveRequest request,
        CancellationToken cancellationToken);

    Task<BreakpointEnableResult> BreakpointEnableAsync(
        BreakpointEnableRequest request,
        CancellationToken cancellationToken);

    Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken);

    Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken);

    Task<EvaluateExpressionResult> DebugEvaluateAsync(
        EvaluateExpressionRequest request,
        CancellationToken cancellationToken);
}
