using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed class CodeActionRecoveryPlan
{
    public Solution? UndoSolution { get; private set; }
    public bool MatchesPreview { get; private set; } = true;
    public bool HasObservedChanges { get; private set; }
    public bool Ambiguous { get; private set; }
    public IReadOnlyCollection<DocumentId> OwnedDocuments { get; private set; } = new DocumentId[0];
    public IReadOnlyCollection<CodeActionDocumentVersion> Versions { get; private set; } = new CodeActionDocumentVersion[0];

    public static async Task<CodeActionRecoveryPlan> CreateAsync(Solution before, Solution expected, Solution? observed,
        CancellationToken cancellationToken)
    {
        var result = new CodeActionRecoveryPlan();
        var versions = new List<CodeActionDocumentVersion>();
        var owned = expected.GetChanges(before).GetProjectChanges().SelectMany(p => p.GetChangedDocuments()).ToArray();
        result.OwnedDocuments = owned;
        var undo = observed;
        foreach (var id in owned)
        {
            var originalDocument = before.GetDocument(id)!;
            var originalText = await originalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var expectedText = await expected.GetDocument(id)!.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var observedDocument = observed?.GetDocument(id);
            var actualText = observedDocument is null ? null : await observedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            versions.Add(new(originalDocument.FilePath, TextContentVersion.Create(originalText.ToString()),
                TextContentVersion.Create(expectedText.ToString()), actualText is null ? null : TextContentVersion.Create(actualText.ToString())));
            if (observed is null) continue; // Preview evidence only.
            if (actualText is null) { result.Ambiguous = true; result.MatchesPreview = false; continue; }
            if (!actualText.ContentEquals(expectedText)) result.MatchesPreview = false;
            if (actualText.ContentEquals(originalText)) continue;
            result.HasObservedChanges = true;
            if (!actualText.ContentEquals(expectedText)) { result.Ambiguous = true; continue; }
            // Restore only verified, owned source changes. Preserve other developer edits
            // that may have occurred in unrelated documents during workspace application.
            undo = undo!.WithDocumentText(id, originalText, PreservationMode.PreserveIdentity);
        }
        result.Versions = versions;
        if (result.HasObservedChanges && !result.Ambiguous) result.UndoSolution = undo;
        return result;
    }
}
