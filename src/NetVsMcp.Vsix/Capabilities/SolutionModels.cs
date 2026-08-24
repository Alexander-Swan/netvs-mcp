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

internal sealed class SolutionOpenRequest
{
    public string Path { get; set; } = string.Empty;
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

internal sealed class SolutionAddProjectRequest
{
    public string ProjectPath { get; set; } = string.Empty;
}

internal sealed class ProjectFileRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}

internal sealed class ProjectFileResult
{
    public ProjectFileResult(bool success, string? message, ProjectInfo? project, string filePath)
    {
        Success = success;
        Message = message;
        Project = project;
        FilePath = filePath;
    }

    public bool Success { get; }
    public string? Message { get; }
    public ProjectInfo? Project { get; }
    public string FilePath { get; }
}

internal sealed class ProjectReferenceRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = "assembly";
    public string HintPath { get; set; } = string.Empty;
}

internal sealed class ProjectReferenceResult
{
    public ProjectReferenceResult(bool success, string message, ProjectInfo project, string reference, string referenceType)
    {
        Success = success;
        Message = message;
        Project = project;
        Reference = reference;
        ReferenceType = referenceType;
    }

    public bool Success { get; }
    public string Message { get; }
    public ProjectInfo Project { get; }
    public string Reference { get; }
    public string ReferenceType { get; }
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

internal sealed class TestDebugRequest
{
    public string? ProjectName { get; set; }
    public string? Filter { get; set; }
    public int AttachTimeoutSeconds { get; set; } = 30;
    public bool NoBuild { get; set; }
    public string? Configuration { get; set; }
    public string? Framework { get; set; }
}

internal sealed class TestResultsRequest
{
    public string? RunId { get; set; }
}

internal sealed class PackageRestoreRequest
{
    public string? ProjectName { get; set; }
}

internal sealed class NugetListRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

internal sealed class NugetSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
    public bool IncludePrerelease { get; set; }
}

internal sealed class NugetPackageMutationRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

internal sealed class NugetPackageInfo
{
    public NugetPackageInfo(string id, string version, string projectName, string projectPath)
    {
        Id = id;
        Version = version;
        ProjectName = projectName;
        ProjectPath = projectPath;
    }

    public string Id { get; }
    public string Version { get; }
    public string ProjectName { get; }
    public string ProjectPath { get; }
}

internal sealed class NugetListResult
{
    public NugetListResult(IReadOnlyCollection<NugetPackageInfo> packages)
    {
        Packages = packages;
    }

    public IReadOnlyCollection<NugetPackageInfo> Packages { get; }
}

internal sealed class NugetSearchResult
{
    public NugetSearchResult(IReadOnlyCollection<NugetPackageInfo> packages)
    {
        Packages = packages;
    }

    public IReadOnlyCollection<NugetPackageInfo> Packages { get; }
}

internal sealed class NugetMutationResult
{
    public NugetMutationResult(bool success, string message, ProjectInfo project, string packageId, string version, int exitCode)
    {
        Success = success;
        Message = message;
        Project = project;
        PackageId = packageId;
        Version = version;
        ExitCode = exitCode;
    }

    public bool Success { get; }
    public string Message { get; }
    public ProjectInfo Project { get; }
    public string PackageId { get; }
    public string Version { get; }
    public int ExitCode { get; }
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

internal sealed class TestDebugResult
{
    public TestDebugResult(
        bool supported,
        string message,
        string? projectName,
        string? filter,
        int? testHostProcessId,
        string? testHostProcessName,
        int? testRunnerProcessId = null,
        string? testRunnerProcessName = null,
        string? commandLine = null,
        string? workingDirectory = null,
        string? targetPath = null,
        int attachTimeoutSeconds = 30)
    {
        Supported = supported;
        Message = message;
        ProjectName = projectName;
        Filter = filter;
        TestHostProcessId = testHostProcessId;
        TestHostProcessName = testHostProcessName;
        TestRunnerProcessId = testRunnerProcessId;
        TestRunnerProcessName = testRunnerProcessName;
        CommandLine = commandLine;
        WorkingDirectory = workingDirectory;
        TargetPath = targetPath;
        AttachTimeoutSeconds = attachTimeoutSeconds;
    }

    public bool Supported { get; }
    public string Message { get; }
    public string? ProjectName { get; }
    public string? Filter { get; }
    public int? TestHostProcessId { get; }
    public string? TestHostProcessName { get; }
    public int? TestRunnerProcessId { get; }
    public string? TestRunnerProcessName { get; }
    public string? CommandLine { get; }
    public string? WorkingDirectory { get; }
    public string? TargetPath { get; }
    public int AttachTimeoutSeconds { get; }
}

internal sealed class PackageRestoreResult
{
    public PackageRestoreResult(bool supported, string message, ProjectInfo? project, int exitCode)
    {
        Supported = supported;
        Message = message;
        Project = project;
        ExitCode = exitCode;
    }

    public bool Supported { get; }
    public string Message { get; }
    public ProjectInfo? Project { get; }
    public int ExitCode { get; }
}
