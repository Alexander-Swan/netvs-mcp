using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "build_project")]
    [Description("Planned: builds a project in the routed Visual Studio session.")]
    public Task<ToolResponse<UnsupportedToolResult>> BuildProject(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Build", "Implement VSIX project build through SolutionBuild.BuildProject.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "build_cancel")]
    [Description("Planned: cancels an active build.")]
    public Task<ToolResponse<UnsupportedToolResult>> BuildCancel(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Build", "Implement VSIX build cancellation through SolutionBuild.Cancel.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "clean_solution")]
    [Description("Planned: cleans the routed Visual Studio solution.")]
    public Task<ToolResponse<UnsupportedToolResult>> CleanSolution(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Build", "Implement VSIX solution clean operation.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "rebuild_solution")]
    [Description("Planned: rebuilds the routed Visual Studio solution.")]
    public Task<ToolResponse<UnsupportedToolResult>> RebuildSolution(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Build", "Implement VSIX solution rebuild operation.", sessionId, solutionName, solutionPath, cancellationToken);

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
    [Description("Planned: sets solution build configuration.")]
    public Task<ToolResponse<UnsupportedToolResult>> BuildConfigurationSet(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Build", "Implement solution configuration/platform mutation with profile checks.", sessionId, solutionName, solutionPath, cancellationToken);

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
