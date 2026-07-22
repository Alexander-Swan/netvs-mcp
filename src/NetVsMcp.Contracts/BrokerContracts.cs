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
}
