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
    [McpServerTool(Name = "vs_list_sessions")]
    [Description("Lists Visual Studio instances registered with the local NetVsMcp broker.")]
    public ToolResponse<IReadOnlyCollection<VsSessionInfo>> VsListSessions()
    {
        var response = ToolResponse<IReadOnlyCollection<VsSessionInfo>>.Ok(_runtime.Sessions.ListSessions());
        AuditToolResult(nameof(VsListSessions), null, response.Success, null, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_get_status")]
    [Description("Returns local broker endpoint, uptime, registration pipe, and registered Visual Studio session status.")]
    public ToolResponse<BrokerStatus> VsGetStatus()
    {
        var response = ToolResponse<BrokerStatus>.Ok(_runtime.GetStatus());
        AuditToolResult(nameof(VsGetStatus), null, response.Success, null, response.Message);
        return response;
    }
    [McpServerTool(Name = "netvs_doctor")]
    [Description("Diagnoses local broker endpoint, registration pipe, registered sessions, and tool endpoint health.")]
    public ToolResponse<BrokerDoctorResult> NetVsDoctor()
    {
        var status = _runtime.GetStatus();
        var capabilities = CreateCapabilities();
        var checks = CreateDoctorChecks(status, capabilities).ToArray();
        var errorCount = checks.Count(check => check.Severity == BrokerDoctorSeverity.Error && !check.Passed);
        var warningCount = checks.Count(check => check.Severity == BrokerDoctorSeverity.Warning && !check.Passed);
        var healthy = errorCount == 0;
        var summary = healthy
            ? warningCount == 0
                ? "NetVsMcp broker and Visual Studio session health look good."
                : $"NetVsMcp doctor found {warningCount} warning(s)."
            : $"NetVsMcp doctor found {errorCount} error(s) and {warningCount} warning(s).";

        var response = ToolResponse<BrokerDoctorResult>.Ok(new BrokerDoctorResult(
            healthy,
            summary,
            status,
            checks));
        AuditToolResult(nameof(NetVsDoctor), null, response.Success, null, response.Message);
        return response;
    }

    private BrokerCapabilities CreateCapabilities()
    {
        var tools = ToolDescriptors.Select(WithCategoryMetadata).ToArray();
        return new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            tools,
            VisualStudioCapabilities);
    }

    private IEnumerable<BrokerDoctorCheck> CreateDoctorChecks(BrokerStatus status, BrokerCapabilities capabilities)
    {
        var sessions = status.Sessions.ToArray();
        var connectedCount = sessions.Count(session => session.Health == SessionHealth.Connected);
        var staleCount = sessions.Count(session => session.Health == SessionHealth.Stale);

        yield return new BrokerDoctorCheck(
            "broker_http_endpoint",
            BrokerDoctorSeverity.Error,
            _runtime.IsHttpEndpointRunning,
            _runtime.IsHttpEndpointRunning
                ? $"Broker HTTP endpoint is listening at '{status.McpEndpoint}'."
                : $"Broker HTTP endpoint is not listening at '{status.McpEndpoint}'.");

        yield return new BrokerDoctorCheck(
            "vsix_registration_pipe",
            BrokerDoctorSeverity.Error,
            _runtime.IsRegistrationPipeRunning,
            _runtime.IsRegistrationPipeRunning
                ? $"VSIX registration pipe is listening at '{status.PipeName}'."
                : $"VSIX registration pipe is not listening at '{status.PipeName}'.");

        yield return new BrokerDoctorCheck(
            "registered_sessions",
            BrokerDoctorSeverity.Warning,
            sessions.Length > 0,
            sessions.Length > 0
                ? $"{sessions.Length} Visual Studio session(s) are registered."
                : $"No Visual Studio sessions are registered. Install or enable the NetVsMcp Visual Studio extension, open a solution, and confirm the extension can reach the broker pipe. Setup: {ProductLinks.VisualStudioExtensionSetupUrl}");

        yield return new BrokerDoctorCheck(
            "connected_sessions",
            BrokerDoctorSeverity.Warning,
            connectedCount > 0,
            connectedCount > 0
                ? $"{connectedCount} Visual Studio session(s) are connected."
                : "No registered Visual Studio session has a fresh heartbeat.");

        yield return new BrokerDoctorCheck(
            "stale_sessions",
            BrokerDoctorSeverity.Warning,
            staleCount == 0,
            staleCount == 0
                ? "No stale Visual Studio sessions are registered."
                : $"{staleCount} stale Visual Studio session(s) are registered; restart Visual Studio or wait for cleanup if routing is confusing.");

        var hasPendingRestartSettings =
            _runtime.PendingPort is not null ||
            !string.IsNullOrWhiteSpace(_runtime.PendingLogsDirectory) ||
            !string.IsNullOrWhiteSpace(_runtime.PendingSessionsDirectory);
        yield return new BrokerDoctorCheck(
            "pending_restart_settings",
            BrokerDoctorSeverity.Warning,
            !hasPendingRestartSettings,
            hasPendingRestartSettings
                ? "One or more persisted broker settings will not take effect until the broker restarts."
                : "No pending broker settings require a restart.");

        yield return new BrokerDoctorCheck(
            "rpc_protocol",
            BrokerDoctorSeverity.Info,
            true,
            $"Broker expects VSIX RPC protocol major version {VsRpcProtocol.CurrentMajorVersion} ({VsRpcProtocol.CurrentVersion}); incompatible VSIX sessions are rejected during registration.");

        yield return new BrokerDoctorCheck(
            "mcp_client_config",
            BrokerDoctorSeverity.Info,
            true,
            $"Configure your MCP client with 'netvs' at '{_runtime.Options.McpEndpoint}'. Add optional 'netvs-web-automation' at '{_runtime.Options.McpWebAutomationEndpoint}' only when you need ui_* or web_* tools.");

        var splitEndpointToolCount = capabilities.Tools.Count(tool => tool.McpEndpointPath != McpEndpointRouting.DefaultEndpointPath);
        yield return new BrokerDoctorCheck(
            "tool_endpoints",
            BrokerDoctorSeverity.Info,
            true,
            splitEndpointToolCount == 0
                ? "All broker tools are served from the default MCP endpoint."
                : $"{splitEndpointToolCount} UI/browser automation tool(s) are served from '{McpEndpointRouting.WebAutomationEndpointPath}', not the default MCP endpoint.");
    }

    [McpServerTool(Name = "get_help")]
    [Description("Lists NetVsMcp broker tools, categories, and endpoint metadata.")]
    public ToolResponse<BrokerCapabilities> GetHelp(bool? requiresVisualStudioSession = null)
    {
        var tools = ToolDescriptors
            .Select(WithCategoryMetadata)
            .Where(tool => requiresVisualStudioSession is null || tool.RequiresVisualStudioSession == requiresVisualStudioSession.Value)
            .ToArray();
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            tools,
            VisualStudioCapabilities);

        // This catalog lists every tool the broker knows how to serve, not just the ones
        // reachable from whichever endpoint the caller is actually connected to — check each
        // tool's McpEndpointPath before assuming it is callable here. Tools whose
        // McpEndpointPath is "/mcp-wu" are only served from that separate opt-in endpoint and
        // will report ToolNotFound if called through the default "/mcp" connection; see the
        // automate-visual-studio best-practices guide and README for the second-server config
        // needed to reach them.
        var message = tools.Any(tool => tool.McpEndpointPath != McpEndpointRouting.DefaultEndpointPath)
            ? $"Some listed tools are only served from '{McpEndpointRouting.WebAutomationEndpointPath}', a separate opt-in endpoint, not this connection's endpoint. Check each tool's McpEndpointPath."
            : null;
        var response = ToolResponse<BrokerCapabilities>.Ok(capabilities, message);
        AuditToolResult(nameof(GetHelp), null, response.Success, null, response.Message);
        return response;
    }
    [McpServerTool(Name = "netvs_get_best_practices")]
    [Description("CALL THIS FIRST before using tools from a matching category. Lists bundled agent-neutral NetVsMcp best-practices guides, or reads one guide file. Call without arguments to list guides (each with its description and matching tool-name prefixes); pass guide and optional file to read one guide's content. Guides: manage-visual-studio (session/window/solution/project/test tools), navigate-visual-studio (code_*, symbol_*, diagnostics_*), edit-visual-studio (document_*, editor_*, selection_*, edit_*, safe-edit tools), build-visual-studio (build_*, output_*, nuget_*, package_*, project_add_reference), debug-visual-studio (debug_*, breakpoint_*, watch_*, thread_*, process_*, module_list, exception_settings_*, parallel_*, immediate_execute, test_debug), automate-visual-studio (console_*, ui_*, web_*).")]
    public ToolResponse<BestPracticeGuideToolResult> NetVsGetBestPractices(
        string? guide = null,
        string? file = null)
    {
        var response = _runtime.BestPracticeGuides.Read(guide, file);
        AuditToolResult(nameof(NetVsGetBestPractices), null, response.Success, null, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_get_session")]
    [Description("Resolves a Visual Studio session using sessionId, solutionName, or solutionPath and returns its current broker status.")]
    public ToolResponse<VsSessionStatus> VsGetSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<VsSessionStatus>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsGetSession), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var status = GetSessionStatus(route.Session);
        var response = status is null
            ? ToolResponse<VsSessionStatus>.Fail($"Visual Studio session '{route.Session.SessionId}' is no longer registered.")
            : ToolResponse<VsSessionStatus>.Ok(status);
        AuditToolResult(nameof(VsGetSession), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_select_session")]
    [Description("Resolves and returns a Visual Studio session using broker routing rules without storing global selection state.")]
    public ToolResponse<VsSessionInfo> VsSelectSession(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<VsSessionInfo>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsSelectSession), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var response = ToolResponse<VsSessionInfo>.Ok(route.Session);
        AuditToolResult(nameof(VsSelectSession), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_ping")]
    [Description("Returns lightweight broker health and optional routed Visual Studio session status.")]
    public ToolResponse<BrokerPing> VsPing(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        if (!HasRoutingFields(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath))
        {
            var brokerOnlyResponse = ToolResponse<BrokerPing>.Ok(CreatePing(null));
            AuditToolResult(nameof(VsPing), null, brokerOnlyResponse.Success, null, brokerOnlyResponse.Message);
            return brokerOnlyResponse;
        }

        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var route = _runtime.Sessions.Resolve(target);
        if (!route.Success || route.Session is null)
        {
            var failure = new ToolResponse<BrokerPing>(
                false,
                default,
                route.Message,
                CreateRouteFailureMetadata(route));
            AuditToolResult(nameof(VsPing), target, failure.Success, null, failure.Message, route.FailureReason.ToString());
            return failure;
        }

        var status = GetSessionStatus(route.Session);
        var response = ToolResponse<BrokerPing>.Ok(CreatePing(status));
        AuditToolResult(nameof(VsPing), target, response.Success, route.Session.SessionId, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_launch_instance")]
    [Description("Launches a new Visual Studio (devenv.exe) process, optionally opening a solution and/or running experimental (/rootsuffix Exp), and waits for it to register with the broker.")]
    public async Task<ToolResponse<VsLaunchInstanceResult>> VsLaunchInstance(
        string? solutionPath = null,
        bool experimental = false,
        string? edition = null,
        int timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        var result = await _runtime.Launcher.LaunchAsync(solutionPath, experimental, edition, timeoutSeconds, cancellationToken);
        var response = result.Success
            ? ToolResponse<VsLaunchInstanceResult>.Ok(result)
            : new ToolResponse<VsLaunchInstanceResult>(false, result, result.Message);
        AuditToolResult(nameof(VsLaunchInstance), null, response.Success, result.Session?.SessionId, response.Message);
        return response;
    }
    [McpServerTool(Name = "vs_context_snapshot")]
    [Description("Returns active session, solution, document, selection, debugger, build, errors, and pending edit context.")]
    public Task<ToolResponse<VsContextSnapshotResult>> VsContextSnapshot(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var status = await connection.GetStatusAsync(ct);
                var activeDocument = await connection.GetActiveDocumentAsync(ct);
                return new VsContextSnapshotResult(
                    status.Value,
                    await connection.SolutionInfoAsync(ct),
                    activeDocument.Value,
                    await connection.SelectionGetAsync(ct),
                    await connection.DebugStatusAsync(ct),
                    await TryGetSnapshotValueAsync(() => connection.BuildStatusAsync(ct)),
                    await connection.ErrorsListAsync(new ErrorListRequest { IncludeWarnings = true, MaxItems = 50 }, ct),
                    await connection.EditListPendingAsync(ct));
            },
            cancellationToken);
    }

    private static async Task<T?> TryGetSnapshotValueAsync<T>(Func<Task<T>> getValue)
        where T : class
    {
        try
        {
            return await getValue();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
    [McpServerTool(Name = "execute_command")]
    [Description("Executes a Visual Studio command in a routed session.")]
    public Task<ToolResponse<ExecuteCommandResult>> ExecuteCommand(
        string commandName,
        string? arguments = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return Task.FromResult(ToolResponse<ExecuteCommandResult>.Fail("Command name is required."));
        }

        var request = new ExecuteCommandRequest
        {
            CommandName = commandName.Trim(),
            Arguments = NormalizeOptional(arguments)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ExecuteCommandAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "get_status")]
    [Description("Returns Visual Studio session status through a routed session.")]
    public async Task<ToolResponse<VsSessionInfo>> GetStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            static (connection, ct) => connection.GetStatusAsync(ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(GetStatus), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "window_list")]
    [Description("Lists Visual Studio windows in a routed session.")]
    public Task<ToolResponse<WindowListResult>> WindowList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.WindowListAsync(ct),
            cancellationToken);
    }
    [McpServerTool(Name = "window_activate")]
    [Description("Activates a Visual Studio window in a routed session.")]
    public Task<ToolResponse<WindowActivateResult>> WindowActivate(
        string? caption = null,
        string? objectKind = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(caption) && string.IsNullOrWhiteSpace(objectKind))
        {
            return Task.FromResult(ToolResponse<WindowActivateResult>.Fail("Window caption or object kind is required."));
        }

        var request = new WindowActivateRequest
        {
            Caption = NormalizeOptional(caption),
            ObjectKind = NormalizeOptional(objectKind)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.WindowActivateAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "toolwindow_show")]
    [Description("Shows a Visual Studio tool window in a routed session.")]
    public Task<ToolResponse<ToolWindowResult>> ToolwindowShow(
        string? caption = null,
        string? objectKind = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (CreateToolWindowRequest(caption, objectKind) is not { } request)
        {
            return Task.FromResult(ToolResponse<ToolWindowResult>.Fail("Tool window caption or object kind is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ToolWindowShowAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "toolwindow_hide")]
    [Description("Hides a Visual Studio tool window in a routed session.")]
    public Task<ToolResponse<ToolWindowResult>> ToolwindowHide(
        string? caption = null,
        string? objectKind = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (CreateToolWindowRequest(caption, objectKind) is not { } request)
        {
            return Task.FromResult(ToolResponse<ToolWindowResult>.Fail("Tool window caption or object kind is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ToolWindowHideAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "solution_info")]
    [Description("Returns solution metadata from a routed Visual Studio session.")]
    public async Task<ToolResponse<SolutionInfoResult>> SolutionInfo(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null,
        CancellationToken cancellationToken = default)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath, processId, workspacePath, rootPath);
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            static (connection, ct) => connection.SolutionInfoAsync(ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(SolutionInfo), target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }
    [McpServerTool(Name = "solution_open")]
    [Description("Opens a solution in a routed Visual Studio session.")]
    public Task<ToolResponse<SolutionInfoResult>> SolutionOpen(
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<SolutionInfoResult>.Fail(validation));
        }

        var request = new SolutionOpenRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SolutionOpenAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "solution_close")]
    [Description("Closes the open solution in a routed Visual Studio session.")]
    public Task<ToolResponse<SolutionInfoResult>> SolutionClose(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.SolutionCloseAsync(ct),
            cancellationToken);
    }
    [McpServerTool(Name = "project_list")]
    [Description("Lists projects from a routed Visual Studio session.")]
    public Task<ToolResponse<ProjectListResult>> ProjectList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProjectListAsync(ct),
            cancellationToken);
    }
    [McpServerTool(Name = "solution_add_project")]
    [Description("Adds an existing project file to the routed Visual Studio solution.")]
    public Task<ToolResponse<ProjectInfo>> SolutionAddProject(
        string projectPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(projectPath) is { } validation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo>.Fail(validation));
        }

        var request = new SolutionAddProjectRequest { ProjectPath = projectPath.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SolutionAddProjectAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "solution_remove_project")]
    [Description("Removes a project from the routed Visual Studio solution.")]
    public Task<ToolResponse<ProjectInfo>> SolutionRemoveProject(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo>.Fail(validation));
        }

        var request = new ProjectInfoRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SolutionRemoveProjectAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "project_info")]
    [Description("Returns project metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<ProjectInfo?>> ProjectInfo(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo?>.Fail(validation));
        }

        var request = new ProjectInfoRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ProjectInfoAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "project_add_file")]
    [Description("Adds an existing file to a project in the routed Visual Studio solution.")]
    public Task<ToolResponse<ProjectInfo>> ProjectAddFile(
        string projectName,
        string filePath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } projectValidation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo>.Fail(projectValidation));
        }

        if (ValidateRequiredPath(filePath) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<ProjectInfo>.Fail(pathValidation));
        }

        var request = new ProjectFileRequest
        {
            ProjectName = projectName.Trim(),
            FilePath = filePath.Trim()
        };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.ProjectAddFileAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "startup_project_get")]
    [Description("Returns startup project metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<StartupProjectResult>> StartupProjectGet(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.StartupProjectGetAsync(ct),
            cancellationToken);
    }
    [McpServerTool(Name = "solution_overview")]
    [Description("Returns solution, project, startup-project, and test-project summary.")]
    public Task<ToolResponse<SolutionOverviewResult>> SolutionOverview(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var solution = await connection.SolutionInfoAsync(ct);
                var projects = await connection.ProjectListAsync(ct);
                var startup = await connection.StartupProjectGetAsync(ct);
                var testProjects = projects.Projects
                    .Where(project => IsLikelyTestProject(project.Name) || IsLikelyTestProject(project.UniqueName) || IsLikelyTestProject(project.FullName))
                    .ToArray();
                return new SolutionOverviewResult(solution, projects, startup, testProjects);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "project_dependencies")]
    [Description("Returns project/package references parsed from a project file when available.")]
    public Task<ToolResponse<ProjectDependenciesResult>> ProjectDependencies(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<ProjectDependenciesResult>.Fail(validation));
        }

        var request = new ProjectInfoRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var project = await connection.ProjectInfoAsync(request, ct);
                var projectPath = project?.FullName;
                if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                {
                    return new ProjectDependenciesResult(project, [], [], []);
                }

                var dependencies = ReadProjectDependencies(projectPath!);
                return new ProjectDependenciesResult(
                    project,
                    dependencies.TargetFrameworks,
                    dependencies.ProjectReferences,
                    dependencies.PackageReferences);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "startup_project_set")]
    [Description("Sets the startup project in a routed Visual Studio session.")]
    public Task<ToolResponse<StartupProjectResult>> StartupProjectSet(
        string projectName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateProjectName(projectName) is { } validation)
        {
            return Task.FromResult(ToolResponse<StartupProjectResult>.Fail(validation));
        }

        var request = new StartupProjectSetRequest { ProjectName = projectName.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.StartupProjectSetAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "test_discover")]
    [Description("Discovers tests through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestDiscover(
        string? projectName = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestDiscoverRequest { ProjectName = NormalizeOptional(projectName) };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestDiscoverAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "test_run")]
    [Description("Runs tests through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestRun(
        string? projectName = null,
        string? filter = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestRunRequest
        {
            ProjectName = NormalizeOptional(projectName),
            Filter = NormalizeOptional(filter)
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestRunAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "test_results")]
    [Description("Returns test results through a routed Visual Studio session.")]
    public Task<ToolResponse<TestOperationResult>> TestResults(
        string? runId = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TestResultsRequest { RunId = NormalizeOptional(runId) };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TestResultsAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "test_run_and_get_results")]
    [Description("Runs tests and then returns captured test results through a routed Visual Studio session.")]
    public Task<ToolResponse<TestRunAndGetResultsResult>> TestRunAndGetResults(
        string? projectName = null,
        string? filter = null,
        string? runId = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var runRequest = new TestRunRequest
        {
            ProjectName = NormalizeOptional(projectName),
            Filter = NormalizeOptional(filter)
        };
        var resultsRequest = new TestResultsRequest { RunId = NormalizeOptional(runId) };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var run = await connection.TestRunAsync(runRequest, ct);
                var results = await connection.TestResultsAsync(resultsRequest, ct);
                return new TestRunAndGetResultsResult(run, results);
            },
            cancellationToken);
    }
    [McpServerTool(Name = "task_list_get")]
    [Description("Lists Task List items (TODO/HACK/UNDONE comment tasks and user tasks) from a routed Visual Studio session.")]
    public Task<ToolResponse<TaskListResult>> TaskListGet(
        bool includeCommentTasks = true,
        bool includeUserTasks = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<TaskListResult>.Fail("Max items must be greater than zero."));
        }

        var request = new TaskListRequest
        {
            IncludeCommentTasks = includeCommentTasks,
            IncludeUserTasks = includeUserTasks,
            MaxItems = maxItems
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TaskListGetAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "task_list_add")]
    [Description("Adds a user task to the Task List through a routed Visual Studio session.")]
    public Task<ToolResponse<TaskListMutationResult>> TaskListAdd(
        [Description("The task description text.")]
        string description,
        [Description("Priority: High, Medium, or Low.")]
        string priority = "Medium",
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Task.FromResult(ToolResponse<TaskListMutationResult>.Fail("Description is required."));
        }

        var request = new TaskListAddRequest
        {
            Description = description.Trim(),
            Priority = priority
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TaskListAddAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "task_list_remove")]
    [Description("Removes a user task from the Task List through a routed Visual Studio session. Only user tasks (added via task_list_add) can be removed.")]
    public Task<ToolResponse<TaskListMutationResult>> TaskListRemove(
        [Description("The 1-based index of the task item, as returned by task_list_get.")]
        int index,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TaskListMutationRequest { Index = index };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TaskListRemoveAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "task_list_set_checked")]
    [Description("Checks or unchecks a user task in the Task List through a routed Visual Studio session. Only user tasks (added via task_list_add) support checking.")]
    public Task<ToolResponse<TaskListMutationResult>> TaskListSetChecked(
        [Description("The 1-based index of the task item, as returned by task_list_get.")]
        int index,
        bool @checked,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TaskListSetCheckedRequest { Index = index, Checked = @checked };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.TaskListSetCheckedAsync(request, ct),
            cancellationToken);
    }
    [McpServerTool(Name = "git_context")]
    [Description("Returns best-effort git status for the routed solution root.")]
    public Task<ToolResponse<GitContextResult>> GitContext(
        string? rootPath = null,
        int maxFiles = 100,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxFiles < 1)
        {
            return Task.FromResult(ToolResponse<GitContextResult>.Fail("Max files must be greater than zero."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var solution = await connection.SolutionInfoAsync(ct);
                var searchRoot = ResolveSearchRoot(rootPath, solution);
                return ReadGitContext(searchRoot, maxFiles);
            },
            cancellationToken,
            rootPath: GetRoutableWorkspacePath(rootPath));
    }
    private static bool IsLikelyTestProject(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            (value.Contains(".Tests", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Test", StringComparison.OrdinalIgnoreCase));
    }
    private static ProjectDependencyReadResult ReadProjectDependencies(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        var targetFrameworks = document.Descendants()
            .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(element => (element.Value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectReferences = document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element =>
            {
                var include = element.Attribute("Include")?.Value ?? string.Empty;
                var name = Path.GetFileNameWithoutExtension(include);
                return new ProjectDependencyInfo(
                    string.IsNullOrWhiteSpace(name) ? include : name,
                    null,
                    include);
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Name))
            .ToArray();

        var packageReferences = document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element =>
            {
                var include = element.Attribute("Include")?.Value ?? string.Empty;
                var version = element.Attribute("Version")?.Value ??
                    element.Elements().FirstOrDefault(child => child.Name.LocalName == "Version")?.Value;
                return new ProjectDependencyInfo(include, version, null);
            })
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Name))
            .ToArray();

        return new ProjectDependencyReadResult(targetFrameworks, projectReferences, packageReferences);
    }
    private static GitContextResult ReadGitContext(string rootPath, int maxFiles)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{rootPath}\" status --short",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (!process.Start())
            {
                return new GitContextResult(false, "Unable to start git.", rootPath, []);
            }

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return new GitContextResult(false, "git status timed out.", rootPath, []);
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (process.ExitCode != 0)
            {
                return new GitContextResult(false, string.IsNullOrWhiteSpace(error) ? "git status failed." : error.Trim(), rootPath, []);
            }

            var changedFiles = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Length > 3 ? line[3..].Trim() : line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(maxFiles)
                .ToArray();

            return new GitContextResult(true, "git status completed.", rootPath, changedFiles);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new GitContextResult(false, ex.Message, rootPath, []);
        }
    }
    private sealed record ProjectDependencyReadResult(
        IReadOnlyCollection<string> TargetFrameworks,
        IReadOnlyCollection<ProjectDependencyInfo> ProjectReferences,
        IReadOnlyCollection<ProjectDependencyInfo> PackageReferences);
    private VsSessionStatus? GetSessionStatus(VsSessionInfo session)
    {
        return _runtime.Sessions.ListSessionStatuses()
            .SingleOrDefault(status => string.Equals(
                status.Session.SessionId,
                session.SessionId,
                StringComparison.OrdinalIgnoreCase));
    }
    private BrokerPing CreatePing(VsSessionStatus? targetSession)
    {
        return new BrokerPing(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            IsRunning: _runtime.IsHttpEndpointRunning,
            McpEndpoint: _runtime.Options.McpEndpoint,
            PipeName: _runtime.Options.PipeName,
            Uptime: DateTimeOffset.UtcNow - _runtime.StartedUtc,
            RegisteredSessionCount: _runtime.Sessions.ListSessions().Count,
            TargetSession: targetSession);
    }
    private static string? ValidateProjectName(string? projectName)
    {
        return string.IsNullOrWhiteSpace(projectName)
            ? "Project name is required."
            : null;
    }
    private static ToolWindowRequest? CreateToolWindowRequest(string? caption, string? objectKind)
    {
        if (string.IsNullOrWhiteSpace(caption) && string.IsNullOrWhiteSpace(objectKind))
        {
            return null;
        }

        return new ToolWindowRequest
        {
            Caption = NormalizeOptional(caption),
            ObjectKind = NormalizeOptional(objectKind)
        };
    }
    [McpServerTool(Name = "project_remove_file")]
    [Description("Removes a file item from a project in the routed Visual Studio solution without deleting it from disk.")]
    public Task<ToolResponse<ProjectFileResult>> ProjectRemoveFile(string projectName, string filePath, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return Task.FromResult(FailWithCode<ProjectFileResult>("Project name is required.", ToolErrorCodes.InvalidRequest));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(FailWithCode<ProjectFileResult>("File path is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ProjectFileRequest
        {
            ProjectName = projectName,
            FilePath = filePath
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ProjectRemoveFileAsync(request, ct), cancellationToken);
    }
    [McpServerTool(Name = "vs_get_logs")]
    [Description("Returns recent broker log files with bounded tail text.")]
    public ToolResponse<BrokerLogResult> VsGetLogs(int maxFiles = 5, int maxCharsPerFile = 20000, string? minLevel = null)
    {
        if (maxFiles <= 0)
        {
            return FailWithCode<BrokerLogResult>("Max files must be greater than zero.", ToolErrorCodes.InvalidRequest);
        }

        if (maxCharsPerFile <= 0)
        {
            return FailWithCode<BrokerLogResult>("Max chars per file must be greater than zero.", ToolErrorCodes.InvalidRequest);
        }

        if (!TryParseLogLevel(minLevel, out var parsedMinLevel))
        {
            return FailWithCode<BrokerLogResult>("minLevel must be one of: debug, info, warning, error.", ToolErrorCodes.InvalidRequest);
        }

        var logsDirectory = _runtime.Options.EffectiveLogsDirectory;
        if (!Directory.Exists(logsDirectory))
        {
            var empty = ToolResponse<BrokerLogResult>.Ok(new BrokerLogResult(logsDirectory, []));
            AuditToolResult(nameof(VsGetLogs), null, empty.Success, null, empty.Message);
            return empty;
        }

        var files = Directory.EnumerateFiles(logsDirectory)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(maxFiles)
            .Select(file => ReadBrokerLogEntry(file, maxCharsPerFile, parsedMinLevel))
            .ToArray();
        var response = ToolResponse<BrokerLogResult>.Ok(new BrokerLogResult(logsDirectory, files));
        AuditToolResult(nameof(VsGetLogs), null, response.Success, null, response.Message);
        return response;
    }

    private static BrokerLogEntry ReadBrokerLogEntry(FileInfo file, int maxChars, BrokerLogLevel? minLevel)
    {
        var text = File.ReadAllText(file.FullName);
        if (minLevel is not null)
        {
            text = FilterLogTextByLevel(text, minLevel.Value);
        }

        var truncated = text.Length > maxChars;
        if (truncated)
        {
            text = text.Substring(text.Length - maxChars, maxChars);
        }

        return new BrokerLogEntry(
            file.FullName,
            file.Name,
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            file.Length,
            text,
            truncated);
    }

    private static bool TryParseLogLevel(string? value, out BrokerLogLevel? level)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            level = null;
            return true;
        }

        if (Enum.TryParse<BrokerLogLevel>(value.Trim(), ignoreCase: true, out var parsed))
        {
            level = parsed;
            return true;
        }

        level = null;
        return false;
    }

    private static string FilterLogTextByLevel(string text, BrokerLogLevel minLevel)
    {
        var lines = text.Split(
            new[] { "\r\n", "\n" },
            StringSplitOptions.None);
        var matches = lines.Where(line =>
            !string.IsNullOrWhiteSpace(line) &&
            TryReadLogLevel(line, out var level) &&
            level >= minLevel);
        return string.Join(Environment.NewLine, matches);
    }

    private static bool TryReadLogLevel(string line, out BrokerLogLevel level)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("level", out var levelElement))
            {
                level = BrokerLogLevel.Info;
                return true;
            }

            if (levelElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                Enum.TryParse(levelElement.GetString(), ignoreCase: true, out level))
            {
                return true;
            }

            if (levelElement.ValueKind == System.Text.Json.JsonValueKind.Number &&
                levelElement.TryGetInt32(out var numeric) &&
                Enum.IsDefined(typeof(BrokerLogLevel), numeric))
            {
                level = (BrokerLogLevel)numeric;
                return true;
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        level = BrokerLogLevel.Info;
        return false;
    }
}
