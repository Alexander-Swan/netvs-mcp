using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace NetVsMcp.Broker.Services;

[McpServerToolType]
public sealed partial class BrokerToolService
{
    private const string DocumentPathParameterDescription = "Document path relative to the solution or absolute path. Prefer forward slashes, for example src/Project/File.cs; if using Windows backslashes in JSON, escape them as double backslashes.";
    private const string OptionalDocumentPathParameterDescription = "Optional document path relative to the solution or absolute path. Prefer forward slashes, for example src/Project/File.cs; if using Windows backslashes in JSON, escape them as double backslashes.";
    private const string DocumentPathsParameterDescription = "Document paths relative to the solution or absolute paths. Prefer forward slashes, for example src/Project/File.cs; if using Windows backslashes in JSON, escape them as double backslashes.";
    private const string LineParameterDescription = "1-based line number as shown in the Visual Studio editor.";
    private const string ColumnParameterDescription = "1-based column number.";

    private static readonly BrokerToolDescriptor[] ToolDescriptors =
    [
        new("vs_list_sessions", "Lists Visual Studio instances registered with the local broker.", false),
        new("vs_get_status", "Returns local broker endpoint, uptime, and registered session status.", false),
        new("vs_get_capabilities", "Lists broker tools and Visual Studio capability categories.", false),
        new("vs_get_session", "Resolves a Visual Studio session and returns its current broker status.", false),
        new("vs_select_session", "Resolves a Visual Studio session using broker routing rules without persisting selection.", false),
        new("vs_ping", "Returns lightweight broker health and optional routed Visual Studio session status.", false),
        new("vs_launch_instance", "Launches a new Visual Studio (devenv.exe) process and waits for it to register with the broker.", false),
        new("vs_context_snapshot", "Returns a compact routed Visual Studio context snapshot.", true),
        new("execute_command", "Executes a Visual Studio command in a routed session.", true),
        new("get_status", "Returns Visual Studio session status through a routed session.", true),
        new("get_help", "Lists NetVsMcp broker tools and Visual Studio capability categories.", false),
        new("window_list", "Lists Visual Studio windows in a routed session.", true),
        new("window_activate", "Activates a Visual Studio window in a routed session.", true),
        new("toolwindow_show", "Shows a Visual Studio tool window in a routed session.", true),
        new("toolwindow_hide", "Hides a Visual Studio tool window in a routed session.", true),
        new("document_active", "Returns the active document for a routed Visual Studio session.", true),
        new("code_document_symbols", "Lists document symbols through a routed Visual Studio session. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("code_go_to_definition", "Finds and navigates to a symbol definition through a routed Visual Studio session. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("code_find_references", "Finds symbol references through a routed Visual Studio session. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("symbol_context", "Returns document text, nearby snippet, definition, and references for a code position. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("document_outline", "Returns document symbol outline information. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("find_implementations", "Returns best-effort implementation lookup status for a code position. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("rename_symbol_preview", "Returns a safe rename preview status for a code position. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("diagnostics_for_document", "Filters routed diagnostics to one document.", true),
        new("workspace_search", "Searches files under the routed solution root.", true),
        new("git_context", "Returns best-effort git status for the routed solution root.", true),
        new("open_relevant_files", "Opens a set of relevant files in the routed Visual Studio session.", true),
        new("build_solution", "Starts a solution build in a routed Visual Studio session.", true),
        new("build_status", "Returns build status from a routed Visual Studio session.", true),
        new("build_and_get_errors", "Builds the routed solution and returns diagnostics.", true),
        new("errors_list", "Lists errors and warnings from a routed Visual Studio session.", true),
        new("output_read", "Reads an output pane from a routed Visual Studio session.", true),
        new("solution_overview", "Returns solution, project, startup, and test-project summary.", true),
        new("project_dependencies", "Returns project/package references parsed from a project file when available.", true),
        new("package_restore", "Returns package restore support status for a routed project.", true),
        new("test_run_and_get_results", "Runs tests and returns captured results.", true),
        new("debug_status", "Returns debugger status from a routed Visual Studio session.", true),
        new("debug_snapshot", "Optionally advances the debugger (step/continue/break), waits for it to settle, then returns state, locals, and the requested include categories in one call.", true),
        new("debug_wait_for_break", "Waits for the debugger to leave dbgRunMode (e.g. a breakpoint fires), then returns state, locals, and the requested include categories in one call.", true),
        new("debug_eval_many", "Evaluates multiple debugger expressions.", true),
        new("debug_get_mode", "Returns debugger mode from a routed Visual Studio session.", true),
        new("debug_start", "Starts debugging in a routed Visual Studio session.", true),
        new("debug_stop", "Stops debugging in a routed Visual Studio session.", true),
        new("debug_continue", "Continues debugging in a routed Visual Studio session.", true),
        new("debug_break", "Breaks into debugging in a routed Visual Studio session.", true),
        new("debug_step", "Steps the debugger in a routed Visual Studio session.", true),
        new("breakpoint_set", "Sets a breakpoint in a routed Visual Studio session.", true),
        new("breakpoint_list", "Lists breakpoints from a routed Visual Studio session.", true),
        new("breakpoint_group_list", "Lists breakpoint groups from a routed Visual Studio session.", true),
        new("breakpoint_remove", "Removes breakpoints in a routed Visual Studio session.", true),
        new("breakpoint_enable", "Enables or disables breakpoints in a routed Visual Studio session.", true),
        new("breakpoint_group_enable", "Enables or disables all breakpoints in a group.", true),
        new("breakpoint_group_remove", "Removes all breakpoints in a group.", true),
        new("debug_get_callstack", "Returns the current call stack from a routed Visual Studio session.", true),
        new("debug_get_locals", "Returns locals from a routed Visual Studio session.", true),
        new("debug_evaluate", "Evaluates an expression in a routed Visual Studio session.", true),
        new("document_read", "Reads a document through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("document_open", "Opens a document through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("selection_get", "Returns the current editor selection from a routed Visual Studio session.", true),
        new("document_write", "Replaces a document buffer through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("document_save", "Saves a document through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("editor_insert", "Inserts text through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("editor_replace", "Replaces a text range through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("editor_goto_line", "Moves the caret through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("task_list_get", "Lists Task List items (comment tasks and user tasks) from a routed Visual Studio session.", true),
        new("task_list_add", "Adds a user task to the Task List through a routed Visual Studio session.", true),
        new("task_list_remove", "Removes a user task from the Task List through a routed Visual Studio session.", true),
        new("task_list_set_checked", "Checks or unchecks a user task in the Task List through a routed Visual Studio session.", true),
        new("selection_set", "Sets the editor selection through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("document_cleanup", "Formats/cleans up a document through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("format_and_organize", "Formats/cleans up a document and reports organize-import status. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("edit_preview", "Creates a pending safe-edit preview through a routed Visual Studio session. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("prepare_safe_edit", "Reads a document and creates a safe-edit preview. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("edit_approve", "Approves a pending safe edit through a routed Visual Studio session.", true),
        new("apply_safe_edit_and_build", "Approves a pending edit, builds, and returns errors.", true),
        new("edit_reject", "Rejects a pending safe edit through a routed Visual Studio session.", true),
        new("edit_list_pending", "Lists pending safe edits through a routed Visual Studio session.", true),
        new("solution_open", "Opens a solution in a routed Visual Studio session.", true),
        new("solution_close", "Closes the open solution in a routed Visual Studio session.", true),
        new("solution_info", "Returns solution metadata from a routed Visual Studio session.", true),
        new("solution_add_project", "Adds an existing project file to the routed Visual Studio solution.", true),
        new("solution_remove_project", "Removes a project from the routed Visual Studio solution.", true),
        new("project_list", "Lists projects from a routed Visual Studio session.", true),
        new("project_info", "Returns project metadata from a routed Visual Studio session.", true),
        new("project_add_file", "Adds an existing file to a project in the routed Visual Studio solution.", true),
        new("project_remove_file", "Removes a file item from a project in the routed Visual Studio solution without deleting it from disk.", true),
        new("project_add_reference", "Adds an assembly or project reference to a project in the routed Visual Studio solution.", true),
        new("project_remove_reference", "Removes an assembly or project reference from a project in the routed Visual Studio solution.", true),
        new("startup_project_get", "Returns startup project metadata from a routed Visual Studio session.", true),
        new("startup_project_set", "Sets the startup project in a routed Visual Studio session.", true),
        new("test_discover", "Discovers tests through a routed Visual Studio session.", true),
        new("test_run", "Runs tests through a routed Visual Studio session.", true),
        new("test_results", "Returns test results through a routed Visual Studio session.", true),
        new("document_list", "Lists open documents in a routed Visual Studio session.", true),
        new("document_close", "Closes an open document with save, discard, or no-save policy. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("editor_find", "Finds text in one editor document. Prefer forward slashes in path values like src/Project/File.cs.", true),
        new("find_in_files", "Searches files under a Visual Studio solution or root path. Prefer forward slashes in rootPath values like src/Project.", true),
        new("code_go_to_implementation", "Finds implementation locations for a symbol at a code position. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("code_workspace_symbols", "Searches symbols in the live Visual Studio workspace.", true),
        new("build_project", "Builds one project in the routed Visual Studio session.", true),
        new("build_cancel", "Cancels an active Visual Studio build.", true),
        new("clean_solution", "Cleans the routed Visual Studio solution.", true),
        new("rebuild_solution", "Rebuilds the routed Visual Studio solution.", true),
        new("build_configuration_get", "Returns the active solution build configuration and platform.", true),
        new("build_configuration_set", "Sets the active solution build configuration and optional platform.", true),
        new("output_list_panes", "Lists Visual Studio output panes.", true),
        new("output_write", "Writes text to a Visual Studio output pane.", true),
        new("output_clear", "Clears a Visual Studio output pane.", true),
        new("diagnostics_binding_errors", "Returns binding diagnostics when a VSIX diagnostics backend is available.", true),
        new("debug_start_without_debugging", "Starts the current startup project without debugging.", true),
        new("debug_restart", "Restarts the active debug session.", true),
        new("debug_attach", "Attaches the Visual Studio debugger to a local process by id or name.", true),
        new("debug_get_threads", "Lists debugger threads for the current debug program.", true),
        new("debug_set_variable", "Sets a debugger variable by evaluating an assignment expression.", true),
        new("watch_add", "Adds a debugger watch expression when supported by the VSIX debugger service.", true),
        new("watch_remove", "Removes a debugger watch expression when supported by the VSIX debugger service.", true),
        new("watch_list", "Lists debugger watch expressions when supported by the VSIX debugger service.", true),
        new("thread_switch", "Switches the active debugger thread.", true),
        new("thread_set_frozen", "Freezes or thaws a debugger thread when supported by the active debug engine.", true),
        new("thread_get_callstack", "Returns the call stack for a debugger thread when supported by the active debug engine.", true),
        new("process_list_debugged", "Lists processes currently being debugged by Visual Studio.", true),
        new("process_list_local", "Lists local processes visible to Visual Studio for debugger attach workflows.", true),
        new("process_detach", "Detaches the Visual Studio debugger from a debugged process by id or name.", true),
        new("process_terminate", "Terminates a debugged process by id or name when supported by the active debug engine.", true),
        new("immediate_execute", "Executes text in the immediate window when supported by the VSIX debugger service.", true),
        new("module_list", "Lists debugger modules when supported by the VSIX debugger service.", true),
        new("exception_settings_get", "Returns debugger exception settings when supported by the VSIX debugger service.", true),
        new("exception_settings_set", "Sets debugger exception settings when supported by the VSIX debugger service.", true),
        new("parallel_stacks", "Returns parallel stack information when the active Visual Studio debug engine exposes it.", true),
        new("parallel_watch", "Returns parallel watch expressions when the active Visual Studio debug engine exposes them.", true),
        new("console_read", "Reads debuggee console output when a VSIX console backend is available.", true),
        new("console_send", "Sends debuggee console input when a VSIX console backend is available.", true),
        new("console_get_info", "Returns debuggee console metadata when a VSIX console backend is available.", true),
        new("ui_capture_window", "Captures a debuggee window when a VSIX UI automation backend is available.", true),
        new("ui_capture_region", "Captures a screen region when a VSIX UI automation backend is available.", true),
        new("ui_snapshot", "Returns a debuggee UI snapshot when a VSIX UI automation backend is available.", true),
        new("ui_get_tree", "Returns a debuggee UI automation tree when a VSIX UI automation backend is available.", true),
        new("ui_find_elements", "Finds UI automation elements when a VSIX UI automation backend is available.", true),
        new("ui_get_element", "Returns one UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_click", "Clicks a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_double_click", "Double-clicks a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_right_click", "Right-clicks a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_drag", "Drags a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_set_value", "Sets a UI automation value when a VSIX UI automation backend is available.", true),
        new("ui_invoke", "Invokes a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_send_keys", "Sends keys to the debuggee UI when a VSIX UI automation backend is available.", true),
        new("ui_wait_for_element", "Waits for a UI automation element when a VSIX UI automation backend is available.", true),
        new("ui_wait_idle", "Waits for debuggee UI idle when a VSIX UI automation backend is available.", true),
        new("web_connect", "Connects browser debugging when a VSIX browser backend is available.", true),
        new("web_disconnect", "Disconnects browser debugging when a VSIX browser backend is available.", true),
        new("web_status", "Returns browser debugging status when a VSIX browser backend is available.", true),
        new("web_navigate", "Navigates a connected browser when a VSIX browser backend is available.", true),
        new("web_screenshot", "Captures a browser screenshot when a VSIX browser backend is available.", true),
        new("web_dom_get", "Returns browser DOM data when a VSIX browser backend is available.", true),
        new("web_dom_query", "Queries browser DOM elements when a VSIX browser backend is available.", true),
        new("web_console", "Returns browser console entries when a VSIX browser backend is available.", true),
        new("web_js_execute", "Executes JavaScript in a connected browser when a VSIX browser backend is available.", true),
        new("web_network", "Returns browser network events when a VSIX browser backend is available.", true),
        new("web_element_click", "Clicks a browser element when a VSIX browser backend is available.", true),
        new("web_element_set_value", "Sets a browser element value when a VSIX browser backend is available.", true),
        new("nuget_list", "Lists PackageReference NuGet packages from project files in the routed Visual Studio solution.", true),
        new("nuget_search", "Searches NuGet packages from nuget.org.", true),
        new("nuget_install", "Installs a NuGet package into a project.", true),
        new("nuget_update", "Updates a NuGet package in a project.", true),
        new("nuget_uninstall", "Uninstalls a NuGet package from a project.", true),
        new("vs_get_logs", "Returns recent broker log files with bounded tail text.", false)
    ];

    private static readonly VsCapability[] VisualStudioCapabilities =
    [
        VsCapability.Editor,
        VsCapability.Navigation,
        VsCapability.Build,
        VsCapability.Debugger,
        VsCapability.Diagnostics,
        VsCapability.Tests,
        VsCapability.ProjectSystem
    ];

    private readonly BrokerRuntime _runtime;

    public BrokerToolService(BrokerRuntime runtime)
    {
        _runtime = runtime;
    }

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

    [McpServerTool(Name = "vs_get_capabilities")]
    [Description("Lists NetVsMcp broker tools and Visual Studio capability categories.")]
    public ToolResponse<BrokerCapabilities> VsGetCapabilities()
    {
        var capabilities = new BrokerCapabilities(
            _runtime.Options.McpEndpoint,
            ToolDescriptors.Select(WithCategoryMetadata).ToArray(),
            VisualStudioCapabilities);

        var response = ToolResponse<BrokerCapabilities>.Ok(capabilities);
        AuditToolResult(nameof(VsGetCapabilities), null, response.Success, null, response.Message);
        return response;
    }

    [McpServerTool(Name = "get_help")]
    [Description("Lists NetVsMcp broker tools and Visual Studio capability categories.")]
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

        var response = ToolResponse<BrokerCapabilities>.Ok(capabilities);
        AuditToolResult(nameof(GetHelp), null, response.Success, null, response.Message);
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
                    await connection.BuildStatusAsync(ct),
                    await connection.ErrorsListAsync(new ErrorListRequest { IncludeWarnings = true, MaxItems = 50 }, ct),
                    await connection.EditListPendingAsync(ct));
            },
            cancellationToken);
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

    [McpServerTool(Name = "document_active")]
    [Description("Returns the active document for a routed Visual Studio session.")]
    public async Task<ToolResponse<string?>> DocumentActive(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            static (connection, ct) => connection.GetActiveDocumentAsync(ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(DocumentActive), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "code_document_symbols")]
    [Description("Lists document symbols for a document in a routed Visual Studio session.")]
    public async Task<ToolResponse<IReadOnlyCollection<string>>> CodeDocumentSymbols(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<IReadOnlyCollection<string>>.Fail("Document path is required.");
        }

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ListDocumentSymbolsAsync(documentPath, ct),
            cancellationToken);

        var response = ToToolResponse(dispatch);
        AuditToolResult(nameof(CodeDocumentSymbols), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "code_go_to_definition")]
    [Description("Finds and navigates to a symbol definition through a routed Visual Studio session.")]
    public Task<ToolResponse<GoToDefinitionResult>> CodeGoToDefinition(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<GoToDefinitionResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeGoToDefinitionAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "code_find_references")]
    [Description("Finds symbol references through a routed Visual Studio session.")]
    public Task<ToolResponse<FindReferencesResult>> CodeFindReferences(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<FindReferencesResult>.Fail(validation));
        }

        var request = new CodePositionRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindReferencesAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "symbol_context")]
    [Description("Returns document text, nearby snippet, definition, and references for a code position.")]
    public Task<ToolResponse<SymbolContextResult>> SymbolContext(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        int contextLines = 4,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<SymbolContextResult>.Fail(validation));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var position = new CodePositionRequest { DocumentPath = documentPath.Trim(), Line = line, Column = column };
                var document = await connection.DocumentReadAsync(new DocumentReadRequest { Path = position.DocumentPath }, ct);
                return new SymbolContextResult(
                    document,
                    await connection.CodeGoToDefinitionAsync(position, ct),
                    await connection.CodeFindReferencesAsync(position, ct),
                    ExtractSnippet(document.Text, line, Math.Max(0, contextLines)));
            },
            cancellationToken);
    }

    [McpServerTool(Name = "document_outline")]
    [Description("Returns document symbol outline information.")]
    public async Task<ToolResponse<DocumentOutlineResult>> DocumentOutline(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<DocumentOutlineResult>.Fail("Document path is required.");
        }

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            async (connection, ct) =>
            {
                var response = await connection.ListDocumentSymbolsAsync(documentPath.Trim(), ct);
                return new DocumentOutlineResult(documentPath.Trim(), response.Value ?? []);
            },
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(DocumentOutline), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    [McpServerTool(Name = "find_implementations")]
    [Description("Returns best-effort implementation lookup status for a code position.")]
    public Task<ToolResponse<FindImplementationsResult>> FindImplementations(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<FindImplementationsResult>.Fail(validation));
        }

        var position = new CodePositionRequest { DocumentPath = documentPath.Trim(), Line = line, Column = column };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeFindImplementationsAsync(position, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "rename_symbol_preview")]
    [Description("Returns safe rename preview status for a code position.")]
    public Task<ToolResponse<RenameSymbolPreviewResult>> RenameSymbolPreview(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string newName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateCodePosition(documentPath, line, column) is { } validation)
        {
            return Task.FromResult(ToolResponse<RenameSymbolPreviewResult>.Fail(validation));
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return Task.FromResult(ToolResponse<RenameSymbolPreviewResult>.Fail("New name is required."));
        }

        var request = new RenameSymbolRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            NewName = newName.Trim()
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.CodeRenameSymbolPreviewAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_read")]
    [Description("Reads a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentReadResult>> DocumentRead(
        [Description(DocumentPathParameterDescription)]
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<DocumentReadResult>.Fail(validation));
        }

        var request = new DocumentReadRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentReadAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_open")]
    [Description("Opens a document through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> DocumentOpen(
        [Description(DocumentPathParameterDescription)]
        string path,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(validation));
        }

        var request = new DocumentOpenRequest { Path = path.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentOpenAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "open_relevant_files")]
    [Description("Opens a set of relevant files in the routed Visual Studio session.")]
    public Task<ToolResponse<OpenRelevantFilesResult>> OpenRelevantFiles(
        [Description(DocumentPathsParameterDescription)]
        string[] paths,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (paths is null || paths.Length == 0)
        {
            return Task.FromResult(ToolResponse<OpenRelevantFilesResult>.Fail("At least one path is required."));
        }

        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length == 0)
        {
            return Task.FromResult(ToolResponse<OpenRelevantFilesResult>.Fail("At least one path is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var documents = new List<EditorDocumentInfo>();
                foreach (var path in normalizedPaths)
                {
                    documents.Add(await connection.DocumentOpenAsync(new DocumentOpenRequest { Path = path }, ct));
                }

                return new OpenRelevantFilesResult(documents);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "selection_get")]
    [Description("Returns the current editor selection from a routed Visual Studio session.")]
    public Task<ToolResponse<SelectionInfo?>> SelectionGet(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.SelectionGetAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_write")]
    [Description("Replaces a document buffer through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentWrite(
        [Description(DocumentPathParameterDescription)]
        string path,
        string text,
        bool createIfMissing = false,
        bool saveAfterWrite = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new DocumentWriteRequest
        {
            Path = path.Trim(),
            Text = text,
            CreateIfMissing = createIfMissing,
            SaveAfterWrite = saveAfterWrite
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentWriteAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_save")]
    [Description("Saves a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> DocumentSave(
        [Description(OptionalDocumentPathParameterDescription)]
        string? path = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DocumentSaveRequest { Path = NormalizeOptional(path) ?? string.Empty };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentSaveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_insert")]
    [Description("Inserts text through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorInsert(
        [Description(DocumentPathParameterDescription)]
        string path,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column,
        string text,
        bool saveAfterEdit = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (ValidatePosition(line, column) is { } positionValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(positionValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new EditorInsertRequest
        {
            Path = path.Trim(),
            Line = line,
            Column = column,
            Text = text,
            SaveAfterEdit = saveAfterEdit
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorInsertAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_replace")]
    [Description("Replaces a text range through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentMutationResult>> EditorReplace(
        [Description(DocumentPathParameterDescription)]
        string path,
        [Description(LineParameterDescription)]
        int startLine,
        [Description(ColumnParameterDescription)]
        int startColumn,
        [Description(LineParameterDescription)]
        int endLine,
        [Description(ColumnParameterDescription)]
        int endColumn,
        string text,
        bool saveAfterEdit = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(pathValidation));
        }

        if (ValidateRange(startLine, startColumn, endLine, endColumn) is { } rangeValidation)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail(rangeValidation));
        }

        if (text is null)
        {
            return Task.FromResult(ToolResponse<DocumentMutationResult>.Fail("Text is required."));
        }

        var request = new EditorReplaceRequest
        {
            Path = path.Trim(),
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn,
            Text = text,
            SaveAfterEdit = saveAfterEdit
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorReplaceAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "editor_goto_line")]
    [Description("Moves the caret through a routed Visual Studio session.")]
    public Task<ToolResponse<EditorDocumentInfo>> EditorGotoLine(
        [Description(DocumentPathParameterDescription)]
        string path,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column = 1,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(pathValidation));
        }

        if (ValidatePosition(line, column) is { } positionValidation)
        {
            return Task.FromResult(ToolResponse<EditorDocumentInfo>.Fail(positionValidation));
        }

        var request = new EditorGotoLineRequest
        {
            Path = path.Trim(),
            Line = line,
            Column = column
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditorGotoLineAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "selection_set")]
    [Description("Sets the editor selection through a routed Visual Studio session.")]
    public Task<ToolResponse<SelectionInfo>> SelectionSet(
        [Description(DocumentPathParameterDescription)]
        string path,
        [Description(LineParameterDescription)]
        int startLine,
        [Description(ColumnParameterDescription)]
        int startColumn,
        [Description(LineParameterDescription)]
        int endLine,
        [Description(ColumnParameterDescription)]
        int endColumn,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(pathValidation));
        }

        if (ValidateRange(startLine, startColumn, endLine, endColumn) is { } rangeValidation)
        {
            return Task.FromResult(ToolResponse<SelectionInfo>.Fail(rangeValidation));
        }

        var request = new SelectionSetRequest
        {
            Path = path.Trim(),
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.SelectionSetAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "document_cleanup")]
    [Description("Formats/cleans up a document through a routed Visual Studio session.")]
    public Task<ToolResponse<DocumentCleanupResult>> DocumentCleanup(
        [Description(DocumentPathParameterDescription)]
        string path,
        bool saveAfterCleanup = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<DocumentCleanupResult>.Fail(pathValidation));
        }

        var request = new DocumentCleanupRequest
        {
            Path = path.Trim(),
            SaveAfterCleanup = saveAfterCleanup
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DocumentCleanupAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "format_and_organize")]
    [Description("Formats/cleans up a document and reports organize-import status.")]
    public Task<ToolResponse<FormatAndOrganizeResult>> FormatAndOrganize(
        [Description(DocumentPathParameterDescription)]
        string path,
        bool saveAfterCleanup = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return Task.FromResult(ToolResponse<FormatAndOrganizeResult>.Fail(pathValidation));
        }

        var request = new DocumentCleanupRequest
        {
            Path = path.Trim(),
            SaveAfterCleanup = saveAfterCleanup
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var cleanup = await connection.DocumentCleanupAsync(request, ct);
                var message = cleanup.Command is null
                    ? "Document cleanup completed; organize imports command was not reported by the VSIX."
                    : $"Document cleanup completed with command '{cleanup.Command}'.";
                return new FormatAndOrganizeResult(cleanup, message);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "edit_preview")]
    [Description("Creates a pending safe-edit preview through a routed Visual Studio session.")]
    public Task<ToolResponse<EditPreviewResult>> EditPreview(
        string operation,
        [Description(DocumentPathParameterDescription)]
        string path,
        string text,
        bool createIfMissing = false,
        bool saveAfterEdit = false,
        [Description("1-based line number; required when operation is 'insert'.")]
        int line = 0,
        [Description("1-based column number; required when operation is 'insert'.")]
        int column = 0,
        [Description("1-based start line number; required when operation is 'replace'.")]
        int startLine = 0,
        [Description("1-based start column number; required when operation is 'replace'.")]
        int startColumn = 0,
        [Description("1-based end line number; required when operation is 'replace'.")]
        int endLine = 0,
        [Description("1-based end column number; required when operation is 'replace'.")]
        int endColumn = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditPreview(operation, path, text, line, column, startLine, startColumn, endLine, endColumn) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditPreviewResult>.Fail(validation));
        }

        var normalizedOperation = operation.Trim().ToLowerInvariant();
        var request = new EditPreviewRequest
        {
            Operation = normalizedOperation,
            Path = path.Trim(),
            Text = text,
            CreateIfMissing = createIfMissing,
            SaveAfterEdit = saveAfterEdit,
            Line = line,
            Column = column,
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditPreviewAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "prepare_safe_edit")]
    [Description("Reads a document and creates a safe-edit preview through a routed Visual Studio session.")]
    public Task<ToolResponse<PrepareSafeEditResult>> PrepareSafeEdit(
        string operation,
        [Description(DocumentPathParameterDescription)]
        string path,
        string text,
        bool createIfMissing = false,
        bool saveAfterEdit = false,
        [Description("1-based line number; required when operation is 'insert'.")]
        int line = 0,
        [Description("1-based column number; required when operation is 'insert'.")]
        int column = 0,
        [Description("1-based start line number; required when operation is 'replace'.")]
        int startLine = 0,
        [Description("1-based start column number; required when operation is 'replace'.")]
        int startColumn = 0,
        [Description("1-based end line number; required when operation is 'replace'.")]
        int endLine = 0,
        [Description("1-based end column number; required when operation is 'replace'.")]
        int endColumn = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditPreview(operation, path, text, line, column, startLine, startColumn, endLine, endColumn) is { } validation)
        {
            return Task.FromResult(ToolResponse<PrepareSafeEditResult>.Fail(validation));
        }

        var normalizedPath = path.Trim();
        var request = new EditPreviewRequest
        {
            Operation = operation.Trim().ToLowerInvariant(),
            Path = normalizedPath,
            Text = text,
            CreateIfMissing = createIfMissing,
            SaveAfterEdit = saveAfterEdit,
            Line = line,
            Column = column,
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var original = await connection.DocumentReadAsync(new DocumentReadRequest { Path = normalizedPath }, ct);
                var preview = await connection.EditPreviewAsync(request, ct);
                return new PrepareSafeEditResult(original, preview);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "edit_approve")]
    [Description("Approves a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditApprove(
        string editId,
        bool saveAfterApply = false,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditDecisionResult>.Fail(validation));
        }

        var request = new EditDecisionRequest
        {
            EditId = editId.Trim(),
            SaveAfterApply = saveAfterApply
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditApproveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "apply_safe_edit_and_build")]
    [Description("Approves a pending safe edit, builds the routed solution, and returns diagnostics.")]
    public Task<ToolResponse<ApplySafeEditAndBuildResult>> ApplySafeEditAndBuild(
        string editId,
        bool saveAfterApply = true,
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<ApplySafeEditAndBuildResult>.Fail(validation));
        }

        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<ApplySafeEditAndBuildResult>.Fail("Max items must be greater than zero."));
        }

        var editRequest = new EditDecisionRequest
        {
            EditId = editId.Trim(),
            SaveAfterApply = saveAfterApply
        };

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
                var edit = await connection.EditApproveAsync(editRequest, ct);
                var build = await connection.BuildSolutionAsync(new BuildSolutionRequest { WaitForBuildToFinish = true }, ct);
                var errors = await connection.ErrorsListAsync(errorsRequest, ct);
                return new ApplySafeEditAndBuildResult(edit, build, errors);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "edit_reject")]
    [Description("Rejects a pending safe edit through a routed Visual Studio session.")]
    public Task<ToolResponse<EditDecisionResult>> EditReject(
        string editId,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateEditId(editId) is { } validation)
        {
            return Task.FromResult(ToolResponse<EditDecisionResult>.Fail(validation));
        }

        var request = new EditDecisionRequest { EditId = editId.Trim() };
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.EditRejectAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "edit_list_pending")]
    [Description("Lists pending safe edits through a routed Visual Studio session.")]
    public Task<ToolResponse<PendingEditListResult>> EditListPending(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.EditListPendingAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "solution_info")]
    [Description("Returns solution metadata from a routed Visual Studio session.")]
    public Task<ToolResponse<SolutionInfoResult>> SolutionInfo(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.SolutionInfoAsync(ct),
            cancellationToken);
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

    [McpServerTool(Name = "errors_list")]
    [Description("Lists errors and warnings from a routed Visual Studio session.")]
    public async Task<ToolResponse<ErrorListResult>> ErrorsList(
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (maxItems < 1)
        {
            return ToolResponse<ErrorListResult>.Fail("Max items must be greater than zero.");
        }

        var request = new ErrorListRequest
        {
            IncludeWarnings = includeWarnings,
            MaxItems = maxItems
        };

        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            CreateTarget(sessionId, solutionName, solutionPath),
            (connection, ct) => connection.ErrorsListAsync(request, ct),
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(nameof(ErrorsList), CreateTarget(sessionId, solutionName, solutionPath), response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
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

    [McpServerTool(Name = "diagnostics_for_document")]
    [Description("Filters routed diagnostics to one document path.")]
    public Task<ToolResponse<DiagnosticsForDocumentResult>> DiagnosticsForDocument(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        bool includeWarnings = true,
        int maxItems = 200,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateRequiredPath(documentPath) is { } validation)
        {
            return Task.FromResult(ToolResponse<DiagnosticsForDocumentResult>.Fail(validation));
        }

        if (maxItems < 1)
        {
            return Task.FromResult(ToolResponse<DiagnosticsForDocumentResult>.Fail("Max items must be greater than zero."));
        }

        var normalizedPath = documentPath.Trim();
        var request = new ErrorListRequest
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
                var errors = await connection.ErrorsListAsync(request, ct);
                var items = errors.Items
                    .Where(item => PathsEqual(item.File, normalizedPath))
                    .ToArray();
                return new DiagnosticsForDocumentResult(normalizedPath, items);
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

    [McpServerTool(Name = "workspace_search")]
    [Description("Searches files under the routed solution root.")]
    public Task<ToolResponse<WorkspaceSearchResult>> WorkspaceSearch(
        string query,
        string filePattern = "*.*",
        string? rootPath = null,
        int maxMatches = 100,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(ToolResponse<WorkspaceSearchResult>.Fail("Query is required."));
        }

        if (maxMatches < 1)
        {
            return Task.FromResult(ToolResponse<WorkspaceSearchResult>.Fail("Max matches must be greater than zero."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var solution = await connection.SolutionInfoAsync(ct);
                var searchRoot = ResolveSearchRoot(rootPath, solution);
                var result = SearchWorkspace(searchRoot, query.Trim(), string.IsNullOrWhiteSpace(filePattern) ? "*.*" : filePattern.Trim(), maxMatches, ct);
                return result;
            },
            cancellationToken);
    }

    [McpServerTool(Name = "debug_status")]
    [Description("Returns debugger status from a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStatus(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStatusAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_mode")]
    [Description("Returns debugger mode from a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugGetMode(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetModeAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_start")]
    [Description("Starts debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStart(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStartAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_stop")]
    [Description("Stops debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStop(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStopAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_continue")]
    [Description("Continues debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugContinue(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugContinueAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_break")]
    [Description("Breaks into debugging in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugBreak(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugBreakAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_step")]
    [Description("Steps the debugger in a routed Visual Studio session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStep(
        DebugStepKind stepKind = DebugStepKind.Over,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(stepKind))
        {
            return Task.FromResult(ToolResponse<DebuggerStateInfo>.Fail("Debug step kind is invalid."));
        }

        var request = new DebugStepRequest
        {
            StepKind = stepKind
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DebugStepAsync(request, ct),
            cancellationToken);
    }

    private const string BreakpointActionMetadataWarning =
        "'dependsOnBreakpointName' is stored as informational metadata only. Visual Studio's EnvDTE automation API " +
        "does not expose breakpoint dependencies, so this breakpoint will not actually wait for another breakpoint " +
        "to be hit first. 'actionMessage' + 'continueAfterAction' are real: when this breakpoint is hit, the broker's " +
        "VSIX extension logs the (expression-interpolated, e.g. \"value={x}\") message to the Debug output pane and, " +
        "if requested, resumes execution automatically instead of breaking.";

    private static bool HasUnsupportedBreakpointActionMetadata(BreakpointSetRequest request) =>
        !string.IsNullOrWhiteSpace(request.DependsOnBreakpointName);

    [McpServerTool(Name = "breakpoint_set")]
    [Description("Sets a breakpoint in a routed Visual Studio session.")]
    public async Task<ToolResponse<BreakpointInfo>> BreakpointSet(
        [Description(DocumentPathParameterDescription)]
        string documentPath,
        [Description(LineParameterDescription)]
        int line,
        [Description(ColumnParameterDescription)]
        int column = 1,
        string? condition = null,
        string? action = null,
        string? actionMessage = null,
        bool continueAfterAction = false,
        int? hitCount = null,
        string? hitCountType = null,
        string? dependsOnBreakpointName = null,
        string? groupName = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ToolResponse<BreakpointInfo>.Fail("Document path is required.");
        }

        if (line < 1)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint line must be greater than zero.");
        }

        if (column < 1)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint column must be greater than zero.");
        }

        if (hitCount is < 0)
        {
            return ToolResponse<BreakpointInfo>.Fail("Breakpoint hit count must be zero or greater.");
        }

        var request = new BreakpointSetRequest
        {
            DocumentPath = documentPath.Trim(),
            Line = line,
            Column = column,
            Condition = NormalizeOptional(condition),
            Action = NormalizeOptional(action),
            ActionMessage = NormalizeOptional(actionMessage),
            ContinueAfterAction = continueAfterAction,
            HitCount = hitCount,
            HitCountType = NormalizeOptional(hitCountType),
            DependsOnBreakpointName = NormalizeOptional(dependsOnBreakpointName),
            GroupName = NormalizeOptional(groupName)
        };

        var response = await DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointSetAsync(request, ct),
            cancellationToken);

        if (response.Success && HasUnsupportedBreakpointActionMetadata(request))
        {
            return response with { Message = BreakpointActionMetadataWarning };
        }

        return response;
    }

    [McpServerTool(Name = "breakpoint_list")]
    [Description("Lists breakpoints from a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointListResult>> BreakpointList(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.BreakpointListAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_group_list")]
    [Description("Lists breakpoint groups from a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointGroupListResult>> BreakpointGroupList(
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
                var breakpoints = await connection.BreakpointListAsync(ct);
                var groups = breakpoints.Breakpoints
                    .Select(breakpoint => NormalizeOptional(breakpoint.GroupName))
                    .Where(group => group is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(group => group!)
                    .ToArray();
                return new BreakpointGroupListResult(groups, breakpoints.Breakpoints);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_remove")]
    [Description("Removes breakpoints in a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointRemoveResult>> BreakpointRemove(
        string? name = null,
        [Description(OptionalDocumentPathParameterDescription)]
        string? documentPath = null,
        [Description("1-based line number; used with documentPath to identify the breakpoint to remove.")]
        int line = 0,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBreakpointLookup(name, documentPath, line);
        if (validation is not null)
        {
            return Task.FromResult(ToolResponse<BreakpointRemoveResult>.Fail(validation));
        }

        var request = new BreakpointRemoveRequest
        {
            Name = NormalizeOptional(name),
            DocumentPath = NormalizeOptional(documentPath),
            Line = line
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.BreakpointRemoveAsync(request, ct),
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_enable")]
    [Description("Enables or disables breakpoints in a routed Visual Studio session. When disabling, the response also includes the current debugger state (similar to debug_snapshot); pass continueExecution to resume the debugger afterward.")]
    public Task<ToolResponse<BreakpointEnableResult>> BreakpointEnable(
        bool enabled,
        string? name = null,
        [Description(OptionalDocumentPathParameterDescription)]
        string? documentPath = null,
        [Description("1-based line number; used with documentPath to identify the breakpoint to enable or disable.")]
        int line = 0,
        [Description("When disabling, continue debugger execution afterward if it is paused.")]
        bool continueExecution = false,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateBreakpointLookup(name, documentPath, line);
        if (validation is not null)
        {
            return Task.FromResult(ToolResponse<BreakpointEnableResult>.Fail(validation));
        }

        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<BreakpointEnableResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var request = new BreakpointEnableRequest
        {
            Name = NormalizeOptional(name),
            DocumentPath = NormalizeOptional(documentPath),
            Line = line,
            Enabled = enabled
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var result = await connection.BreakpointEnableAsync(request, ct);

                if (enabled)
                {
                    return result;
                }

                var state = await SettleDebuggerStateAsync(connection, continueExecution, settleTimeoutMilliseconds, ct);
                return result with { State = state };
            },
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_group_enable")]
    [Description("Enables or disables all breakpoints in a group through a routed Visual Studio session. When disabling, the response also includes the current debugger state (similar to debug_snapshot); pass continueExecution to resume the debugger afterward.")]
    public Task<ToolResponse<BreakpointGroupOperationResult>> BreakpointGroupEnable(
        string groupName,
        bool enabled,
        [Description("When disabling, continue debugger execution afterward if it is paused.")]
        bool continueExecution = false,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("Breakpoint group name is required."));
        }

        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var normalizedGroupName = groupName.Trim();
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var list = await connection.BreakpointListAsync(ct);
                var matches = BreakpointsInGroup(list.Breakpoints, normalizedGroupName).ToArray();
                var updated = 0;
                var updatedBreakpoints = new List<BreakpointInfo>();
                foreach (var breakpoint in matches)
                {
                    var request = new BreakpointEnableRequest
                    {
                        Name = breakpoint.Name,
                        DocumentPath = breakpoint.File,
                        Line = breakpoint.Line,
                        Enabled = enabled
                    };
                    var result = await connection.BreakpointEnableAsync(request, ct);
                    updated += result.Updated;
                    updatedBreakpoints.AddRange(result.Breakpoints);
                }

                DebuggerStateInfo? state = null;
                if (!enabled)
                {
                    state = await SettleDebuggerStateAsync(connection, continueExecution, settleTimeoutMilliseconds, ct);
                }

                return new BreakpointGroupOperationResult(normalizedGroupName, matches.Length, updated, updatedBreakpoints, state);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "breakpoint_group_remove")]
    [Description("Removes all breakpoints in a group through a routed Visual Studio session.")]
    public Task<ToolResponse<BreakpointGroupOperationResult>> BreakpointGroupRemove(
        string groupName,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return Task.FromResult(ToolResponse<BreakpointGroupOperationResult>.Fail("Breakpoint group name is required."));
        }

        var normalizedGroupName = groupName.Trim();
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var list = await connection.BreakpointListAsync(ct);
                var matches = BreakpointsInGroup(list.Breakpoints, normalizedGroupName).ToArray();
                var removed = 0;
                foreach (var breakpoint in matches)
                {
                    var request = new BreakpointRemoveRequest
                    {
                        Name = breakpoint.Name,
                        DocumentPath = breakpoint.File,
                        Line = breakpoint.Line
                    };
                    removed += (await connection.BreakpointRemoveAsync(request, ct)).Removed;
                }

                return new BreakpointGroupOperationResult(normalizedGroupName, matches.Length, removed, []);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_callstack")]
    [Description("Returns the current call stack from a routed Visual Studio session.")]
    public Task<ToolResponse<CallStackResult>> DebugGetCallstack(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetCallstackAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_get_locals")]
    [Description("Returns locals from a routed Visual Studio session.")]
    public Task<ToolResponse<LocalsResult>> DebugGetLocals(
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugGetLocalsAsync(ct),
            cancellationToken);
    }

    [McpServerTool(Name = "debug_evaluate")]
    [Description("Evaluates an expression in a routed Visual Studio session.")]
    public Task<ToolResponse<EvaluateExpressionResult>> DebugEvaluate(
        string expression,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("Expression is required."));
        }

        if (timeoutMilliseconds < 1)
        {
            return Task.FromResult(ToolResponse<EvaluateExpressionResult>.Fail("Timeout milliseconds must be greater than zero."));
        }

        var request = new EvaluateExpressionRequest
        {
            Expression = expression.Trim(),
            TimeoutMilliseconds = timeoutMilliseconds
        };

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            (connection, ct) => connection.DebugEvaluateAsync(request, ct),
            cancellationToken);
    }

    private const int DebugSnapshotPollIntervalMilliseconds = 50;

    private static async Task<DebuggerStateInfo> SettleDebuggerStateAsync(
        IVisualStudioSessionRpc connection,
        bool continueExecution,
        int settleTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var state = await connection.DebugStatusAsync(cancellationToken);

        if (!continueExecution || state.Mode != "dbgBreakMode")
        {
            return state;
        }

        state = await connection.DebugContinueAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
        {
            await Task.Delay(DebugSnapshotPollIntervalMilliseconds, cancellationToken);
            state = await connection.DebugStatusAsync(cancellationToken);
        }

        return state;
    }

    private static readonly string[] DebugSnapshotKnownIncludeKeys =
    [
        "callStack",
        "breakpoints",
        "watch",
        "threads",
        "modules",
        "parallelStacks",
        "parallelWatch"
    ];

    [McpServerTool(Name = "debug_snapshot")]
    [Description("Optionally advances the debugger (stepInto, stepOver, stepOut, continue, or break), waits for it to settle, and returns state plus locals in one call. Use 'include' to also fetch any of callStack, breakpoints, watch, threads, modules, parallelStacks, parallelWatch (defaults to callStack only when omitted; pass an empty array to fetch none of them). Locals are always fetched best-effort while paused. When 'action' is omitted this is a pure, non-mutating inspection of current state.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugSnapshot(
        DebugAdvanceAction? action = null,
        string[]? include = null,
        int settleTimeoutMilliseconds = 300,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (settleTimeoutMilliseconds < 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("settleTimeoutMilliseconds must be zero or greater."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                DebuggerStateInfo state;

                if (action is null)
                {
                    state = await connection.DebugStatusAsync(ct);
                }
                else
                {
                    state = action.Value switch
                    {
                        DebugAdvanceAction.StepInto => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Into }, ct),
                        DebugAdvanceAction.StepOver => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Over }, ct),
                        DebugAdvanceAction.StepOut => await connection.DebugStepAsync(new DebugStepRequest { StepKind = DebugStepKind.Out }, ct),
                        DebugAdvanceAction.Continue => await connection.DebugContinueAsync(ct),
                        DebugAdvanceAction.Break => await connection.DebugBreakAsync(ct),
                        _ => await connection.DebugStatusAsync(ct)
                    };

                    var stopwatch = Stopwatch.StartNew();
                    while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < settleTimeoutMilliseconds)
                    {
                        await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                        state = await connection.DebugStatusAsync(ct);
                    }
                }

                return await CollectDebugSnapshotAsync(connection, state, includeKeys, unrecognizedInclude, ct);
            },
            cancellationToken);
    }

    [McpServerTool(Name = "debug_wait_for_break")]
    [Description("Waits for a routed Visual Studio session's debugger to leave dbgRunMode - typically because a breakpoint or tracepoint fired - then returns state, locals, and the requested include categories in one call, the same shape as debug_snapshot. Does not itself advance the debugger; call debug_continue, debug_snapshot (with an action), or breakpoint_group_enable(..., continueExecution: true) first if the debuggee is not already running. Use 'include' the same way as debug_snapshot.")]
    public Task<ToolResponse<DebugSnapshotResult>> DebugWaitForBreak(
        [Description("Maximum time in seconds to wait for the debugger to leave dbgRunMode before giving up and returning the still-running state.")]
        int timeoutSeconds = 30,
        string[]? include = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (timeoutSeconds <= 0)
        {
            return Task.FromResult(ToolResponse<DebugSnapshotResult>.Fail("timeoutSeconds must be greater than zero."));
        }

        var (includeKeys, unrecognizedInclude) = ParseDebugSnapshotInclude(include);
        var timeoutMilliseconds = timeoutSeconds * 1000L;

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var state = await connection.DebugStatusAsync(ct);

                var stopwatch = Stopwatch.StartNew();
                while (state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    await Task.Delay(DebugSnapshotPollIntervalMilliseconds, ct);
                    state = await connection.DebugStatusAsync(ct);
                }

                var timedOut = state.Mode == "dbgRunMode" && stopwatch.ElapsedMilliseconds >= timeoutMilliseconds;
                return await CollectDebugSnapshotAsync(connection, state, includeKeys, unrecognizedInclude, ct, timedOut);
            },
            cancellationToken);
    }

    private static async Task<DebugSnapshotResult> CollectDebugSnapshotAsync(
        IVisualStudioSessionRpc connection,
        DebuggerStateInfo state,
        HashSet<string> includeKeys,
        IReadOnlyCollection<string>? unrecognizedInclude,
        CancellationToken cancellationToken,
        bool timedOut = false)
    {
        if (state.Mode != "dbgBreakMode")
        {
            return new DebugSnapshotResult(state, null, null, null, UnrecognizedInclude: unrecognizedInclude, TimedOut: timedOut);
        }

        var locals = await connection.DebugGetLocalsAsync(cancellationToken);
        var callStack = includeKeys.Contains("callStack") ? await connection.DebugGetCallstackAsync(cancellationToken) : null;
        var breakpoints = includeKeys.Contains("breakpoints") ? await connection.BreakpointListAsync(cancellationToken) : null;
        var watch = includeKeys.Contains("watch") ? await connection.WatchListAsync(cancellationToken) : null;
        var threads = includeKeys.Contains("threads") ? await connection.DebugGetThreadsAsync(cancellationToken) : null;
        var modules = includeKeys.Contains("modules") ? await connection.ModuleListAsync(cancellationToken) : null;
        var parallelStacks = includeKeys.Contains("parallelStacks") ? await connection.ParallelStacksAsync(cancellationToken) : null;
        var parallelWatch = includeKeys.Contains("parallelWatch") ? await connection.ParallelWatchAsync(cancellationToken) : null;

        return new DebugSnapshotResult(
            state,
            callStack,
            locals,
            breakpoints,
            watch,
            threads,
            modules,
            parallelStacks,
            parallelWatch,
            unrecognizedInclude,
            timedOut);
    }

    private static (HashSet<string> Keys, IReadOnlyCollection<string>? Unrecognized) ParseDebugSnapshotInclude(string[]? include)
    {
        if (include is null)
        {
            return (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "callStack" }, null);
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string>? unrecognized = null;

        foreach (var entry in include)
        {
            if (entry is null)
            {
                continue;
            }

            var matched = Array.Find(
                DebugSnapshotKnownIncludeKeys,
                known => string.Equals(known, entry, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
            {
                keys.Add(matched);
            }
            else
            {
                (unrecognized ??= []).Add(entry);
            }
        }

        return (keys, unrecognized is { Count: > 0 } ? unrecognized : null);
    }

    [McpServerTool(Name = "debug_eval_many")]
    [Description("Evaluates multiple debugger expressions through a routed Visual Studio session.")]
    public Task<ToolResponse<DebugEvalManyResult>> DebugEvalMany(
        string[] expressions,
        int timeoutMilliseconds = 5000,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (expressions is null || expressions.Length == 0)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("At least one expression is required."));
        }

        if (timeoutMilliseconds < 1)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("Timeout milliseconds must be greater than zero."));
        }

        var normalizedExpressions = expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Select(expression => expression.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedExpressions.Length == 0)
        {
            return Task.FromResult(ToolResponse<DebugEvalManyResult>.Fail("At least one expression is required."));
        }

        return DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            async (connection, ct) =>
            {
                var state = await connection.DebugStatusAsync(ct);
                var results = new List<EvaluateExpressionResult>();
                foreach (var expression in normalizedExpressions)
                {
                    results.Add(await connection.DebugEvaluateAsync(new EvaluateExpressionRequest
                    {
                        Expression = expression,
                        TimeoutMilliseconds = timeoutMilliseconds
                    }, ct));
                }

                return new DebugEvalManyResult(state, results);
            },
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
            cancellationToken);
    }

    private static RoutingTarget? CreateTarget(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId) &&
            string.IsNullOrWhiteSpace(solutionName) &&
            string.IsNullOrWhiteSpace(solutionPath) &&
            processId is null &&
            string.IsNullOrWhiteSpace(workspacePath) &&
            string.IsNullOrWhiteSpace(rootPath))
        {
            return null;
        }

        return new RoutingTarget(
            NormalizeOptional(sessionId),
            NormalizeOptional(solutionName),
            NormalizeOptional(solutionPath),
            processId,
            NormalizeOptional(workspacePath),
            NormalizeOptional(rootPath));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static string ResolveSearchRoot(string? rootPath, SolutionInfoResult solution)
    {
        var candidate = NormalizeOptional(rootPath);
        if (candidate is null && !string.IsNullOrWhiteSpace(solution.Path))
        {
            candidate = Path.GetDirectoryName(solution.Path);
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new DirectoryNotFoundException("A root path or routed solution path is required.");
        }

        var fullPath = Path.GetFullPath(candidate);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Root path '{fullPath}' does not exist.");
        }

        return fullPath;
    }

    private static WorkspaceSearchResult SearchWorkspace(
        string rootPath,
        string query,
        string filePattern,
        int maxMatches,
        CancellationToken cancellationToken)
    {
        var matches = new List<WorkspaceSearchMatch>();
        foreach (var file in EnumerateWorkspaceFiles(rootPath, filePattern))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsProbablyBinaryFile(file))
            {
                continue;
            }

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new WorkspaceSearchMatch(file, lineNumber, line.Trim()));
                    if (matches.Count >= maxMatches)
                    {
                        return new WorkspaceSearchResult(rootPath, matches, true);
                    }
                }
            }
        }

        return new WorkspaceSearchResult(rootPath, matches, false);
    }

    private static bool IsProbablyBinaryFile(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            Span<byte> buffer = stackalloc byte[8000];
            var read = stream.Read(buffer);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static IEnumerable<string> EnumerateWorkspaceFiles(string rootPath, string filePattern)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false
        };

        foreach (var file in Directory.EnumerateFiles(rootPath, filePattern, options))
        {
            yield return file;
        }

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", options))
        {
            var name = Path.GetFileName(directory);
            if (name is ".git" or ".vs" or "bin" or "obj" or "node_modules")
            {
                continue;
            }

            foreach (var file in EnumerateWorkspaceFiles(directory, filePattern))
            {
                yield return file;
            }
        }
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

    private static IEnumerable<BreakpointInfo> BreakpointsInGroup(
        IEnumerable<BreakpointInfo> breakpoints,
        string groupName)
    {
        return breakpoints.Where(breakpoint =>
            string.Equals(breakpoint.GroupName, groupName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(left) && Path.IsPathRooted(right))
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSnippet(string text, int centerLine, int contextLines)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        if (lines.Length == 0)
        {
            return string.Empty;
        }

        var start = Math.Max(1, centerLine - contextLines);
        var end = Math.Min(lines.Length, centerLine + contextLines);
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(start, end - start + 1)
                .Select(lineNumber => $"{lineNumber}: {lines[lineNumber - 1]}"));
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

    private static bool HasRoutingFields(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        int? processId = null,
        string? workspacePath = null,
        string? rootPath = null)
    {
        return !string.IsNullOrWhiteSpace(sessionId) ||
            !string.IsNullOrWhiteSpace(solutionName) ||
            !string.IsNullOrWhiteSpace(solutionPath) ||
            processId is not null ||
            !string.IsNullOrWhiteSpace(workspacePath) ||
            !string.IsNullOrWhiteSpace(rootPath);
    }

    private static string? ValidateBreakpointLookup(
        string? name,
        string? documentPath,
        int line)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Breakpoint name or document path is required.";
        }

        return line < 1
            ? "Breakpoint line must be greater than zero."
            : null;
    }

    private static string? ValidateRequiredPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? "Path is required."
            : null;
    }

    private static string? ValidatePosition(int line, int column)
    {
        if (line < 1)
        {
            return "Line must be greater than zero.";
        }

        return column < 1
            ? "Column must be greater than zero."
            : null;
    }

    private static string? ValidateRange(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        if (ValidatePosition(startLine, startColumn) is { } startValidation)
        {
            return startValidation;
        }

        if (ValidatePosition(endLine, endColumn) is { } endValidation)
        {
            return endValidation;
        }

        if (endLine < startLine || (endLine == startLine && endColumn < startColumn))
        {
            return "End position must be greater than or equal to start position.";
        }

        return null;
    }

    private static string? ValidateEditPreview(
        string? operation,
        string? path,
        string? text,
        int line,
        int column,
        int startLine,
        int startColumn,
        int endLine,
        int endColumn)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return "Edit operation is required.";
        }

        if (ValidateRequiredPath(path) is { } pathValidation)
        {
            return pathValidation;
        }

        if (text is null)
        {
            return "Text is required.";
        }

        return operation.Trim().ToLowerInvariant() switch
        {
            "write" => null,
            "insert" => ValidatePosition(line, column),
            "replace" => ValidateRange(startLine, startColumn, endLine, endColumn),
            _ => "Edit operation must be one of: write, insert, replace."
        };
    }

    private static string? ValidateEditId(string? editId)
    {
        return string.IsNullOrWhiteSpace(editId)
            ? "Edit id is required."
            : null;
    }

    private static string? ValidateProjectName(string? projectName)
    {
        return string.IsNullOrWhiteSpace(projectName)
            ? "Project name is required."
            : null;
    }

    private static string? ValidateCodePosition(
        string? documentPath,
        int line,
        int column)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return "Document path is required.";
        }

        return ValidatePosition(line, column);
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

    private static ToolResponse<T> ToToolResponse<T>(
        VsSessionDispatchResult<ToolResponse<T>> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        return dispatch.Value ?? ToolResponse<T>.Fail("Visual Studio session returned no response.");
    }

    private static ToolResponse<T> FailWithCode<T>(string message, string errorCode) =>
        new(false, default, message, new Dictionary<string, string>
        {
            ["error_code"] = errorCode
        });

    private static ToolResponse<T> ToValueToolResponse<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        if (!dispatch.Success)
        {
            return new ToolResponse<T>(
                false,
                default,
                dispatch.Message,
                CreateFailureMetadata(dispatch));
        }

        if (dispatch.Value is null)
        {
            return ToolResponse<T>.Fail("Visual Studio session returned no response.");
        }

        return ToolResponse<T>.Ok(dispatch.Value);
    }

    private async Task<ToolResponse<T>> DispatchValueAsync<T>(
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        Func<IVisualStudioSessionRpc, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        [CallerMemberName] string toolName = "")
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        var dispatch = await _runtime.Dispatcher.DispatchAsync(
            target,
            operation,
            cancellationToken);

        var response = ToValueToolResponse(dispatch);
        AuditToolResult(toolName, target, response.Success, dispatch.Session?.SessionId, response.Message, dispatch.FailureReason.ToString());
        return response;
    }

    private void AuditToolResult(
        string toolName,
        RoutingTarget? target,
        bool success,
        string? selectedSessionId,
        string? message,
        string? failureReason = null)
    {
        try
        {
            _runtime.AuditLog.RecordToolCall(new AuditToolCall(
                TimestampUtc: DateTimeOffset.UtcNow,
                ToolName: ToMcpToolName(toolName),
                Success: success,
                SessionId: selectedSessionId ?? target?.SessionId,
                SolutionName: target?.SolutionName,
                SolutionPath: target?.SolutionPath,
                FailureReason: success ? null : NormalizeFailureReason(failureReason),
                Message: TruncateAuditMessage(message)));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp audit logging failed: {ex}");
        }
    }

    private static string? NormalizeFailureReason(string? failureReason)
    {
        return string.IsNullOrWhiteSpace(failureReason) || failureReason == "None"
            ? null
            : failureReason;
    }

    private static string? TruncateAuditMessage(string? message)
    {
        const int maxLength = 500;
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message[..maxLength];
    }

    private static string ToMcpToolName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
        {
            return "unknown";
        }

        var chars = new List<char>(methodName.Length + 8);
        for (var index = 0; index < methodName.Length; index++)
        {
            var character = methodName[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string([.. chars]);
    }

    private static BrokerToolDescriptor WithCategoryMetadata(BrokerToolDescriptor descriptor)
    {
        return descriptor with
        {
            Category = CategorizeTool(descriptor.Name)
        };
    }

    private static BrokerToolCategory CategorizeTool(string toolName)
    {
        if (toolName is "vs_launch_instance")
        {
            return BrokerToolCategory.Admin;
        }

        if (toolName.StartsWith("vs_", StringComparison.Ordinal) &&
            toolName is not "vs_context_snapshot")
        {
            return BrokerToolCategory.Broker;
        }

        if (toolName.StartsWith("ui_", StringComparison.Ordinal) ||
            toolName.StartsWith("web_", StringComparison.Ordinal))
        {
            return BrokerToolCategory.Admin;
        }

        if (toolName.StartsWith("console_", StringComparison.Ordinal) ||
            toolName.StartsWith("thread_", StringComparison.Ordinal) ||
            toolName.StartsWith("parallel_", StringComparison.Ordinal) ||
            toolName.StartsWith("register_", StringComparison.Ordinal) ||
            toolName is "debug_attach" or
                "debug_restart" or
                "debug_set_variable" or
                "debug_start_without_debugging" or
                "exception_settings_set" or
                "immediate_execute" or
                "process_detach" or
                "process_list_local" or
                "process_terminate")
        {
            return toolName is "process_terminate" or "immediate_execute"
                ? BrokerToolCategory.Admin
                : BrokerToolCategory.Debug;
        }

        if (toolName is "build_cancel" or
            "build_configuration_set" or
            "build_project" or
            "clean_solution" or
            "rebuild_solution")
        {
            return BrokerToolCategory.Build;
        }

        if (toolName is "project_remove_file" or
            "project_add_reference" or
            "project_remove_reference" or
            "nuget_install" or
            "nuget_update" or
            "nuget_uninstall" or
            "output_write" or
            "output_clear")
        {
            return BrokerToolCategory.Admin;
        }

        if (toolName is "document_close")
        {
            return BrokerToolCategory.EditDirect;
        }

        return toolName switch
        {
            "document_active" or
            "document_list" or
            "editor_find" or
            "find_in_files" or
            "code_go_to_implementation" or
            "code_workspace_symbols" or
            "diagnostics_binding_errors" or
            "build_configuration_get" or
            "output_list_panes" or
            "nuget_list" or
            "nuget_search" or
            "get_status" or
            "get_help" or
            "window_list" or
            "window_activate" or
            "toolwindow_show" or
            "toolwindow_hide" or
            "document_read" or
            "document_open" or
            "selection_get" or
            "code_document_symbols" or
            "code_go_to_definition" or
            "code_find_references" or
            "symbol_context" or
            "document_outline" or
            "find_implementations" or
            "rename_symbol_preview" or
            "diagnostics_for_document" or
            "workspace_search" or
            "git_context" or
            "open_relevant_files" or
            "solution_info" or
            "project_list" or
            "project_info" or
            "solution_overview" or
            "project_dependencies" or
            "package_restore" or
            "startup_project_get" or
            "build_status" or
            "errors_list" or
            "task_list_get" or
            "output_read" or
            "debug_status" or
            "debug_get_mode" or
            "debug_get_callstack" or
            "debug_get_locals" or
            "debug_get_threads" or
            "debug_snapshot" or
            "debug_wait_for_break" or
            "exception_settings_get" or
            "module_list" or
            "process_list_debugged" or
            "watch_list" or
            "breakpoint_list" or
            "breakpoint_group_list" or
            "edit_list_pending" => BrokerToolCategory.Read,

            "edit_preview" or
            "prepare_safe_edit" or
            "edit_reject" => BrokerToolCategory.EditPreview,

            "document_write" or
            "document_save" or
            "editor_insert" or
            "editor_replace" or
            "editor_goto_line" or
            "selection_set" or
            "document_cleanup" or
            "format_and_organize" or
            "edit_approve" or
            "task_list_add" or
            "task_list_remove" or
            "task_list_set_checked" => BrokerToolCategory.EditDirect,

            "build_solution" or
            "build_and_get_errors" or
            "apply_safe_edit_and_build" => BrokerToolCategory.Build,

            "debug_start" or
            "debug_stop" or
            "debug_continue" or
            "debug_break" or
            "debug_step" or
            "debug_evaluate" or
            "debug_eval_many" or
            "watch_add" or
            "watch_remove" or
            "breakpoint_set" or
            "breakpoint_remove" or
            "breakpoint_enable" or
            "breakpoint_group_enable" or
            "breakpoint_group_remove" => BrokerToolCategory.Debug,

            "startup_project_set" or
            "solution_open" or
            "solution_close" or
            "solution_add_project" or
            "solution_remove_project" or
            "project_add_file" => BrokerToolCategory.Admin,

            "execute_command" => BrokerToolCategory.Admin,

            "test_discover" or
            "test_run" or
            "test_results" or
            "test_run_and_get_results" => BrokerToolCategory.Test,

            _ => BrokerToolCategory.Admin
        };
    }

    private static IReadOnlyDictionary<string, string> CreateRouteFailureMetadata(RouteResult route)
    {
        var metadata = new Dictionary<string, string>
        {
            ["error_code"] = ToolErrorCodes.SessionRoutingFailed,
            ["failureReason"] = route.FailureReason.ToString()
        };

        AddCandidateMetadata(metadata, route.Candidates);
        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateFailureMetadata<T>(
        VsSessionDispatchResult<T> dispatch)
    {
        var metadata = new Dictionary<string, string>
        {
            ["error_code"] = MapDispatchFailureToErrorCode(dispatch.FailureReason),
            ["failureReason"] = dispatch.FailureReason.ToString()
        };

        if (dispatch.Session is not null)
        {
            metadata["sessionId"] = dispatch.Session.SessionId;
        }

        if (dispatch.Candidates.Count > 0)
        {
            AddCandidateMetadata(metadata, dispatch.Candidates);
        }

        return metadata;
    }

    private static string MapDispatchFailureToErrorCode(VsSessionDispatchFailureReason reason)
    {
        return reason switch
        {
            VsSessionDispatchFailureReason.StaleSession or
            VsSessionDispatchFailureReason.MissingConnection => ToolErrorCodes.SessionNotConnected,
            VsSessionDispatchFailureReason.RpcFailure => ToolErrorCodes.RpcFailure,
            _ => ToolErrorCodes.SessionRoutingFailed
        };
    }

    private static void AddCandidateMetadata(
        IDictionary<string, string> metadata,
        IReadOnlyCollection<VsSessionInfo> candidates)
    {
        if (candidates.Count == 0)
        {
            return;
        }

        metadata["candidateCount"] = candidates.Count.ToString();
        metadata["candidateSessionIds"] = string.Join(",", candidates.Select(candidate => candidate.SessionId));
        metadata["candidateProcessIds"] = string.Join(",", candidates.Select(candidate => candidate.ProcessId));
        metadata["candidateSolutionNames"] = string.Join(",", candidates.Select(candidate => candidate.SolutionName ?? string.Empty));
        metadata["candidateSolutionPaths"] = string.Join("|", candidates.Select(candidate => candidate.SolutionPath ?? string.Empty));
        metadata["candidateActiveWindow"] = string.Join(",", candidates.Select(candidate => candidate.IsActiveWindow.ToString()));
        metadata["candidateLastSeenUtc"] = string.Join(",", candidates.Select(candidate => candidate.LastSeenUtc.ToString("O")));
    }
}

public sealed record BrokerPing(
    DateTimeOffset ServerTimeUtc,
    bool IsRunning,
    string McpEndpoint,
    string PipeName,
    TimeSpan Uptime,
    int RegisteredSessionCount,
    VsSessionStatus? TargetSession);
