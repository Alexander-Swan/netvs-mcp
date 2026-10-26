namespace NetVsMcp.Contracts;

public sealed class DebugExceptionRequest
{
    public string ExpectedStopId { get; set; } = string.Empty;
    public long ExpectedGeneration { get; set; }
}

public sealed record DebugExceptionEvidence(string? Category, string? TypeName, string? Description,
    string? Code, string NotificationKind, bool? Handled, int? ProcessId, int? ThreadId,
    DateTimeOffset CapturedUtc, bool EvidenceTruncated, string Source = "debuggerExceptionEvent", string? ProcessingState = null,
    string? Message = null);

public sealed record DebugExceptionLocation(string File, int Line, int Column,
    string Source = "throwingThreadTopFrame");

public sealed record DebugExceptionResult(DebuggerStateInfo State, string EvidenceStatus,
    DebugExceptionEvidence? Exception, DebugExceptionLocation? Location, string? Reason);
