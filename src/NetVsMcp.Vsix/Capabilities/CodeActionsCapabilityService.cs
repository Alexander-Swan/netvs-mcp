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
    private sealed class Selection(CodeAction action)
    {
        public CodeAction Action { get; } = action;
        public Microsoft.CodeAnalysis.Solution? Prepared { get; set; }
    }
    private readonly SnapshotActionStore<Selection> selections = new();
    private readonly SnapshotActionStore<Microsoft.CodeAnalysis.Solution> recoveries = new(capacity: 32, retention: TimeSpan.FromMinutes(15));

    public CodeActionsCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<CodeActionsListResult> ListCodeActionsAsync(CodeActionsListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (await TryResolveDocumentAndSpanAsync(
            request.DocumentPath,
            request.Line,
            request.Column,
            request.EndLine,
            request.EndColumn,
            cancellationToken) is not { } resolved)
        {
            var position = new CodePositionRequest
            {
                DocumentPath = request.DocumentPath,
                Line = request.Line,
                Column = request.Column
            };
            return new CodeActionsListResult(position, Array.Empty<CodeActionInfo>());
        }

        var (document, span, codePosition) = resolved;

        var computed = await ComputeActionsAsync(document, span, cancellationToken);
        var actions = computed.Actions;
        var boundSnapshot = await WorkspaceActionSnapshot.CaptureAsync(document.Project.Solution, cancellationToken);
        var infos = actions
            .Select((entry, index) => new CodeActionInfo(index, entry.Action.Title, entry.Kind, entry.DiagnosticId, entry.Action.EquivalenceKey,
                selections.Add(boundSnapshot, Target(document.Id, span), new Selection(entry.Action))))
            .ToArray();

        return new CodeActionsListResult(codePosition, infos, computed.Discovery);
    }

    public async Task<CodeActionsApplyResult> ApplyCodeActionAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (await TryResolveDocumentAndSpanAsync(
            request.DocumentPath,
            request.Line,
            request.Column,
            request.EndLine,
            request.EndColumn,
            cancellationToken) is not { } resolved)
        {
            return new CodeActionsApplyResult(
                false,
                "Document was not found in the live Visual Studio workspace.",
                null,
                Array.Empty<RenameSymbolChangeInfo>());
        }

        var (document, span, _) = resolved;

        if (!string.IsNullOrWhiteSpace(request.ActionId))
        {
            var oldSnapshot = document.Project.Solution;
            var workspaceForAction = oldSnapshot.Workspace;
            var currentSnapshot = await WorkspaceActionSnapshot.CaptureAsync(oldSnapshot, cancellationToken);
            var selection = selections.Get(request.ActionId!, currentSnapshot, Target(document.Id, span), consume: !request.PreviewOnly);
            var selected = selection.Action;
            if (selection.Prepared is null)
            {
                var selectedOperations = await selected.GetOperationsAsync(cancellationToken);
                // Reject custom operations: they may launch dialogs or mutate outside the solution.
                if (selectedOperations.Length != 1 || selectedOperations[0] is not ApplyChangesOperation textOperation)
                    return new(false, "This action requires custom operations; guarded application supports one workspace change only.", null, Array.Empty<RenameSymbolChangeInfo>());
                if (!SourceOnlyChangePolicy.Accepts(oldSnapshot, textOperation.ChangedSolution))
                    return new(false, "Guarded previews currently support edits to existing source documents only; structural changes require a different workflow.", null, Array.Empty<RenameSymbolChangeInfo>());
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                // If two previews raced, retain the first prepared transformation for both callers.
                selection.Prepared ??= textOperation.ChangedSolution;
            }
            var guardedChanges = await CreateChangesAsync(oldSnapshot, selection.Prepared, cancellationToken);
            if (selection.Prepared.GetChanges(oldSnapshot).GetProjectChanges().Sum(p => p.GetChangedDocuments().Count()) > 50 ||
                ExceedsEvidenceBudget(guardedChanges))
                return new(false, "Action exceeds the guarded preview budget (50 documents, 1000 edits, or 262144 changed characters). Choose a narrower action.", null, Array.Empty<RenameSymbolChangeInfo>(), "unsupported");
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await WorkspaceActionSnapshot.CaptureAsync(workspaceForAction.CurrentSolution, cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!currentSnapshot.MatchesCurrent(workspaceForAction.CurrentSolution))
                throw new StaleActionException("Workspace changed while preparing the action. List actions again.");
            if (request.PreviewOnly)
            {
                var preview = await CodeActionRecoveryPlan.CreateAsync(oldSnapshot, selection.Prepared, null, cancellationToken);
                return new(true, "Preview only; no buffers changed. Apply the same actionId to use this exact transformation.", selected.Title, guardedChanges,
                    "preview", documentVersions: preview.Versions);
            }
            cancellationToken.ThrowIfCancellationRequested();
            bool accepted;
            string? applicationFailure = null;
            try { accepted = workspaceForAction.TryApplyChanges(selection.Prepared); }
            catch (Exception ex) { accepted = false; applicationFailure = ex.Message; }
            // Capture immediately, before any async work can observe later developer edits.
            var observed = workspaceForAction.CurrentSolution;
            return await VerifyApplicationAsync(oldSnapshot, selection.Prepared, observed, selected.Title, accepted, applicationFailure);
        }

        // Recompute rather than cache CodeAction objects across calls - the workspace
        // snapshot backing a cached action can go stale between list and apply.
        var actions = (await ComputeActionsAsync(document, span, cancellationToken)).Actions;
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

    private static string Target(DocumentId document, TextSpan span) => document + ":" + span.Start + ":" + span.Length;

    private async Task<CodeActionsApplyResult> VerifyApplicationAsync(Microsoft.CodeAnalysis.Solution before,
        Microsoft.CodeAnalysis.Solution expected, Microsoft.CodeAnalysis.Solution observed, string? title, bool accepted, string? failure)
    {
        // Verification continues after a caller cancels its wait. Do not lose partial-change evidence.
        var plan = await CodeActionRecoveryPlan.CreateAsync(before, expected, observed, CancellationToken.None);
        var actualChanges = await CreateChangesAsync(before, observed, CancellationToken.None, plan.OwnedDocuments);
        var changesTruncated = ExceedsEvidenceBudget(actualChanges);
        if (changesTruncated) actualChanges = Array.Empty<RenameSymbolChangeInfo>();
        string? recoveryId = null;
        if (plan.UndoSolution is { } undo)
        {
            var key = await WorkspaceActionSnapshot.CaptureAsync(observed, CancellationToken.None);
            recoveryId = recoveries.Add(key, "code-action-recovery", undo);
        }
        var success = accepted && plan.MatchesPreview;
        var outcome = success ? "applied" : plan.Ambiguous ? "unknown" : plan.HasObservedChanges ? "failedWithChanges" : "notApplied";
        var message = success ? $"Applied '{title}' and verified source text." :
            "Workspace application was not fully verified. Inspect returned versions/changes before retrying. " + failure;
        if (changesTruncated) message += " Observed differences exceed the evidence budget; inspect current documents with ranged reads.";
        return new(success, message, title, actualChanges, outcome, plan.HasObservedChanges, recoveryId, plan.Versions, changesTruncated);
    }

    private static bool ExceedsEvidenceBudget(IReadOnlyCollection<RenameSymbolChangeInfo> changes) =>
        changes.Count > 1000 || changes.Sum(c => (long)c.NewText.Length + (c.OldText?.Length ?? 0)) > 262144;

    public async Task<CodeActionsApplyResult> RevertCodeActionAsync(NetVsMcp.Contracts.CodeActionRecoveryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecoveryId)) throw new ArgumentException("recoveryId is required.");
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var workspace = await GetVisualStudioWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Visual Studio workspace is unavailable.");
        var before = workspace.CurrentSolution;
        var key = await WorkspaceActionSnapshot.CaptureAsync(before, cancellationToken);
        Microsoft.CodeAnalysis.Solution recovery;
        try { recovery = recoveries.Take(request.RecoveryId, key, "code-action-recovery"); }
        catch (StaleActionException)
        { throw new StaleActionException("Recovery receipt is expired, already used, or conflicts with current workspace versions. Inspect current buffers; no recovery was applied."); }
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (!key.MatchesCurrent(workspace.CurrentSolution)) throw new StaleActionException("Workspace changed while preparing recovery. Inspect current buffers; no recovery was applied.");
        cancellationToken.ThrowIfCancellationRequested();
        bool accepted;
        string? failure = null;
        try { accepted = workspace.TryApplyChanges(recovery); }
        catch (Exception ex) { accepted = false; failure = ex.Message; }
        var observed = workspace.CurrentSolution;
        return await VerifyApplicationAsync(before, recovery, observed, "Revert code action", accepted, failure);
    }

    private async Task<(IReadOnlyList<(CodeAction Action, string Kind, string? DiagnosticId)> Actions, NetVsMcp.Contracts.CodeActionDiscoveryInfo Discovery)> ComputeActionsAsync(
        Microsoft.CodeAnalysis.Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var results = new List<(CodeAction, string, string?)>();
        var errors = new List<string>();
        int diagnosticCount = 0, fixProviderCount = 0;
        void Failure(object provider, Exception ex)
        {
            if (errors.Count < 5)
            {
                var message = provider.GetType().Name + ": " + ex.GetType().Name + ": " + ex.Message;
                errors.Add(message.Length > 300 ? message.Substring(0, 300) : message);
            }
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (semanticModel is not null)
        {
            var diagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken)
                .Where(diagnostic => diagnostic.Location.IsInSource && CodeActionDiscovery.Intersects(diagnostic.Location.SourceSpan, span))
                .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                .ToArray();
            diagnosticCount = diagnostics.Length;

            if (diagnostics.Length > 0)
            {
                var fixProviders = await GetCodeFixProvidersAsync(cancellationToken);
                fixProviderCount = fixProviders.Count;
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
                        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                        {
                            Failure(provider, ex);
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
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Failure(provider, ex);
            }
        }

        var flattened = new List<(CodeAction, string, string?)>();
        var truncated = false;
        foreach (var entry in results)
        {
            var leaves = CodeActionDiscovery.Flatten(new[] { entry.Item1 }, out var limited);
            foreach (var leaf in leaves)
            {
                if (flattened.Count == 100) { truncated = true; break; }
                flattened.Add((leaf, entry.Item2, entry.Item3));
            }
            truncated |= limited;
        }
        return (flattened, new(diagnosticCount, fixProviderCount, refactoringProviders.Count, errors, truncated));
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

    private async Task<(Microsoft.CodeAnalysis.Document Document, TextSpan Span, CodePositionRequest Position)?> TryResolveDocumentAndSpanAsync(
        string documentPath,
        int line,
        int column,
        int? endLine,
        int? endColumn,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveDocumentAndSpanAsync(documentPath, line, column, endLine, endColumn, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
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
        CancellationToken cancellationToken, IReadOnlyCollection<DocumentId>? ownedDocuments = null)
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
                if (ownedDocuments is not null && !ownedDocuments.Contains(newDocument.Id)) continue;
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
                        change.NewText ?? string.Empty, oldText.ToString(change.Span)));
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
