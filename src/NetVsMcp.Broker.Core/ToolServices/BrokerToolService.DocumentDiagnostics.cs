using System.ComponentModel;
using ModelContextProtocol.Server;
using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [BrokerToolMetadata(BrokerToolCategory.Read, requiresVisualStudioSession: true)]
    [McpServerTool(Name = "document_diagnostics", Title = "Read Versioned Compiler Diagnostics", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Computes compiler diagnostics for an immutable Roslyn document, including unsaved text. Excludes analyzers and build Error List entries. Returns severity groups and version/paging evidence. On stale_document, start a fresh diagnostics query. Requires an updated VSIX. Compact output.")]
    public async Task<ToolResponse<DocumentDiagnosticsResult>> DocumentDiagnostics(
        [Description("Path to the document to analyze.")] string path,
        [Description("Pass the first diagnostics page's textVersion on subsequent pages to reject changed text.")] string? expectedTextVersion = null,
        [Description("Project name or path to disambiguate a linked document.")] string? projectName = null,
        [Description("Include warnings. Defaults to true.")] bool includeWarnings = true,
        [Description("Zero-based offset. Use nextOffset from the preceding page.")] int offset = 0,
        [Description("Maximum diagnostics per page, 1 to 200. Defaults to 100.")] int maxItems = 100,
        string? sessionId = null, string? solutionName = null,
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
                ["nextActions"] = "Request a fresh first diagnostics page. Pass its textVersion as expectedTextVersion on subsequent pages. Avoid mutations based on stale diagnostics."
            } };
        return response;
    }
}
