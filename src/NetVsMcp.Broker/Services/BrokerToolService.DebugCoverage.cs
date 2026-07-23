using NetVsMcp.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace NetVsMcp.Broker.Services;

public sealed partial class BrokerToolService
{
    [McpServerTool(Name = "debug_start_without_debugging")]
    [Description("Starts the current startup project without debugging.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugStartWithoutDebugging(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugStartWithoutDebuggingAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "debug_restart")]
    [Description("Restarts the active debug session.")]
    public Task<ToolResponse<DebuggerStateInfo>> DebugRestart(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.DebugRestartAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "debug_attach")]
    [Description("Planned: attaches the debugger to a process.")]
    public Task<ToolResponse<UnsupportedToolResult>> DebugAttach(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Debugger", "Implement attach by process id/name with ambiguity responses.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "debug_get_threads")]
    [Description("Lists debugger threads for the current debug program.")]
    public Task<ToolResponse<DebugThreadListResult>> DebugGetThreads(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.DebugGetThreadsAsync(ct), cancellationToken);

    [McpServerTool(Name = "debug_set_variable")]
    [Description("Planned: sets a debugger variable.")]
    public Task<ToolResponse<UnsupportedToolResult>> DebugSetVariable(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Debugger", "Implement debugger expression assignment with engine acceptance reporting.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "watch_add")]
    [Description("Adds a debugger watch expression when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchOperationResult>> WatchAdd(string expression, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchAddRequest { Expression = expression };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.WatchAddAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "watch_remove")]
    [Description("Removes a debugger watch expression when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchOperationResult>> WatchRemove(string expression, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(FailWithCode<WatchOperationResult>("Watch expression is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new WatchRemoveRequest { Expression = expression };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.WatchRemoveAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "watch_list")]
    [Description("Lists debugger watch expressions when supported by the VSIX debugger service.")]
    public Task<ToolResponse<WatchListResult>> WatchList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.WatchListAsync(ct), cancellationToken);

    [McpServerTool(Name = "thread_switch")]
    [Description("Switches the active debugger thread.")]
    public Task<ToolResponse<ThreadSwitchResult>> ThreadSwitch(int threadId, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadSwitchResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadSwitchRequest { ThreadId = threadId };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadSwitchAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "thread_set_frozen")]
    [Description("Planned: freezes or thaws a debugger thread.")]
    public Task<ToolResponse<UnsupportedToolResult>> ThreadSetFrozen(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement thread freeze/thaw support.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "thread_get_callstack")]
    [Description("Planned: returns call stack for a debugger thread.")]
    public Task<ToolResponse<UnsupportedToolResult>> ThreadGetCallstack(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement per-thread call stack retrieval.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "process_list_debugged")]
    [Description("Lists processes currently being debugged by Visual Studio.")]
    public Task<ToolResponse<DebuggedProcessListResult>> ProcessListDebugged(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListDebuggedAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "process_list_local")]
    [Description("Planned: lists local processes for attach workflows.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProcessListLocal(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement local process listing with filters.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "process_detach")]
    [Description("Planned: detaches from a debugged process.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProcessDetach(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement debugger process detach.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "process_terminate")]
    [Description("Planned: terminates a process.")]
    public Task<ToolResponse<UnsupportedToolResult>> ProcessTerminate(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement admin-gated process termination.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "immediate_execute")]
    [Description("Executes text in the immediate window when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ImmediateExecuteResult>> ImmediateExecute(string statement, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return Task.FromResult(FailWithCode<ImmediateExecuteResult>("Statement is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ImmediateExecuteRequest { Statement = statement };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ImmediateExecuteAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "module_list")]
    [Description("Lists debugger modules when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ModuleListResult>> ModuleList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ModuleListAsync(ct), cancellationToken);

    [McpServerTool(Name = "exception_settings_get")]
    [Description("Returns debugger exception settings when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsGet(string? exceptionName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsGetAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "exception_settings_set")]
    [Description("Sets debugger exception settings when supported by the VSIX debugger service.")]
    public Task<ToolResponse<ExceptionSettingsResult>> ExceptionSettingsSet(string exceptionName, bool breakOnThrown, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exceptionName))
        {
            return Task.FromResult(FailWithCode<ExceptionSettingsResult>("Exception name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ExceptionSettingsRequest { ExceptionName = exceptionName, BreakOnThrown = breakOnThrown };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ExceptionSettingsSetAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "memory_read")]
    [Description("Planned: reads debugger memory.")]
    public Task<ToolResponse<UnsupportedToolResult>> MemoryRead(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement bounded native/mixed-mode memory reads.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "register_list")]
    [Description("Planned: lists debugger registers.")]
    public Task<ToolResponse<UnsupportedToolResult>> RegisterList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement register enumeration.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "register_get")]
    [Description("Planned: returns one debugger register.")]
    public Task<ToolResponse<UnsupportedToolResult>> RegisterGet(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement one-register lookup.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "parallel_stacks")]
    [Description("Planned: returns parallel stacks data.")]
    public Task<ToolResponse<UnsupportedToolResult>> ParallelStacks(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement parallel stacks extraction.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "parallel_watch")]
    [Description("Planned: returns parallel watch data.")]
    public Task<ToolResponse<UnsupportedToolResult>> ParallelWatch(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement parallel watch extraction.", sessionId, solutionName, solutionPath, cancellationToken);

    [McpServerTool(Name = "parallel_tasks_list")]
    [Description("Planned: lists parallel tasks.")]
    public Task<ToolResponse<UnsupportedToolResult>> ParallelTasksList(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) => PlannedTool("Advanced Debug", "Implement parallel task enumeration.", sessionId, solutionName, solutionPath, cancellationToken);
}
