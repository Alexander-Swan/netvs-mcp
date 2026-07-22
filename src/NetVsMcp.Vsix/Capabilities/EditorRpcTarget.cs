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

    public Task<DocumentMutationResult> DocumentWriteAsync(DocumentWriteRequest request, CancellationToken cancellationToken)
    {
        return editor.WriteDocumentAsync(request, cancellationToken);
    }

    public Task<DocumentMutationResult> DocumentSaveAsync(DocumentSaveRequest request, CancellationToken cancellationToken)
    {
        return editor.SaveDocumentAsync(request, cancellationToken);
    }

    public Task<DocumentMutationResult> EditorInsertAsync(EditorInsertRequest request, CancellationToken cancellationToken)
    {
        return editor.InsertAsync(request, cancellationToken);
    }

    public Task<DocumentMutationResult> EditorReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken)
    {
        return editor.ReplaceAsync(request, cancellationToken);
    }

    public Task<EditorDocumentInfo> EditorGotoLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken)
    {
        return editor.GoToLineAsync(request, cancellationToken);
    }

    public Task<SelectionInfo> SelectionSetAsync(SelectionSetRequest request, CancellationToken cancellationToken)
    {
        return editor.SetSelectionAsync(request, cancellationToken);
    }

    public Task<DocumentCleanupResult> DocumentCleanupAsync(DocumentCleanupRequest request, CancellationToken cancellationToken)
    {
        return editor.CleanupDocumentAsync(request, cancellationToken);
    }

    public Task<EditPreviewResult> EditPreviewAsync(EditPreviewRequest request, CancellationToken cancellationToken)
    {
        return editor.PreviewEditAsync(request, cancellationToken);
    }

    public Task<EditDecisionResult> EditApproveAsync(EditDecisionRequest request, CancellationToken cancellationToken)
    {
        return editor.ApproveEditAsync(request, cancellationToken);
    }

    public Task<EditDecisionResult> EditRejectAsync(EditDecisionRequest request, CancellationToken cancellationToken)
    {
        return editor.RejectEditAsync(request, cancellationToken);
    }

    public Task<PendingEditListResult> EditListPendingAsync(CancellationToken cancellationToken)
    {
        return editor.ListPendingEditsAsync(cancellationToken);
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
