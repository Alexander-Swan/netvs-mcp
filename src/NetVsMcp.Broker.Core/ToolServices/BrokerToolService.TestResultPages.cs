using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_results_page", Title = "Read Retained Test Result Page", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads one exact captured CLI or Test Explorer run without rerunning it; no backend argument is needed because runId identifies the runner. Use the runId from test_run or test_run_and_get_results; offset is zero-based and maxItems is 1\u2013100 (default 25). Follow nextOffset: rich failure evidence can return fewer items under a 65536-character page budget. Reports total/retained counts, retention truncation, adapter/execution IDs, target-scoped case fingerprints, stack traces, and evidence truncation. VSIX retains up to eight runs for 15 minutes, capped at 10000 cases/two million evidence characters per run. Unknown/expired/restarted history fails explicitly; do not repeat tests merely to recover output. Requires an updated extension. Output: standard reduced MCP JSON without success/value wrappers. Source paths are solution-relative when possible; source locations use file:line:column. Call failures set isError and retain an error message/recovery metadata. Returns run metadata/results once, preserving runner exit code, pagination, failure messages, stack traces and truncation.")]
    public Task<ToolResponse<TestResultPage>> TestResultsPage(string runId, int offset = 0, int maxItems = 25,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || runId.Length > 200 || offset < 0 || maxItems is < 1 or > 100)
            return Task.FromResult(FailWithCode<TestResultPage>("runId is required (up to 200 characters), offset must be nonnegative, and maxItems must be 1–100.", ToolErrorCodes.InvalidRequest));
        var request = new TestResultsPageRequest { RunId = runId, Offset = offset, MaxItems = maxItems };
        return DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.TestResultsPageAsync(request, ct), cancellationToken);
    }
}
