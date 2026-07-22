using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface INavigationCapabilityService
{
    Task GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<DocumentSymbolsResult> ListDocumentSymbolsAsync(string? documentPath, CancellationToken cancellationToken);
}

internal sealed class NavigationCapabilityService : INavigationCapabilityService
{
    private readonly AsyncPackage package;

    public NavigationCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public Task GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        _ = package;
        _ = documentPath;
        _ = line;
        _ = column;
        _ = cancellationToken;
        throw new System.NotImplementedException("Use Visual Studio's live workspace/language services instead of standalone Roslyn parsing.");
    }

    public Task FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        _ = documentPath;
        _ = line;
        _ = column;
        _ = cancellationToken;
        throw new System.NotImplementedException("Use VS Find All References APIs or Roslyn from the VisualStudioWorkspace.");
    }

    public async Task<DocumentSymbolsResult> ListDocumentSymbolsAsync(string? documentPath, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var resolvedPath = ResolveDocumentPath(dte, documentPath);
        var workspace = await GetVisualStudioWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            throw new InvalidOperationException("Visual Studio Roslyn workspace service is unavailable.");
        }

        var document = FindWorkspaceDocument(workspace.CurrentSolution, resolvedPath);
        if (document is null)
        {
            throw new FileNotFoundException("Document was not found in the live Visual Studio workspace.", resolvedPath);
        }

        var symbols = await ReadDeclaredSymbolsAsync(document, cancellationToken);
        return new DocumentSymbolsResult(resolvedPath, symbols);
    }

    private async Task<DTE?> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE;
    }

    private async Task<VisualStudioWorkspace?> GetVisualStudioWorkspaceAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
        return componentModel?.GetService<VisualStudioWorkspace>();
    }

    private static string ResolveDocumentPath(DTE? dte, string? documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var path = string.IsNullOrWhiteSpace(documentPath)
            ? dte?.ActiveDocument?.FullName
            : documentPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Document path is required when there is no active document.", nameof(documentPath));
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

    private static Microsoft.CodeAnalysis.Document? FindWorkspaceDocument(Microsoft.CodeAnalysis.Solution solution, string resolvedPath)
    {
        return solution.Projects
            .SelectMany(project => project.Documents)
            .FirstOrDefault(document => string.Equals(document.FilePath, resolvedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IReadOnlyList<DocumentSymbolInfo>> ReadDeclaredSymbolsAsync(
        Microsoft.CodeAnalysis.Document document,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            return Array.Empty<DocumentSymbolInfo>();
        }

        var result = new List<DocumentSymbolInfo>();
        foreach (var node in root.DescendantNodesAndSelf())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (symbol is null || !ShouldInclude(symbol))
            {
                continue;
            }

            var lineSpan = root.SyntaxTree.GetLineSpan(node.Span, cancellationToken);
            result.Add(DocumentSymbolInfoFactory.FromSymbol(
                symbol,
                document.FilePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1));
        }

        return result
            .OrderBy(symbol => symbol.Line)
            .ThenBy(symbol => symbol.Column)
            .ToArray();
    }

    private static bool ShouldInclude(ISymbol symbol)
    {
        return symbol.Kind is SymbolKind.NamedType
            or SymbolKind.Method
            or SymbolKind.Property
            or SymbolKind.Field
            or SymbolKind.Event
            or SymbolKind.Namespace;
    }
}
