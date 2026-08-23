using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface INavigationCapabilityService
{
    Task<GoToDefinitionResult> GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<FindReferencesResult> FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<FindImplementationsResult> FindImplementationsAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<CodeWorkspaceSymbolsResult> WorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken);
    Task<CallHierarchyResult> CallHierarchyAsync(CallHierarchyRequest request, CancellationToken cancellationToken);
    Task<RenameSymbolPreviewResult> RenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken);
    Task<DocumentSymbolsResult> ListDocumentSymbolsAsync(string? documentPath, CancellationToken cancellationToken);
}

internal sealed class NavigationCapabilityService : INavigationCapabilityService
{
    private readonly AsyncPackage package;

    public NavigationCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<GoToDefinitionResult> GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        var resolvedSymbol = await ResolveSymbolAtPositionAsync(documentPath, line, column, cancellationToken);
        if (resolvedSymbol.Symbol is null)
        {
            return new GoToDefinitionResult(null, Array.Empty<CodeLocationInfo>(), false);
        }

        var locations = GetSourceLocations(resolvedSymbol.Symbol)
            .Select(location => CreateLocationInfo(location, resolvedSymbol.Symbol))
            .Where(location => location is not null)
            .Cast<CodeLocationInfo>()
            .ToArray();

        var primaryLocation = locations.FirstOrDefault();
        if (primaryLocation is not null)
        {
            await NavigateToLocationAsync(primaryLocation, cancellationToken);
        }

        return new GoToDefinitionResult(
            DocumentSymbolInfoFactory.FromSymbol(resolvedSymbol.Symbol, primaryLocation?.File, primaryLocation?.Line ?? 0, primaryLocation?.Column ?? 0),
            locations,
            primaryLocation is not null);
    }

    public async Task<FindReferencesResult> FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        var resolvedSymbol = await ResolveSymbolAtPositionAsync(documentPath, line, column, cancellationToken);
        if (resolvedSymbol.Symbol is null)
        {
            return new FindReferencesResult(null, Array.Empty<CodeReferenceInfo>());
        }

        var referencedSymbols = await SymbolFinder.FindReferencesAsync(
            resolvedSymbol.Symbol,
            resolvedSymbol.Document.Project.Solution,
            cancellationToken);

        var references = new List<CodeReferenceInfo>();
        foreach (var referencedSymbol in referencedSymbols)
        {
            foreach (var referenceLocation in referencedSymbol.Locations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var location = await CreateReferenceInfoAsync(
                    referenceLocation,
                    referencedSymbol.Definition,
                    cancellationToken);
                if (location is not null)
                {
                    references.Add(location);
                }
            }
        }

        var orderedReferences = references
            .OrderBy(reference => reference.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.Line)
            .ThenBy(reference => reference.Column)
            .ToArray();

        return new FindReferencesResult(
            DocumentSymbolInfoFactory.FromSymbol(resolvedSymbol.Symbol, resolvedSymbol.Document.FilePath, line, column),
            orderedReferences);
    }

    public async Task<FindImplementationsResult> FindImplementationsAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        var position = new CodePositionRequest { DocumentPath = documentPath, Line = line, Column = column };
        var resolvedSymbol = await ResolveSymbolAtPositionAsync(documentPath, line, column, cancellationToken);
        if (resolvedSymbol.Symbol is null)
        {
            return new FindImplementationsResult(true, "No symbol found at the requested position.", position, Array.Empty<CodeLocationInfo>());
        }

        var implementations = await SymbolFinder.FindImplementationsAsync(
            resolvedSymbol.Symbol,
            resolvedSymbol.Document.Project.Solution,
            cancellationToken: cancellationToken);

        var locations = implementations
            .SelectMany(symbol => GetSourceLocations(symbol).Select(location => CreateLocationInfo(location, symbol)))
            .Where(location => location is not null)
            .Cast<CodeLocationInfo>()
            .OrderBy(location => location.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(location => location.Line)
            .ThenBy(location => location.Column)
            .ToArray();

        return new FindImplementationsResult(
            true,
            $"Found {locations.Length} implementation location(s).",
            position,
            locations);
    }

    public async Task<CodeWorkspaceSymbolsResult> WorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Query is required.", nameof(request));
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var workspace = await GetVisualStudioWorkspaceAsync(cancellationToken);
        if (workspace is null)
        {
            throw new InvalidOperationException("Visual Studio Roslyn workspace service is unavailable.");
        }

        return await SearchWorkspaceSymbolsAsync(workspace.CurrentSolution, request.Query, request.MaxResults, cancellationToken);
    }

    internal static async Task<CodeWorkspaceSymbolsResult> SearchWorkspaceSymbolsAsync(
        Microsoft.CodeAnalysis.Solution solution,
        string rawQuery,
        int requestedMaxResults,
        CancellationToken cancellationToken)
    {
        var query = rawQuery.Trim();
        var maxResults = requestedMaxResults <= 0 ? 100 : Math.Min(requestedMaxResults, 1000);
        var symbols = new List<DocumentSymbolInfo>();
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<ISymbol> declarations;
            try
            {
                declarations = await SymbolFinder.FindSourceDeclarationsWithPatternAsync(
                    project,
                    query,
                    SymbolFilter.All,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                continue;
            }

            foreach (var symbol in declarations)
            {
                if (!ShouldInclude(symbol))
                {
                    continue;
                }

                foreach (var location in GetSourceLocations(symbol))
                {
                    var lineSpan = location.GetLineSpan();
                    symbols.Add(DocumentSymbolInfoFactory.FromSymbol(
                        symbol,
                        lineSpan.Path,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1));

                    if (symbols.Count > maxResults)
                    {
                        break;
                    }
                }

                if (symbols.Count > maxResults)
                {
                    break;
                }
            }

            if (symbols.Count > maxResults)
            {
                break;
            }
        }

        var truncated = symbols.Count > maxResults;
        var result = symbols
            .Take(maxResults)
            .OrderBy(symbol => symbol.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(symbol => symbol.Line)
            .ThenBy(symbol => symbol.Column)
            .ToArray();
        return new CodeWorkspaceSymbolsResult(query, result.Length, truncated, result);
    }

    public async Task<CallHierarchyResult> CallHierarchyAsync(CallHierarchyRequest request, CancellationToken cancellationToken)
    {
        var position = new CodePositionRequest
        {
            DocumentPath = request.DocumentPath,
            Line = request.Line,
            Column = request.Column
        };
        var direction = NormalizeDirection(request.Direction);
        var maxDepth = request.MaxDepth <= 0 ? 3 : Math.Min(request.MaxDepth, 6);

        var resolvedSymbol = await ResolveSymbolAtPositionAsync(request.DocumentPath, request.Line, request.Column, cancellationToken);
        if (resolvedSymbol.Symbol is null)
        {
            return new CallHierarchyResult(
                true,
                "No symbol found at the requested position.",
                position,
                direction,
                null,
                Array.Empty<CallHierarchyNode>(),
                Array.Empty<CallHierarchyNode>());
        }

        var symbol = resolvedSymbol.Symbol;
        var solution = resolvedSymbol.Document.Project.Solution;
        var budget = new CallHierarchyBudget(500);

        IReadOnlyList<CallHierarchyNode> incoming = Array.Empty<CallHierarchyNode>();
        IReadOnlyList<CallHierarchyNode> outgoing = Array.Empty<CallHierarchyNode>();

        if (direction is "incoming" or "both")
        {
            var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { symbol };
            incoming = await BuildIncomingCallsAsync(symbol, solution, visited, 1, maxDepth, budget, cancellationToken);
        }

        if (direction is "outgoing" or "both")
        {
            var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { symbol };
            outgoing = await BuildOutgoingCallsAsync(symbol, solution, visited, 1, maxDepth, budget, cancellationToken);
        }

        var nodeCount = CountNodes(incoming) + CountNodes(outgoing);
        return new CallHierarchyResult(
            true,
            $"Found {nodeCount} call hierarchy node(s).",
            position,
            direction,
            DocumentSymbolInfoFactory.FromSymbol(symbol, resolvedSymbol.Document.FilePath, request.Line, request.Column),
            incoming,
            outgoing);
    }

    public async Task<RenameSymbolPreviewResult> RenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            throw new ArgumentException("New name is required.", nameof(request));
        }

        var resolvedSymbol = await ResolveSymbolAtPositionAsync(request.DocumentPath, request.Line, request.Column, cancellationToken);
        var position = new CodePositionRequest
        {
            DocumentPath = request.DocumentPath,
            Line = request.Line,
            Column = request.Column
        };
        if (resolvedSymbol.Symbol is null)
        {
            return new RenameSymbolPreviewResult(
                true,
                "No symbol found at the requested position.",
                position,
                request.NewName,
                null,
                Array.Empty<RenameSymbolChangeInfo>());
        }

        var oldSolution = resolvedSymbol.Document.Project.Solution;
#pragma warning disable CS0618
        var newSolution = await Renamer.RenameSymbolAsync(
            oldSolution,
            resolvedSymbol.Symbol,
            request.NewName,
            oldSolution.Workspace.Options,
            cancellationToken);
#pragma warning restore CS0618
        var changes = await CreateRenameChangesAsync(oldSolution, newSolution, cancellationToken);

        return new RenameSymbolPreviewResult(
            true,
            $"Rename preview contains {changes.Count} text change(s).",
            position,
            request.NewName,
            DocumentSymbolInfoFactory.FromSymbol(resolvedSymbol.Symbol, resolvedSymbol.Document.FilePath, request.Line, request.Column),
            changes);
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

    private async Task<ResolvedSymbolAtPosition> ResolveSymbolAtPositionAsync(
        string documentPath,
        int line,
        int column,
        CancellationToken cancellationToken)
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

        var position = await GetTextPositionAsync(document, line, column, cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
            ?? throw new InvalidOperationException("Document semantic model is unavailable.");
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, workspace, cancellationToken);
        return new ResolvedSymbolAtPosition(document, symbol);
    }

    private static string ResolveDocumentPath(DTE? dte, string? documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return DocumentPathResolver.Resolve(dte, documentPath, allowActiveDocument: true);
    }

    private static Microsoft.CodeAnalysis.Document? FindWorkspaceDocument(Microsoft.CodeAnalysis.Solution solution, string resolvedPath)
    {
        return solution.Projects
            .SelectMany(project => project.Documents)
            .FirstOrDefault(document => string.Equals(document.FilePath, resolvedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<int> GetTextPositionAsync(
        Microsoft.CodeAnalysis.Document document,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line must be 1-based.");
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be 1-based.");
        }

        var text = await document.GetTextAsync(cancellationToken);
        if (line > text.Lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line is outside the document.");
        }

        var textLine = text.Lines[line - 1];
        var zeroBasedColumn = Math.Min(column - 1, textLine.Span.Length);
        return textLine.Start + zeroBasedColumn;
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

    private static IEnumerable<Location> GetSourceLocations(ISymbol symbol)
    {
        var targetSymbol = symbol.OriginalDefinition ?? symbol;
        return targetSymbol.Locations.Where(location => location.IsInSource);
    }

    private static CodeLocationInfo? CreateLocationInfo(Location location, ISymbol symbol)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        var position = lineSpan.StartLinePosition;
        return new CodeLocationInfo(
            lineSpan.Path,
            position.Line + 1,
            position.Character + 1,
            DocumentSymbolInfoFactory.FromSymbol(
                symbol,
                lineSpan.Path,
                position.Line + 1,
                position.Character + 1));
    }

    private static async Task<CodeReferenceInfo?> CreateReferenceInfoAsync(
        ReferenceLocation referenceLocation,
        ISymbol symbol,
        CancellationToken cancellationToken)
    {
        var sourceSpan = referenceLocation.Location;
        var document = referenceLocation.Document;
        if (!sourceSpan.IsInSource)
        {
            return null;
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        var lineSpan = syntaxTree?.GetLineSpan(sourceSpan.SourceSpan, cancellationToken)
            ?? sourceSpan.GetLineSpan();
        var position = lineSpan.StartLinePosition;

        return new CodeReferenceInfo(
            lineSpan.Path,
            position.Line + 1,
            position.Character + 1,
            referenceLocation.IsImplicit,
            DocumentSymbolInfoFactory.FromSymbol(
                symbol,
                lineSpan.Path,
                position.Line + 1,
                position.Character + 1));
    }

    private static async Task<IReadOnlyCollection<RenameSymbolChangeInfo>> CreateRenameChangesAsync(
        Microsoft.CodeAnalysis.Solution oldSolution,
        Microsoft.CodeAnalysis.Solution newSolution,
        CancellationToken cancellationToken)
    {
        var changes = new List<RenameSymbolChangeInfo>();
        foreach (var newProject in newSolution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var oldProject = oldSolution.GetProject(newProject.Id);
            if (oldProject is null)
            {
                continue;
            }

            foreach (var newDocument in newProject.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var oldDocument = oldProject.GetDocument(newDocument.Id);
                if (oldDocument is null)
                {
                    continue;
                }

                var oldText = await oldDocument.GetTextAsync(cancellationToken);
                var newText = await newDocument.GetTextAsync(cancellationToken);
                foreach (var change in newText.GetTextChanges(oldText))
                {
                    var start = oldText.Lines.GetLinePosition(change.Span.Start);
                    var end = oldText.Lines.GetLinePosition(change.Span.End);
                    changes.Add(new RenameSymbolChangeInfo(
                        newDocument.FilePath,
                        start.Line + 1,
                        start.Character + 1,
                        end.Line + 1,
                        end.Character + 1,
                        change.NewText ?? string.Empty));
                }
            }
        }

        return changes
            .OrderBy(change => change.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.StartLine)
            .ThenBy(change => change.StartColumn)
            .ToArray();
    }

    private static string NormalizeDirection(string? direction)
    {
        return direction?.Trim().ToLowerInvariant() switch
        {
            "outgoing" => "outgoing",
            "both" => "both",
            _ => "incoming"
        };
    }

    private static int CountNodes(IEnumerable<CallHierarchyNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count += 1 + CountNodes(node.Children);
        }

        return count;
    }

    private async Task<IReadOnlyList<CallHierarchyNode>> BuildIncomingCallsAsync(
        ISymbol symbol,
        Microsoft.CodeAnalysis.Solution solution,
        HashSet<ISymbol> visitedOnPath,
        int depth,
        int maxDepth,
        CallHierarchyBudget budget,
        CancellationToken cancellationToken)
    {
        if (!budget.TryConsume())
        {
            return Array.Empty<CallHierarchyNode>();
        }

        var callers = await SymbolFinder.FindCallersAsync(symbol, solution, cancellationToken);
        var nodes = new List<CallHierarchyNode>();
        foreach (var caller in callers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryConsume())
            {
                break;
            }

            var callingSymbol = caller.CallingSymbol;
            var callSite = caller.Locations
                .Select(location => CreateLocationInfo(location, callingSymbol))
                .FirstOrDefault(location => location is not null);

            var isRecursive = !visitedOnPath.Add(callingSymbol);
            var atDepthLimit = depth >= maxDepth;
            IReadOnlyList<CallHierarchyNode> children = Array.Empty<CallHierarchyNode>();
            if (!isRecursive && !atDepthLimit)
            {
                children = await BuildIncomingCallsAsync(callingSymbol, solution, visitedOnPath, depth + 1, maxDepth, budget, cancellationToken);
                visitedOnPath.Remove(callingSymbol);
            }
            else if (!isRecursive)
            {
                visitedOnPath.Remove(callingSymbol);
            }

            nodes.Add(new CallHierarchyNode(
                DocumentSymbolInfoFactory.FromSymbol(callingSymbol, callSite?.File, callSite?.Line ?? 0, callSite?.Column ?? 0),
                callSite,
                children,
                isRecursive,
                atDepthLimit && !isRecursive));
        }

        return nodes;
    }

    private async Task<IReadOnlyList<CallHierarchyNode>> BuildOutgoingCallsAsync(
        ISymbol symbol,
        Microsoft.CodeAnalysis.Solution solution,
        HashSet<ISymbol> visitedOnPath,
        int depth,
        int maxDepth,
        CallHierarchyBudget budget,
        CancellationToken cancellationToken)
    {
        if (!budget.TryConsume())
        {
            return Array.Empty<CallHierarchyNode>();
        }

        var callees = await FindOutgoingCalleesAsync(symbol, solution, cancellationToken);
        var nodes = new List<CallHierarchyNode>();
        foreach (var (calleeSymbol, callSite) in callees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryConsume())
            {
                break;
            }

            var isRecursive = !visitedOnPath.Add(calleeSymbol);
            var atDepthLimit = depth >= maxDepth;
            IReadOnlyList<CallHierarchyNode> children = Array.Empty<CallHierarchyNode>();
            if (!isRecursive && !atDepthLimit)
            {
                children = await BuildOutgoingCallsAsync(calleeSymbol, solution, visitedOnPath, depth + 1, maxDepth, budget, cancellationToken);
                visitedOnPath.Remove(calleeSymbol);
            }
            else if (!isRecursive)
            {
                visitedOnPath.Remove(calleeSymbol);
            }

            nodes.Add(new CallHierarchyNode(
                DocumentSymbolInfoFactory.FromSymbol(calleeSymbol, callSite?.File, callSite?.Line ?? 0, callSite?.Column ?? 0),
                callSite,
                children,
                isRecursive,
                atDepthLimit && !isRecursive));
        }

        return nodes;
    }

    // No direct Roslyn "FindCallees" API exists, so outgoing calls are found by walking the
    // symbol's declaring syntax (C# only) for invocation/object-creation/constructor-initializer
    // nodes and resolving each one through the semantic model - still Roslyn's semantic APIs
    // end-to-end, just without a single convenience method like SymbolFinder.FindCallersAsync.
    private static async Task<IReadOnlyList<(ISymbol Symbol, CodeLocationInfo? CallSite)>> FindOutgoingCalleesAsync(
        ISymbol symbol,
        Microsoft.CodeAnalysis.Solution solution,
        CancellationToken cancellationToken)
    {
        var results = new List<(ISymbol, CodeLocationInfo?)>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = solution.GetDocument(syntaxRef.SyntaxTree);
            var semanticModel = document is null ? null : await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel is null)
            {
                continue;
            }

            var node = await syntaxRef.GetSyntaxAsync(cancellationToken);
            foreach (var candidate in node.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidate is not (InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ConstructorInitializerSyntax))
                {
                    continue;
                }

                var calleeSymbol = semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol;
                if (calleeSymbol is null || !seen.Add(calleeSymbol))
                {
                    continue;
                }

                var lineSpan = candidate.SyntaxTree.GetLineSpan(candidate.Span, cancellationToken);
                var callSite = new CodeLocationInfo(
                    lineSpan.Path,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.StartLinePosition.Character + 1,
                    DocumentSymbolInfoFactory.FromSymbol(
                        calleeSymbol,
                        lineSpan.Path,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.StartLinePosition.Character + 1));

                results.Add((calleeSymbol, callSite));
            }
        }

        return results;
    }

    private sealed class CallHierarchyBudget
    {
        private int remaining;

        public CallHierarchyBudget(int max)
        {
            remaining = max;
        }

        public bool TryConsume()
        {
            if (remaining <= 0)
            {
                return false;
            }

            remaining--;
            return true;
        }
    }

    private async Task NavigateToLocationAsync(CodeLocationInfo location, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(location.File))
        {
            return;
        }

        var dte = await GetDteAsync();
        var window = dte?.ItemOperations.OpenFile(location.File);
        window?.Activate();

        if (window?.Document?.Selection is TextSelection selection)
        {
            selection.MoveToLineAndOffset(location.Line, location.Column);
        }
    }

    private sealed class ResolvedSymbolAtPosition
    {
        public ResolvedSymbolAtPosition(Microsoft.CodeAnalysis.Document document, ISymbol? symbol)
        {
            Document = document;
            Symbol = symbol;
        }

        public Microsoft.CodeAnalysis.Document Document { get; }
        public ISymbol? Symbol { get; }
    }
}
