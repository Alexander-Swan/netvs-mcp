namespace NetVsMcp.Contracts;

public sealed class DebugRunToRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; } = 1;
    public string ExpectedStopId { get; set; } = string.Empty;
    public long ExpectedGeneration { get; set; }
    public int ExpectedProcessId { get; set; }
    public int TimeoutSeconds { get; set; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(DocumentPath) || DocumentPath.Length > 4096 || Line < 1 || Column < 1 ||
            string.IsNullOrWhiteSpace(ExpectedStopId) || ExpectedStopId.Length > 200 || ExpectedGeneration < 0 ||
            ExpectedProcessId < 1 || TimeoutSeconds is < 1 or > 120)
            throw new ArgumentException("Run-to requires a source path (up to 4096 characters), positive line/column/process ID, current stopId/generation, and timeoutSeconds 1–120.");
    }
}

public sealed record TemporaryBreakpointCleanup(int Created, int Removed, int Preserved,
    IReadOnlyList<string> Failures, bool Complete);

public sealed record DebugRunToResult(DebuggerStateInfo State, string Outcome, string OwnerId,
    string TemporaryBreakpointGroup, string RequestedFile, int RequestedLine, int RequestedColumn,
    string? HitFile, int? HitLine, int? HitColumn, TemporaryBreakpointCleanup Cleanup, string? Message,
    int? ProcessId = null, long? StartedGeneration = null, string? StartedStopId = null, string SourceMatchStatus = "notVerified",
    string BreakpointMode = "temporaryOwned");
