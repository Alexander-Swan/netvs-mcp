using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IBuildCapabilityService
{
    Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken);
    Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken);
    Task<BuildStatusInfo> CancelBuildAsync(CancellationToken cancellationToken);
    Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken);
    Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken);
    Task<BuildStatusInfo> GetBuildStatusAsync(CancellationToken cancellationToken);
    Task<BuildConfigurationInfo> GetBuildConfigurationAsync(CancellationToken cancellationToken);
    Task<BuildConfigurationInfo> SetBuildConfigurationAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken);
    Task<ErrorListResult> ListErrorsAsync(ErrorListRequest request, CancellationToken cancellationToken);
    Task<TaskListResult> ListTaskItemsAsync(TaskListRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> AddTaskItemAsync(TaskListAddRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> RemoveTaskItemAsync(TaskListMutationRequest request, CancellationToken cancellationToken);
    Task<TaskListMutationResult> SetTaskItemCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken);
    Task<OutputReadResult> ReadOutputAsync(OutputReadRequest request, CancellationToken cancellationToken);
    Task<OutputPaneListResult> ListOutputPanesAsync(CancellationToken cancellationToken);
    Task<OutputReadResult> ClearOutputAsync(OutputPaneRequest request, CancellationToken cancellationToken);
    Task<OutputReadResult> WriteOutputAsync(OutputWriteRequest request, CancellationToken cancellationToken);
}

internal sealed class BuildCapabilityService : IBuildCapabilityService
{
    private readonly AsyncPackage package;

    public BuildCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");

        solutionBuild.Build(request.WaitForBuildToFinish);

        return new BuildSolutionResult(
            GetBuildStatus(solutionBuild),
            solutionBuild.LastBuildInfo);
    }

    public async Task<BuildSolutionResult> BuildProjectAsync(BuildProjectRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        var projectUniqueName = FindProjectUniqueName(dte.Solution, request.ProjectName)
            ?? throw new InvalidOperationException($"Project '{request.ProjectName}' was not found.");

        solutionBuild.BuildProject(solutionBuild.ActiveConfiguration.Name, projectUniqueName, request.WaitForBuildToFinish);
        return new BuildSolutionResult(GetBuildStatus(solutionBuild), solutionBuild.LastBuildInfo);
    }

    public async Task<BuildStatusInfo> CancelBuildAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        dte.ExecuteCommand("Build.Cancel");
        return GetBuildStatus(solutionBuild);
    }

    public async Task<BuildSolutionResult> CleanSolutionAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        solutionBuild.Clean(WaitForCleanToFinish: true);
        return new BuildSolutionResult(GetBuildStatus(solutionBuild), solutionBuild.LastBuildInfo);
    }

    public async Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        solutionBuild.Clean(WaitForCleanToFinish: true);
        solutionBuild.Build(request.WaitForBuildToFinish);
        return new BuildSolutionResult(GetBuildStatus(solutionBuild), solutionBuild.LastBuildInfo);
    }

    public async Task<BuildStatusInfo> GetBuildStatusAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");

        return GetBuildStatus(solutionBuild);
    }

    public async Task<BuildConfigurationInfo> GetBuildConfigurationAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var activeConfiguration = dte.Solution?.SolutionBuild?.ActiveConfiguration;
        if (activeConfiguration is null)
        {
            return new BuildConfigurationInfo(string.Empty, string.Empty);
        }

        var platform = activeConfiguration is SolutionConfiguration2 configuration2
            ? configuration2.PlatformName
            : string.Empty;
        return new BuildConfigurationInfo(activeConfiguration.Name, platform ?? string.Empty);
    }

    public async Task<BuildConfigurationInfo> SetBuildConfigurationAsync(BuildConfigurationSetRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Configuration))
        {
            throw new ArgumentException("Configuration is required.", nameof(request));
        }

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var configurations = dte.Solution?.SolutionBuild?.SolutionConfigurations
            ?? throw new InvalidOperationException("Visual Studio solution build configurations are unavailable.");

        for (var index = 1; index <= configurations.Count; index++)
        {
            var configuration = configurations.Item(index);
            var configuration2 = configuration as SolutionConfiguration2;
            var platform = configuration2?.PlatformName ?? string.Empty;
            if (string.Equals(configuration.Name, request.Configuration, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(request.Platform) || string.Equals(platform, request.Platform, StringComparison.OrdinalIgnoreCase)))
            {
                configuration.Activate();
                return new BuildConfigurationInfo(configuration.Name, platform);
            }
        }

        throw new InvalidOperationException($"Build configuration '{request.Configuration}' was not found.");
    }


    public async Task<ErrorListResult> ListErrorsAsync(ErrorListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var errorItems = dte.ToolWindows?.ErrorList?.ErrorItems;
        if (errorItems is null)
        {
            return new ErrorListResult(Array.Empty<ErrorListItemInfo>());
        }

        var items = new List<ErrorListItemInfo>();
        var maxItems = request.MaxItems <= 0 ? int.MaxValue : request.MaxItems;
        for (var index = 1; index <= errorItems.Count && items.Count < maxItems; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = errorItems.Item(index);
            if (item is null || !ShouldInclude(item, request))
            {
                continue;
            }

            items.Add(ErrorListItemInfo.FromErrorItem(item));
        }

        return new ErrorListResult(items);
    }

    public async Task<TaskListResult> ListTaskItemsAsync(TaskListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var taskItems = dte.ToolWindows?.TaskList?.TaskItems;
        if (taskItems is null)
        {
            return new TaskListResult(Array.Empty<TaskListItemInfo>());
        }

        var items = new List<TaskListItemInfo>();
        var maxItems = request.MaxItems <= 0 ? int.MaxValue : request.MaxItems;
        for (var index = 1; index <= taskItems.Count && items.Count < maxItems; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = taskItems.Item(index);
            if (item is null)
            {
                continue;
            }

            var info = TaskListItemInfo.FromTaskItem(index, item);
            if (info.IsUserTask && !request.IncludeUserTasks)
            {
                continue;
            }

            if (!info.IsUserTask && !request.IncludeCommentTasks)
            {
                continue;
            }

            items.Add(info);
        }

        return new TaskListResult(items);
    }

    public async Task<TaskListMutationResult> AddTaskItemAsync(TaskListAddRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return new TaskListMutationResult(false, "Description is required.");
        }

        if (!Enum.TryParse<vsTaskPriority>("vsTaskPriority" + request.Priority, true, out var priority))
        {
            return new TaskListMutationResult(false, $"Unrecognized priority '{request.Priority}'. Use High, Medium, or Low.");
        }

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var taskItems = dte.ToolWindows?.TaskList?.TaskItems
            ?? throw new InvalidOperationException("Visual Studio Task List service is unavailable.");

        taskItems.Add(
            TaskListCategories.User,
            string.Empty,
            request.Description,
            priority,
            vsTaskIcon.vsTaskIconUser,
            Checkable: true);

        return new TaskListMutationResult(true, "Task item added.");
    }

    public async Task<TaskListMutationResult> RemoveTaskItemAsync(TaskListMutationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var item = await GetUserTaskItemAsync(request.Index, "removed", cancellationToken);
        if (item is null)
        {
            return new TaskListMutationResult(false, $"No editable user task item was found at index {request.Index}.");
        }

        item.Delete();
        return new TaskListMutationResult(true, "Task item removed.");
    }

    public async Task<TaskListMutationResult> SetTaskItemCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var item = await GetUserTaskItemAsync(request.Index, "checked", cancellationToken);
        if (item is null)
        {
            return new TaskListMutationResult(false, $"No editable user task item was found at index {request.Index}.");
        }

        item.Checked = request.Checked;
        return new TaskListMutationResult(true, "Task item updated.");
    }

    private async Task<TaskItem?> GetUserTaskItemAsync(int index, string action, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var taskItems = dte.ToolWindows?.TaskList?.TaskItems
            ?? throw new InvalidOperationException("Visual Studio Task List service is unavailable.");

        if (index < 1 || index > taskItems.Count)
        {
            return null;
        }

        var item = taskItems.Item(index);
        if (item is null || !string.Equals(item.Category, TaskListCategories.User, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Task item at index {index} is not a user task and cannot be {action}. Only tasks added via task_list_add can be modified.");
        }

        return item;
    }

    public async Task<OutputReadResult> ReadOutputAsync(OutputReadRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var panes = dte.ToolWindows?.OutputWindow?.OutputWindowPanes;
        if (panes is null)
        {
            return new OutputReadResult(null, string.Empty, true);
        }

        var pane = FindOutputPane(panes, request.PaneName);
        if (pane is null)
        {
            return new OutputReadResult(request.PaneName, string.Empty, true);
        }

        var text = ReadOutputPaneText(pane);
        var maxChars = request.MaxChars <= 0 ? text.Length : request.MaxChars;
        var truncated = text.Length > maxChars;
        if (truncated)
        {
            text = text.Substring(text.Length - maxChars, maxChars);
        }

        return new OutputReadResult(pane.Name, text, truncated);
    }

    public async Task<OutputPaneListResult> ListOutputPanesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var panes = dte.ToolWindows?.OutputWindow?.OutputWindowPanes;
        if (panes is null)
        {
            return new OutputPaneListResult(Array.Empty<OutputPaneInfo>());
        }

        var result = new List<OutputPaneInfo>();
        for (var index = 1; index <= panes.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(new OutputPaneInfo(panes.Item(index).Name));
        }

        return new OutputPaneListResult(result);
    }

    public async Task<OutputReadResult> ClearOutputAsync(OutputPaneRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var panes = dte.ToolWindows?.OutputWindow?.OutputWindowPanes;
        if (panes is null)
        {
            return new OutputReadResult(request.PaneName, string.Empty, true);
        }

        var pane = FindOutputPane(panes, request.PaneName);
        if (pane is null)
        {
            return new OutputReadResult(request.PaneName, string.Empty, true);
        }

        pane.Clear();
        return new OutputReadResult(pane.Name, string.Empty, false);
    }

    public async Task<OutputReadResult> WriteOutputAsync(OutputWriteRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var panes = dte.ToolWindows?.OutputWindow?.OutputWindowPanes
            ?? throw new InvalidOperationException("Visual Studio output window is unavailable.");
        var pane = FindOrCreateOutputPane(panes, request.PaneName);
        if (request.Activate)
        {
            pane.Activate();
        }

        pane.OutputString(request.Text ?? string.Empty);
        return new OutputReadResult(pane.Name, ReadOutputPaneText(pane), false);
    }

    private async Task<DTE?> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE;
    }

    private async Task<DTE2?> GetDte2Async()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE2;
    }

    private static BuildStatusInfo GetBuildStatus(SolutionBuild solutionBuild)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return new BuildStatusInfo(
            solutionBuild.BuildState.ToString(),
            solutionBuild.LastBuildInfo);
    }

    private static bool ShouldInclude(ErrorItem item, ErrorListRequest request)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (request.IncludeWarnings || item.ErrorLevel != vsBuildErrorLevel.vsBuildErrorLevelMedium)
        {
            return true;
        }

        return false;
    }

    private static OutputWindowPane? FindOutputPane(OutputWindowPanes panes, string? paneName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!string.IsNullOrWhiteSpace(paneName))
        {
            for (var index = 1; index <= panes.Count; index++)
            {
                var pane = panes.Item(index);
                if (string.Equals(pane.Name, paneName, StringComparison.OrdinalIgnoreCase))
                {
                    return pane;
                }
            }

            return null;
        }

        OutputWindowPane? firstPane = null;
        for (var index = 1; index <= panes.Count; index++)
        {
            var pane = panes.Item(index);
            firstPane ??= pane;

            if (string.Equals(pane.Name, "Build", StringComparison.OrdinalIgnoreCase))
            {
                return pane;
            }
        }

        return firstPane;
    }

    private static OutputWindowPane FindOrCreateOutputPane(OutputWindowPanes panes, string? paneName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var pane = FindOutputPane(panes, paneName);
        if (pane is not null)
        {
            return pane;
        }

        return panes.Add(string.IsNullOrWhiteSpace(paneName) ? "NetVsMcp" : paneName);
    }

    private static string ReadOutputPaneText(OutputWindowPane pane)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (pane.TextDocument is not TextDocument textDocument)
        {
            return string.Empty;
        }

        var editPoint = textDocument.StartPoint.CreateEditPoint();
        return editPoint.GetText(textDocument.EndPoint);
    }

    private static string? FindProjectUniqueName(Solution? solution, string projectName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (solution?.Projects is null)
        {
            return null;
        }

        foreach (Project project in solution.Projects)
        {
            var uniqueName = FindProjectUniqueName(project, projectName);
            if (uniqueName is not null)
            {
                return uniqueName;
            }
        }

        return null;
    }

    private static string? FindProjectUniqueName(Project project, string projectName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
        {
            foreach (ProjectItem item in project.ProjectItems)
            {
                if (item.SubProject is not null)
                {
                    var child = FindProjectUniqueName(item.SubProject, projectName);
                    if (child is not null)
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        if (string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.UniqueName, projectName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.FullName, projectName, StringComparison.OrdinalIgnoreCase))
        {
            return project.UniqueName;
        }

        return null;
    }
}
