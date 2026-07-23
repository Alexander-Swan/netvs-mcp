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
    [Description("Adds an assembly or project reference to a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<ProjectReferenceResult>> ProjectAddReference(
        string projectName,
        string reference,
        string referenceType = "assembly",
        string? hintPath = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProjectReference(projectName, reference);
        if (validation is not null)
        {
            return Task.FromResult(FailWithCode<ProjectReferenceResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        var request = new ProjectReferenceRequest
        {
            ProjectName = projectName,
            Reference = reference,
            ReferenceType = referenceType,
            HintPath = hintPath
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ProjectAddReferenceAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "project_remove_reference")]
    [Description("Removes an assembly or project reference from a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<ProjectReferenceResult>> ProjectRemoveReference(
        string projectName,
        string reference,
        string referenceType = "assembly",
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateProjectReference(projectName, reference);
        if (validation is not null)
        {
            return Task.FromResult(FailWithCode<ProjectReferenceResult>(validation, ToolErrorCodes.InvalidRequest));
        }

        var request = new ProjectReferenceRequest
        {
            ProjectName = projectName,
            Reference = reference,
            ReferenceType = referenceType
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ProjectRemoveReferenceAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "nuget_list")]
    [Description("Lists PackageReference NuGet packages from project files in the routed Visual Studio solution.")]
    public Task<ToolResponse<NugetListResult>> NugetList(string? projectName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new NugetListRequest { ProjectName = projectName };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.NugetListAsync(request, ct),
            cancellationToken);
    }

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

    private static string? ValidateProjectReference(string? projectName, string? reference)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return "Project name is required.";
        }

        return string.IsNullOrWhiteSpace(reference)
            ? "Reference is required."
            : null;
    }
}
