using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface ISolutionCapabilityService
{
    Task<SolutionInfoResult> GetSolutionInfoAsync(CancellationToken cancellationToken);
    Task<ProjectListResult> ListProjectsAsync(CancellationToken cancellationToken);
    Task<ProjectInfo?> GetProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken);
    Task<StartupProjectResult> GetStartupProjectAsync(CancellationToken cancellationToken);
    Task<StartupProjectResult> SetStartupProjectAsync(StartupProjectSetRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> DiscoverTestsAsync(TestDiscoverRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> RunTestsAsync(TestRunRequest request, CancellationToken cancellationToken);
    Task<TestOperationResult> GetTestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken);
}

internal sealed class SolutionCapabilityService : ISolutionCapabilityService
{
    private const string TestPlatformUnsupportedMessage =
        "Visual Studio Test Platform integration is not wired in this VSIX slice yet.";

    private readonly AsyncPackage package;

    public SolutionCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<SolutionInfoResult> GetSolutionInfoAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var solution = dte.Solution;
        var path = EmptyToNull(solution?.FullName);
        var projects = EnumerateProjects(solution).ToArray();
        var startupProject = GetStartupProjectNames(dte).FirstOrDefault();

        return new SolutionInfoResult(
            string.IsNullOrWhiteSpace(path) ? null : Path.GetFileNameWithoutExtension(path),
            path,
            !string.IsNullOrWhiteSpace(path),
            projects.Length,
            startupProject);
    }

    public async Task<ProjectListResult> ListProjectsAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var projects = EnumerateProjects(dte.Solution)
            .Select(ProjectInfoFromProject)
            .ToArray();

        return new ProjectListResult(projects);
    }

    public async Task<ProjectInfo?> GetProjectInfoAsync(ProjectInfoRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        var dte = await GetDteAsync();
        var project = FindProject(dte.Solution, request.ProjectName);
        return project is null ? null : ProjectInfoFromProject(project);
    }

    public async Task<StartupProjectResult> GetStartupProjectAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var startupProjects = GetStartupProjectNames(dte).ToArray();
        return new StartupProjectResult(startupProjects, startupProjects.Length > 1);
    }

    public async Task<StartupProjectResult> SetStartupProjectAsync(StartupProjectSetRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        var dte = await GetDteAsync();
        var project = FindProject(dte.Solution, request.ProjectName)
            ?? throw new InvalidOperationException($"Project '{request.ProjectName}' was not found or is unsupported.");

        dte.Solution.SolutionBuild.StartupProjects = new[] { project.UniqueName };
        return new StartupProjectResult([project.UniqueName], false);
    }

    public Task<TestOperationResult> DiscoverTestsAsync(TestDiscoverRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TestOperationResult.Unsupported(TestPlatformUnsupportedMessage));
    }

    public Task<TestOperationResult> RunTestsAsync(TestRunRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TestOperationResult.Unsupported(TestPlatformUnsupportedMessage));
    }

    public Task<TestOperationResult> GetTestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken)
    {
        _ = request;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TestOperationResult.Unsupported(TestPlatformUnsupportedMessage));
    }

    private async Task<DTE2> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE2
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
    }

    private static IEnumerable<Project> EnumerateProjects(Solution? solution)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (solution?.Projects is null)
        {
            yield break;
        }

        foreach (Project project in solution.Projects)
        {
            foreach (var child in EnumerateProject(project))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<Project> EnumerateProject(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (project.Kind != ProjectKinds.vsProjectKindSolutionFolder)
        {
            yield return project;
            yield break;
        }

        foreach (ProjectItem item in project.ProjectItems)
        {
            var subProject = item.SubProject;
            if (subProject is null)
            {
                continue;
            }

            foreach (var child in EnumerateProject(subProject))
            {
                yield return child;
            }
        }
    }

    private static Project? FindProject(Solution? solution, string projectName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        foreach (var project in EnumerateProjects(solution))
        {
            if (string.Equals(GetProjectName(project), projectName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetProjectUniqueName(project), projectName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetProjectFullName(project), projectName, StringComparison.OrdinalIgnoreCase))
            {
                return project;
            }
        }

        return null;
    }

    private static ProjectInfo ProjectInfoFromProject(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var fullName = GetProjectFullName(project);
        return new ProjectInfo(
            GetProjectName(project),
            GetProjectUniqueName(project),
            fullName,
            GetProjectKind(project),
            !string.IsNullOrWhiteSpace(fullName),
            TryGetProperty(project, "Language"),
            TryGetProperty(project, "OutputFileName"));
    }

    private static string? GetProjectName(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return project.Name;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProjectUniqueName(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return project.UniqueName;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProjectFullName(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return EmptyToNull(project.FullName);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProjectKind(Project project)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return project.Kind;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyCollection<string> GetStartupProjectNames(DTE2 dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var startupProjects = dte.Solution?.SolutionBuild?.StartupProjects;
        if (startupProjects is null)
        {
            return [];
        }

        if (startupProjects is Array array)
        {
            return array.Cast<object>()
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();
        }

        var single = startupProjects.ToString();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    private static string? TryGetProperty(Project project, string propertyName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            return project.Properties?.Item(propertyName)?.Value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
