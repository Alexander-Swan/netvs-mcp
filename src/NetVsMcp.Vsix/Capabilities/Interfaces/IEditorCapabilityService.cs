using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IEditorCapabilityService
{
    Task<EditorDocumentInfo?> GetActiveDocumentAsync(CancellationToken cancellationToken);
    Task<DocumentListResult> ListDocumentsAsync(CancellationToken cancellationToken);
    Task<DocumentCloseResult> CloseDocumentAsync(DocumentCloseRequest request, CancellationToken cancellationToken);
    Task<DocumentReadResult> ReadDocumentAsync(string path, CancellationToken cancellationToken);
    Task<TextSearchResult> FindInDocumentAsync(EditorFindRequest request, CancellationToken cancellationToken);
    Task<TextSearchResult> FindInFilesAsync(FindInFilesRequest request, CancellationToken cancellationToken);
    Task<EditorDocumentInfo> OpenDocumentAsync(string path, CancellationToken cancellationToken);
    Task<SelectionInfo?> GetSelectionAsync(CancellationToken cancellationToken);
    Task<DocumentMutationResult> WriteDocumentAsync(DocumentWriteRequest request, CancellationToken cancellationToken);
    Task<DocumentMutationResult> SaveDocumentAsync(DocumentSaveRequest request, CancellationToken cancellationToken);
    Task<DocumentMutationResult> InsertAsync(EditorInsertRequest request, CancellationToken cancellationToken);
    Task<DocumentMutationResult> ReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken);
    Task<EditorDocumentInfo> GoToLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken);
    Task<SelectionInfo> SetSelectionAsync(SelectionSetRequest request, CancellationToken cancellationToken);
    Task<DocumentCleanupResult> CleanupDocumentAsync(DocumentCleanupRequest request, CancellationToken cancellationToken);
    Task<EditPreviewResult> PreviewEditAsync(EditPreviewRequest request, CancellationToken cancellationToken);
    Task<EditDecisionResult> ApproveEditAsync(EditDecisionRequest request, CancellationToken cancellationToken);
    Task<EditDecisionResult> RejectEditAsync(EditDecisionRequest request, CancellationToken cancellationToken);
    Task<PendingEditListResult> ListPendingEditsAsync(CancellationToken cancellationToken);
}
