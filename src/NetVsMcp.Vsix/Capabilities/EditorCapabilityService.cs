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
