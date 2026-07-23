using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class DebuggerRpcTarget
{
    private readonly IDebuggerCapabilityService debugger;

    public DebuggerRpcTarget(IDebuggerCapabilityService debugger)
    {
        this.debugger = debugger;
    }

    public Task<DebuggerStateInfo> DebugStartAsync(CancellationToken cancellationToken)
    {
        return debugger.StartAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugStartWithoutDebuggingAsync(CancellationToken cancellationToken)
    {
        return debugger.StartWithoutDebuggingAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugRestartAsync(CancellationToken cancellationToken)
    {
        return debugger.RestartAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugStopAsync(CancellationToken cancellationToken)
    {
        return debugger.StopAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugContinueAsync(CancellationToken cancellationToken)
    {
        return debugger.ContinueAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugBreakAsync(CancellationToken cancellationToken)
    {
        return debugger.BreakAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugStepAsync(DebugStepRequest request, CancellationToken cancellationToken)
    {
        return debugger.StepAsync(request.StepKind, cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugStatusAsync(CancellationToken cancellationToken)
    {
        return debugger.GetStatusAsync(cancellationToken);
    }

    public Task<DebuggerStateInfo> DebugGetModeAsync(CancellationToken cancellationToken)
    {
        return debugger.GetStatusAsync(cancellationToken);
    }

    public Task<BreakpointInfo> BreakpointSetAsync(BreakpointSetRequest request, CancellationToken cancellationToken)
    {
        return debugger.SetBreakpointAsync(request, cancellationToken);
    }

    public Task<BreakpointListResult> BreakpointListAsync(CancellationToken cancellationToken)
    {
        return debugger.ListBreakpointsAsync(cancellationToken);
    }

    public Task<BreakpointRemoveResult> BreakpointRemoveAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken)
    {
        return debugger.RemoveBreakpointAsync(request, cancellationToken);
    }

    public Task<BreakpointEnableResult> BreakpointEnableAsync(BreakpointEnableRequest request, CancellationToken cancellationToken)
    {
        return debugger.SetBreakpointEnabledAsync(request, cancellationToken);
    }

    public Task<CallStackResult> DebugGetCallstackAsync(CancellationToken cancellationToken)
    {
        return debugger.GetCallStackAsync(cancellationToken);
    }

    public Task<LocalsResult> DebugGetLocalsAsync(CancellationToken cancellationToken)
    {
        return debugger.GetLocalsAsync(cancellationToken);
    }

    public Task<EvaluateExpressionResult> DebugEvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken)
    {
        return debugger.EvaluateAsync(request, cancellationToken);
    }

    public Task<DebugSetVariableResult> DebugSetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken)
    {
        return debugger.SetVariableAsync(request, cancellationToken);
    }

    public Task<WatchOperationResult> WatchAddAsync(WatchAddRequest request, CancellationToken cancellationToken)
    {
        return debugger.AddWatchAsync(request, cancellationToken);
    }

    public Task<WatchOperationResult> WatchRemoveAsync(WatchRemoveRequest request, CancellationToken cancellationToken)
    {
        return debugger.RemoveWatchAsync(request, cancellationToken);
    }

    public Task<WatchListResult> WatchListAsync(CancellationToken cancellationToken)
    {
        return debugger.ListWatchesAsync(cancellationToken);
    }

    public Task<DebugThreadListResult> DebugGetThreadsAsync(CancellationToken cancellationToken)
    {
        return debugger.GetThreadsAsync(cancellationToken);
    }

    public Task<DebuggedProcessListResult> ProcessListDebuggedAsync(CancellationToken cancellationToken)
    {
        return debugger.ListDebuggedProcessesAsync(cancellationToken);
    }

    public Task<LocalProcessListResult> ProcessListLocalAsync(CancellationToken cancellationToken)
    {
        return debugger.ListLocalProcessesAsync(cancellationToken);
    }

    public Task<DebugAttachResult> DebugAttachAsync(DebugAttachRequest request, CancellationToken cancellationToken)
    {
        return debugger.AttachAsync(request, cancellationToken);
    }

    public Task<ProcessDetachResult> ProcessDetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken)
    {
        return debugger.DetachAsync(request, cancellationToken);
    }

    public Task<ProcessTerminateResult> ProcessTerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken)
    {
        return debugger.TerminateAsync(request, cancellationToken);
    }

    public Task<ThreadSwitchResult> ThreadSwitchAsync(ThreadSwitchRequest request, CancellationToken cancellationToken)
    {
        return debugger.SwitchThreadAsync(request, cancellationToken);
    }

    public Task<ThreadSetFrozenResult> ThreadSetFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken)
    {
        return debugger.SetThreadFrozenAsync(request, cancellationToken);
    }

    public Task<ThreadCallStackResult> ThreadGetCallstackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken)
    {
        return debugger.GetThreadCallStackAsync(request, cancellationToken);
    }

    public Task<ModuleListResult> ModuleListAsync(CancellationToken cancellationToken)
    {
        return debugger.ListModulesAsync(cancellationToken);
    }

    public Task<ImmediateExecuteResult> ImmediateExecuteAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken)
    {
        return debugger.ExecuteImmediateAsync(request, cancellationToken);
    }

    public Task<ExceptionSettingsResult> ExceptionSettingsGetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        return debugger.GetExceptionSettingsAsync(request, cancellationToken);
    }

    public Task<ExceptionSettingsResult> ExceptionSettingsSetAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        return debugger.SetExceptionSettingsAsync(request, cancellationToken);
    }

    public Task<MemoryReadResult> MemoryReadAsync(MemoryReadRequest request, CancellationToken cancellationToken)
    {
        return debugger.ReadMemoryAsync(request, cancellationToken);
    }

    public Task<RegisterListResult> RegisterListAsync(CancellationToken cancellationToken)
    {
        return debugger.ListRegistersAsync(cancellationToken);
    }

    public Task<RegisterGetResult> RegisterGetAsync(RegisterGetRequest request, CancellationToken cancellationToken)
    {
        return debugger.GetRegisterAsync(request, cancellationToken);
    }

    public Task<ParallelStacksResult> ParallelStacksAsync(CancellationToken cancellationToken)
    {
        return debugger.GetParallelStacksAsync(cancellationToken);
    }

    public Task<ParallelWatchResult> ParallelWatchAsync(CancellationToken cancellationToken)
    {
        return debugger.GetParallelWatchAsync(cancellationToken);
    }

    public Task<ParallelTasksResult> ParallelTasksListAsync(CancellationToken cancellationToken)
    {
        return debugger.ListParallelTasksAsync(cancellationToken);
    }
}
