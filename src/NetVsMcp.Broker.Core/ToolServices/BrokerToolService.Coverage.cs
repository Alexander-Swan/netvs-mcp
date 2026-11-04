using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_coverage_run", Title = "Collect Test Coverage", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Runs one explicit VSTest project with its installed XPlat Code Coverage collector. Requires updated VSIX and coverlet.collector; installs nothing. Returns run-bound line coverage, runner outcome, and the first file page. Missing reports are unavailable, not zero coverage. Read retained files using test_coverage_results with exact runId. Coverage does not imply tests passed. Output: standard reduced MCP JSON without success/value wrappers. Coverage paths preserve exact retained-history keys; pass the returned path to test_coverage_file. Call failures set isError and retain an error message/recovery metadata. Coverage uses covered/total counts and line-to-hit-count objects. Run IDs and pagination remain explicit.")]
    public async Task<ToolResponse<CoverageResultPage>> TestCoverageRun(string projectName, string? filter = null,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName)) return FailWithCode<CoverageResultPage>("projectName is required.", ToolErrorCodes.InvalidRequest);
        var response = await DispatchValueAsync(sessionId, solutionName, solutionPath,
            async (connection, ct) =>
            {
                var capabilities = await connection.SessionCapabilitiesAsync(ct);
                if (!capabilities.Features.Any(f => f.Name == "coverage" && f.Status == "requiresCollector"))
                    throw new NotSupportedException("This VSIX does not support run-bound coverage collection. Update it before running coverage; no test run was started.");
                return await connection.TestRunAsync(new TestRunRequest { ProjectName = projectName, Filter = filter, CollectCoverage = true }, ct);
            }, cancellationToken);
        return CoveragePage(response, 0, 10);
    }

    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_coverage_results", Title = "Read Retained Coverage", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads run-bound line coverage without rerunning tests. Pass exact runId, zero-based file offset, and maxFiles (1\u201350). Reports are bounded to 1000 files/50000 lines; truncation is explicit. Retention follows test history (eight runs/15 minutes, VSIX lifetime). Output: standard reduced MCP JSON without success/value wrappers. Coverage paths preserve exact retained-history keys; pass the returned path to test_coverage_file. Call failures set isError and retain an error message/recovery metadata. Coverage uses covered/total counts and line-to-hit-count objects. Run IDs and pagination remain explicit.")]
    public async Task<ToolResponse<CoverageResultPage>> TestCoverageResults(string runId, int offset = 0, int maxFiles = 10,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || offset < 0 || maxFiles is < 1 or > 50)
            return FailWithCode<CoverageResultPage>("runId, nonnegative offset, and maxFiles 1–50 are required.", ToolErrorCodes.InvalidRequest);
        var response = await TestResults(runId, sessionId, solutionName, solutionPath, cancellationToken);
        return CoveragePage(response, offset, maxFiles);
    }

    private static ToolResponse<CoverageResultPage> CoveragePage(ToolResponse<TestOperationResult> response, int offset, int maxFiles)
    {
        if (!response.Success || response.Value is null) return new(false, null, response.Message, response.Metadata);
        var run = response.Value;
        var report = run.Coverage;
        if (report is null) return FailWithCode<CoverageResultPage>($"Coverage evidence is unavailable (runId={run.RunId}, backend={run.Backend}). Update the VSIX and collect coverage for this run. {run.Message}", ToolErrorCodes.FeatureUnavailable);
        if (offset > report.Files.Count) return FailWithCode<CoverageResultPage>("offset exceeds retained files.", ToolErrorCodes.InvalidRequest);
        var page = report.Files.Skip(offset).Take(maxFiles).ToArray();
        return ToolResponse<CoverageResultPage>.Ok(new(report.RunId, run.Outcome ?? "unknown", run.RunnerExitCode,
            report with { Files = page.Select(f => f with { Lines = Array.Empty<CoverageLine>() }).ToArray() },
            offset, report.Files.Count, offset + page.Length < report.Files.Count ? offset + page.Length : null));
    }

    [BrokerToolMetadata(BrokerToolCategory.Test, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "test_coverage_file", Title = "Read Coverage Lines", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Reads bounded line-hit evidence from exact retained runId/path returned by test_coverage_results. offset is zero-based; maxLines is 1\u2013500. Does not read source files or rerun tests. Paths may be relative when source roots are ambiguous. Output: standard reduced MCP JSON without success/value wrappers. Coverage paths preserve exact retained-history keys; pass the returned path to test_coverage_file. Call failures set isError and retain an error message/recovery metadata. Coverage uses covered/total counts and line-to-hit-count objects. Run IDs and pagination remain explicit.")]
    public async Task<ToolResponse<CoverageLinePage>> TestCoverageFile(string runId, string path, int offset = 0, int maxLines = 100,
        string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(path) || offset < 0 || maxLines is < 1 or > 500)
            return FailWithCode<CoverageLinePage>("runId/path, nonnegative offset and maxLines 1–500 are required.", ToolErrorCodes.InvalidRequest);
        var response = await TestResults(runId, sessionId, solutionName, solutionPath, cancellationToken);
        if (!response.Success) return new(false, null, response.Message, response.Metadata);
        var report = response.Value?.Coverage;
        var file = report?.Files.SingleOrDefault(f => string.Equals(f.Path.Replace('\\', '/'), path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (file is null) return FailWithCode<CoverageLinePage>("No retained coverage file matches this runId/path.", ToolErrorCodes.HistoryUnavailable);
        if (offset > file.Lines.Count) return FailWithCode<CoverageLinePage>("offset exceeds retained lines.", ToolErrorCodes.InvalidRequest);
        var lines = file.Lines.Skip(offset).Take(maxLines).ToArray();
        return ToolResponse<CoverageLinePage>.Ok(new(runId, file.Path, offset, file.Lines.Count,
            offset + lines.Length < file.Lines.Count ? offset + lines.Length : null, lines, report!.Truncated));
    }
}
