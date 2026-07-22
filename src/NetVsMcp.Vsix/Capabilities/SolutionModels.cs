using System.Collections.Generic;

namespace NetVsMcp.Vsix;

internal sealed class SolutionInfoResult
{
    public SolutionInfoResult(string? name, string? path, bool isOpen, int projectCount, string? startupProject)
    {
        Name = name;
        Path = path;
        IsOpen = isOpen;
        ProjectCount = projectCount;
        StartupProject = startupProject;
    }

    public string? Name { get; }
    public string? Path { get; }
    public bool IsOpen { get; }
    public int ProjectCount { get; }
    public string? StartupProject { get; }
}

internal sealed class ProjectListResult
{
    public ProjectListResult(IReadOnlyCollection<ProjectInfo> projects)
    {
        Projects = projects;
    }

    public IReadOnlyCollection<ProjectInfo> Projects { get; }
}

internal sealed class ProjectInfoRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

internal sealed class ProjectInfo
{
    public ProjectInfo(
        string? name,
        string? uniqueName,
        string? fullName,
        string? kind,
        bool isLoaded,
        string? language,
        string? outputFileName)
    {
        Name = name;
        UniqueName = uniqueName;
        FullName = fullName;
        Kind = kind;
        IsLoaded = isLoaded;
        Language = language;
        OutputFileName = outputFileName;
    }

    public string? Name { get; }
    public string? UniqueName { get; }
    public string? FullName { get; }
    public string? Kind { get; }
    public bool IsLoaded { get; }
    public string? Language { get; }
    public string? OutputFileName { get; }
}

internal sealed class StartupProjectResult
{
    public StartupProjectResult(IReadOnlyCollection<string> projects, bool isMultiStartup)
    {
        Projects = projects;
        IsMultiStartup = isMultiStartup;
    }

    public IReadOnlyCollection<string> Projects { get; }
    public bool IsMultiStartup { get; }
}

internal sealed class StartupProjectSetRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

internal sealed class TestDiscoverRequest
{
    public string? ProjectName { get; set; }
}

internal sealed class TestRunRequest
{
    public string? ProjectName { get; set; }
    public string? Filter { get; set; }
}

internal sealed class TestResultsRequest
{
    public string? RunId { get; set; }
}

internal sealed class TestOperationResult
{
    public TestOperationResult(bool supported, string message, IReadOnlyCollection<TestCaseInfo> tests, IReadOnlyCollection<TestResultInfo> results)
    {
        Supported = supported;
        Message = message;
        Tests = tests;
        Results = results;
    }

    public bool Supported { get; }
    public string Message { get; }
    public IReadOnlyCollection<TestCaseInfo> Tests { get; }
    public IReadOnlyCollection<TestResultInfo> Results { get; }

    public static TestOperationResult Unsupported(string message) =>
        new(false, message, [], []);
}

internal sealed class TestCaseInfo
{
    public TestCaseInfo(string name, string? projectName, string? source)
    {
        Name = name;
        ProjectName = projectName;
        Source = source;
    }

    public string Name { get; }
    public string? ProjectName { get; }
    public string? Source { get; }
}

internal sealed class TestResultInfo
{
    public TestResultInfo(string name, string outcome, string? duration, string? message)
    {
        Name = name;
        Outcome = outcome;
        Duration = duration;
        Message = message;
    }

    public string Name { get; }
    public string Outcome { get; }
    public string? Duration { get; }
    public string? Message { get; }
}
