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
