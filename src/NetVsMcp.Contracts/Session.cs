namespace NetVsMcp.Contracts;

/// <summary>Capability families a VS session can advertise support for during registration/heartbeat.</summary>
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

/// <summary>Liveness classification the broker assigns to a registered VS session based on heartbeat recency.</summary>
public enum SessionHealth
{
    Unknown,
    Connected,
    /// <summary>Heartbeats have stopped but the session hasn't been removed yet.</summary>
    Stale,
    Disconnected
}

/// <summary>Why <see cref="RouteResult"/> failed to resolve a target VS session for a tool call.</summary>
public enum RouteFailureReason
{
    None,
    /// <summary>No VS session is currently registered with the broker at all.</summary>
    NoRegisteredSessions,
    SessionNotFound,
    ProcessIdNotFound,
    SolutionPathNotFound,
    SolutionNameNotFound,
    WorkspacePathNotFound,
    /// <summary>More than one candidate session matched the routing target and none could be preferred.</summary>
    Ambiguous
}

/// <summary>Broad classification used to group tools in help/capability listings and audit categorization.</summary>
public enum BrokerToolCategory
{
    /// <summary>Broker-only tool that doesn't route to a VS session (e.g. session selection).</summary>
    Broker,
    Read,
    /// <summary>Mutating tool that stages a change for explicit approval rather than applying it directly.</summary>
    EditPreview,
    /// <summary>Mutating tool that applies a change immediately, bypassing the preview/approve queue.</summary>
    EditDirect,
    Build,
    Debug,
    Project,
    Test,
    Admin
}

public enum BrokerDoctorSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Single source of truth for which MCP HTTP endpoint serves a given tool. The broker splits
/// its tool surface across "/mcp" (the default) and "/mcp-wu" (opt-in debuggee UI/browser
/// automation) to keep the default tool list smaller; every place that needs to know which
/// endpoint a tool lives on (session filtering, get_help/vs_get_capabilities reporting, guide
/// metadata) should go through this instead of re-deriving the prefix rule independently.
/// </summary>
public static class McpEndpointRouting
{
    public const string DefaultEndpointPath = "/mcp";
    public const string WebAutomationEndpointPath = "/mcp-wu";

    public static bool IsWebAutomationTool(string toolName) =>
        toolName.StartsWith("ui_", StringComparison.Ordinal) ||
        toolName.StartsWith("web_", StringComparison.Ordinal);

    public static string ResolveEndpointPath(string toolName) =>
        IsWebAutomationTool(toolName) ? WebAutomationEndpointPath : DefaultEndpointPath;
}

/// <summary>
/// Version of the broker/VSIX registration RPC contract. Only the major version is enforced as a
/// hard mismatch (see <c>BrokerRegistrationRpcService</c>); the minor version is informational.
/// </summary>
public static class VsRpcProtocol
{
    public const string CurrentVersion = "1.1";
    public const int CurrentMajorVersion = 1;
}

/// <summary>Stable machine-readable error codes returned in tool failure responses, for callers that branch on error type rather than message text.</summary>
public static class ToolErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string SessionRoutingFailed = "session_routing_failed";
    public const string SessionNotConnected = "session_not_connected";
    public const string RpcFailure = "rpc_failure";
    public const string ProtocolMismatch = "protocol_mismatch";
    public const string ToolNotImplemented = "tool_not_implemented";
    /// <summary>The VS-side capability service recognized the request but the installed VS/extension version doesn't support it.</summary>
    public const string UnsupportedByVsix = "unsupported_by_vsix";
    public const string VisualStudioError = "visual_studio_error";
    public const string OperationTimedOut = "operation_timed_out";
}

/// <summary>
/// Identifies which VS session a tool call should be routed to. All members are optional; when
/// none are set, the broker falls back to auto-selection heuristics (single session, active window).
/// </summary>
public sealed record RoutingTarget(
    string? SessionId = null,
    string? SolutionName = null,
    string? SolutionPath = null,
    int? ProcessId = null,
    string? WorkspacePath = null,
    string? RootPath = null);

public sealed class ExecuteCommandRequest
{
    /// <summary>DTE command name, e.g. "Edit.Format" or "File.SaveAll".</summary>
    public string CommandName { get; set; } = string.Empty;

    public string? Arguments { get; set; }
}

public sealed record ExecuteCommandResult(
    bool Success,
    string CommandName,
    string? Arguments,
    string? Message);

public sealed record WindowInfo(
    string? Caption,
    string? Kind,
    string? ObjectKind,
    bool IsActive,
    bool IsVisible);

public sealed record WindowListResult(
    IReadOnlyCollection<WindowInfo> Windows);

public sealed class WindowActivateRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

public sealed record WindowActivateResult(
    bool Success,
    string? Message,
    WindowInfo? Window);

public sealed class ToolWindowRequest
{
    public string? Caption { get; set; }

    public string? ObjectKind { get; set; }
}

public sealed record ToolWindowResult(
    bool Success,
    string? Message,
    WindowInfo? Window);

/// <summary>Snapshot of a registered VS session as known to the broker.</summary>
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

public sealed record VsLaunchInstanceResult(
    bool Success,
    string? Message,
    int? ProcessId,
    VsSessionInfo? Session);

/// <summary>Payload a VSIX instance sends when first registering with the broker.</summary>
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
    IReadOnlyCollection<VsCapability> Capabilities,
    /// <summary>Only the major component is validated; see <see cref="VsRpcProtocol"/>.</summary>
    string? ProtocolVersion = VsRpcProtocol.CurrentVersion);

/// <summary>Payload a VSIX instance sends on each heartbeat to refresh its session state. <see cref="Capabilities"/> null means "unchanged".</summary>
public sealed record VsSessionUpdate(
    string SessionId,
    string? SolutionName,
    string? SolutionPath,
    string? ActiveDocument,
    DebuggerMode DebuggerMode,
    bool IsActiveWindow,
    IReadOnlyCollection<VsCapability>? Capabilities = null,
    string? ProtocolVersion = VsRpcProtocol.CurrentVersion);

public sealed record VsSessionStatus(
    VsSessionInfo Session,
    SessionHealth Health,
    /// <summary>Time since the session's last heartbeat, used to derive <see cref="Health"/>.</summary>
    TimeSpan Age);

/// <summary>Top-level status payload shown in the broker's tray/status UI and returned by <c>get_status</c>.</summary>
public sealed record BrokerStatus(
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    DateTimeOffset StartedUtc,
    string Version,
    IReadOnlyCollection<VsSessionStatus> Sessions);

/// <summary>
/// Hand-maintained metadata entry mirroring one <c>[McpServerTool]</c>-attributed method (see ARCH-2:
/// nothing currently enforces the two stay in sync).
/// </summary>
public sealed record BrokerToolDescriptor(
    string Name,
    string Description,
    /// <summary>False for tools that don't need a routed VS session (e.g. broker-only session management).</summary>
    bool RequiresVisualStudioSession,
    BrokerToolCategory Category = BrokerToolCategory.Read,
    string McpEndpointPath = McpEndpointRouting.DefaultEndpointPath);

public sealed record BrokerCapabilities(
    string McpEndpoint,
    IReadOnlyCollection<BrokerToolDescriptor> Tools,
    /// <summary>Union of capabilities across all currently registered sessions, not any single session's capabilities.</summary>
    IReadOnlyCollection<VsCapability> VisualStudioCapabilities);

public sealed record BrokerDoctorCheck(
    string Name,
    BrokerDoctorSeverity Severity,
    bool Passed,
    string Message);

public sealed record BrokerDoctorResult(
    bool Healthy,
    string Summary,
    BrokerStatus Status,
    IReadOnlyCollection<BrokerDoctorCheck> Checks);

public sealed record BestPracticeGuideFileInfo(
    string Path,
    string ResourceUri,
    string MimeType);

/// <summary>
/// Which MCP HTTP endpoint(s) the tools a guide covers are actually served from. Most guides
/// cover only default-endpoint tools ("*" -> "/mcp"); a guide can list more than one entry when
/// it spans tool families that live on different endpoints (see McpEndpointRouting), so an
/// agent reading the guide catalog can tell up front whether it needs a second MCP server
/// connection before any of the guide's tools will resolve.
/// </summary>
public sealed record BestPracticeGuideEndpointInfo(
    string ToolNamePattern,
    string McpEndpointPath);

public sealed record BestPracticeGuideInfo(
    string Name,
    string Description,
    string PrimaryResourceUri,
    IReadOnlyCollection<BestPracticeGuideFileInfo> Files,
    IReadOnlyCollection<BestPracticeGuideEndpointInfo> Endpoints);

public sealed record BestPracticeGuideContent(
    string Guide,
    string File,
    string ResourceUri,
    string MimeType,
    string Text);

public sealed record BestPracticeGuideToolResult(
    string Message,
    IReadOnlyCollection<BestPracticeGuideInfo> Guides,
    /// <summary>The specific file's content when a single file was requested; null when listing guides/files only.</summary>
    BestPracticeGuideContent? Content);

/// <summary>Result of resolving a <see cref="RoutingTarget"/> to a concrete VS session.</summary>
public sealed record RouteResult(
    bool Success,
    VsSessionInfo? Session,
    RouteFailureReason FailureReason,
    string? Message,
    /// <summary>Populated only for <see cref="RouteFailureReason.Ambiguous"/>, listing every session that matched.</summary>
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

/// <summary>Generic success/failure envelope used by broker&lt;-&gt;VSIX RPC methods that don't return a payload.</summary>
public sealed record ToolResponse(
    bool Success,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse Ok(string? message = null) => new(true, message);

    public static ToolResponse Fail(string message) => new(false, message);
}

/// <summary>Generic success/failure envelope carrying a typed payload; <see cref="Value"/> is default when <see cref="Success"/> is false.</summary>
public sealed record ToolResponse<T>(
    bool Success,
    T? Value,
    string? Message = null,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static ToolResponse<T> Ok(T value, string? message = null) => new(true, value, message);

    public static ToolResponse<T> Fail(string message) => new(false, default, message);
}

public sealed record BrokerLogEntry(
    string Path,
    string Name,
    DateTimeOffset LastWriteUtc,
    long Length,
    string Text,
    /// <summary>True if <see cref="Text"/> was cut off by a max-size limit rather than being the whole file.</summary>
    bool Truncated);

public sealed record BrokerLogResult(
    string LogsDirectory,
    IReadOnlyCollection<BrokerLogEntry> Files);

/// <summary>RPC surface the VSIX calls on the broker to register/refresh/unregister its session (see <c>NamedPipeBrokerConnectionFactory</c>).</summary>
public interface IBrokerRegistrationRpc
{
    Task<ToolResponse> RegisterAsync(VsSessionRegistration registration, CancellationToken cancellationToken);

    Task<ToolResponse> UpdateAsync(VsSessionUpdate update, CancellationToken cancellationToken);

    Task<ToolResponse> HeartbeatAsync(string sessionId, CancellationToken cancellationToken);

    Task<ToolResponse> UnregisterAsync(string sessionId, CancellationToken cancellationToken);
}
