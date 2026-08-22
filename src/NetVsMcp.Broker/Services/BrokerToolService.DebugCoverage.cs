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
    [Description("Attaches the Visual Studio debugger to a local process by id or name, or to a process on a remote debugger transport (SSH/WSL/Docker/etc.) when transport is set.")]
    public Task<ToolResponse<DebugAttachResult>> DebugAttach(
        int? processId = null,
        string? processName = null,
        string? transport = null,
        string? transportQualifier = null,
        string? engine = null,
        string? sessionId = null,
        string? solutionName = null,
        string? solutionPath = null,
        CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<DebugAttachResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new DebugAttachRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName),
            Transport = NormalizeOptional(transport),
            TransportQualifier = NormalizeOptional(transportQualifier),
            Engine = NormalizeOptional(engine)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.DebugAttachAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "debug_get_threads")]
    [Description("Lists debugger threads for the current debug program.")]
    public Task<ToolResponse<DebugThreadListResult>> DebugGetThreads(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.DebugGetThreadsAsync(ct), cancellationToken);

    [McpServerTool(Name = "debug_set_variable")]
    [Description("Sets a debugger variable by evaluating an assignment expression.")]
    public Task<ToolResponse<DebugSetVariableResult>> DebugSetVariable(string name, string value, int timeoutMilliseconds = 5000, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Variable name is required.", ToolErrorCodes.InvalidRequest));
        }

        if (value is null)
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Value is required.", ToolErrorCodes.InvalidRequest));
        }

        if (timeoutMilliseconds <= 0)
        {
            return Task.FromResult(FailWithCode<DebugSetVariableResult>("Timeout must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new DebugSetVariableRequest { Name = name, Value = value, TimeoutMilliseconds = timeoutMilliseconds };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.DebugSetVariableAsync(request, ct), cancellationToken);
    }

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
    [Description("Freezes or thaws a debugger thread when supported by the active debug engine.")]
    public Task<ToolResponse<ThreadSetFrozenResult>> ThreadSetFrozen(int threadId, bool frozen, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadSetFrozenResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadSetFrozenRequest { ThreadId = threadId, Frozen = frozen };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadSetFrozenAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "thread_get_callstack")]
    [Description("Returns the call stack for a debugger thread when supported by the active debug engine.")]
    public Task<ToolResponse<ThreadCallStackResult>> ThreadGetCallstack(int threadId, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (threadId <= 0)
        {
            return Task.FromResult(FailWithCode<ThreadCallStackResult>("Thread id must be greater than zero.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ThreadCallStackRequest { ThreadId = threadId };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ThreadGetCallstackAsync(request, ct), cancellationToken);
    }

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
    [Description("Lists local processes visible to Visual Studio for debugger attach workflows.")]
    public Task<ToolResponse<LocalProcessListResult>> ProcessListLocal(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(
            sessionId,
            solutionName,
            solutionPath,
            static (connection, ct) => connection.ProcessListLocalAsync(ct),
            cancellationToken);

    [McpServerTool(Name = "process_detach")]
    [Description("Detaches the Visual Studio debugger from a debugged process by id or name.")]
    public Task<ToolResponse<ProcessDetachResult>> ProcessDetach(int? processId = null, string? processName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<ProcessDetachResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ProcessDetachRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ProcessDetachAsync(request, ct), cancellationToken);
    }

    [McpServerTool(Name = "process_terminate")]
    [Description("Terminates a debugged process by id or name when supported by the active debug engine.")]
    public Task<ToolResponse<ProcessTerminateResult>> ProcessTerminate(int? processId = null, string? processName = null, string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default)
    {
        if (processId is null && string.IsNullOrWhiteSpace(processName))
        {
            return Task.FromResult(FailWithCode<ProcessTerminateResult>("Process id or process name is required.", ToolErrorCodes.InvalidRequest));
        }

        var request = new ProcessTerminateRequest
        {
            ProcessId = processId,
            ProcessName = NormalizeOptional(processName)
        };
        return DispatchValueAsync(sessionId, solutionName, solutionPath, (connection, ct) => connection.ProcessTerminateAsync(request, ct), cancellationToken);
    }

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

    [McpServerTool(Name = "parallel_stacks")]
    [Description("Returns parallel stack information when the active Visual Studio debug engine exposes it.")]
    public Task<ToolResponse<ParallelStacksResult>> ParallelStacks(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelStacksAsync(ct), cancellationToken);

    [McpServerTool(Name = "parallel_watch")]
    [Description("Returns parallel watch expressions when the active Visual Studio debug engine exposes them.")]
    public Task<ToolResponse<ParallelWatchResult>> ParallelWatch(string? sessionId = null, string? solutionName = null, string? solutionPath = null, CancellationToken cancellationToken = default) =>
        DispatchValueAsync(sessionId, solutionName, solutionPath, static (connection, ct) => connection.ParallelWatchAsync(ct), cancellationToken);
}
