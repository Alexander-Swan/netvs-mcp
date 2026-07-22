using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

public enum VsSessionDispatchFailureReason
{
    None,
    NoRegisteredSessions,
    SessionNotFound,
    SolutionPathNotFound,
    SolutionNameNotFound,
    AmbiguousTarget,
    StaleSession,
    MissingConnection,
    RpcFailure
}

public sealed record VsSessionDispatchResult<T>(
    bool Success,
    T? Value,
    VsSessionInfo? Session,
    VsSessionDispatchFailureReason FailureReason,
    string? Message,
    IReadOnlyCollection<VsSessionInfo> Candidates)
{
    public static VsSessionDispatchResult<T> Ok(VsSessionInfo session, T value) =>
        new(true, value, session, VsSessionDispatchFailureReason.None, null, Array.Empty<VsSessionInfo>());

    public static VsSessionDispatchResult<T> Failed(
        VsSessionDispatchFailureReason reason,
        string message,
        VsSessionInfo? session = null,
        IReadOnlyCollection<VsSessionInfo>? candidates = null) =>
        new(false, default, session, reason, message, candidates ?? Array.Empty<VsSessionInfo>());
}
