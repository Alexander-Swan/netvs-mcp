namespace NetVsMcp.Contracts;

/// <summary>Aggregated point-in-time view of the whole VS session, used by <c>vs_context_snapshot</c> to save an agent multiple round-trips.</summary>
public sealed record VsContextSnapshotResult(
    VsSessionInfo? Session,
    SolutionInfoResult? Solution,
    string? ActiveDocument,
    SelectionInfo? Selection,
    DebuggerStateInfo? Debugger,
    BuildStatusInfo? Build,
    ErrorListResult? Errors,
    PendingEditListResult? PendingEdits);
