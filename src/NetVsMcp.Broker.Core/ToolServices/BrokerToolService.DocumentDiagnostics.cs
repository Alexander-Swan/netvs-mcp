using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "document_diagnostics", Title = "Read Versioned Compiler Diagnostics", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Computes compiler diagnostics from an immutable Roslyn document, including unsaved text. Returns its textVersion (same format as document_read), project/language identity, source=roslynCompiler, current-state check, and pages up to 200 items. This excludes analyzer and build Error List diagnostics. Pass expectedTextVersion from document_read or the first page to reject different text; linked files require unambiguous projectName/project path. A changed workspace returns stale_document with the observed version and requires a fresh query. Requires an updated extension.")]
    public async Task<ToolResponse<DocumentDiagnosticsResult>> DocumentDiagnostics(string path,
        string? expectedTextVersion = null, string? projectName = null, bool includeWarnings = true,
        int offset = 0, int maxItems = 100, string? sessionId = null, string? solutionName = null,
        string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || offset < 0 || maxItems is < 1 or > 200)
            return new(false, null, "path is required; offset must be nonnegative and maxItems must be 1–200.",
                new Dictionary<string, string> { ["error_code"] = ToolErrorCodes.InvalidRequest, ["nextActions"] = "Supply a valid document path and diagnostic page limits." });
        var request = new DocumentDiagnosticsRequest
        {
            Path = path.Trim(), ExpectedTextVersion = expectedTextVersion, ProjectName = projectName,
            IncludeWarnings = includeWarnings, Offset = offset, MaxItems = maxItems
        };
        var response = await DispatchValueAsync(sessionId, solutionName, solutionPath,
            (connection, ct) => connection.DocumentDiagnosticsAsync(request, ct), cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.Path));
        if (response.Success && response.Value is { } value && (!value.VersionMatches || !value.IsCurrent))
            return response with { Success = false, Message = value.Message, Metadata = new Dictionary<string, string>
            {
                ["error_code"] = ToolErrorCodes.StaleDocument, ["textVersion"] = value.TextVersion,
                ["nextActions"] = "Read the current live document and request fresh diagnostics. Use that textVersion on subsequent pages; do not mutate based on stale diagnostics."
            } };
        return response;
    }
}
