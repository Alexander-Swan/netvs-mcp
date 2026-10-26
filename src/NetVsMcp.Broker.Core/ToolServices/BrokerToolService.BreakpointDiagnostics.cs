using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Debug, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "breakpoint_diagnostics", Title = "Inspect Breakpoint Binding Evidence", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads breakpoint binding evidence in design, running, or paused mode without changing breakpoints or execution. Pages at most 100 roots (default 50), up to 20 bound locations per root and 200 per response, exposing bound/pending/disabled/notDebugging/unknown, automation type, counts, process/source locations, and truncation. Also returns up to 32 bounded historical breakpoint error/warning notifications observed in this debugger generation, newest first. Notifications are not correlated to individual roots and are not proof a current breakpoint still has that error; missing notifications never prove error-free binding. Observation may start after an earlier failure. Breakpoint pages are volatile: refresh after edits. Use module_list and debug_source_evidence while paused for symbol/source mismatches. Requires an updated VSIX with no fallback that infers binding from Enabled or a filename.")]
    public Task<ToolResponse<BreakpointDiagnosticsResult>> BreakpointDiagnostics(int offset = 0, int maxItems = 50,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (offset < 0 || maxItems is < 1 or > 100)
            return Task.FromResult(FailWithCode<BreakpointDiagnosticsResult>("offset must be nonnegative and maxItems must be 1–100.", ToolErrorCodes.InvalidRequest));
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.BreakpointDiagnosticsAsync(new(offset, maxItems), ct), cancellationToken);
    }
}
