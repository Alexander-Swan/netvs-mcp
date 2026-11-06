using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_results_page", Title = "Read Retained Test Result Page", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads retained test results by run ID. Returns result rows, nextOffset when more rows exist, and truncation when evidence was dropped. An active Test Explorer run returns its current outcome. Requires an updated VSIX. History retains up to eight runs for 15 minutes. Expired or unknown runs fail explicitly. Avoid rerunning tests merely to recover output. Compact output.")]
    public Task<ToolResponse<TestResultPage>> TestResultsPage(
        [Description("Exact runId from test_run or test_run_and_get_results, up to 200 characters.")] string runId,
        [Description("Zero-based result offset. Pass nextOffset from the preceding page.")] int offset = 0,
        [Description("Maximum results per page, 1 to 100. Defaults to 25. Evidence limits can produce shorter pages.")] int maxItems = 25,
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
