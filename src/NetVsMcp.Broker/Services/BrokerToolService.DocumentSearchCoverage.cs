using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "document_list")]
    [Description("Lists open documents in a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentListResult>> DocumentList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DocumentListAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "document_close")]
    [Description("Planned: closes a document in a routed Visual Studio session.")]
    public Task<ToolResponse<UnsupportedToolResult>> DocumentClose(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Documents", "Implement VSIX document close with save/discard policy.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "editor_find")]
    [Description("Finds text in one editor document.")]
    public Task<ToolResponse<TextSearchResult>> EditorFind(
        string query,
        string path = "",
        bool matchCase = false,
        bool wholeWord = false,
        bool useRegex = false,
        int maxResults = 100,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(FailWithCode<TextSearchResult>("Query is required.", ToolErrorCodes.InvalidRequest));
        }

        if (maxResults <= 0)
        {
            return Task.FromResult(FailWithCode<TextSearchResult>("Max results must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new EditorFindRequest
        {
            Path = path,
            Query = query,
            MatchCase = matchCase,
            WholeWord = wholeWord,
            UseRegex = useRegex,
            MaxResults = maxResults
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorFindAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "find_in_files")]
    [Description("Searches files under a Visual Studio solution or root path.")]
    public Task<ToolResponse<TextSearchResult>> FindInFiles(
        string query,
        string? rootPath = null,
        string? filePattern = null,
        bool matchCase = false,
        bool wholeWord = false,
        bool useRegex = false,
        int maxResults = 100,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(FailWithCode<TextSearchResult>("Query is required.", ToolErrorCodes.InvalidRequest));
        }

        if (maxResults <= 0)
        {
            return Task.FromResult(FailWithCode<TextSearchResult>("Max results must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new FindInFilesRequest
        {
            Query = query,
            RootPath = rootPath,
            FilePattern = filePattern,
            MatchCase = matchCase,
            WholeWord = wholeWord,
            UseRegex = useRegex,
            MaxResults = maxResults
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.FindInFilesAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "code_go_to_implementation")]
    [Description("Planned: navigates to implementation for a code position.")]
    public Task<ToolResponse<UnsupportedToolResult>> CodeGoToImplementation(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Navigation", "Implement Roslyn-backed implementation lookup and optional navigation.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "code_workspace_symbols")]
    [Description("Planned: searches workspace symbols.")]
    public Task<ToolResponse<UnsupportedToolResult>> CodeWorkspaceSymbols(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Navigation", "Implement Roslyn workspace symbol search with result limits.", sessionId, solutionName, solutionPath, cancellationToken);

    private Task<ToolResponse<UnsupportedToolResult>> PlannedTool(
        string category,
        string implementationHint,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string toolName = "")
    {
        var mcpToolName = ToMcpToolName(toolName);
        var request = new PlannedToolRequest
        {
            ToolName = mcpToolName,
            Category = category,
            ImplementationHint = implementationHint
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.PlannedToolAsync(request, ct),
            cancellationToken,
            toolName);
    }
}
