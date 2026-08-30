using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IDebuggerCapabilityService
{
    Task<DebuggerStateInfo> StartAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> StartWithoutDebuggingAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> RestartAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> StopAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> ContinueAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> BreakAsync(CancellationToken cancellationToken);
    Task<DebuggerStateInfo> StepAsync(DebugStepKind stepKind, CancellationToken cancellationToken);
    Task<DebuggerStateInfo> GetStatusAsync(CancellationToken cancellationToken);
    Task<HotReloadApplyResult> ApplyHotReloadAsync(CancellationToken cancellationToken);
    Task<BreakpointInfo> SetBreakpointAsync(BreakpointSetRequest request, CancellationToken cancellationToken);
    Task<BreakpointListResult> ListBreakpointsAsync(CancellationToken cancellationToken);
    Task<BreakpointRemoveResult> RemoveBreakpointAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken);
    Task<BreakpointEnableResult> SetBreakpointEnabledAsync(BreakpointEnableRequest request, CancellationToken cancellationToken);
    Task<CallStackResult> GetCallStackAsync(CancellationToken cancellationToken);
    Task<LocalsResult> GetLocalsAsync(CancellationToken cancellationToken);
    Task<EvaluateExpressionResult> EvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken);
    Task<DebugSetVariableResult> SetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken);
    Task<WatchOperationResult> AddWatchAsync(WatchAddRequest request, CancellationToken cancellationToken);
    Task<WatchOperationResult> RemoveWatchAsync(WatchRemoveRequest request, CancellationToken cancellationToken);
    Task<WatchListResult> ListWatchesAsync(CancellationToken cancellationToken);
    Task<DebugThreadListResult> GetThreadsAsync(CancellationToken cancellationToken);
    Task<DebuggedProcessListResult> ListDebuggedProcessesAsync(CancellationToken cancellationToken);
    Task<LocalProcessListResult> ListLocalProcessesAsync(CancellationToken cancellationToken);
    Task<DebugAttachResult> AttachAsync(DebugAttachRequest request, CancellationToken cancellationToken);
    Task<ProcessDetachResult> DetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken);
    Task<ProcessTerminateResult> TerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken);
    Task<ThreadSwitchResult> SwitchThreadAsync(ThreadSwitchRequest request, CancellationToken cancellationToken);
    Task<ThreadSetFrozenResult> SetThreadFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken);
    Task<ThreadCallStackResult> GetThreadCallStackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken);
    Task<ModuleListResult> ListModulesAsync(CancellationToken cancellationToken);
    Task<ImmediateExecuteResult> ExecuteImmediateAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken);
    Task<ExceptionSettingsResult> GetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken);
    Task<ExceptionSettingsResult> SetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken);
    Task<ParallelStacksResult> GetParallelStacksAsync(CancellationToken cancellationToken);
    Task<ParallelWatchResult> GetParallelWatchAsync(CancellationToken cancellationToken);
}
