using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class EditorRpcTarget
{
    private readonly IEditorCapabilityService editor;

    public EditorRpcTarget(IEditorCapabilityService editor)
    {
        this.editor = editor;
    }

    public Task<EditorDocumentInfo?> DocumentActiveAsync(CancellationToken cancellationToken)
    {
        return editor.GetActiveDocumentAsync(cancellationToken);
    }

    public Task<DocumentReadResult> DocumentReadAsync(DocumentReadRequest request, CancellationToken cancellationToken)
    {
        return editor.ReadDocumentAsync(request.Path, cancellationToken);
    }

    public Task<EditorDocumentInfo> DocumentOpenAsync(DocumentOpenRequest request, CancellationToken cancellationToken)
    {
        return editor.OpenDocumentAsync(request.Path, cancellationToken);
    }

    public Task<SelectionInfo?> SelectionGetAsync(CancellationToken cancellationToken)
    {
        return editor.GetSelectionAsync(cancellationToken);
    }
}

internal sealed class DocumentReadRequest
{
    public string Path { get; set; } = string.Empty;
}

internal sealed class DocumentOpenRequest
{
    public string Path { get; set; } = string.Empty;
}
