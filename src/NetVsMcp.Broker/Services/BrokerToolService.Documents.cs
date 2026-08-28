using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "document_active")]
    [Description("Returns the active document for a routed Visual Studio session.")]
    public async Task<ToolResponse<string?>> DocumentActive(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.GetActiveDocumentAsync(ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(DocumentActive), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "code_document_symbols")]
    [Description("Lists document symbols for a document in a routed Visual Studio session.")]
    public async Task<ToolResponse<IReadOnlyCollection<string>>> CodeDocumentSymbols(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<IReadOnlyCollection<string>>.Fail("Document path is required.");
        }

        var target = CreateTarget(
            sessionId,
            solutionName,
            solutionPath,
            workspacePath: GetInferredWorkspacePath(documentPath, sessionId, solutionName, solutionPath));
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            (connection, ct) => connection.ListDocumentSymbolsAsync(documentPath, ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(CodeDocumentSymbols), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "code_go_to_definition")]
    [Description("Finds and navigates to a symbol definition through a routed Visual Studio session.")]
    public Task<ToolResponse<GoToDefinitionResult>> CodeGoToDefinition(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<GoToDefinitionResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeGoToDefinitionAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "code_find_references")]
    [Description("Finds symbol references through a routed Visual Studio session.")]
    public Task<ToolResponse<FindReferencesResult>> CodeFindReferences(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<FindReferencesResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindReferencesAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "symbol_context")]
    [Description("Returns document text, nearby snippet, definition, and references for a code position.")]
    public Task<ToolResponse<SymbolContextResult>> SymbolContext(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        int contextLines = 4,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<SymbolContextResult>.Fail(validation));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var position = new CodePositionRequest { DocumentPath = documentPath.Trim(), Line = line, Column = column };
                var document = await connection.DocumentReadAsync(new DocumentReadRequest { Path = position.DocumentPath }, ct);
                return new SymbolContextResult(
                    document,
                    await connection.CodeGoToDefinitionAsync(position, ct),
                    await connection.CodeFindReferencesAsync(position, ct),
                    ExtractSnippet(document.Text, line, Math.Max(0, contextLines)));
            },
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(documentPath));
    }
    [McpServerTool(Name = "document_outline")]
    [Description("Returns document symbol outline information.")]
    public async Task<ToolResponse<DocumentOutlineResult>> DocumentOutline(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<DocumentOutlineResult>.Fail("Document path is required.");
        }

        var target = CreateTarget(
            sessionId,
            solutionName,
            solutionPath,
            workspacePath: GetInferredWorkspacePath(documentPath, sessionId, solutionName, solutionPath));
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            async (connection, ct) =>
            {
                var response = await connection.ListDocumentSymbolsAsync(documentPath.Trim(), ct);
                return new DocumentOutlineResult(documentPath.Trim(), response.Value ?? []);
            },
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(DocumentOutline), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "find_implementations")]
    [Description("Returns best-effort implementation lookup status for a code position.")]
    public Task<ToolResponse<FindImplementationsResult>> FindImplementations(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<FindImplementationsResult>.Fail(validation));
        }

        var position = new CodePositionRequest { DocumentPath = documentPath.Trim(), Line = line, Column = column };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindImplementationsAsync(position, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(position.DocumentPath));
    }
    [McpServerTool(Name = "rename_symbol_preview")]
    [Description("Returns safe rename preview status for a code position.")]
    public Task<ToolResponse<RenameSymbolPreviewResult>> RenameSymbolPreview(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string newName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<RenameSymbolPreviewResult>.Fail(validation));
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ToolResponse<RenameSymbolPreviewResult>.Fail("New name is required."));
        }

        var request = new RenameSymbolRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            NewName = newName.Trim()
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeRenameSymbolPreviewAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "rename_symbol_apply")]
    [Description("Applies a Roslyn solution-wide rename for the symbol at a code position through a routed Visual Studio session.")]
    public Task<ToolResponse<RenameSymbolApplyResult>> RenameSymbolApply(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string newName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<RenameSymbolApplyResult>.Fail(validation));
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ToolResponse<RenameSymbolApplyResult>.Fail("New name is required."));
        }

        var request = new RenameSymbolRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            NewName = newName.Trim()
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeRenameSymbolApplyAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "call_hierarchy_get")]
    [Description("Returns the call hierarchy (incoming callers and/or outgoing callees) for the symbol at a code position through a routed Visual Studio session.")]
    public Task<ToolResponse<CallHierarchyResult>> CallHierarchyGet(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        [Description("Direction: incoming, outgoing, or both. Defaults to incoming.")]
        string direction = "incoming",
        [Description("Maximum recursion depth (1-6). Defaults to 3.")]
        int maxDepth = 3,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<CallHierarchyResult>.Fail(validation));
        }

        var request = new CallHierarchyRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            Direction = direction,
            MaxDepth = maxDepth
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CallHierarchyGetAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "code_actions_list")]
    [Description("Lists available code fixes and refactorings (like the VS lightbulb) at a code position or selection through a routed Visual Studio session.")]
    public Task<ToolResponse<CodeActionsListResult>> CodeActionsList(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        [Description("Optional selection end line (1-based). Omit for a single-position lookup.")]
        int? endLine = null,
        [Description("Optional selection end column (1-based). Omit for a single-position lookup.")]
        int? endColumn = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<CodeActionsListResult>.Fail(validation));
        }

        var request = new CodeActionsListRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeActionsListAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "code_actions_apply")]
    [Description("Applies a code fix or refactoring by index (as returned by code_actions_list) through a routed Visual Studio session. Recomputes the action list before applying.")]
    public Task<ToolResponse<CodeActionsApplyResult>> CodeActionsApply(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        [Description("The action index, as returned by code_actions_list.")]
        int index,
        [Description("Optional selection end line (1-based). Must match the code_actions_list call that produced the index.")]
        int? endLine = null,
        [Description("Optional selection end column (1-based). Must match the code_actions_list call that produced the index.")]
        int? endColumn = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<CodeActionsApplyResult>.Fail(validation));
        }

        var request = new CodeActionsApplyRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            EndLine = endLine,
            EndColumn = endColumn,
            Index = index
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeActionsApplyAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.DocumentPath));
    }
    [McpServerTool(Name = "document_read")]
    [Description("Reads a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentReadResult>> DocumentRead(
        [Description(DocumentPathParameterDescription)]
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<DocumentReadResult>.Fail(validation));
        }

        var request = new DocumentReadRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentReadAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.Path));
    }
    [McpServerTool(Name = "document_open")]
    [Description("Opens a document through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> DocumentOpen(
        [Description(DocumentPathParameterDescription)]
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(validation));
        }

        var request = new DocumentOpenRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentOpenAsync(request, ct),
            cancellationToken,
            workspacePath: GetRoutableWorkspacePath(request.Path));
    }
    [McpServerTool(Name = "open_relevant_files")]
    [Description("Opens a set of relevant files in the routed Visual Studio session.")]
    public Task<ToolResponse<OpenRelevantFilesResult>> OpenRelevantFiles(
        [Description(DocumentPathsParameterDescription)]
        string[]? paths = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (paths is null || paths.Length == 0)
        {
            return Task.FromResult(ToolResponse<OpenRelevantFilesResult>.Fail("At least one path is required."));
        }

        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length == 0)
        {
            return Task.FromResult(ToolResponse<OpenRelevantFilesResult>.Fail("At least one path is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var documents = new List<EditorDocumentInfo>();
                foreach (var path in normalizedPaths)
                {
                    documents.Add(await connection.DocumentOpenAsync(new DocumentOpenRequest { Path = path }, ct));
                }

                return new OpenRelevantFilesResult(documents);
            },
            cancellationToken,
            workspacePath: normalizedPaths.Select(GetRoutableWorkspacePath).FirstOrDefault(path => path is not null));
    }
    [McpServerTool(Name = "errors_list")]
    [Description("Lists errors and warnings from a routed Visual Studio session.")]
    public async Task<ToolResponse<ErrorListResult>> ErrorsList(
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return ToolResponse<ErrorListResult>.Fail("Max items must be greater than zero.");
        }

        var request = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ErrorsListAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(ErrorsList), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "diagnostics_for_document")]
    [Description("Filters routed diagnostics to one document path.")]
    public Task<ToolResponse<DiagnosticsForDocumentResult>> DiagnosticsForDocument(
        [Description(DocumentPathParameterDescription)]
        string? documentPath = null,
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(documentPath) is { } validation)
        {
            return Task.FromResult(ToolResponse<DiagnosticsForDocumentResult>.Fail(validation));
        }

        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<DiagnosticsForDocumentResult>.Fail("Max items must be greater than zero."));
        }

        var normalizedPath = documentPath!.Trim();
        var request = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var errors = await connection.ErrorsListAsync(request, ct);
                var items = errors.Items
                    .Where(item => PathsEqual(item.File, normalizedPath))
                    .ToArray();
                return new DiagnosticsForDocumentResult(normalizedPath, items);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "workspace_search")]
    [Description("Searches files under the routed solution root.")]
    public Task<ToolResponse<WorkspaceSearchResult>> WorkspaceSearch(
        string query,
        string filePattern = "*.*",
        string? rootPath = null,
        int maxMatches = 100,
        bool matchCase = false,
        bool wholeWord = false,
        bool useRegex = false,
        int contextLines = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(ToolResponse<WorkspaceSearchResult>.Fail("Query is required."));
        }

        if (maxMatches < 1)
        {
            return Task.FromResult(ToolResponse<WorkspaceSearchResult>.Fail("Max matches must be greater than zero."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var solution = await connection.SolutionInfoAsync(ct);
                var searchRoot = ResolveSearchRoot(rootPath, solution);
                var result = SearchWorkspace(
                    searchRoot,
                    query.Trim(),
                    string.IsNullOrWhiteSpace(filePattern) ? "*.*" : filePattern.Trim(),
                    maxMatches,
                    matchCase,
                    wholeWord,
                    useRegex,
                    contextLines,
                    ct);
                return result;
            },
            cancellationToken,
            rootPath: GetRoutableWorkspacePath(rootPath));
    }
    private static string ResolveSearchRoot(string? rootPath, SolutionInfoResult solution)
    {
        var candidate = NormalizeOptional(rootPath);
        if (candidate is not null && !Path.IsPathRooted(candidate) && !string.IsNullOrWhiteSpace(solution.Path))
        {
            var solutionDirectory = Path.GetDirectoryName(solution.Path);
            if (!string.IsNullOrWhiteSpace(solutionDirectory))
            {
                candidate = Path.Combine(solutionDirectory, candidate);
            }
        }

        if (candidate is null && !string.IsNullOrWhiteSpace(solution.Path))
        {
            candidate = Path.GetDirectoryName(solution.Path);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new DirectoryNotFoundException("A root path or routed solution path is required.");
        }

        var fullPath = Path.GetFullPath(candidate);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Root path '{fullPath}' does not exist.");
        }

        return fullPath;
    }
    private static WorkspaceSearchResult SearchWorkspace(
        string rootPath,
        string query,
        string filePattern,
        int maxMatches,
        bool matchCase,
        bool wholeWord,
        bool useRegex,
        int contextLines,
        CancellationToken cancellationToken)
    {
        var regex = CreateSearchRegex(query, matchCase, wholeWord, useRegex);
        var contextSpan = Math.Max(0, contextLines);
        var matches = new List<WorkspaceSearchMatch>();
        foreach (var file in EnumerateWorkspaceFiles(rootPath, filePattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsProbablyBinaryFile(file))
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!regex.IsMatch(lines[i]))
                {
                    continue;
                }

                var beforeStart = Math.Max(0, i - contextSpan);
                var before = contextSpan == 0 ? Array.Empty<string>() : lines[beforeStart..i];
                var afterEnd = Math.Min(lines.Length, i + 1 + contextSpan);
                var after = contextSpan == 0 ? Array.Empty<string>() : lines[(i + 1)..afterEnd];

                matches.Add(new WorkspaceSearchMatch(file, i + 1, lines[i].Trim(), before, after));
                if (matches.Count >= maxMatches)
                {
                    return new WorkspaceSearchResult(rootPath, matches, true);
                }
            }
        }

        return new WorkspaceSearchResult(rootPath, matches, false);
    }
    private static Regex CreateSearchRegex(string query, bool matchCase, bool wholeWord, bool useRegex)
    {
        var pattern = useRegex ? query : Regex.Escape(query);
        if (wholeWord)
        {
            pattern = $@"\b(?:{pattern})\b";
        }

        var options = RegexOptions.CultureInvariant;
        if (!matchCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, options, TimeSpan.FromSeconds(2));
    }
    private static bool IsProbablyBinaryFile(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            Span<byte> buffer = stackalloc byte[8000];
            var read = stream.Read(buffer);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
    private static IEnumerable<string> EnumerateWorkspaceFiles(string rootPath, string filePattern)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        foreach (var file in Directory.EnumerateFiles(rootPath, filePattern, options))
        {
            yield return file;
        }

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", options))
        {
            var name = Path.GetFileName(directory);
            if (name is ".git" or ".vs" or "bin" or "obj" or "node_modules")
            {
                continue;
            }

            foreach (var file in EnumerateWorkspaceFiles(directory, filePattern))
            {
                yield return file;
            }
        }
    }
    private static string ExtractSnippet(string text, int centerLine, int contextLines)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var start = Math.Max(1, centerLine - contextLines);
        var end = Math.Min(lines.Length, centerLine + contextLines);
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(start, end - start + 1)
                .Select(lineNumber => $"{lineNumber}: {lines[lineNumber - 1]}"));
    }
    [McpServerTool(Name = "diagnostics_binding_errors")]
    [Description("Returns binding diagnostics when a VSIX diagnostics backend is available.")]
    public Task<ToolResponse<AutomationResult>> DiagnosticsBindingErrors(string? target = null, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (timeoutMilliseconds <= 0)
        {
            return Task.FromResult(FailWithCode<AutomationResult>("Timeout must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new AutomationRequest
        {
            ToolName = "diagnostics_binding_errors",
            Target = target,
            TimeoutMilliseconds = timeoutMilliseconds
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.DiagnosticsBindingErrorsAsync(request, ct), cancellationToken);
    }
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
        int contextLines = 0,
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
            MaxResults = maxResults,
            ContextLines = contextLines
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
        int contextLines = 0,
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
            MaxResults = maxResults,
            ContextLines = contextLines
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.FindInFilesAsync(request, ct),
            cancellationToken,
            rootPath: GetRoutableWorkspacePath(rootPath));
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
