using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface ICodeActionsCapabilityService
{
    Task<CodeActionsListResult> ListCodeActionsAsync(CodeActionsListRequest request, CancellationToken cancellationToken);
    Task<CodeActionsApplyResult> ApplyCodeActionAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken);
}

// Mirrors what the VS lightbulb does, using only public Roslyn APIs (no internal
// ICodeFixService): MEF-discovers CodeFixProvider/CodeRefactoringProvider exports the
// same way VS itself does, computes actions for a position/span, and applies the chosen
// one via CodeAction.GetOperationsAsync + operation.Apply(workspace, ...) - the same
// mechanism Roslyn's own test harness and third-party tools use to apply actions outside
// the IDE. v1 limitation: actions with nested sub-actions (which need further interactive
// input, e.g. "Generate constructor..." option pickers) are skipped.
internal sealed class CodeActionsCapabilityService : ICodeActionsCapabilityService
{
    private readonly AsyncPackage package;

    public CodeActionsCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<CodeActionsListResult> ListCodeActionsAsync(CodeActionsListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var (document, span, position) = await ResolveDocumentAndSpanAsync(
            request.DocumentPath, request.Line, request.Column, request.EndLine, request.EndColumn, cancellationToken);

        var actions = await ComputeActionsAsync(document, span, cancellationToken);
        var infos = actions
            .Select((entry, index) => new CodeActionInfo(index, entry.Action.Title, entry.Kind, entry.DiagnosticId, entry.Action.EquivalenceKey))
            .ToArray();

        return new CodeActionsListResult(position, infos);
    }

    public async Task<CodeActionsApplyResult> ApplyCodeActionAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var (document, span, _) = await ResolveDocumentAndSpanAsync(
            request.DocumentPath, request.Line, request.Column, request.EndLine, request.EndColumn, cancellationToken);

        // Recompute rather than cache CodeAction objects across calls - the workspace
        // snapshot backing a cached action can go stale between list and apply.
        var actions = await ComputeActionsAsync(document, span, cancellationToken);
        if (request.Index < 0 || request.Index >= actions.Count)
        {
            return new CodeActionsApplyResult(
                false,
                $"No code action was found at index {request.Index}. Call code_actions_list again to get current indices.",
                null,
                Array.Empty<RenameSymbolChangeInfo>());
        }

        var chosen = actions[request.Index];
        var workspace = document.Project.Solution.Workspace;
        var oldSolution = document.Project.Solution;

        var operations = await chosen.Action.GetOperationsAsync(cancellationToken);
        foreach (var operation in operations)
        {
            operation.Apply(workspace, cancellationToken);
        }

        var changes = await CreateChangesAsync(oldSolution, workspace.CurrentSolution, cancellationToken);
        return new CodeActionsApplyResult(true, $"Applied '{chosen.Action.Title}'.", chosen.Action.Title, changes);
    }

    private async Task<IReadOnlyList<(CodeAction Action, string Kind, string? DiagnosticId)>> ComputeActionsAsync(
        Microsoft.CodeAnalysis.Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var results = new List<(CodeAction, string, string?)>();

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is not null)
        {
            var diagnostics = semanticModel.GetDiagnostics(span, cancellationToken)
                .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                .ToArray();

            if (diagnostics.Length > 0)
            {
                var fixProviders = await GetCodeFixProvidersAsync(cancellationToken);
                foreach (var diagnostic in diagnostics)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var provider in fixProviders)
                    {
                        if (!provider.FixableDiagnosticIds.Contains(diagnostic.Id))
                        {
                            continue;
                        }

                        try
                        {
                            var context = new CodeFixContext(
                                document,
                                diagnostic,
                                (action, _) => results.Add((action, "fix", diagnostic.Id)),
                                cancellationToken);
                            await provider.RegisterCodeFixesAsync(context);
                        }
                        catch (Exception) when (!cancellationToken.IsCancellationRequested)
                        {
                            // A misbehaving fixer shouldn't break the whole listing.
                        }
                    }
                }
            }
        }

        var refactoringProviders = await GetCodeRefactoringProvidersAsync(cancellationToken);
        foreach (var provider in refactoringProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var context = new CodeRefactoringContext(
                    document,
                    span,
                    action => results.Add((action, "refactor", null)),
                    cancellationToken);
                await provider.ComputeRefactoringsAsync(context);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A misbehaving refactoring provider shouldn't break the whole listing.
            }
        }

        return results
            .Where(entry => entry.Item1.NestedActions.IsDefaultOrEmpty)
            .ToArray();
    }

    private async Task<IReadOnlyList<CodeFixProvider>> GetCodeFixProvidersAsync(CancellationToken cancellationToken)
    {
        var exportProvider = await GetExportProviderAsync(cancellationToken);
        if (exportProvider is null)
        {
            return Array.Empty<CodeFixProvider>();
        }

        return exportProvider.GetExports<CodeFixProvider, IDictionary<string, object>>()
            .Where(export => IsCSharp(export.Metadata))
            .Select(export => export.Value)
            .ToArray();
    }

    private async Task<IReadOnlyList<CodeRefactoringProvider>> GetCodeRefactoringProvidersAsync(CancellationToken cancellationToken)
    {
        var exportProvider = await GetExportProviderAsync(cancellationToken);
        if (exportProvider is null)
        {
            return Array.Empty<CodeRefactoringProvider>();
        }

        return exportProvider.GetExports<CodeRefactoringProvider, IDictionary<string, object>>()
            .Where(export => IsCSharp(export.Metadata))
            .Select(export => export.Value)
            .ToArray();
    }

    private static bool IsCSharp(IDictionary<string, object> metadata)
    {
        if (!metadata.TryGetValue("Languages", out var languagesObj))
        {
            // No language metadata to filter on - include it rather than silently drop it.
            return true;
        }

        return languagesObj is IEnumerable<string> languages
            && languages.Contains(LanguageNames.CSharp, StringComparer.Ordinal);
    }

    private async Task<ExportProvider?> GetExportProviderAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var componentModel = await package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
        return componentModel?.DefaultExportProvider;
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

    private async Task<(Microsoft.CodeAnalysis.Document Document, TextSpan Span, CodePositionRequest Position)> ResolveDocumentAndSpanAsync(
        string documentPath,
        int line,
        int column,
        int? endLine,
        int? endColumn,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var resolvedPath = DocumentPathResolver.Resolve(dte, documentPath, allowActiveDocument: true);

        var workspace = await GetVisualStudioWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Visual Studio Roslyn workspace service is unavailable.");

        var document = workspace.CurrentSolution.Projects
            .SelectMany(project => project.Documents)
            .FirstOrDefault(candidate => string.Equals(candidate.FilePath, resolvedPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("Document was not found in the live Visual Studio workspace.", resolvedPath);

        var text = await document.GetTextAsync(cancellationToken);
        var startPosition = GetTextPosition(text, line, column);
        var endPosition = endLine.HasValue && endColumn.HasValue
            ? GetTextPosition(text, endLine.Value, endColumn.Value)
            : startPosition;

        var span = startPosition <= endPosition
            ? TextSpan.FromBounds(startPosition, endPosition)
            : TextSpan.FromBounds(endPosition, startPosition);

        var position = new CodePositionRequest { DocumentPath = resolvedPath, Line = line, Column = column };
        return (document, span, position);
    }

    private static int GetTextPosition(SourceText text, int line, int column)
    {
        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line must be 1-based.");
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column), "Column must be 1-based.");
        }

        if (line > text.Lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Line is outside the document.");
        }

        var textLine = text.Lines[line - 1];
        var zeroBasedColumn = Math.Min(column - 1, textLine.Span.Length);
        return textLine.Start + zeroBasedColumn;
    }

    private static async Task<IReadOnlyCollection<RenameSymbolChangeInfo>> CreateChangesAsync(
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
}
