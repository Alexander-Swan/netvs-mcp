using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "project_remove_file")]
    [Description("Planned: removes a file from a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProjectRemoveFile(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Project System", "Implement project item lookup and removal with profile checks.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "project_add_reference")]
    [Description("Planned: adds a reference to a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProjectAddReference(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Project System", "Implement assembly/project reference addition.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "project_remove_reference")]
    [Description("Planned: removes a reference from a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProjectRemoveReference(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Project System", "Implement reference lookup and removal.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "nuget_list")]
    [Description("Planned: lists NuGet packages.")]
    public Task<ToolResponse<UnsupportedToolResult>> NugetList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("NuGet", "Implement package listing from project assets or NuGet APIs.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "nuget_search")]
    [Description("Planned: searches NuGet packages.")]
    public Task<ToolResponse<UnsupportedToolResult>> NugetSearch(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("NuGet", "Implement NuGet API search with result limits and version metadata.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "nuget_install")]
    [Description("Planned: installs a NuGet package.")]
    public Task<ToolResponse<UnsupportedToolResult>> NugetInstall(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("NuGet", "Implement profile-gated package install and restore reporting.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "nuget_update")]
    [Description("Planned: updates a NuGet package.")]
    public Task<ToolResponse<UnsupportedToolResult>> NugetUpdate(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("NuGet", "Implement profile-gated package update and restore reporting.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "nuget_uninstall")]
    [Description("Planned: uninstalls a NuGet package.")]
    public Task<ToolResponse<UnsupportedToolResult>> NugetUninstall(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("NuGet", "Implement profile-gated package uninstall and restore reporting.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "vs_get_logs")]
    [Description("Planned: returns broker log information.")]
    public Task<ToolResponse<UnsupportedToolResult>> VsGetLogs(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        PlannedTool("Broker", "Implement bounded broker log retrieval.", sessionId, solutionName, solutionPath, cancellationToken);
}
