using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

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
    [Description("Closes an open document with save, discard, or no-save policy.")]
    public Task<ToolResponse<DocumentCloseResult>> DocumentClose(
        [Description(DocumentPathParameterDescription)]
        string path = "",
        DocumentClosePolicy policy = DocumentClosePolicy.NoSave,
        bool allowDirtyDiscard = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DocumentCloseRequest
        {
            Path = path,
            Policy = policy,
            AllowDirtyDiscard = allowDirtyDiscard
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentCloseAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_find")]
    [Description("Finds text in one editor document.")]
    public Task<ToolResponse<TextSearchResult>> EditorFind(
        string query,
        [Description(OptionalDocumentPathParameterDescription)]
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
        [Description("Optional search root relative to the solution or an absolute path. Prefer forward slashes, for example src/Project; if using Windows backslashes in JSON, escape them as double backslashes.")]
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
    [Description("Finds implementation locations for a symbol at a code position.")]
    public Task<ToolResponse<FindImplementationsResult>> CodeGoToImplementation(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        int line,
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(FailWithCode<FindImplementationsResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath,
            Line = line,
            Column = column
        };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindImplementationsAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "code_workspace_symbols")]
    [Description("Searches symbols in the live Visual Studio workspace.")]
    public Task<ToolResponse<CodeWorkspaceSymbolsResult>> CodeWorkspaceSymbols(string query, int maxResults = 100, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(FailWithCode<CodeWorkspaceSymbolsResult>("Query is required.", ToolErrorCodes.InvalidRequest));
        }

        if (maxResults <= 0)
        {
            return Task.FromResult(FailWithCode<CodeWorkspaceSymbolsResult>("Max results must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new CodeWorkspaceSymbolsRequest { Query = query, MaxResults = maxResults };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.CodeWorkspaceSymbolsAsync(request, ct), cancellationToken);
    }
}
