using System.Collections.Generic;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal sealed class BuildSolutionRequest
{
    public bool WaitForBuildToFinish { get; set; }
}

internal sealed class BuildProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public bool WaitForBuildToFinish { get; set; } = true;
}

internal sealed class BuildConfigurationSetRequest
{
    public string Configuration { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
}

internal sealed class BuildSolutionResult
{
    public BuildSolutionResult(BuildStatusInfo status, int lastBuildInfo)
    {
        Status = status;
        LastBuildInfo = lastBuildInfo;
    }

    public BuildStatusInfo Status { get; }
    public int LastBuildInfo { get; }
}

internal sealed class BuildStatusInfo
{
    public BuildStatusInfo(string state, int lastBuildInfo)
    {
        State = state;
        LastBuildInfo = lastBuildInfo;
    }

    public string State { get; }
    public int LastBuildInfo { get; }
}

internal sealed class BuildConfigurationInfo
{
    public BuildConfigurationInfo(string configuration, string platform)
    {
        Configuration = configuration;
        Platform = platform;
    }

    public string Configuration { get; }
    public string Platform { get; }
}

internal sealed class ErrorListRequest
{
    public bool IncludeWarnings { get; set; } = true;
    public int MaxItems { get; set; } = 200;
}

internal sealed class ErrorListResult
{
    public ErrorListResult(IReadOnlyCollection<ErrorListItemInfo> items)
    {
        Items = items;
    }

    public IReadOnlyCollection<ErrorListItemInfo> Items { get; }
}

internal sealed class ErrorListItemInfo
{
    public ErrorListItemInfo(
        string? description,
        string? file,
        int line,
        int column,
        string level,
        string? project)
    {
        Description = description;
        File = file;
        Line = line;
        Column = column;
        Level = level;
        Project = project;
    }

    public string? Description { get; }
    public string? File { get; }
    public int Line { get; }
    public int Column { get; }
    public string Level { get; }
    public string? Project { get; }

    public static ErrorListItemInfo FromErrorItem(ErrorItem item)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new ErrorListItemInfo(
            item.Description,
            item.FileName,
            item.Line,
            item.Column,
            item.ErrorLevel.ToString(),
            item.Project);
    }
}

internal static class TaskListCategories
{
    // EnvDTE task categories are free-form strings, not an enum - VS has no
    // built-in "user task" constant. This is our own convention: task_list_add
    // always tags new items with this category so task_list_remove/set_checked
    // can tell them apart from read-only comment-token tasks (typically "Comment").
    public const string User = "NetVsMcp User Task";
}

internal sealed class TaskListRequest
{
    public bool IncludeCommentTasks { get; set; } = true;
    public bool IncludeUserTasks { get; set; } = true;
    public int MaxItems { get; set; } = 200;
}

internal sealed class TaskListResult
{
    public TaskListResult(IReadOnlyCollection<TaskListItemInfo> items)
    {
        Items = items;
    }

    public IReadOnlyCollection<TaskListItemInfo> Items { get; }
}

internal sealed class TaskListItemInfo
{
    public TaskListItemInfo(
        int index,
        string? description,
        string? file,
        int line,
        string priority,
        string category,
        bool isUserTask,
        bool? isChecked)
    {
        Index = index;
        Description = description;
        File = file;
        Line = line;
        Priority = priority;
        Category = category;
        IsUserTask = isUserTask;
        Checked = isChecked;
    }

    public int Index { get; }
    public string? Description { get; }
    public string? File { get; }
    public int Line { get; }
    public string Priority { get; }
    public string Category { get; }
    public bool IsUserTask { get; }
    public bool? Checked { get; }

    public static TaskListItemInfo FromTaskItem(int index, TaskItem item)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var isUserTask = string.Equals(item.Category, TaskListCategories.User, System.StringComparison.OrdinalIgnoreCase);
        bool? isChecked = null;
        try
        {
            isChecked = item.Checked;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Not all task items expose a checkbox; leave Checked null in that case.
        }

        return new TaskListItemInfo(
            index,
            item.Description,
            item.FileName,
            item.Line,
            item.Priority.ToString(),
            item.Category,
            isUserTask,
            isChecked);
    }
}

internal sealed class TaskListAddRequest
{
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}

internal sealed class TaskListMutationRequest
{
    public int Index { get; set; }
}

internal sealed class TaskListSetCheckedRequest
{
    public int Index { get; set; }
    public bool Checked { get; set; }
}

internal sealed class TaskListMutationResult
{
    public TaskListMutationResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }
}

internal sealed class OutputReadRequest
{
    public string? PaneName { get; set; }
    public int MaxChars { get; set; } = 20000;
}

internal sealed class OutputReadResult
{
    public OutputReadResult(string? paneName, string text, bool truncated)
    {
        PaneName = paneName;
        Text = text;
        Truncated = truncated;
    }

    public string? PaneName { get; }
    public string Text { get; }
    public bool Truncated { get; }
}

internal sealed class OutputPaneInfo
{
    public OutputPaneInfo(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

internal sealed class OutputPaneListResult
{
    public OutputPaneListResult(IReadOnlyCollection<OutputPaneInfo> panes)
    {
        Panes = panes;
    }

    public IReadOnlyCollection<OutputPaneInfo> Panes { get; }
}

internal sealed class OutputPaneRequest
{
    public string PaneName { get; set; } = string.Empty;
}

internal sealed class OutputWriteRequest
{
    public string PaneName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool Activate { get; set; }
}
