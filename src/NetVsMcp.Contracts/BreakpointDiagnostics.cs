namespace NetVsMcp.Contracts;

public sealed record BreakpointDiagnosticsRequest(int Offset = 0, int MaxItems = 50);
public sealed record BoundBreakpointLocation(string? File, int Line, int Column, string? FunctionName, int? ProcessId);
public sealed record BreakpointBindingEvidence(string? Name, string? File, int Line, bool? Enabled,
    string BindingState, string? AutomationType, int? TotalBoundLocations, IReadOnlyList<BoundBreakpointLocation> BoundLocations,
    bool LocationsTruncated, string? Message = null);
public sealed record BreakpointErrorNotification(long Sequence, DateTimeOffset ObservedUtc, long? Generation,
    int? ProcessId, string? EngineId, string ErrorType, string Message, bool MessageTruncated, uint? RawFlags = null);
public sealed record BreakpointDiagnosticsResult(DebuggerStateInfo State, int Offset, int TotalBreakpoints, int? NextOffset,
    IReadOnlyList<BreakpointBindingEvidence> Breakpoints, bool ErrorObservationAvailable,
    IReadOnlyList<BreakpointErrorNotification> ErrorNotifications, bool ErrorNotificationsTruncated, string Message);
