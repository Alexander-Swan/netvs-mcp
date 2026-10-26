namespace NetVsMcp.Contracts;

public sealed record DebugSourceEvidenceRequest(string ExpectedStopId, long ExpectedGeneration, int ExpectedProcessId, int? ThreadId = null);

public sealed record SourceChecksumComparison(string Status, string? Checksum = null, string? TextVersion = null,
    string? Encoding = null, string? Message = null);

public sealed record DebugSourceEvidenceResult(DebuggerStateInfo State, int ProcessId, int? ThreadId,
    string? File, int? Line, int? Column, string ChecksumAlgorithm, IReadOnlyList<string> ExpectedChecksums,
    SourceChecksumComparison Disk, SourceChecksumComparison Editor, string? Message = null);
