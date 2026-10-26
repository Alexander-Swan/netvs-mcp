using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal sealed class CodeActionsListRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
}

internal sealed class CodeActionsApplyRequest
{
    public string? ActionId { get; set; }
    public bool PreviewOnly { get; set; }
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
    public int Index { get; set; }
}

internal sealed class CodeActionInfo
{
    public CodeActionInfo(int index, string title, string kind, string? diagnosticId, string? equivalenceKey, string? actionId = null)
    {
        Index = index;
        Title = title;
        Kind = kind;
        DiagnosticId = diagnosticId;
        EquivalenceKey = equivalenceKey;
        ActionId = actionId;
    }

    public int Index { get; }
    public string Title { get; }

    // "fix" (tied to a compiler/analyzer diagnostic) or "refactor" (CodeRefactoringProvider).
    public string Kind { get; }
    public string? DiagnosticId { get; }
    public string? EquivalenceKey { get; }
    public string? ActionId { get; }
}

internal sealed class CodeActionsListResult
{
    public CodeActionsListResult(CodePositionRequest position, IReadOnlyCollection<CodeActionInfo> actions,
        NetVsMcp.Contracts.CodeActionDiscoveryInfo? discovery = null)
    {
        Position = position;
        Actions = actions;
        Discovery = discovery;
    }

    public CodePositionRequest Position { get; }
    public IReadOnlyCollection<CodeActionInfo> Actions { get; }
    public NetVsMcp.Contracts.CodeActionDiscoveryInfo? Discovery { get; }
}

internal sealed class CodeActionsApplyResult
{
    public CodeActionsApplyResult(
        bool success,
        string message,
        string? appliedTitle,
        IReadOnlyCollection<RenameSymbolChangeInfo> changes, string? outcome = null, bool hasObservedChanges = false,
        string? recoveryId = null, IReadOnlyCollection<NetVsMcp.Contracts.CodeActionDocumentVersion>? documentVersions = null,
        bool changesTruncated = false)
    {
        Success = success;
        Message = message;
        AppliedTitle = appliedTitle;
        Changes = changes;
        Outcome = outcome; HasObservedChanges = hasObservedChanges; RecoveryId = recoveryId; DocumentVersions = documentVersions;
        ChangesTruncated = changesTruncated;
    }

    public bool Success { get; }
    public string Message { get; }
    public string? AppliedTitle { get; }
    public IReadOnlyCollection<RenameSymbolChangeInfo> Changes { get; }
    public string? Outcome { get; }
    public bool HasObservedChanges { get; }
    public string? RecoveryId { get; }
    public IReadOnlyCollection<NetVsMcp.Contracts.CodeActionDocumentVersion>? DocumentVersions { get; }
    public bool ChangesTruncated { get; }
}
