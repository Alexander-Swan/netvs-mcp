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
