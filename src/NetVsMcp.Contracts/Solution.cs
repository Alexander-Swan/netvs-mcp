namespace NetVsMcp.Contracts;

public sealed record SolutionInfoResult(
    string? Name,
    string? Path,
    bool IsOpen,
    int ProjectCount,
    string? StartupProject);

public sealed class SolutionOpenRequest
{
    public string Path { get; set; } = string.Empty;
}

public sealed record ProjectListResult(
    IReadOnlyCollection<ProjectInfo> Projects);

public sealed class ProjectInfoRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class SolutionAddProjectRequest
{
    public string ProjectPath { get; set; } = string.Empty;
}

public sealed class ProjectFileRequest
{
    public string ProjectName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
}

public sealed record ProjectFileResult(
    bool Success,
    string? Message,
    ProjectInfo? Project,
    string FilePath);

public sealed record ProjectInfo(
    string? Name,
    string? UniqueName,
    string? FullName,
    string? Kind,
    bool IsLoaded,
    string? Language,
    string? OutputFileName);

public sealed class ProjectReferenceRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    /// <summary>"assembly", "project", or "package" — selects how <see cref="Reference"/> is interpreted.</summary>
    public string ReferenceType { get; set; } = "assembly";
    /// <summary>Optional path hint for assembly references that aren't on a standard search path.</summary>
    public string? HintPath { get; set; }
}

public sealed record ProjectReferenceResult(
    bool Success,
    string? Message,
    ProjectInfo? Project,
    string Reference,
    string ReferenceType);

public sealed class NugetListRequest
{
    /// <summary>Restricts the listing to one project; omit to list across the whole solution.</summary>
    public string? ProjectName { get; set; }
}

public sealed class NugetSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
    public bool IncludePrerelease { get; set; }
}

public sealed class NugetPackageMutationRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    /// <summary>Omit to let NuGet pick the latest compatible version (install/update) or ignore for uninstall.</summary>
    public string? Version { get; set; }
}

public sealed record NugetPackageInfo(
    string Id,
    string? Version,
    string? ProjectName,
    string? ProjectPath);

public sealed record NugetListResult(
    IReadOnlyCollection<NugetPackageInfo> Packages);

public sealed record NugetSearchResult(
    IReadOnlyCollection<NugetPackageInfo> Packages);

public sealed record NugetMutationResult(
    bool Success,
    string Message,
    ProjectInfo? Project,
    string PackageId,
    string? Version,
    /// <summary>Exit code of the underlying NuGet/dotnet operation.</summary>
    int ExitCode);

public sealed record StartupProjectResult(
    IReadOnlyCollection<string> Projects,
    bool IsMultiStartup);

public sealed class StartupProjectSetRequest
{
    public string ProjectName { get; set; } = string.Empty;
}

public sealed class TestDiscoverRequest
{
    public string? ProjectName { get; set; }
}

public sealed class TestRunRequest
{
    public string? ProjectName { get; set; }

    /// <summary>An MSTest/xUnit/NUnit-style test filter expression passed through to the underlying test runner.</summary>
    public string? Filter { get; set; }
}

public sealed class TestDebugRequest
{
    public string? ProjectName { get; set; }

    public string? Filter { get; set; }

    public int AttachTimeoutSeconds { get; set; } = 30;

    public bool NoBuild { get; set; }

    public string? Configuration { get; set; }

    public string? Framework { get; set; }
}

public sealed class TestResultsRequest
{
    /// <summary>Identifies a previously started run; omit to get the most recent run's results.</summary>
    public string? RunId { get; set; }
}

public sealed record TestOperationResult(
    /// <summary>False when the solution has no test adapter/runner support available.</summary>
    bool Supported,
    string Message,
    IReadOnlyCollection<TestCaseInfo> Tests,
    IReadOnlyCollection<TestResultInfo> Results);

public sealed record TestCaseInfo(
    string Name,
    string? ProjectName,
    string? Source);

public sealed record TestResultInfo(
    string Name,
    /// <summary>e.g. "Passed", "Failed", "Skipped" — from the TRX outcome value.</summary>
    string Outcome,
    string? Duration,
    string? Message);

public sealed record TestDebugResult(
    bool Supported,
    string Message,
    string? ProjectName,
    string? Filter,
    int? TestHostProcessId,
    string? TestHostProcessName,
    int? TestRunnerProcessId = null,
    string? TestRunnerProcessName = null,
    string? CommandLine = null,
    string? WorkingDirectory = null,
    string? TargetPath = null,
    int AttachTimeoutSeconds = 30);

public sealed record TestRunAndGetResultsResult(
    TestOperationResult Run,
    TestOperationResult Results);

public sealed record SolutionOverviewResult(
    SolutionInfoResult Solution,
    ProjectListResult Projects,
    StartupProjectResult StartupProject,
    IReadOnlyCollection<ProjectInfo> TestProjects);

public sealed record ProjectDependenciesResult(
    ProjectInfo? Project,
    IReadOnlyCollection<string> TargetFrameworks,
    IReadOnlyCollection<ProjectDependencyInfo> ProjectReferences,
    IReadOnlyCollection<ProjectDependencyInfo> PackageReferences);

public sealed record ProjectDependencyInfo(
    string Name,
    string? Version,
    string? Path);

public sealed class PackageRestoreRequest
{
    /// <summary>Restricts restore to one project; omit to restore the whole solution.</summary>
    public string? ProjectName { get; set; }
}

public sealed record PackageRestoreResult(
    bool Supported,
    string Message,
    ProjectInfo? Project,
    int ExitCode = 0);

public sealed record GitContextResult(
    bool Supported,
    string Message,
    /// <summary>Repository root as resolved by git, which may differ from the solution's directory.</summary>
    string? RootPath,
    IReadOnlyCollection<string> ChangedFiles);
