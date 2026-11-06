using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed class BuildCapabilityService : IBuildCapabilityService, IDisposable
{
    private static readonly Guid TaskProviderGuid = new("e22e7a99-2a83-4f12-ae69-1bc4504a7ee7");

    private readonly AsyncPackage package;
    private TaskProvider? taskProvider;
    private readonly BuildExecutionTracker tracker = new();
    private readonly NetVsMcp.Contracts.SessionEventBuffer? events;
    private BuildEvents? buildEvents;
    private SolutionBuild? observedBuild;
    private BuildOutputCapture? buildOutputCapture;

    public BuildCapabilityService(AsyncPackage package, NetVsMcp.Contracts.SessionEventBuffer? events = null)
    {
        this.package = package;
        this.events = events;
    }

    public async Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");

        var outputWindow = request.CaptureDiagnostics is null ? null :
            await package.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        return await RunTrackedBuildAsync(dte, solutionBuild, null, vsBuildScope.vsBuildScopeSolution,
            vsBuildAction.vsBuildActionBuild, () => { ThreadHelper.ThrowIfNotOnUIThread(); solutionBuild.Build(false); },
            request.WaitForBuildToFinish, cancellationToken, request.CaptureDiagnostics, outputWindow);
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

        return await RunTrackedBuildAsync(dte, solutionBuild, projectUniqueName, vsBuildScope.vsBuildScopeProject,
            vsBuildAction.vsBuildActionBuild, () => { ThreadHelper.ThrowIfNotOnUIThread(); solutionBuild.BuildProject(solutionBuild.ActiveConfiguration.Name, projectUniqueName, false); },
            request.WaitForBuildToFinish, cancellationToken);
    }

    public async Task<BuildStatusInfo> CancelBuildAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");

        // Build.Cancel reports E_FAIL when Visual Studio is idle. This tool is intentionally
        // idempotent, so returning the current status is a safer and more useful result than
        // forwarding that command failure to the caller.
        if (solutionBuild.BuildState != vsBuildState.vsBuildStateInProgress)
        {
            return GetBuildStatus(solutionBuild);
        }

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
        return await RunTrackedBuildAsync(dte, solutionBuild, null, vsBuildScope.vsBuildScopeSolution,
            vsBuildAction.vsBuildActionClean, () => { ThreadHelper.ThrowIfNotOnUIThread(); solutionBuild.Clean(false); }, true, cancellationToken);
    }

    public async Task<BuildSolutionResult> RebuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDte2Async()
            ?? throw new InvalidOperationException("Visual Studio DTE2 service is unavailable.");
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        await RunTrackedBuildAsync(dte, solutionBuild, null, vsBuildScope.vsBuildScopeSolution,
            vsBuildAction.vsBuildActionClean, () => { ThreadHelper.ThrowIfNotOnUIThread(); solutionBuild.Clean(false); }, true, cancellationToken);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        return await RunTrackedBuildAsync(dte, solutionBuild, null, vsBuildScope.vsBuildScopeSolution,
            vsBuildAction.vsBuildActionBuild, () => { ThreadHelper.ThrowIfNotOnUIThread(); solutionBuild.Build(false); }, request.WaitForBuildToFinish, cancellationToken);
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
            var providerItem = FindProviderTaskItem(item);
            if (providerItem is not null)
            {
                info = new TaskListItemInfo(
                    info.Index,
                    info.Description,
                    info.File,
                    info.Line,
                    providerItem.Priority.ToString(),
                    info.Category,
                    info.IsUserTask,
                    providerItem.Checked);
            }

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

        if (!TryParseTaskPriority(request.Priority, out var priority))
        {
            return new TaskListMutationResult(false, $"Unrecognized priority '{request.Priority}'. Use High, Medium, or Low.");
        }

        var provider = GetTaskProvider();
        provider.Tasks.Add(new TaskListItem
        {
            Category = TaskCategory.User,
            Text = request.Description.Trim(),
            Priority = priority,
            Checked = false,
            IsCheckedEditable = true,
            IsPriorityEditable = true,
            IsTextEditable = true,
            CanDelete = true,
        });
        provider.Refresh();

        return new TaskListMutationResult(true, "Task item added.");
    }

    public async Task<TaskListMutationResult> RemoveTaskItemAsync(TaskListMutationRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var item = await GetEditableTaskItemAsync(request.Index, "removed", cancellationToken);
        if (item is null)
        {
            return new TaskListMutationResult(false, $"No editable user task item was found at index {request.Index}.");
        }

        if (item.ProviderItem is not null)
        {
            GetTaskProvider().Tasks.Remove(item.ProviderItem);
            GetTaskProvider().Refresh();
            return new TaskListMutationResult(true, "Task item removed.");
        }

        try
        {
            item.DteItem.Delete();
            return new TaskListMutationResult(true, "Task item removed.");
        }
        catch (NotImplementedException)
        {
            return new TaskListMutationResult(
                false,
                "Visual Studio does not support removing this task item through DTE. Only tasks added by this running NetVsMcp VSIX session can be removed.");
        }
    }

    public async Task<TaskListMutationResult> SetTaskItemCheckedAsync(TaskListSetCheckedRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var item = await GetEditableTaskItemAsync(request.Index, "checked", cancellationToken);
        if (item is null)
        {
            return new TaskListMutationResult(false, $"No editable user task item was found at index {request.Index}.");
        }

        if (item.ProviderItem is not null)
        {
            item.ProviderItem.Checked = request.Checked;
            GetTaskProvider().Refresh();
            return new TaskListMutationResult(true, "Task item updated.");
        }

        try
        {
            item.DteItem.Checked = request.Checked;
            return new TaskListMutationResult(true, "Task item updated.");
        }
        catch (NotImplementedException)
        {
            return new TaskListMutationResult(
                false,
                "Visual Studio does not support updating this task item through DTE. Only tasks added by this running NetVsMcp VSIX session can be checked.");
        }
    }

    private async Task<EditableTaskItem?> GetEditableTaskItemAsync(int index, string action, CancellationToken cancellationToken)
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
        if (item is null || !TaskListCategories.IsEditableUserTask(item.Category))
        {
            throw new InvalidOperationException(
                $"Task item at index {index} is not a user task and cannot be {action}. Only tasks added via task_list_add can be modified.");
        }

        return new EditableTaskItem(item, FindProviderTaskItem(item));
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

        var text = await OutputPaneTextReader.ReadAsync(package, pane, cancellationToken);
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
        return new OutputReadResult(pane.Name, await OutputPaneTextReader.ReadAsync(package, pane, cancellationToken), false);
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

    private TaskProvider GetTaskProvider()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (taskProvider is null)
        {
            taskProvider = new TaskProvider(package)
            {
                ProviderGuid = TaskProviderGuid,
                ProviderName = "NetVsMcp",
            };
        }

        return taskProvider;
    }

    private TaskListItem? FindProviderTaskItem(TaskItem item)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (taskProvider is null)
        {
            return null;
        }

        foreach (TaskListItem candidate in taskProvider.Tasks)
        {
            if (string.Equals(candidate.Text, item.Description, StringComparison.Ordinal) &&
                candidate.Category == TaskCategory.User)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryParseTaskPriority(string? priorityText, out TaskPriority priority)
    {
        if (string.Equals(priorityText, "High", StringComparison.OrdinalIgnoreCase))
        {
            priority = TaskPriority.High;
            return true;
        }

        if (string.Equals(priorityText, "Medium", StringComparison.OrdinalIgnoreCase))
        {
            priority = TaskPriority.Normal;
            return true;
        }

        if (string.Equals(priorityText, "Low", StringComparison.OrdinalIgnoreCase))
        {
            priority = TaskPriority.Low;
            return true;
        }

        priority = TaskPriority.Normal;
        return false;
    }

    private async Task<BuildSolutionResult> RunTrackedBuildAsync(DTE2 dte, SolutionBuild solutionBuild,
        string? project, vsBuildScope scope, vsBuildAction action, Action issue, bool wait, CancellationToken cancellationToken,
        ErrorListRequest? captureDiagnostics = null, IVsOutputWindow? outputWindow = null)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (solutionBuild.BuildState == vsBuildState.vsBuildStateInProgress)
            throw new InvalidOperationException("Visual Studio is already building. Reconcile the existing build instead of submitting another.");
        if (buildEvents is null)
        {
            buildEvents = dte.Events.BuildEvents;
            buildEvents.OnBuildBegin += OnTrackedBuildBegin;
            buildEvents.OnBuildDone += OnTrackedBuildDone;
        }
        observedBuild = solutionBuild;
        var configuration = solutionBuild.ActiveConfiguration;
        var platform = (configuration as SolutionConfiguration2)?.PlatformName ?? string.Empty;
        var ticket = tracker.Begin(configuration.Name, platform, project, scope.ToString(), action.ToString());
        _ = ticket.Completion.Task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        BuildOutputCapture? capture = null;
        try
        {
            try
            {
                if (captureDiagnostics is not null)
                    buildOutputCapture = capture = new BuildOutputCapture(ticket, dte, outputWindow);
                issue();
            }
            catch { tracker.Abort(ticket); throw; }
            // After acceptance, cancellation only ends the broker/client wait. Continue observing
            // this actual build until its completion event; build_cancel is the explicit control.
            // This source is completed by VS build events, not another joinable operation.
#pragma warning disable VSTHRD003
            if (wait) await ticket.Completion.Task;
#pragma warning restore VSTHRD003
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var errors = ticket.Completed.HasValue ? ticket.Errors ?? -1 : solutionBuild.LastBuildInfo;
            var outcome = ticket.Completed.HasValue ? errors == 0 ? "completed" : "failed" : ticket.Begun ? "running" : "accepted";
            var status = new BuildStatusInfo(ticket.Completed.HasValue ? vsBuildState.vsBuildStateDone.ToString() : solutionBuild.BuildState.ToString(),
                errors, ticket.Id, outcome, ticket.Configuration, ticket.Platform, ticket.Project);
            var diagnostics = capture is null || captureDiagnostics is null ? null :
                BuildOutputDiagnostics.Read(capture.Before, capture.After, capture.Clears, errors, captureDiagnostics);
            return new(status, errors, ticket.Id, outcome, ticket.Configuration, ticket.Platform, ticket.Project, diagnostics);
        }
        finally
        {
            if (capture is not null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (ReferenceEquals(buildOutputCapture, capture)) buildOutputCapture = null;
                capture.Dispose();
            }
        }
    }

    private void OnTrackedBuildBegin(vsBuildScope scope, vsBuildAction action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        tracker.ObserveBegin(scope.ToString(), action.ToString());
        events?.Publish("build.started", operationId: tracker.Current?.Id);
    }

    private void OnTrackedBuildDone(vsBuildScope scope, vsBuildAction action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var ticket = tracker.Current;
        if (ticket is { Completed: null })
        {
            try
            {
                if (buildOutputCapture is { } capture && ReferenceEquals(capture.Ticket, ticket) &&
                    ticket.Begun && ticket.Scope == scope.ToString() && ticket.Action == action.ToString())
                    capture.Complete();
                tracker.ObserveDone(scope.ToString(), action.ToString(), observedBuild?.LastBuildInfo ?? -1);
            }
            catch (COMException ex)
            {
                System.Diagnostics.Trace.TraceWarning("NetVsMcp: build completion state unavailable: {0}", ex.Message);
                tracker.Abort(ticket);
            }
        }
        events?.Publish("build.completed", operationId: ticket?.Id);
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (buildEvents is not null)
        {
            buildEvents.OnBuildBegin -= OnTrackedBuildBegin;
            buildEvents.OnBuildDone -= OnTrackedBuildDone;
            buildEvents = null;
        }
        if (tracker.Current is { Completed: null } ticket) tracker.Abort(ticket);
        taskProvider?.Dispose();
    }

    private sealed class BuildOutputCapture : IDisposable
    {
        private readonly DTE2 dte;
        private readonly IVsOutputWindow? outputWindow;
        private readonly OutputWindowEvents? outputEvents;
        private int observedClears;
        public BuildExecutionTracker.Ticket Ticket { get; }
        public string? Before { get; }
        public string? After { get; private set; }
        public int Clears { get; private set; }

        public BuildOutputCapture(BuildExecutionTracker.Ticket ticket, DTE2 dte, IVsOutputWindow? outputWindow)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Ticket = ticket;
            this.dte = dte;
            this.outputWindow = outputWindow;
            Before = ReadText();
            try
            {
                outputEvents = dte.Events.OutputWindowEvents[null];
                if (outputEvents is not null) outputEvents.PaneClearing += OnPaneClearing;
            }
            catch (COMException) { outputEvents = null; }
        }

        public void Complete()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            After = ReadText();
            Clears = observedClears;
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try { if (outputEvents is not null) outputEvents.PaneClearing -= OnPaneClearing; }
            catch (COMException) { }
        }

        private void OnPaneClearing(OutputWindowPane pane)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (Guid.TryParse(pane.Guid, out var id) && id == VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid)
                    observedClears++;
            }
            catch (COMException) { }
        }

        private string? ReadText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var id = VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid;
            var paneExists = false;
            try
            {
                if (outputWindow is not null && outputWindow.GetPane(ref id, out var pane) >= 0 && pane is not null)
                {
                    paneExists = true;
                    IVsTextLines? buffer = null;
                    if (pane is IVsTextBufferProvider provider) provider.GetTextBuffer(out buffer);
                    if (buffer is null && pane is IVsTextView view) view.GetBuffer(out buffer);
                    if (buffer is not null && buffer.GetLastLineIndex(out var line, out var column) >= 0 &&
                        buffer.GetLineText(0, 0, line, column, out var text) >= 0)
                        return text ?? string.Empty;
                }
            }
            catch (COMException) { return null; }

            try
            {
                var panes = dte.ToolWindows?.OutputWindow?.OutputWindowPanes;
                if (panes is null) return null;
                for (var index = 1; index <= panes.Count; index++)
                {
                    var candidate = panes.Item(index);
                    if (Guid.TryParse(candidate.Guid, out var candidateId) && candidateId == id)
                        return candidate.TextDocument is TextDocument document
                            ? document.StartPoint.CreateEditPoint().GetText(document.EndPoint)
                            : null;
                }
                return paneExists ? null : string.Empty;
            }
            catch (COMException) { return null; }
        }
    }

    private BuildStatusInfo GetBuildStatus(SolutionBuild solutionBuild)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var ticket = tracker.Current;
        return new BuildStatusInfo(
            solutionBuild.BuildState.ToString(),
            solutionBuild.LastBuildInfo, ticket?.Id,
            ticket is null ? null : ticket.Completed.HasValue ? ticket.Errors == 0 ? "completed" : "failed" : ticket.Begun ? "running" : "accepted",
            ticket?.Configuration, ticket?.Platform, ticket?.Project);
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

    private sealed class EditableTaskItem
    {
        public EditableTaskItem(TaskItem dteItem, TaskListItem? providerItem)
        {
            DteItem = dteItem;
            ProviderItem = providerItem;
        }

        public TaskItem DteItem { get; }
        public TaskListItem? ProviderItem { get; }
    }
}
