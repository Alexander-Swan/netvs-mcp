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

public sealed record PrepareSafeEditResult(
    /// <summary>The document's content as read immediately before staging the edit, for the caller to diff against.</summary>
    DocumentReadResult Original,
    EditPreviewResult Preview);

public sealed record ApplySafeEditAndBuildResult(
    EditDecisionResult Edit,
    BuildSolutionResult Build,
    ErrorListResult Errors);
