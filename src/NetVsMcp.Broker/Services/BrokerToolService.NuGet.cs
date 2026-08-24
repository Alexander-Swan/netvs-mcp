using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "package_restore")]
    [Description("Returns package restore support status for a routed project.")]
    public Task<ToolResponse<PackageRestoreResult>> PackageRestore(
        string? projectName = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new PackageRestoreRequest { ProjectName = NormalizeOptional(projectName) };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.PackageRestoreAsync(request, ct),
            cancellationToken);
    }
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
    [Description("Searches NuGet packages from nuget.org.")]
    public Task<ToolResponse<NugetSearchResult>> NugetSearch(string query, int maxResults = 20, bool includePrerelease = false, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(FailWithCode<NugetSearchResult>("Query is required.", ToolErrorCodes.InvalidRequest));
        }

        if (maxResults <= 0)
        {
            return Task.FromResult(FailWithCode<NugetSearchResult>("Max results must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new NugetSearchRequest { Query = query, MaxResults = maxResults, IncludePrerelease = includePrerelease };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.NugetSearchAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "nuget_install")]
    [Description("Installs a NuGet package into a project.")]
    public Task<ToolResponse<NugetMutationResult>> NugetInstall(string projectName, string packageId, string? version = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchNugetMutation(projectName, packageId, version, sessionId, solutionName, solutionPath, (connection, request, ct) => connection.NugetInstallAsync(request, ct), cancellationToken);
    [McpServerTool(Name = "nuget_update")]
    [Description("Updates a NuGet package in a project; pass version to pin a specific version.")]
    public Task<ToolResponse<NugetMutationResult>> NugetUpdate(string projectName, string packageId, string? version = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchNugetMutation(projectName, packageId, version, sessionId, solutionName, solutionPath, (connection, request, ct) => connection.NugetUpdateAsync(request, ct), cancellationToken);
    [McpServerTool(Name = "nuget_uninstall")]
    [Description("Uninstalls a NuGet package from a project.")]
    public Task<ToolResponse<NugetMutationResult>> NugetUninstall(string projectName, string packageId, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchNugetMutation(projectName, packageId, null, sessionId, solutionName, solutionPath, (connection, request, ct) => connection.NugetUninstallAsync(request, ct), cancellationToken);
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
    private Task<ToolResponse<NugetMutationResult>> DispatchNugetMutation(
        string? projectName,
        string? packageId,
        string? version,
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        Func<IVisualStudioSessionRpc, NugetPackageMutationRequest, CancellationToken, Task<NugetMutationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return Task.FromResult(FailWithCode<NugetMutationResult>("Project name is required.", ToolErrorCodes.InvalidRequest));
        }

        if (string.IsNullOrWhiteSpace(packageId))
        {
            return Task.FromResult(FailWithCode<NugetMutationResult>("Package id is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new NugetPackageMutationRequest
        {
            ProjectName = projectName,
            PackageId = packageId,
            Version = version
        };

        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => operation(connection, request, ct), cancellationToken);
    }
}
