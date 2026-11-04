using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "debug_run_to", Title = "Run To Source Location With Owned Cleanup", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("From an exact current pause, continues once to a source location with bounded waiting (timeoutSeconds 1\u2013120). Creates process-filtered temporary breakpoints and removes only returned owned handles, or reuses an existing enabled/unconditional stopping breakpoint without modifying it and verifies the actual stopped source position. Existing conditional/disabled/counted/differently filtered breakpoints are never reconfigured. Requires expectedStopId/generation and the single debugged process's expectedProcessId. Check outcome and cleanup.complete: reachedLocation, alreadyAtLocation (no execution), boundAtDifferentLocation, stoppedElsewhere, exited, timedOut, waitCanceled, setupFailed, contextChanged, or outcomeUnknown. Column 1 selects a line; other columns require an exact binding match. Source checksums are not verified (sourceMatchStatus=notVerified). Preserves an earlier unrelated stop and developer-modified ownership tags. Timeouts/cancellation never stop the target or replay execution. Returns ownerId/group, actual hit location, current state, and explicit cleanup failures. Requires an updated VSIX; no fallback to broad breakpoint removal. Output: standard reduced MCP JSON without success/value wrappers. Source paths are solution-relative when possible; source locations use file:line:column. Call failures set isError and retain an error message/recovery metadata. Returns observed outcome/state, process/thread identity when available, requestedLocation/hitLocation and owned cleanup evidence. Locations remain distinct.")]
    public Task<ToolResponse<DebugRunToResult>> DebugRunTo(string documentPath, int line, string expectedStopId,
        long expectedGeneration, int expectedProcessId, int column = 1, int timeoutSeconds = 30,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new DebugRunToRequest { DocumentPath = documentPath, Line = line, Column = column,
            ExpectedStopId = expectedStopId, ExpectedGeneration = expectedGeneration, ExpectedProcessId = expectedProcessId,
            TimeoutSeconds = timeoutSeconds };
        try { request.Validate(); }
        catch (ArgumentException ex) { return Task.FromResult(FailWithCode<DebugRunToResult>(ex.Message, ToolErrorCodes.InvalidRequest)); }
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DebugRunToAsync(request, ct), cancellationToken);
    }
}
