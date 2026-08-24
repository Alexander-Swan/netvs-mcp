namespace NetVsMcp.Contracts;

public sealed class CodePositionRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    /// <summary>1-based line number.</summary>
    public int Line { get; set; }

    /// <summary>1-based column number.</summary>
    public int Column { get; set; }
}

public sealed class CodeWorkspaceSymbolsRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 100;
}

public sealed record DocumentSymbolInfo(
    string Name,
    /// <summary>Roslyn symbol kind, e.g. "Method", "Property", "NamedType".</summary>
    string Kind,
    string? File,
    int Line,
    int Column,
    string? ContainingType,
    string? ContainingNamespace);

public sealed record CodeLocationInfo(
    string? File,
    int Line,
    int Column,
    DocumentSymbolInfo Symbol);

public sealed record CodeReferenceInfo(
    string? File,
    int Line,
    int Column,
    /// <summary>True for a reference synthesized by the compiler (e.g. deconstruction, implicit operator) rather than a literal source occurrence.</summary>
    bool IsImplicit,
    DocumentSymbolInfo Symbol);

public sealed record GoToDefinitionResult(
    DocumentSymbolInfo? Symbol,
    IReadOnlyCollection<CodeLocationInfo> Definitions,
    /// <summary>True if VS actually navigated the editor to the (single) definition, vs. just reporting locations.</summary>
    bool Navigated);

public sealed record FindReferencesResult(
    DocumentSymbolInfo? Symbol,
    IReadOnlyCollection<CodeReferenceInfo> References);

public sealed record CodeWorkspaceSymbolsResult(
    string Query,
    int MatchCount,
    bool Truncated,
    IReadOnlyCollection<DocumentSymbolInfo> Symbols);

public sealed class CallHierarchyRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    /// <summary>"incoming", "outgoing", or "both".</summary>
    public string Direction { get; set; } = "incoming";
    public int MaxDepth { get; set; } = 3;
}

public sealed record CallHierarchyNode(
    DocumentSymbolInfo Symbol,
    /// <summary>The specific call expression's location, when known.</summary>
    CodeLocationInfo? CallSite,
    IReadOnlyCollection<CallHierarchyNode> Children,
    /// <summary>True if this node's symbol already appears higher in the same chain (cycle guard).</summary>
    bool IsRecursive,
    /// <summary>True if <see cref="Children"/> was cut off by <see cref="CallHierarchyRequest.MaxDepth"/>.</summary>
    bool Truncated);

public sealed record CallHierarchyResult(
    bool Supported,
    string Message,
    CodePositionRequest Position,
    string Direction,
    DocumentSymbolInfo? Symbol,
    IReadOnlyCollection<CallHierarchyNode> Incoming,
    IReadOnlyCollection<CallHierarchyNode> Outgoing);

public sealed class CodeActionsListRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    /// <summary>Omit for a single-position request; set with <see cref="EndColumn"/> to scope to a range.</summary>
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
}

public sealed class CodeActionsApplyRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
    /// <summary>Index into the list previously returned by <c>code_actions_list</c> for this position.</summary>
    public int Index { get; set; }
}

public sealed record CodeActionInfo(
    int Index,
    string Title,
    /// <summary>Roslyn code-action kind/tag, e.g. "quickfix", "refactor".</summary>
    string Kind,
    string? DiagnosticId,
    string? EquivalenceKey);

public sealed record CodeActionsListResult(
    CodePositionRequest Position,
    IReadOnlyCollection<CodeActionInfo> Actions);

public sealed record CodeActionsApplyResult(
    bool Success,
    string Message,
    string? AppliedTitle,
    /// <summary>Text edits applied across all affected files, not just the requested document; this bypasses the preview/approve queue.</summary>
    IReadOnlyCollection<RenameSymbolChangeInfo> Changes);

public sealed class RenameSymbolRequest
{
    public string DocumentPath { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; }

    public string NewName { get; set; } = string.Empty;
}

public sealed record RenameSymbolChangeInfo(
    string? File,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    string NewText);

public sealed record RenameSymbolPreviewResult(
    bool Supported,
    string Message,
    CodePositionRequest Position,
    string NewName,
    DocumentSymbolInfo? Symbol = null,
    /// <summary>Preview-only; nothing is written to disk. See <see cref="RenameSymbolApplyResult"/> for applying the rename.</summary>
    IReadOnlyCollection<RenameSymbolChangeInfo>? Changes = null);

public sealed record RenameSymbolApplyResult(
    bool Success,
    string Message,
    CodePositionRequest Position,
    string NewName,
    DocumentSymbolInfo? Symbol = null,
    IReadOnlyCollection<RenameSymbolChangeInfo>? Changes = null);

public sealed record FindImplementationsResult(
    bool Supported,
    string Message,
    CodePositionRequest Position,
    IReadOnlyCollection<CodeLocationInfo> Implementations);

public sealed record DocumentOutlineResult(
    string DocumentPath,
    /// <summary>Formatted symbol labels (kind + name + location), not raw <see cref="DocumentSymbolInfo"/> instances.</summary>
    IReadOnlyCollection<string> Symbols);

public sealed record WorkspaceSearchResult(
    string RootPath,
    IReadOnlyCollection<WorkspaceSearchMatch> Matches,
    bool Truncated);

public sealed record WorkspaceSearchMatch(
    string Path,
    /// <summary>Null for a filename-only match with no line-level hit.</summary>
    int? Line,
    string? Preview,
    IReadOnlyCollection<string>? ContextBefore = null,
    IReadOnlyCollection<string>? ContextAfter = null);

public sealed record DiagnosticsForDocumentResult(
    string DocumentPath,
    IReadOnlyCollection<ErrorListItemInfo> Items);

/// <summary>Combined go-to-definition + find-references + a code snippet around a symbol, for a single-call "explain this symbol" workflow.</summary>
public sealed record SymbolContextResult(
    DocumentReadResult Document,
    GoToDefinitionResult Definition,
    FindReferencesResult References,
    string Snippet);
