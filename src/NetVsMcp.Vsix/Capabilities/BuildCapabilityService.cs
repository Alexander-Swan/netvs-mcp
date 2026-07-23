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
    Task<BuildStatusInfo> GetBuildStatusAsync(CancellationToken cancellationToken);
    Task<BuildConfigurationInfo> GetBuildConfigurationAsync(CancellationToken cancellationToken);
    Task<ErrorListResult> ListErrorsAsync(ErrorListRequest request, CancellationToken cancellationToken);
    Task<OutputReadResult> ReadOutputAsync(OutputReadRequest request, CancellationToken cancellationToken);
    Task<OutputPaneListResult> ListOutputPanesAsync(CancellationToken cancellationToken);
    Task<OutputReadResult> ClearOutputAsync(OutputPaneRequest request, CancellationToken cancellationToken);
    Task BuildProjectAsync(string projectName, CancellationToken cancellationToken);
    Task CancelBuildAsync(CancellationToken cancellationToken);
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

    public Task BuildProjectAsync(string projectName, CancellationToken cancellationToken)
    {
        _ = projectName;
        _ = cancellationToken;
        throw new System.NotImplementedException("Resolve the project in the active solution before invoking VS build services.");
    }

    public Task CancelBuildAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Cancel the active Visual Studio build operation.");
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
}
