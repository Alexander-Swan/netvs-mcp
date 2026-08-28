using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
        new("netvs_doctor", "Diagnoses local broker, endpoint, registration pipe, and Visual Studio session health.", false),
        new("vs_get_session", "Resolves a Visual Studio session and returns its current broker status.", false),
        new("vs_select_session", "Resolves a Visual Studio session using broker routing rules without persisting selection.", false),
        new("vs_ping", "Returns lightweight broker health and optional routed Visual Studio session status.", false),
        new("vs_launch_instance", "Launches a new Visual Studio (devenv.exe) process and waits for it to register with the broker.", false),
        new("vs_context_snapshot", "Returns a compact routed Visual Studio context snapshot.", true),
        new("execute_command", "Executes a Visual Studio command in a routed session.", true),
        new("get_status", "Returns Visual Studio session status through a routed session.", true),
        new("get_help", "Lists NetVsMcp broker tools, categories, and endpoint metadata.", false),
        new("netvs_get_best_practices", "Lists or reads bundled agent-neutral NetVsMcp best-practices guides for Visual Studio MCP workflows.", false),
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
        new("rename_symbol_apply", "Applies a Roslyn solution-wide rename at a code position. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
        new("call_hierarchy_get", "Returns the call hierarchy (incoming callers and/or outgoing callees) for a code position.", true),
        new("code_actions_list", "Lists available code fixes and refactorings at a code position or selection.", true),
        new("code_actions_apply", "Applies a code fix or refactoring by index. For documentPath, prefer forward slashes like src/Project/File.cs.", true),
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
        new("debug_hot_reload_apply", "Applies pending code changes via Hot Reload to the running debuggee.", true),
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
        new("test_debug", "Runs one filtered test under the Visual Studio debugger and attaches to the test host.", true),
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
    private static string? GetRoutableWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.IsPathRooted(path.Trim()) ? path.Trim() : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
    private static string? GetInferredWorkspacePath(
        string? path,
        string? sessionId,
        string? solutionName,
        string? solutionPath)
    {
        return HasRoutingFields(sessionId, solutionName, solutionPath)
            ? null
            : GetRoutableWorkspacePath(path);
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
        string? workspacePath = null,
        string? rootPath = null,
        [CallerMemberName] string toolName = "")
    {
        var target = CreateTarget(
            sessionId,
            solutionName,
            solutionPath,
            workspacePath: GetInferredWorkspacePath(workspacePath, sessionId, solutionName, solutionPath),
            rootPath: rootPath);
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
        string? failureReason = null,
        BrokerLogLevel? level = null)
    {
        try
        {
            var effectiveLevel = level ?? (success ? BrokerLogLevel.Info : BrokerLogLevel.Error);
            if (effectiveLevel < _runtime.MinimumLogLevel)
            {
                return;
            }

            _runtime.AuditLog.RecordToolCall(new AuditToolCall(
                TimestampUtc: DateTimeOffset.UtcNow,
                ToolName: ToMcpToolName(toolName),
                Success: success,
                SessionId: selectedSessionId ?? target?.SessionId,
                SolutionName: target?.SolutionName,
                SolutionPath: target?.SolutionPath,
                FailureReason: success ? null : NormalizeFailureReason(failureReason),
                Message: TruncateAuditMessage(message),
                Level: effectiveLevel));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"NetVsMcp audit logging failed: {ex}");
        }
    }

    private ToolResponse<T> AuditLocalFailure<T>(
        string toolName,
        string? sessionId,
        string? solutionName,
        string? solutionPath,
        string message)
    {
        var target = CreateTarget(sessionId, solutionName, solutionPath);
        AuditToolResult(
            toolName,
            target,
            success: false,
            selectedSessionId: null,
            message,
            failureReason: "InvalidRequest",
            level: BrokerLogLevel.Warning);
        return ToolResponse<T>.Fail(message);
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
            Category = CategorizeTool(descriptor.Name),
            McpEndpointPath = McpEndpointRouting.ResolveEndpointPath(descriptor.Name)
        };
    }

    private static BrokerToolCategory CategorizeTool(string toolName)
    {
        if (toolName is "vs_launch_instance")
        {
            return BrokerToolCategory.Admin;
        }

        if ((toolName.StartsWith("vs_", StringComparison.Ordinal) ||
             toolName.StartsWith("netvs_", StringComparison.Ordinal)) &&
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
            "call_hierarchy_get" or
            "code_actions_list" or
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
            "task_list_set_checked" or
            "rename_symbol_apply" or
            "code_actions_apply" => BrokerToolCategory.EditDirect,

            "build_solution" or
            "build_and_get_errors" or
            "apply_safe_edit_and_build" => BrokerToolCategory.Build,

            "debug_start" or
            "debug_stop" or
            "debug_continue" or
            "debug_break" or
            "debug_step" or
            "debug_hot_reload_apply" or
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
            "test_debug" or
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
            VsSessionDispatchFailureReason.UnsupportedByVsix => ToolErrorCodes.UnsupportedByVsix,
            VsSessionDispatchFailureReason.OperationTimedOut => ToolErrorCodes.OperationTimedOut,
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
