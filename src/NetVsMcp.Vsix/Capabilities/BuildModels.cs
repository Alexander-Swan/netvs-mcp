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
