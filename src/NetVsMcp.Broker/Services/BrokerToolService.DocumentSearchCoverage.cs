using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "document_list")]
    [Description("Planned: lists open documents in a routed Visual Studio session.")]
    public Task<ToolResponse<UnsupportedToolResult>> DocumentList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Documents", "Implement VSIX document enumeration and dirty/active metadata.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "document_close")]
    [Description("Planned: closes a document in a routed Visual Studio session.")]
    public Task<ToolResponse<UnsupportedToolResult>> DocumentClose(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Documents", "Implement VSIX document close with save/discard policy.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "editor_find")]
    [Description("Planned: finds text in an editor document.")]
    public Task<ToolResponse<UnsupportedToolResult>> EditorFind(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Editor", "Implement text search with case, whole-word, regex, and result limits.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "find_in_files")]
    [Description("Planned: searches files through Visual Studio find-in-files.")]
    public Task<ToolResponse<UnsupportedToolResult>> FindInFiles(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Editor", "Implement solution-scoped find-in-files with bounded results.", sessionId, solutionName, solutionPath, cancellationToken);

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
