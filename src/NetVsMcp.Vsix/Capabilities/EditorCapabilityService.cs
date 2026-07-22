using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IEditorCapabilityService
{
    Task<EditorDocumentInfo?> GetActiveDocumentAsync(CancellationToken cancellationToken);
    Task<DocumentReadResult> ReadDocumentAsync(string path, CancellationToken cancellationToken);
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

internal sealed class EditorCapabilityService : IEditorCapabilityService
{
    private readonly object pendingEditLock = new();
    private readonly Dictionary<string, PendingEdit> pendingEdits = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncPackage package;
    private int nextPendingEditId;

    public EditorCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<EditorDocumentInfo?> GetActiveDocumentAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var document = dte?.ActiveDocument;
        return document is null ? null : EditorDocumentInfo.FromDocument(document);
    }

    public async Task<DocumentReadResult> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var resolvedPath = ResolveDocumentPath(dte, path);
        var openDocument = FindOpenDocument(dte, resolvedPath);
        if (openDocument is not null && TryReadTextDocument(openDocument, out var liveText))
        {
            return new DocumentReadResult(
                EditorDocumentInfo.FromDocument(openDocument),
                liveText,
                "live",
                true);
        }

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("Document was not open in Visual Studio and was not found on disk.", resolvedPath);
        }

        var diskText = await Task.Run(() => File.ReadAllText(resolvedPath), cancellationToken);
        return new DocumentReadResult(
            new EditorDocumentInfo(
                Path.GetFileName(resolvedPath),
                resolvedPath,
                null,
                false,
                false),
            diskText,
            "disk",
            false);
    }

    public async Task<EditorDocumentInfo> OpenDocumentAsync(string path, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
        var resolvedPath = ResolveDocumentPath(dte, path);
        var openedWindow = dte.ItemOperations.OpenFile(resolvedPath);
        openedWindow.Activate();

        return EditorDocumentInfo.FromDocument(openedWindow.Document);
    }

    public async Task<SelectionInfo?> GetSelectionAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var document = dte?.ActiveDocument;
        if (document?.Selection is not TextSelection selection)
        {
            return null;
        }

        return new SelectionInfo(
            EditorDocumentInfo.FromDocument(document),
            selection.Text,
            selection.AnchorPoint.Line,
            selection.AnchorPoint.LineCharOffset,
            selection.ActivePoint.Line,
            selection.ActivePoint.LineCharOffset,
            selection.IsEmpty);
    }

    public async Task<DocumentMutationResult> WriteDocumentAsync(DocumentWriteRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var document = OpenTextDocument(dte, request.Path, request.CreateIfMissing, out var textDocument);

            var editPoint = textDocument.StartPoint.CreateEditPoint();
            editPoint.Delete(textDocument.EndPoint);
            editPoint.Insert(request.Text ?? string.Empty);

            var saved = SaveIfRequested(document, request.SaveAfterWrite);
            return new DocumentMutationResult(
                true,
                null,
                EditorDocumentInfo.FromDocument(document),
                saved,
                request.Text?.Length ?? 0);
        }
        catch (Exception ex)
        {
            return new DocumentMutationResult(false, ex.Message, null, false, 0);
        }
    }

    public async Task<DocumentMutationResult> SaveDocumentAsync(DocumentSaveRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var document = GetDocumentForOptionalPath(dte, request.Path, openIfNeeded: false);
            document.Save();

            return new DocumentMutationResult(
                true,
                null,
                EditorDocumentInfo.FromDocument(document),
                true,
                0);
        }
        catch (Exception ex)
        {
            return new DocumentMutationResult(false, ex.Message, null, false, 0);
        }
    }

    public async Task<DocumentMutationResult> InsertAsync(EditorInsertRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            ValidatePosition(request.Line, request.Column);

            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var document = OpenTextDocument(dte, request.Path, createIfMissing: false, out var textDocument);
            var editPoint = textDocument.StartPoint.CreateEditPoint();
            editPoint.MoveToLineAndOffset(request.Line, request.Column);
            editPoint.Insert(request.Text ?? string.Empty);

            var saved = SaveIfRequested(document, request.SaveAfterEdit);
            return new DocumentMutationResult(
                true,
                null,
                EditorDocumentInfo.FromDocument(document),
                saved,
                request.Text?.Length ?? 0);
        }
        catch (Exception ex)
        {
            return new DocumentMutationResult(false, ex.Message, null, false, 0);
        }
    }

    public async Task<DocumentMutationResult> ReplaceAsync(EditorReplaceRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            ValidateRange(request.StartLine, request.StartColumn, request.EndLine, request.EndColumn);

            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var document = OpenTextDocument(dte, request.Path, createIfMissing: false, out var textDocument);
            var startPoint = textDocument.StartPoint.CreateEditPoint();
            var endPoint = textDocument.StartPoint.CreateEditPoint();
            startPoint.MoveToLineAndOffset(request.StartLine, request.StartColumn);
            endPoint.MoveToLineAndOffset(request.EndLine, request.EndColumn);
            startPoint.Delete(endPoint);
            startPoint.Insert(request.Text ?? string.Empty);

            var saved = SaveIfRequested(document, request.SaveAfterEdit);
            return new DocumentMutationResult(
                true,
                null,
                EditorDocumentInfo.FromDocument(document),
                saved,
                request.Text?.Length ?? 0);
        }
        catch (Exception ex)
        {
            return new DocumentMutationResult(false, ex.Message, null, false, 0);
        }
    }

    public async Task<EditorDocumentInfo> GoToLineAsync(EditorGotoLineRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        ValidatePosition(request.Line, request.Column);

        var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
        var document = OpenTextDocument(dte, request.Path, createIfMissing: false, out _);
        document.Activate();

        if (document.Selection is not TextSelection selection)
        {
            throw new NotSupportedException("The document does not expose a text selection.");
        }

        selection.MoveToLineAndOffset(request.Line, request.Column);
        return EditorDocumentInfo.FromDocument(document);
    }

    public async Task<SelectionInfo> SetSelectionAsync(SelectionSetRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        ValidateRange(request.StartLine, request.StartColumn, request.EndLine, request.EndColumn);

        var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
        var document = OpenTextDocument(dte, request.Path, createIfMissing: false, out _);
        document.Activate();

        if (document.Selection is not TextSelection selection)
        {
            throw new NotSupportedException("The document does not expose a text selection.");
        }

        selection.MoveToLineAndOffset(request.StartLine, request.StartColumn);
        selection.MoveToLineAndOffset(request.EndLine, request.EndColumn, true);

        return new SelectionInfo(
            EditorDocumentInfo.FromDocument(document),
            selection.Text,
            selection.AnchorPoint.Line,
            selection.AnchorPoint.LineCharOffset,
            selection.ActivePoint.Line,
            selection.ActivePoint.LineCharOffset,
            selection.IsEmpty);
    }

    public async Task<DocumentCleanupResult> CleanupDocumentAsync(DocumentCleanupRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var document = OpenTextDocument(dte, request.Path, createIfMissing: false, out _);
            document.Activate();

            const string command = "Edit.FormatDocument";
            dte.ExecuteCommand(command);

            var saved = SaveIfRequested(document, request.SaveAfterCleanup);
            return new DocumentCleanupResult(
                true,
                true,
                null,
                EditorDocumentInfo.FromDocument(document),
                saved,
                command);
        }
        catch (Exception ex)
        {
            return new DocumentCleanupResult(false, false, ex.Message, null, false, "Edit.FormatDocument");
        }
    }

    public async Task<EditPreviewResult> PreviewEditAsync(EditPreviewRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        try
        {
            var dte = await GetDteAsync() ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
            var operation = NormalizeEditOperation(request.Operation);
            var resolvedPath = ResolveDocumentPath(dte, request.Path);
            var originalText = CaptureOriginalText(dte, request, operation, resolvedPath);
            var info = CreatePendingEditInfo(
                NextPendingEditId(),
                operation,
                resolvedPath,
                originalText,
                request);
            var pendingEdit = new PendingEdit(info, ClonePreviewRequest(request, resolvedPath, operation));

            lock (pendingEditLock)
            {
                pendingEdits[info.EditId] = pendingEdit;
            }

            return new EditPreviewResult(true, null, info);
        }
        catch (Exception ex)
        {
            return new EditPreviewResult(false, ex.Message, null);
        }
    }

    public async Task<EditDecisionResult> ApproveEditAsync(EditDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryRemovePendingEdit(request.EditId, out var pendingEdit))
        {
            return new EditDecisionResult(false, "Pending edit was not found.", request.EditId, false, null, null);
        }

        var preview = pendingEdit.Request;
        DocumentMutationResult mutation;
        switch (pendingEdit.Info.Operation)
        {
            case "write":
                mutation = await WriteDocumentAsync(
                    new DocumentWriteRequest
                    {
                        Path = preview.Path,
                        Text = preview.Text,
                        CreateIfMissing = preview.CreateIfMissing,
                        SaveAfterWrite = request.SaveAfterApply || preview.SaveAfterEdit
                    },
                    cancellationToken);
                break;
            case "insert":
                mutation = await InsertAsync(
                    new EditorInsertRequest
                    {
                        Path = preview.Path,
                        Line = preview.Line,
                        Column = preview.Column,
                        Text = preview.Text,
                        SaveAfterEdit = request.SaveAfterApply || preview.SaveAfterEdit
                    },
                    cancellationToken);
                break;
            case "replace":
                mutation = await ReplaceAsync(
                    new EditorReplaceRequest
                    {
                        Path = preview.Path,
                        StartLine = preview.StartLine,
                        StartColumn = preview.StartColumn,
                        EndLine = preview.EndLine,
                        EndColumn = preview.EndColumn,
                        Text = preview.Text,
                        SaveAfterEdit = request.SaveAfterApply || preview.SaveAfterEdit
                    },
                    cancellationToken);
                break;
            default:
                mutation = new DocumentMutationResult(false, "Unsupported pending edit operation.", null, false, 0);
                break;
        }

        return new EditDecisionResult(
            mutation.Success,
            mutation.Message,
            pendingEdit.Info.EditId,
            mutation.Success,
            pendingEdit.Info,
            mutation);
    }

    public Task<EditDecisionResult> RejectEditAsync(EditDecisionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryRemovePendingEdit(request.EditId, out var pendingEdit))
        {
            return Task.FromResult(new EditDecisionResult(false, "Pending edit was not found.", request.EditId, false, null, null));
        }

        return Task.FromResult(new EditDecisionResult(
            true,
            "Pending edit rejected.",
            pendingEdit.Info.EditId,
            false,
            pendingEdit.Info,
            null));
    }

    public Task<PendingEditListResult> ListPendingEditsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PendingEditInfo[] edits;
        lock (pendingEditLock)
        {
            edits = pendingEdits.Values
                .OrderBy(edit => edit.Info.CreatedUtc)
                .Select(edit => edit.Info)
                .ToArray();
        }

        return Task.FromResult(new PendingEditListResult(edits));
    }

    private async Task<DTE?> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE;
    }

    private static Document? FindOpenDocument(DTE? dte, string path)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (dte?.Documents is null)
        {
            return null;
        }

        foreach (Document document in dte.Documents)
        {
            if (string.Equals(document.FullName, path, StringComparison.OrdinalIgnoreCase))
            {
                return document;
            }
        }

        return null;
    }

    private static bool TryReadTextDocument(Document document, out string text)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        text = string.Empty;

        if (document.Object("TextDocument") is not TextDocument textDocument)
        {
            return false;
        }

        var editPoint = textDocument.StartPoint.CreateEditPoint();
        text = editPoint.GetText(textDocument.EndPoint);
        return true;
    }

    private static string CaptureOriginalText(DTE dte, EditPreviewRequest request, string operation, string resolvedPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (operation == "write")
        {
            var document = FindOpenDocument(dte, resolvedPath);
            if (document is not null && TryReadTextDocument(document, out var liveText))
            {
                return liveText;
            }

            if (File.Exists(resolvedPath))
            {
                return File.ReadAllText(resolvedPath);
            }

            if (request.CreateIfMissing)
            {
                return string.Empty;
            }

            throw new FileNotFoundException("Document was not found for edit preview.", resolvedPath);
        }

        if (operation == "insert")
        {
            ValidatePosition(request.Line, request.Column);
            OpenTextDocument(dte, resolvedPath, createIfMissing: false, out _);
            return string.Empty;
        }

        ValidateRange(request.StartLine, request.StartColumn, request.EndLine, request.EndColumn);
        OpenTextDocument(dte, resolvedPath, createIfMissing: false, out var textDocument);
        var startPoint = textDocument.StartPoint.CreateEditPoint();
        var endPoint = textDocument.StartPoint.CreateEditPoint();
        startPoint.MoveToLineAndOffset(request.StartLine, request.StartColumn);
        endPoint.MoveToLineAndOffset(request.EndLine, request.EndColumn);
        return startPoint.GetText(endPoint);
    }

    private static Document OpenTextDocument(DTE dte, string path, bool createIfMissing, out TextDocument textDocument)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var resolvedPath = ResolveDocumentPath(dte, path);
        if (!File.Exists(resolvedPath))
        {
            if (!createIfMissing)
            {
                throw new FileNotFoundException("Document was not found on disk.", resolvedPath);
            }

            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedPath, string.Empty);
        }

        var document = FindOpenDocument(dte, resolvedPath);
        if (document is null)
        {
            var openedWindow = dte.ItemOperations.OpenFile(resolvedPath);
            openedWindow.Activate();
            document = openedWindow.Document;
        }
        else
        {
            document.Activate();
        }

        textDocument = document.Object("TextDocument") as TextDocument
            ?? throw new NotSupportedException("The document is not a text document.");
        return document;
    }

    private static Document GetDocumentForOptionalPath(DTE dte, string path, bool openIfNeeded)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (string.IsNullOrWhiteSpace(path))
        {
            return dte.ActiveDocument ?? throw new InvalidOperationException("No active document is available.");
        }

        var resolvedPath = ResolveDocumentPath(dte, path);
        var document = FindOpenDocument(dte, resolvedPath);
        if (document is not null)
        {
            return document;
        }

        if (!openIfNeeded)
        {
            throw new FileNotFoundException("Document is not open in Visual Studio.", resolvedPath);
        }

        var openedWindow = dte.ItemOperations.OpenFile(resolvedPath);
        openedWindow.Activate();
        return openedWindow.Document;
    }

    private static bool SaveIfRequested(Document document, bool save)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!save)
        {
            return false;
        }

        document.Save();
        return true;
    }

    private static void ValidatePosition(int line, int column)
    {
        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line must be 1 or greater.");
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be 1 or greater.");
        }
    }

    private static void ValidateRange(int startLine, int startColumn, int endLine, int endColumn)
    {
        ValidatePosition(startLine, startColumn);
        ValidatePosition(endLine, endColumn);

        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            throw new ArgumentException("End position must be greater than or equal to start position.");
        }
    }

    private static string NormalizeEditOperation(string operation)
    {
        var normalized = (operation ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "write" or "insert" or "replace")
        {
            return normalized;
        }

        throw new ArgumentException("Edit operation must be one of: write, insert, replace.", nameof(operation));
    }

    private string NextPendingEditId()
    {
        var id = Interlocked.Increment(ref nextPendingEditId);
        return $"edit-{id:000000}";
    }

    private bool TryRemovePendingEdit(string editId, out PendingEdit pendingEdit)
    {
        var key = editId ?? string.Empty;
        lock (pendingEditLock)
        {
            if (pendingEdits.TryGetValue(key, out pendingEdit))
            {
                pendingEdits.Remove(key);
                return true;
            }
        }

        pendingEdit = null!;
        return false;
    }

    private static PendingEditInfo CreatePendingEditInfo(
        string editId,
        string operation,
        string resolvedPath,
        string originalText,
        EditPreviewRequest request)
    {
        var proposedText = request.Text ?? string.Empty;
        var summary = operation switch
        {
            "write" => $"Replace full document text in {Path.GetFileName(resolvedPath)} ({originalText.Length} -> {proposedText.Length} chars).",
            "insert" => $"Insert {proposedText.Length} chars at {request.Line}:{request.Column} in {Path.GetFileName(resolvedPath)}.",
            "replace" => $"Replace range {request.StartLine}:{request.StartColumn}-{request.EndLine}:{request.EndColumn} in {Path.GetFileName(resolvedPath)} ({originalText.Length} -> {proposedText.Length} chars).",
            _ => $"Edit {Path.GetFileName(resolvedPath)}."
        };

        return new PendingEditInfo(
            editId,
            operation,
            resolvedPath,
            summary,
            originalText,
            proposedText,
            operation == "write" ? null : operation == "insert" ? request.Line : request.StartLine,
            operation == "write" ? null : operation == "insert" ? request.Column : request.StartColumn,
            operation == "replace" ? request.EndLine : null,
            operation == "replace" ? request.EndColumn : null,
            originalText.Length,
            proposedText.Length,
            DateTimeOffset.UtcNow);
    }

    private static EditPreviewRequest ClonePreviewRequest(EditPreviewRequest request, string resolvedPath, string operation)
    {
        return new EditPreviewRequest
        {
            Operation = operation,
            Path = resolvedPath,
            Text = request.Text ?? string.Empty,
            CreateIfMissing = request.CreateIfMissing,
            SaveAfterEdit = request.SaveAfterEdit,
            Line = request.Line,
            Column = request.Column,
            StartLine = request.StartLine,
            StartColumn = request.StartColumn,
            EndLine = request.EndLine,
            EndColumn = request.EndColumn
        };
    }

    private static string ResolveDocumentPath(DTE? dte, string path)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Document path is required.", nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var solutionPath = dte?.Solution?.FullName;
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return Path.GetFullPath(path);
        }

        var solutionDirectory = Path.GetDirectoryName(solutionPath);
        return Path.GetFullPath(Path.Combine(solutionDirectory ?? Environment.CurrentDirectory, path));
    }

    private sealed class PendingEdit
    {
        public PendingEdit(PendingEditInfo info, EditPreviewRequest request)
        {
            Info = info;
            Request = request;
        }

        public PendingEditInfo Info { get; }
        public EditPreviewRequest Request { get; }
    }
}
