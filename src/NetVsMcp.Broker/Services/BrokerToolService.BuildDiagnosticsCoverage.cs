using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "build_project")]
    [Description("Builds one project in the routed Visual Studio session.")]
    public Task<ToolResponse<BuildSolutionResult>> BuildProject(string projectName, bool waitForBuildToFinish = true, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return Task.FromResult(FailWithCode<BuildSolutionResult>("Project name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new BuildProjectRequest { ProjectName = projectName, WaitForBuildToFinish = waitForBuildToFinish };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.BuildProjectAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "build_cancel")]
    [Description("Cancels an active Visual Studio build.")]
    public Task<ToolResponse<BuildStatusInfo>> BuildCancel(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.BuildCancelAsync(ct), cancellationToken);

    [McpServerTool(Name = "clean_solution")]
    [Description("Cleans the routed Visual Studio solution.")]
    public Task<ToolResponse<BuildSolutionResult>> CleanSolution(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.CleanSolutionAsync(ct), cancellationToken);

    [McpServerTool(Name = "rebuild_solution")]
    [Description("Rebuilds the routed Visual Studio solution.")]
    public Task<ToolResponse<BuildSolutionResult>> RebuildSolution(bool waitForBuildToFinish = true, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new BuildSolutionRequest { WaitForBuildToFinish = waitForBuildToFinish };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.RebuildSolutionAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "build_configuration_get")]
    [Description("Returns the active solution build configuration and platform.")]
    public Task<ToolResponse<BuildConfigurationInfo>> BuildConfigurationGet(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.BuildConfigurationGetAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "build_configuration_set")]
    [Description("Sets the active solution build configuration and optional platform.")]
    public Task<ToolResponse<BuildConfigurationInfo>> BuildConfigurationSet(string configuration, string? platform = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return Task.FromResult(FailWithCode<BuildConfigurationInfo>("Configuration is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new BuildConfigurationSetRequest { Configuration = configuration, Platform = platform };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.BuildConfigurationSetAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "output_list_panes")]
    [Description("Lists Visual Studio output panes.")]
    public Task<ToolResponse<OutputPaneListResult>> OutputListPanes(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.OutputListPanesAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "output_write")]
    [Description("Planned: writes to a Visual Studio output pane.")]
    public Task<ToolResponse<UnsupportedToolResult>> OutputWrite(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Output", "Implement profile-gated output pane writes.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "output_clear")]
    [Description("Clears a Visual Studio output pane.")]
    public Task<ToolResponse<OutputReadResult>> OutputClear(string? paneName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new OutputPaneRequest { PaneName = paneName };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.OutputClearAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "diagnostics_binding_errors")]
    [Description("Planned: returns binding diagnostics.")]
    public Task<ToolResponse<UnsupportedToolResult>> DiagnosticsBindingErrors(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Diagnostics", "Implement WPF/XAML binding diagnostic collection.", sessionId, solutionName, solutionPath, cancellationToken);
}
