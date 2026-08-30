using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    [McpServerTool(Name = "build_solution")]
    [Description("Starts a solution build in a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildSolutionResult>> BuildSolution(
        bool waitForBuildToFinish = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var request = new BuildSolutionRequest
        {
            WaitForBuildToFinish = waitForBuildToFinish
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            (connection, ct) => connection.BuildSolutionAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(BuildSolution), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "build_status")]
    [Description("Returns build status from a routed Visual Studio session.")]
    public async Task<ToolResponse<BuildStatusInfo>> BuildStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.BuildStatusAsync(ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(BuildStatus), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "build_and_get_errors")]
    [Description("Builds the routed solution and returns errors/warnings.")]
    public Task<ToolResponse<BuildAndGetErrorsResult>> BuildAndGetErrors(
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<BuildAndGetErrorsResult>.Fail("Max items must be greater than zero."));
        }

        var errorsRequest = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var build = await connection.BuildSolutionAsync(new BuildSolutionRequest { WaitForBuildToFinish = true }, ct);
                var errors = await connection.ErrorsListAsync(errorsRequest, ct);
                return new BuildAndGetErrorsResult(build, errors);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "output_read")]
    [Description("Reads an output pane from a routed Visual Studio session.")]
    public async Task<ToolResponse<OutputReadResult>> OutputRead(
        string? paneName = null,
        int maxChars = 20000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxChars < 1)
        {
            return ToolResponse<OutputReadResult>.Fail("Max chars must be greater than zero.");
        }

        var request = new OutputReadRequest
        {
            PaneName = NormalizeOptional(paneName),
            MaxChars = maxChars
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.OutputReadAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(OutputRead), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
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
    [Description("Writes text to a Visual Studio output pane.")]
    public Task<ToolResponse<OutputReadResult>> OutputWrite(string text, string? paneName = null, bool activate = false, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (text is null)
        {
            return Task.FromResult(FailWithCode<OutputReadResult>("Text is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new OutputWriteRequest
        {
            PaneName = paneName,
            Text = text,
            Activate = activate
        };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.OutputWriteAsync(request, ct),
            cancellationToken);
    }
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
}
