using System;
using System.Collections.Generic;
using System.Linq;

namespace NetVsMcp.Vsix;

internal sealed class EditDocumentSnapshot(bool exists, string text)
{
    public bool Exists { get; } = exists;
    public string Text { get; } = text;

    public bool Matches(EditDocumentSnapshot other) =>
        Exists == other.Exists && string.Equals(Text, other.Text, StringComparison.Ordinal);
}

internal sealed class PendingEdit(
    PendingEditInfo info, EditPreviewRequest request, EditDocumentSnapshot snapshot)
{
    public PendingEditInfo Info { get; } = info;
    public EditPreviewRequest Request { get; } = request;
    public EditDocumentSnapshot Snapshot { get; } = snapshot;
}

/// <summary>Keeps unsuccessful decisions retryable and prevents concurrent application of a preview.</summary>
internal sealed class PendingEditStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, PendingEdit> edits = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> applying = new(StringComparer.OrdinalIgnoreCase);

    public void Add(PendingEdit edit)
    {
        lock (gate)
            edits.Add(edit.Info.EditId, edit);
    }

    public PendingEditInfo[] List()
    {
        lock (gate)
            return edits.Values.OrderBy(edit => edit.Info.CreatedUtc).Select(edit => edit.Info).ToArray();
    }

    public EditDecisionResult Reject(string editId)
    {
        lock (gate)
        {
            if (applying.Contains(editId))
                return Failure(editId, "Pending edit is currently being applied.");
            if (!edits.TryGetValue(editId, out var edit))
                return Failure(editId, "Pending edit was not found.");
            edits.Remove(editId);
            return new EditDecisionResult(true, "Pending edit rejected.", editId, false, edit.Info, null);
        }
    }

    // The caller runs both callbacks synchronously on the VS UI thread: no await between
    // reading the live buffer and applying the checked edit at its captured coordinates.
    public EditDecisionResult Approve(
        string editId,
        Func<PendingEdit, EditDocumentSnapshot> readCurrent,
        Func<PendingEdit, DocumentMutationResult> apply)
    {
        PendingEdit edit;
        lock (gate)
        {
            if (!edits.TryGetValue(editId, out edit))
                return Failure(editId, "Pending edit was not found.");
            if (!applying.Add(editId))
                return Failure(editId, "Pending edit is currently being applied.");
        }

        try
        {
            if (!edit.Snapshot.Matches(readCurrent(edit)))
                return new EditDecisionResult(false,
                    "Document changed since this edit was previewed. No changes were applied. Reject this pending edit and create a fresh preview.",
                    editId, false, edit.Info, null);

            var mutation = apply(edit);
            if (mutation.Success)
            {
                lock (gate)
                    edits.Remove(editId);
            }

            return new EditDecisionResult(mutation.Success, mutation.Message, editId,
                mutation.Success, edit.Info, mutation);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EditDecisionResult(false, ex.Message, editId, false, edit.Info, null);
        }
        finally
        {
            lock (gate)
                applying.Remove(editId);
        }
    }

    private static EditDecisionResult Failure(string editId, string message) =>
        new(false, message, editId, false, null, null);
}
