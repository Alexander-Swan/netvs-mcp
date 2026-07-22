using System;
using System.IO;
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
}

internal sealed class EditorCapabilityService : IEditorCapabilityService
{
    private readonly AsyncPackage package;

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
}
