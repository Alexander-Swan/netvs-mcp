namespace NetVsMcp.Contracts;

public sealed record EditorDocumentInfo(
    string? Name,
    string? Path,
    string? Language,
    bool IsOpen,
    bool IsSaved);

public sealed record DocumentListResult(
    IReadOnlyCollection<EditorDocumentInfo> Documents,
    string? ActiveDocument);

/// <summary>How <see cref="DocumentCloseRequest"/> should handle unsaved changes.</summary>
public enum DocumentClosePolicy
{
    NoSave,
    Save,
    Discard
}

public sealed class DocumentCloseRequest
{
    public string Path { get; set; } = string.Empty;
    public DocumentClosePolicy Policy { get; set; } = DocumentClosePolicy.NoSave;
    /// <summary>Required to be true for <see cref="DocumentClosePolicy.Discard"/> to actually discard dirty changes, as a safety guard.</summary>
    public bool AllowDirtyDiscard { get; set; }
}

public sealed record DocumentCloseResult(
    bool Success,
    string? Message,
    EditorDocumentInfo? Document,
    DocumentClosePolicy Policy);

public sealed class EditorFindRequest
{
    public string Path { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
    /// <summary>Number of lines of surrounding context to include before/after each match.</summary>
    public int ContextLines { get; set; }
}

public sealed class FindInFilesRequest
{
    public string Query { get; set; } = string.Empty;
    /// <summary>Defaults to the open solution's root when omitted.</summary>
    public string? RootPath { get; set; }
    /// <summary>Glob-style filename filter, e.g. "*.cs".</summary>
    public string? FilePattern { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
    public int ContextLines { get; set; }
}

public sealed record TextSearchMatch(
    string Path,
    int Line,
    int Column,
    string LineText,
    string MatchText,
    IReadOnlyCollection<string>? ContextBefore = null,
    IReadOnlyCollection<string>? ContextAfter = null);

public sealed record TextSearchResult(
    string Query,
    int MatchCount,
    /// <summary>True when the result set was cut off by the request's max-results limit.</summary>
    bool Truncated,
    IReadOnlyCollection<TextSearchMatch> Matches);

public sealed class DocumentReadRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record DocumentReadResult(
    EditorDocumentInfo Document,
    string Text,
    /// <summary>Where <see cref="Text"/> came from, e.g. "live-buffer" vs. "disk".</summary>
    string Source,
    /// <summary>True if the document was already open in VS and its in-memory (possibly unsaved) buffer was read.</summary>
    bool UsedLiveBuffer);

public sealed class DocumentOpenRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record SelectionInfo(
    EditorDocumentInfo Document,
    string Text,
    int AnchorLine,
    int AnchorColumn,
    int ActiveLine,
    int ActiveColumn,
    bool IsEmpty);

public sealed class DocumentWriteRequest
{
    public string Path { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool CreateIfMissing { get; set; }

    public bool SaveAfterWrite { get; set; }
}

public sealed record DocumentMutationResult(
    bool Success,
    string? Message,
    EditorDocumentInfo? Document,
    bool Saved,
    int CharactersChanged);

public sealed class DocumentSaveRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed class EditorInsertRequest
{
    public string Path { get; set; } = string.Empty;

    /// <summary>1-based line number.</summary>
    public int Line { get; set; }

    /// <summary>1-based column number.</summary>
    public int Column { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool SaveAfterEdit { get; set; }
}

public sealed class EditorReplaceRequest
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool SaveAfterEdit { get; set; }
}

public sealed class EditorGotoLineRequest
{
    public string Path { get; set; } = string.Empty;

    public int Line { get; set; }

    public int Column { get; set; } = 1;
}

public sealed class SelectionSetRequest
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }
}

public sealed class DocumentCleanupRequest
{
    public string Path { get; set; } = string.Empty;

    public bool SaveAfterCleanup { get; set; }
}

public sealed record DocumentCleanupResult(
    bool Success,
    /// <summary>False when the installed VS/extension version has no format-and-organize command available.</summary>
    bool Supported,
    string? Message,
    EditorDocumentInfo? Document,
    bool Saved,
    /// <summary>The DTE command name that was actually invoked, for diagnostics.</summary>
    string? Command);

public sealed record FormatAndOrganizeResult(
    DocumentCleanupResult Cleanup,
    string Message);

/// <summary>
/// Describes an edit to stage for later approval via <c>edit_approve</c>/<c>edit_reject</c>, instead
/// of applying it immediately. <see cref="Operation"/> selects which of the position/text fields apply
/// (e.g. "insert" uses <see cref="Line"/>/<see cref="Column"/>, "replace" uses the Start/End pairs).
/// </summary>
public sealed class EditPreviewRequest
{
    public string Operation { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool CreateIfMissing { get; set; }

    public bool SaveAfterEdit { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }

    public int StartLine { get; set; }

    public int StartColumn { get; set; }

    public int EndLine { get; set; }

    public int EndColumn { get; set; }
}

public sealed class EditDecisionRequest
{
    public string EditId { get; set; } = string.Empty;

    public bool SaveAfterApply { get; set; }
}

/// <summary>A staged, not-yet-applied edit awaiting approval or rejection.</summary>
public sealed record PendingEditInfo(
    string EditId,
    string Operation,
    string Path,
    string Summary,
    /// <summary>Buffer contents captured at preview time; used to detect if the live buffer has since diverged.</summary>
    string? OriginalText,
    string ProposedText,
    int? StartLine,
    int? StartColumn,
    int? EndLine,
    int? EndColumn,
    int OriginalLength,
    int ProposedLength,
    DateTimeOffset CreatedUtc);

public sealed record EditPreviewResult(
    bool Success,
    string? Message,
    PendingEditInfo? PendingEdit);

public sealed record EditDecisionResult(
    bool Success,
    string? Message,
    string EditId,
    /// <summary>True if the edit was actually applied (approve path); false for reject or a failed approve.</summary>
    bool Applied,
    PendingEditInfo? PendingEdit,
    DocumentMutationResult? Mutation);

public sealed record PendingEditListResult(
    IReadOnlyCollection<PendingEditInfo> PendingEdits);

public sealed record OpenRelevantFilesResult(
    IReadOnlyCollection<EditorDocumentInfo> Documents);
