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
    public string DocumentPath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public int? EndLine { get; set; }
    public int? EndColumn { get; set; }
    public int Index { get; set; }
}

internal sealed class CodeActionInfo
{
    public CodeActionInfo(int index, string title, string kind, string? diagnosticId, string? equivalenceKey)
    {
        Index = index;
        Title = title;
        Kind = kind;
        DiagnosticId = diagnosticId;
        EquivalenceKey = equivalenceKey;
    }

    public int Index { get; }
    public string Title { get; }

    // "fix" (tied to a compiler/analyzer diagnostic) or "refactor" (CodeRefactoringProvider).
    public string Kind { get; }
    public string? DiagnosticId { get; }
    public string? EquivalenceKey { get; }
}

internal sealed class CodeActionsListResult
{
    public CodeActionsListResult(CodePositionRequest position, IReadOnlyCollection<CodeActionInfo> actions)
    {
        Position = position;
        Actions = actions;
    }

    public CodePositionRequest Position { get; }
    public IReadOnlyCollection<CodeActionInfo> Actions { get; }
}

internal sealed class CodeActionsApplyResult
{
    public CodeActionsApplyResult(
        bool success,
        string message,
        string? appliedTitle,
        IReadOnlyCollection<RenameSymbolChangeInfo> changes)
    {
        Success = success;
        Message = message;
        AppliedTitle = appliedTitle;
        Changes = changes;
    }

    public bool Success { get; }
    public string Message { get; }
    public string? AppliedTitle { get; }
    public IReadOnlyCollection<RenameSymbolChangeInfo> Changes { get; }
}
