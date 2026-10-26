namespace NetVsMcp.Contracts;

/// <summary>Identity is valid only for the returned pause; missing optional engine fields are not inferred.</summary>
public sealed record DebugInspectionContext(string StopId, long Generation, int? ProcessId, int ThreadId,
    int? FrameIndex, uint? FrameDepth, string? FunctionName, string? File = null, int? Line = null);
