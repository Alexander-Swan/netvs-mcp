using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed class EditorDocumentInfo
{
    public EditorDocumentInfo(
        string? name,
        string? path,
        string? language,
        bool isOpen,
        bool isSaved)
    {
        Name = name;
        Path = path;
        Language = language;
        IsOpen = isOpen;
        IsSaved = isSaved;
    }

    public string? Name { get; }
    public string? Path { get; }
    public string? Language { get; }
    public bool IsOpen { get; }
    public bool IsSaved { get; }

    public static EditorDocumentInfo FromDocument(EnvDTE.Document document)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new EditorDocumentInfo(
            document.Name,
            document.FullName,
            document.Language,
            true,
            document.Saved);
    }
}

internal sealed class DocumentReadResult
{
    public DocumentReadResult(
        EditorDocumentInfo document,
        string text,
        string source,
        bool usedLiveBuffer)
    {
        Document = document;
        Text = text;
        Source = source;
        UsedLiveBuffer = usedLiveBuffer;
    }

    public EditorDocumentInfo Document { get; }
    public string Text { get; }
    public string Source { get; }
    public bool UsedLiveBuffer { get; }
}

internal sealed class DocumentListResult
{
    public DocumentListResult(IReadOnlyCollection<EditorDocumentInfo> documents, string activeDocument)
    {
        Documents = documents;
        ActiveDocument = activeDocument;
    }

    public IReadOnlyCollection<EditorDocumentInfo> Documents { get; }
    public string ActiveDocument { get; }
}

internal sealed class EditorFindRequest
{
    public string Path { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
}

internal sealed class FindInFilesRequest
{
    public string Query { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string FilePattern { get; set; } = string.Empty;
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }
    public int MaxResults { get; set; } = 100;
}

internal sealed class TextSearchMatch
{
    public TextSearchMatch(string path, int line, int column, string lineText, string matchText)
    {
        Path = path;
        Line = line;
        Column = column;
        LineText = lineText;
        MatchText = matchText;
    }

    public string Path { get; }
    public int Line { get; }
    public int Column { get; }
    public string LineText { get; }
    public string MatchText { get; }
}

internal sealed class TextSearchResult
{
    public TextSearchResult(string query, int matchCount, bool truncated, IReadOnlyCollection<TextSearchMatch> matches)
    {
        Query = query;
        MatchCount = matchCount;
        Truncated = truncated;
        Matches = matches;
    }

    public string Query { get; }
    public int MatchCount { get; }
    public bool Truncated { get; }
    public IReadOnlyCollection<TextSearchMatch> Matches { get; }
}

internal sealed class SelectionInfo
{
    public SelectionInfo(
        EditorDocumentInfo document,
        string text,
        int anchorLine,
        int anchorColumn,
        int activeLine,
        int activeColumn,
        bool isEmpty)
    {
        Document = document;
        Text = text;
        AnchorLine = anchorLine;
        AnchorColumn = anchorColumn;
        ActiveLine = activeLine;
        ActiveColumn = activeColumn;
        IsEmpty = isEmpty;
    }

    public EditorDocumentInfo Document { get; }
    public string Text { get; }
    public int AnchorLine { get; }
    public int AnchorColumn { get; }
    public int ActiveLine { get; }
    public int ActiveColumn { get; }
    public bool IsEmpty { get; }
}

internal sealed class DocumentWriteRequest
{
    public string Path { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool CreateIfMissing { get; set; }
    public bool SaveAfterWrite { get; set; }
}

internal sealed class DocumentMutationResult
{
    public DocumentMutationResult(
        bool success,
        string? message,
        EditorDocumentInfo? document,
        bool saved,
        int charactersChanged)
    {
        Success = success;
        Message = message;
        Document = document;
        Saved = saved;
        CharactersChanged = charactersChanged;
    }

    public bool Success { get; }
    public string? Message { get; }
    public EditorDocumentInfo? Document { get; }
    public bool Saved { get; }
    public int CharactersChanged { get; }
}

internal sealed class DocumentSaveRequest
{
    public string Path { get; set; } = string.Empty;
}

internal sealed class EditorInsertRequest
{
    public string Path { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool SaveAfterEdit { get; set; }
}

internal sealed class EditorReplaceRequest
{
    public string Path { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool SaveAfterEdit { get; set; }
}

internal sealed class EditorGotoLineRequest
{
    public string Path { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; } = 1;
}

internal sealed class SelectionSetRequest
{
    public string Path { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

internal sealed class DocumentCleanupRequest
{
    public string Path { get; set; } = string.Empty;
    public bool SaveAfterCleanup { get; set; }
}

internal sealed class DocumentCleanupResult
{
    public DocumentCleanupResult(
        bool success,
        bool supported,
        string? message,
        EditorDocumentInfo? document,
        bool saved,
        string? command)
    {
        Success = success;
        Supported = supported;
        Message = message;
        Document = document;
        Saved = saved;
        Command = command;
    }

    public bool Success { get; }
    public bool Supported { get; }
    public string? Message { get; }
    public EditorDocumentInfo? Document { get; }
    public bool Saved { get; }
    public string? Command { get; }
}

internal sealed class EditPreviewRequest
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

internal sealed class EditDecisionRequest
{
    public string EditId { get; set; } = string.Empty;
    public bool SaveAfterApply { get; set; }
}

internal sealed class PendingEditInfo
{
    public PendingEditInfo(
        string editId,
        string operation,
        string path,
        string summary,
        string? originalText,
        string proposedText,
        int? startLine,
        int? startColumn,
        int? endLine,
        int? endColumn,
        int originalLength,
        int proposedLength,
        DateTimeOffset createdUtc)
    {
        EditId = editId;
        Operation = operation;
        Path = path;
        Summary = summary;
        OriginalText = originalText;
        ProposedText = proposedText;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
        OriginalLength = originalLength;
        ProposedLength = proposedLength;
        CreatedUtc = createdUtc;
    }

    public string EditId { get; }
    public string Operation { get; }
    public string Path { get; }
    public string Summary { get; }
    public string? OriginalText { get; }
    public string ProposedText { get; }
    public int? StartLine { get; }
    public int? StartColumn { get; }
    public int? EndLine { get; }
    public int? EndColumn { get; }
    public int OriginalLength { get; }
    public int ProposedLength { get; }
    public DateTimeOffset CreatedUtc { get; }
}

internal sealed class EditPreviewResult
{
    public EditPreviewResult(bool success, string? message, PendingEditInfo? pendingEdit)
    {
        Success = success;
        Message = message;
        PendingEdit = pendingEdit;
    }

    public bool Success { get; }
    public string? Message { get; }
    public PendingEditInfo? PendingEdit { get; }
}

internal sealed class EditDecisionResult
{
    public EditDecisionResult(
        bool success,
        string? message,
        string editId,
        bool applied,
        PendingEditInfo? pendingEdit,
        DocumentMutationResult? mutation)
    {
        Success = success;
        Message = message;
        EditId = editId;
        Applied = applied;
        PendingEdit = pendingEdit;
        Mutation = mutation;
    }

    public bool Success { get; }
    public string? Message { get; }
    public string EditId { get; }
    public bool Applied { get; }
    public PendingEditInfo? PendingEdit { get; }
    public DocumentMutationResult? Mutation { get; }
}

internal sealed class PendingEditListResult
{
    public PendingEditListResult(IReadOnlyCollection<PendingEditInfo> pendingEdits)
    {
        PendingEdits = pendingEdits;
    }

    public IReadOnlyCollection<PendingEditInfo> PendingEdits { get; }
}
