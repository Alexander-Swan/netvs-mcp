namespace NetVsMcp.Contracts;

public sealed class BuildSolutionRequest
{
    /// <summary>If false, the call returns immediately after kicking off the build; poll <c>build_status</c> for completion.</summary>
    public bool WaitForBuildToFinish { get; set; }
}

public sealed record BuildSolutionResult(
    BuildStatusInfo Status,
    int LastBuildInfo);

public sealed record BuildStatusInfo(
    /// <summary>e.g. "Building", "Idle", "Cancelled".</summary>
    string State,
    /// <summary>DTE's raw <c>LastBuildInfo</c> error count.</summary>
    int LastBuildInfo);

public sealed record BuildConfigurationInfo(
    string? Configuration,
    string? Platform);

public sealed class BuildProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public bool WaitForBuildToFinish { get; set; } = true;
}

public sealed class BuildConfigurationSetRequest
{
    public string Configuration { get; set; } = string.Empty;
    /// <summary>Omit to leave the active platform unchanged.</summary>
    public string? Platform { get; set; }
}

public sealed class ErrorListRequest
{
    public bool IncludeWarnings { get; set; } = true;

    public int MaxItems { get; set; } = 200;
}

public sealed record ErrorListResult(
    IReadOnlyCollection<ErrorListItemInfo> Items);

public sealed record ErrorListItemInfo(
    string? Description,
    string? File,
    int Line,
    int Column,
    /// <summary>e.g. "Error", "Warning", "Message" — VS Error List severity level.</summary>
    string Level,
    string? Project);

public sealed class TaskListRequest
{
    /// <summary>Include comment-token tasks (e.g. "// TODO:").</summary>
    public bool IncludeCommentTasks { get; set; } = true;
    /// <summary>Include manually-added Task List entries.</summary>
    public bool IncludeUserTasks { get; set; } = true;
    public int MaxItems { get; set; } = 200;
}

public sealed record TaskListResult(
    IReadOnlyCollection<TaskListItemInfo> Items);

public sealed record TaskListItemInfo(
    /// <summary>Position of this item in VS's Task List, used to target it in mutation requests.</summary>
    int Index,
    string? Description,
    string? File,
    int Line,
    string Priority,
    string Category,
    /// <summary>True for a manually-added task; false for a comment-token task.</summary>
    bool IsUserTask,
    /// <summary>Checkbox state; null for task kinds that don't support it.</summary>
    bool? Checked);

public sealed class TaskListAddRequest
{
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}

public sealed class TaskListMutationRequest
{
    /// <summary>See <see cref="TaskListItemInfo.Index"/>.</summary>
    public int Index { get; set; }
}

public sealed class TaskListSetCheckedRequest
{
    public int Index { get; set; }
    public bool Checked { get; set; }
}

public sealed record TaskListMutationResult(
    bool Success,
    string Message);

public sealed class OutputReadRequest
{
    /// <summary>Omit to read the currently active output pane.</summary>
    public string? PaneName { get; set; }

    public int MaxChars { get; set; } = 20000;
}

public sealed record OutputReadResult(
    string? PaneName,
    string Text,
    /// <summary>True if <see cref="Text"/> was cut off by the request's max-chars limit.</summary>
    bool Truncated);

public sealed record OutputPaneInfo(string Name);

public sealed record OutputPaneListResult(
    IReadOnlyCollection<OutputPaneInfo> Panes);

public sealed class OutputPaneRequest
{
    public string? PaneName { get; set; }
}

public sealed class OutputWriteRequest
{
    public string? PaneName { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>Bring the pane to the foreground of the Output window after writing.</summary>
    public bool Activate { get; set; }
}

public sealed record BuildAndGetErrorsResult(
    BuildSolutionResult Build,
    ErrorListResult Errors);
