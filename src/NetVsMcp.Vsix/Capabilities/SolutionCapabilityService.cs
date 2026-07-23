using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
    Task<PackageRestoreResult> RestorePackagesAsync(PackageRestoreRequest request, CancellationToken cancellationToken);
}

internal sealed class SolutionCapabilityService : ISolutionCapabilityService
{
    private const string DotnetExecutable = "dotnet";

    private readonly AsyncPackage package;
    private TestOperationResult? lastTestResult;
    private string? lastTestRunId;

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

    public async Task<TestOperationResult> DiscoverTestsAsync(TestDiscoverRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var target = ResolveTestTarget(dte, request.ProjectName);
        var process = await RunProcessAsync(
            DotnetExecutable,
            $"test {QuoteArgument(target.Path)} --list-tests",
            target.WorkingDirectory,
            cancellationToken);
        var tests = ParseListedTests(process.StandardOutput, target.ProjectName);

        return new TestOperationResult(
            supported: process.ExitCode == 0,
            message: process.ExitCode == 0
                ? $"Discovered {tests.Count} test(s) from {target.DisplayName}."
                : CreateProcessFailureMessage("Test discovery failed", process),
            tests: tests,
            results: []);
    }

    public async Task<TestOperationResult> RunTestsAsync(TestRunRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var target = ResolveTestTarget(dte, request.ProjectName);
        var runId = Guid.NewGuid().ToString("N");
        var resultsDirectory = Path.Combine(Path.GetTempPath(), "NetVsMcp", "TestResults", runId);
        Directory.CreateDirectory(resultsDirectory);

        var arguments = new StringBuilder()
            .Append("test ")
            .Append(QuoteArgument(target.Path))
            .Append(" --logger ")
            .Append(QuoteArgument($"trx;LogFileName={runId}.trx"))
            .Append(" --results-directory ")
            .Append(QuoteArgument(resultsDirectory));

        var filter = EmptyToNull(request.Filter);
        if (filter is not null)
        {
            arguments
                .Append(" --filter ")
                .Append(QuoteArgument(filter));
        }

        var process = await RunProcessAsync(
            DotnetExecutable,
            arguments.ToString(),
            target.WorkingDirectory,
            cancellationToken);
        var resultPath = Path.Combine(resultsDirectory, $"{runId}.trx");
        var results = File.Exists(resultPath)
            ? ParseTrxResults(resultPath)
            : Array.Empty<TestResultInfo>();

        var result = new TestOperationResult(
            supported: process.ExitCode == 0,
            message: process.ExitCode == 0
                ? $"Ran {results.Count} test result(s) from {target.DisplayName}. RunId: {runId}."
                : CreateProcessFailureMessage($"Test run failed for {target.DisplayName}. RunId: {runId}", process),
            tests: [],
            results: results);

        lastTestRunId = runId;
        lastTestResult = result;
        return result;
    }

    public async Task<TestOperationResult> GetTestResultsAsync(TestResultsRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var runId = EmptyToNull(request.RunId);
        if (lastTestResult is not null &&
            (runId is null || string.Equals(runId, lastTestRunId, StringComparison.OrdinalIgnoreCase)))
        {
            return lastTestResult;
        }

        return TestOperationResult.Unsupported(
            runId is null
                ? "No test results have been captured by this VSIX session yet."
                : $"No captured test results were found for runId '{runId}'.");
    }

    public async Task<PackageRestoreResult> RestorePackagesAsync(PackageRestoreRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var projectName = EmptyToNull(request.ProjectName);
        var target = ResolveTestTarget(dte, projectName);
        ProjectInfo? project = null;
        if (projectName is not null)
        {
            var dteProject = FindProject(dte.Solution, projectName);
            project = dteProject is null ? null : ProjectInfoFromProject(dteProject);
        }

        var process = await RunProcessAsync(
            DotnetExecutable,
            $"restore {QuoteArgument(target.Path)}",
            target.WorkingDirectory,
            cancellationToken);

        return new PackageRestoreResult(
            process.ExitCode == 0,
            process.ExitCode == 0
                ? $"Restored packages for {target.DisplayName}."
                : CreateProcessFailureMessage($"Package restore failed for {target.DisplayName}", process),
            project,
            process.ExitCode);
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

    private static TestTarget ResolveTestTarget(DTE2 dte, string? requestedProjectName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var projectName = EmptyToNull(requestedProjectName);
        if (projectName is not null)
        {
            var project = FindProject(dte.Solution, projectName)
                ?? throw new InvalidOperationException($"Project '{projectName}' was not found or is unsupported.");
            var projectPath = EmptyToNull(GetProjectFullName(project))
                ?? throw new InvalidOperationException($"Project '{projectName}' does not have a project file path.");

            return new TestTarget(
                projectPath,
                Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory,
                GetProjectName(project) ?? projectName);
        }

        var solutionPath = EmptyToNull(dte.Solution?.FullName)
            ?? throw new InvalidOperationException("No solution is open in Visual Studio.");
        return new TestTarget(
            solutionPath,
            Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory,
            Path.GetFileName(solutionPath));
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var output = new StringBuilder();
            var error = new StringBuilder();
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    output.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    error.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                }
            });

            process.WaitForExit();
            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
        }, cancellationToken);
    }

    private static IReadOnlyCollection<TestCaseInfo> ParseListedTests(string output, string? projectName)
    {
        var tests = new List<TestCaseInfo>();
        var inList = false;
        using var reader = new StringReader(output);
        for (var line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("The following Tests are available:", StringComparison.OrdinalIgnoreCase))
            {
                inList = true;
                continue;
            }

            if (!inList ||
                trimmed.StartsWith("Passed!", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Failed!", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tests.Add(new TestCaseInfo(trimmed, projectName, null));
        }

        return tests;
    }

    private static IReadOnlyCollection<TestResultInfo> ParseTrxResults(string resultPath)
    {
        var document = XDocument.Load(resultPath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "UnitTestResult")
            .Select(element => new TestResultInfo(
                element.Attribute("testName")?.Value ?? element.Attribute("testId")?.Value ?? "Unknown",
                element.Attribute("outcome")?.Value ?? "Unknown",
                element.Attribute("duration")?.Value,
                element.Descendants().FirstOrDefault(child => child.Name.LocalName == "Message")?.Value))
            .ToArray();
    }

    private static string CreateProcessFailureMessage(string prefix, ProcessResult process)
    {
        var detail = EmptyToNull(process.StandardError) ?? EmptyToNull(process.StandardOutput);
        return detail is null
            ? $"{prefix}. Exit code: {process.ExitCode}."
            : $"{prefix}. Exit code: {process.ExitCode}. {detail.Trim()}";
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private sealed class TestTarget
    {
        public TestTarget(string path, string workingDirectory, string displayName)
        {
            Path = path;
            WorkingDirectory = workingDirectory;
            DisplayName = displayName;
            ProjectName = System.IO.Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : displayName;
        }

        public string Path { get; }
        public string WorkingDirectory { get; }
        public string DisplayName { get; }
        public string? ProjectName { get; }
    }

    private sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
    }
}
