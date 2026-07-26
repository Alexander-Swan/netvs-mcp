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
    Task<MemoryReadResult> ReadMemoryAsync(MemoryReadRequest request, CancellationToken cancellationToken);
    Task<ParallelStacksResult> GetParallelStacksAsync(CancellationToken cancellationToken);
    Task<ParallelWatchResult> GetParallelWatchAsync(CancellationToken cancellationToken);
    Task<ParallelTasksResult> ListParallelTasksAsync(CancellationToken cancellationToken);
}

internal enum DebugStepKind
{
    Into,
    Over,
    Out
}

internal sealed class DebuggerCapabilityService : IDebuggerCapabilityService
{
    private readonly AsyncPackage package;
    private readonly List<string> watchExpressions = new();

    public DebuggerCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public async Task<DebuggerStateInfo> StartAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        debugger.Go(WaitForBreakOrEnd: false);
        return GetDebuggerState(debugger);
    }

    public async Task<DebuggerStateInfo> StartWithoutDebuggingAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        dte.ExecuteCommand("Debug.StartWithoutDebugging");
        return GetDebuggerState(dte.Debugger);
    }

    public async Task<DebuggerStateInfo> RestartAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        dte.ExecuteCommand("Debug.Restart");
        return GetDebuggerState(dte.Debugger);
    }

    public async Task<DebuggerStateInfo> StopAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        debugger.Stop(WaitForDesignMode: false);
        return GetDebuggerState(debugger);
    }

    public async Task<DebuggerStateInfo> ContinueAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        debugger.Go(WaitForBreakOrEnd: false);
        return GetDebuggerState(debugger);
    }

    public async Task<DebuggerStateInfo> BreakAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        debugger.Break(WaitForBreakMode: false);
        return GetDebuggerState(debugger);
    }

    public async Task<DebuggerStateInfo> StepAsync(DebugStepKind stepKind, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();

        switch (stepKind)
        {
            case DebugStepKind.Into:
                debugger.StepInto(WaitForBreakOrEnd: false);
                break;
            case DebugStepKind.Over:
                debugger.StepOver(WaitForBreakOrEnd: false);
                break;
            case DebugStepKind.Out:
                debugger.StepOut(WaitForBreakOrEnd: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stepKind), stepKind, "Unknown debugger step kind.");
        }

        return GetDebuggerState(debugger);
    }

    public async Task<DebuggerStateInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        return GetDebuggerState(await GetDebuggerAsync());
    }

    public async Task<BreakpointInfo> SetBreakpointAsync(BreakpointSetRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (request.Line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Line), "Breakpoint line must be 1-based.");
        }

        var debugger = await GetDebuggerAsync();
        var file = ResolveDocumentPath(await GetDteAsync(), request.DocumentPath);
        var column = request.Column <= 0 ? 1 : request.Column;
        var hitCount = request.HitCount.GetValueOrDefault();
        var hitCountType = ResolveHitCountType(request.HitCountType, hitCount);
        var breakpoints = debugger.Breakpoints.Add(
            Function: string.Empty,
            File: file,
            Line: request.Line,
            Column: column,
            Condition: request.Condition ?? string.Empty,
            ConditionType: string.IsNullOrWhiteSpace(request.Condition)
                ? dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue
                : dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue,
            Language: string.Empty,
            Data: string.Empty,
            DataCount: 1,
            Address: string.Empty,
            HitCount: hitCount,
            HitCountType: hitCountType);

        var breakpoint = breakpoints.Item(1);
        var metadata = BreakpointMetadata.FromRequest(request);
        metadata.ApplyTo(breakpoint);
        return BreakpointInfo.FromBreakpoint(breakpoint);
    }

    public async Task<BreakpointListResult> ListBreakpointsAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var breakpoints = new List<BreakpointInfo>();

        foreach (Breakpoint breakpoint in debugger.Breakpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            breakpoints.Add(BreakpointInfo.FromBreakpoint(breakpoint));
        }

        return new BreakpointListResult(breakpoints);
    }

    public async Task<BreakpointRemoveResult> RemoveBreakpointAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        var debugger = await GetDebuggerAsync();
        var resolvedDocumentPath = ResolveOptionalDocumentPath(dte, request.DocumentPath);
        var removed = 0;
        var matches = new List<Breakpoint>();

        foreach (Breakpoint breakpoint in debugger.Breakpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (MatchesBreakpoint(breakpoint, request.Name, resolvedDocumentPath, request.Line))
            {
                matches.Add(breakpoint);
            }
        }

        foreach (var breakpoint in matches)
        {
            breakpoint.Delete();
            removed++;
        }

        return new BreakpointRemoveResult(removed);
    }

    public async Task<BreakpointEnableResult> SetBreakpointEnabledAsync(BreakpointEnableRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var debugger = await GetDebuggerAsync();
        var resolvedDocumentPath = ResolveOptionalDocumentPath(dte, request.DocumentPath);
        var updated = 0;
        var breakpoints = new List<BreakpointInfo>();

        foreach (Breakpoint breakpoint in debugger.Breakpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MatchesBreakpoint(breakpoint, request.Name, resolvedDocumentPath, request.Line))
            {
                continue;
            }

            breakpoint.Enabled = request.Enabled;
            updated++;
            breakpoints.Add(BreakpointInfo.FromBreakpoint(breakpoint));
        }

        return new BreakpointEnableResult(updated, breakpoints);
    }

    public async Task<CallStackResult> GetCallStackAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var frames = new List<CallStackFrameInfo>();

        if (debugger.CurrentThread?.StackFrames is StackFrames stackFrames)
        {
            foreach (StackFrame frame in stackFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frames.Add(CallStackFrameInfo.FromStackFrame(frame));
            }
        }

        return new CallStackResult(GetDebuggerState(debugger), frames);
    }

    public async Task<LocalsResult> GetLocalsAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var locals = new List<DebugExpressionInfo>();

        if (debugger.CurrentStackFrame?.Locals is Expressions expressions)
        {
            foreach (Expression expression in expressions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                locals.Add(DebugExpressionInfo.FromExpression(expression));
            }
        }

        return new LocalsResult(GetDebuggerState(debugger), locals);
    }

    public async Task<EvaluateExpressionResult> EvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            throw new ArgumentException("Expression is required.", nameof(request));
        }

        var debugger = await GetDebuggerAsync();
        var timeout = request.TimeoutMilliseconds <= 0 ? 5000 : request.TimeoutMilliseconds;
        var expression = debugger.GetExpression(request.Expression, UseAutoExpandRules: true, Timeout: timeout);
        return new EvaluateExpressionResult(GetDebuggerState(debugger), DebugExpressionInfo.FromExpression(expression));
    }

    public async Task<DebugSetVariableResult> SetVariableAsync(DebugSetVariableRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new DebugSetVariableResult(false, "Variable name is required.", null);
        }

        var expressionText = $"{request.Name} = {request.Value}";
        var evaluation = await EvaluateAsync(
            new EvaluateExpressionRequest
            {
                Expression = expressionText,
                TimeoutMilliseconds = request.TimeoutMilliseconds
            },
            cancellationToken);
        return new DebugSetVariableResult(
            evaluation.Expression.IsValidValue,
            evaluation.Expression.IsValidValue ? null : "Debugger rejected the assignment expression.",
            evaluation);
    }

    public async Task<WatchOperationResult> AddWatchAsync(WatchAddRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return new WatchOperationResult(true, false, "Watch expression is required.", null);
        }

        var expressionText = request.Expression.Trim();
        if (!watchExpressions.Contains(expressionText, StringComparer.OrdinalIgnoreCase))
        {
            watchExpressions.Add(expressionText);
        }

        var evaluation = await EvaluateAsync(
            new EvaluateExpressionRequest { Expression = expressionText },
            cancellationToken);
        return new WatchOperationResult(true, true, null, evaluation.Expression);
    }

    public async Task<WatchOperationResult> RemoveWatchAsync(WatchRemoveRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return new WatchOperationResult(true, false, "Watch expression is required.", null);
        }

        var expressionText = request.Expression.Trim();
        var existingExpression = watchExpressions.FirstOrDefault(expression =>
            string.Equals(expression, expressionText, StringComparison.OrdinalIgnoreCase));
        var removed = existingExpression is not null && watchExpressions.Remove(existingExpression);
        return new WatchOperationResult(
            true,
            removed,
            removed ? null : "Watch expression was not found.",
            new DebugExpressionInfo(expressionText, null, null, false));
    }

    public async Task<WatchListResult> ListWatchesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var watches = new List<DebugExpressionInfo>();
        foreach (var expressionText in watchExpressions.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var evaluation = await EvaluateAsync(
                    new EvaluateExpressionRequest { Expression = expressionText },
                    cancellationToken);
                watches.Add(evaluation.Expression);
            }
            catch (Exception ex)
            {
                watches.Add(new DebugExpressionInfo(expressionText, ex.Message, null, false));
            }
        }

        return new WatchListResult(true, null, watches);
    }

    public async Task<DebugThreadListResult> GetThreadsAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var currentProgram = debugger.CurrentProgram;
        if (currentProgram?.Threads is not Threads threads)
        {
            return new DebugThreadListResult(false, "No current debug program is available.", Array.Empty<DebugThreadInfo>());
        }

        var result = new List<DebugThreadInfo>();
        var currentThread = debugger.CurrentThread;
        foreach (EnvDTE.Thread thread in threads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(DebugThreadInfo.FromThread(thread, currentThread));
        }

        return new DebugThreadListResult(true, null, result);
    }

    public async Task<DebuggedProcessListResult> ListDebuggedProcessesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var result = new List<DebuggedProcessInfo>();
        foreach (Process process in debugger.DebuggedProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(DebuggedProcessInfo.FromProcess(process));
        }

        return new DebuggedProcessListResult(result);
    }

    public async Task<LocalProcessListResult> ListLocalProcessesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var debuggedProcessIds = new HashSet<int>();
        foreach (Process process in debugger.DebuggedProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            debuggedProcessIds.Add(process.ProcessID);
        }

        var result = new List<LocalProcessInfo>();

        foreach (Process process in debugger.LocalProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(LocalProcessInfo.FromProcess(process, debuggedProcessIds.Contains(process.ProcessID)));
        }

        return new LocalProcessListResult(result.OrderBy(process => process.ProcessId).ToArray());
    }

    public async Task<DebugAttachResult> AttachAsync(DebugAttachRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var matches = FindProcesses(debugger.LocalProcesses, request.ProcessId, request.ProcessName);

        if (matches.Count == 0)
        {
            return new DebugAttachResult(false, "No matching local process was found.", null);
        }

        if (matches.Count > 1)
        {
            return new DebugAttachResult(false, $"Process selector matched {matches.Count} local processes; use processId.", null);
        }

        var process = matches[0];
        process.Attach();
        return new DebugAttachResult(true, null, DebuggedProcessInfo.FromProcess(process));
    }

    public async Task<ProcessDetachResult> DetachAsync(ProcessDetachRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var matches = FindProcesses(debugger.DebuggedProcesses, request.ProcessId, request.ProcessName);

        if (matches.Count == 0)
        {
            return new ProcessDetachResult(false, "No matching debugged process was found.", null, GetDebuggerState(debugger));
        }

        if (matches.Count > 1)
        {
            return new ProcessDetachResult(false, $"Process selector matched {matches.Count} debugged processes; use processId.", null, GetDebuggerState(debugger));
        }

        var process = matches[0];
        var info = DebuggedProcessInfo.FromProcess(process);
        process.Detach(WaitForBreakOrEnd: false);
        return new ProcessDetachResult(true, null, info, GetDebuggerState(debugger));
    }

    public async Task<ProcessTerminateResult> TerminateAsync(ProcessTerminateRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var matches = FindProcesses(debugger.DebuggedProcesses, request.ProcessId, request.ProcessName);

        if (matches.Count == 0)
        {
            return new ProcessTerminateResult(false, "No matching debugged process was found.", null, GetDebuggerState(debugger));
        }

        if (matches.Count > 1)
        {
            return new ProcessTerminateResult(false, $"Process selector matched {matches.Count} debugged processes; use processId.", null, GetDebuggerState(debugger));
        }

        var process = matches[0];
        var info = DebuggedProcessInfo.FromProcess(process);
        if (!TryInvoke(process, "Terminate", false) && !TryInvoke(process, "Terminate"))
        {
            return new ProcessTerminateResult(false, "The active Visual Studio debug engine does not expose process termination through EnvDTE.", info, GetDebuggerState(debugger));
        }

        return new ProcessTerminateResult(true, null, info, GetDebuggerState(debugger));
    }

    public async Task<ThreadSwitchResult> SwitchThreadAsync(ThreadSwitchRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var currentProgram = debugger.CurrentProgram;
        if (currentProgram?.Threads is not Threads threads)
        {
            return new ThreadSwitchResult(false, false, "No current debug program is available.", null);
        }

        foreach (EnvDTE.Thread thread in threads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (thread.ID != request.ThreadId)
            {
                continue;
            }

            debugger.CurrentThread = thread;
            return new ThreadSwitchResult(true, true, null, DebugThreadInfo.FromThread(thread, debugger.CurrentThread));
        }

        return new ThreadSwitchResult(true, false, "Thread was not found.", null);
    }

    public async Task<ThreadSetFrozenResult> SetThreadFrozenAsync(ThreadSetFrozenRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var thread = FindThread(debugger, request.ThreadId);
        if (thread is null)
        {
            return new ThreadSetFrozenResult(true, false, "Thread was not found.", null, request.Frozen);
        }

        var method = request.Frozen ? "Freeze" : "Thaw";
        if (!TryInvoke(thread, method))
        {
            return new ThreadSetFrozenResult(false, false, "The active Visual Studio debug engine does not expose thread freeze/thaw through EnvDTE.", DebugThreadInfo.FromThread(thread, debugger.CurrentThread), request.Frozen);
        }

        return new ThreadSetFrozenResult(true, true, null, DebugThreadInfo.FromThread(thread, debugger.CurrentThread), request.Frozen);
    }

    public async Task<ThreadCallStackResult> GetThreadCallStackAsync(ThreadCallStackRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var thread = FindThread(debugger, request.ThreadId);
        if (thread is null)
        {
            return new ThreadCallStackResult(true, "Thread was not found.", null, Array.Empty<CallStackFrameInfo>());
        }

        var stackFrames = TryGetThreadStackFrames(thread);
        if (stackFrames is null)
        {
            return new ThreadCallStackResult(false, "The active Visual Studio debug engine did not expose stack frames for this thread.", DebugThreadInfo.FromThread(thread, debugger.CurrentThread), Array.Empty<CallStackFrameInfo>());
        }

        var frames = new List<CallStackFrameInfo>();
        foreach (StackFrame frame in stackFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frames.Add(CallStackFrameInfo.FromStackFrame(frame));
        }

        return new ThreadCallStackResult(true, null, DebugThreadInfo.FromThread(thread, debugger.CurrentThread), frames);
    }


    public async Task<ModuleListResult> ListModulesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var modules = new List<DebugModuleInfo>();

        AddModulesFromObject(debugger.CurrentProgram, modules, cancellationToken);
        foreach (Process process in debugger.DebuggedProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddModulesFromObject(process, modules, cancellationToken);
        }

        var distinctModules = modules
            .GroupBy(module => $"{module.Name}|{module.Path}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return distinctModules.Length == 0
            ? new ModuleListResult(false, "The active Visual Studio debug engine did not expose module data through EnvDTE.", distinctModules)
            : new ModuleListResult(true, null, distinctModules);
    }

    public async Task<ImmediateExecuteResult> ExecuteImmediateAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Statement))
        {
            return new ImmediateExecuteResult(true, false, "Statement is required.", null);
        }

        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
        {
            return new ImmediateExecuteResult(true, false, "The debugger is not running; the Immediate window is only available while debugging.", null);
        }

        try
        {
            // The Immediate window itself routes statements through the same expression
            // evaluator exposed by Debugger.GetExpression, so this reuses that API directly
            // instead of driving the Immediate window's UI (which risks sending keystrokes to
            // whichever window currently has focus).
            var expression = debugger.GetExpression(request.Statement, UseAutoExpandRules: true, Timeout: 5000);
            var output = expression.IsValidValue
                ? (string.IsNullOrEmpty(expression.Type) ? expression.Value : $"{expression.Value} ({expression.Type})")
                : expression.Value;
            return new ImmediateExecuteResult(true, expression.IsValidValue, null, output);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new ImmediateExecuteResult(true, false, ex.Message, null);
        }
    }

    public async Task<ExceptionSettingsResult> GetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        if (debugger is not EnvDTE90.Debugger3 debugger3)
        {
            return new ExceptionSettingsResult(false, false, "Visual Studio debugger does not support exception settings (EnvDTE90.Debugger3 is unavailable).");
        }

        var filter = string.IsNullOrWhiteSpace(request.ExceptionName) ? null : request.ExceptionName!.Trim();
        var settings = new List<ExceptionSettingInfo>();
        try
        {
            foreach (EnvDTE90.ExceptionSettings group in debugger3.ExceptionGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (EnvDTE90.ExceptionSetting setting in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (filter is not null && setting.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    settings.Add(new ExceptionSettingInfo(group.Name, setting.Name, setting.BreakWhenThrown, setting.BreakWhenUserUnhandled, setting.UserDefined));
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new ExceptionSettingsResult(false, false, ex.Message);
        }

        return new ExceptionSettingsResult(true, true, null, settings);
    }

    public async Task<ExceptionSettingsResult> SetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ExceptionName))
        {
            return new ExceptionSettingsResult(false, false, "Exception name is required.");
        }

        if (request.BreakOnThrown is not { } breakOnThrown)
        {
            return new ExceptionSettingsResult(false, false, "BreakOnThrown is required to change an exception setting.");
        }

        var debugger = await GetDebuggerAsync();
        if (debugger is not EnvDTE90.Debugger3 debugger3)
        {
            return new ExceptionSettingsResult(false, false, "Visual Studio debugger does not support exception settings (EnvDTE90.Debugger3 is unavailable).");
        }

        var exceptionName = request.ExceptionName!.Trim();
        try
        {
            foreach (EnvDTE90.ExceptionSettings group in debugger3.ExceptionGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (EnvDTE90.ExceptionSetting setting in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.Equals(setting.Name, exceptionName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    group.SetBreakWhenThrown(breakOnThrown, setting);
                    var updated = new ExceptionSettingInfo(group.Name, setting.Name, breakOnThrown, setting.BreakWhenUserUnhandled, setting.UserDefined);
                    return new ExceptionSettingsResult(true, true, "Exception setting updated.", new[] { updated });
                }
            }

            if (!breakOnThrown)
            {
                return new ExceptionSettingsResult(false, false, $"Exception '{exceptionName}' was not found among the debugger's exception settings.");
            }

            EnvDTE90.ExceptionSettings? clrGroup = null;
            foreach (EnvDTE90.ExceptionSettings candidateGroup in debugger3.ExceptionGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.Equals(candidateGroup.Name, "Common Language Runtime Exceptions", StringComparison.OrdinalIgnoreCase))
                {
                    clrGroup = candidateGroup;
                    break;
                }
            }

            if (clrGroup is null)
            {
                return new ExceptionSettingsResult(false, false, $"Exception '{exceptionName}' was not found and the Common Language Runtime Exceptions group is unavailable to add it.");
            }

            var newSetting = clrGroup.NewException(exceptionName, 0);
            clrGroup.SetBreakWhenThrown(true, newSetting);
            var created = new ExceptionSettingInfo(clrGroup.Name, newSetting.Name, true, newSetting.BreakWhenUserUnhandled, newSetting.UserDefined);
            return new ExceptionSettingsResult(true, true, "Exception setting created.", new[] { created });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new ExceptionSettingsResult(false, false, ex.Message);
        }
    }

    public Task<MemoryReadResult> ReadMemoryAsync(MemoryReadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MemoryReadResult(
            false,
            false,
            "Debugger memory reads require lower-level Visual Studio debug engine APIs; EnvDTE does not expose a stable memory-read surface.",
            request.AddressExpression,
            request.ByteCount,
            null));
    }

    public async Task<ParallelStacksResult> GetParallelStacksAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var currentProgram = debugger.CurrentProgram;
        if (currentProgram?.Threads is not Threads threads)
        {
            return new ParallelStacksResult(true, "No current debug program is available.", Array.Empty<ParallelStackFrameInfo>());
        }

        var frames = new List<ParallelStackFrameInfo>();
        foreach (EnvDTE.Thread thread in threads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stackFrames = TryGetThreadStackFrames(thread);
            if (stackFrames is null)
            {
                frames.Add(new ParallelStackFrameInfo(thread.ID, thread.Name, null, null, 0, 0));
                continue;
            }

            foreach (StackFrame frame in stackFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frames.Add(new ParallelStackFrameInfo(thread.ID, thread.Name, frame.FunctionName, null, 0, 0));
            }
        }

        return new ParallelStacksResult(true, null, frames);
    }

    public async Task<ParallelWatchResult> GetParallelWatchAsync(CancellationToken cancellationToken)
    {
        var watches = await ListWatchesAsync(cancellationToken);
        return new ParallelWatchResult(watches.Supported, watches.Message, watches.Watches);
    }

    public Task<ParallelTasksResult> ListParallelTasksAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ParallelTasksResult(
            false,
            "Parallel task enumeration requires lower-level Visual Studio debugger APIs; EnvDTE does not expose the Parallel Tasks window data.",
            Array.Empty<ParallelTaskInfo>()));
    }

    private async Task<DTE> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await package.GetServiceAsync(typeof(DTE)) as DTE
            ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
    }

    private async Task<Debugger> GetDebuggerAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return (await GetDteAsync()).Debugger
            ?? throw new InvalidOperationException("Visual Studio debugger service is unavailable.");
    }

    private static DebuggerStateInfo GetDebuggerState(Debugger debugger)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return new DebuggerStateInfo(debugger.CurrentMode.ToString());
    }

    private static string ResolveDocumentPath(DTE dte, string documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            throw new ArgumentException("Document path is required.", nameof(documentPath));
        }

        if (Path.IsPathRooted(documentPath))
        {
            return Path.GetFullPath(documentPath);
        }

        var solutionPath = dte.Solution?.FullName;
        var solutionDirectory = string.IsNullOrWhiteSpace(solutionPath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory;

        return Path.GetFullPath(Path.Combine(solutionDirectory, documentPath));
    }

    private static string? ResolveOptionalDocumentPath(DTE dte, string? documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (documentPath is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var nonEmptyDocumentPath = path.Trim();
        return ResolveDocumentPath(dte, nonEmptyDocumentPath);
    }

    private static bool MatchesBreakpoint(Breakpoint breakpoint, string? name, string? resolvedDocumentPath, int line)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // When a specific breakpoint name is supplied, it must match exactly. Falling through to the
        // coarser file+line match here would let a request targeting one breakpoint incorrectly match
        // (and mutate/delete) a different breakpoint that merely shares the same line.
        if (!string.IsNullOrWhiteSpace(name))
        {
            return string.Equals(breakpoint.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(resolvedDocumentPath)
            && line > 0
            && !string.IsNullOrWhiteSpace(breakpoint.File)
            && string.Equals(Path.GetFullPath(breakpoint.File), resolvedDocumentPath, StringComparison.OrdinalIgnoreCase)
            && breakpoint.FileLine == line;
    }

    private static List<Process> FindProcesses(Processes processes, int? processId, string? processName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var matches = new List<Process>();

        foreach (Process process in processes)
        {
            if (processId is not null && process.ProcessID != processId.Value)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(processName)
                && !string.Equals(Path.GetFileName(process.Name), processName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(process.Name, processName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(process);
        }

        return matches;
    }

    private static EnvDTE.Thread? FindThread(Debugger debugger, int threadId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var currentProgram = debugger.CurrentProgram;
        if (currentProgram?.Threads is not Threads threads)
        {
            return null;
        }

        foreach (EnvDTE.Thread thread in threads)
        {
            if (thread.ID == threadId)
            {
                return thread;
            }
        }

        return null;
    }

    private static bool TryInvoke(object target, string methodName, params object[] arguments)
    {
        try
        {
            target.GetType().InvokeMember(
                methodName,
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                target,
                arguments);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static StackFrames? TryGetThreadStackFrames(EnvDTE.Thread thread)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return thread.StackFrames;
        }
        catch
        {
            return null;
        }
    }

    private static void AddModulesFromObject(object? source, ICollection<DebugModuleInfo> modules, CancellationToken cancellationToken)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var moduleCollection = TryGetProperty(source, "Modules");
        if (moduleCollection is null)
        {
            return;
        }

        if (moduleCollection is System.Collections.IEnumerable enumerable)
        {
            foreach (var module in enumerable)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddModule(module, modules);
            }
        }
    }

    private static void AddModule(object? module, ICollection<DebugModuleInfo> modules)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (module is null)
        {
            return;
        }

        var name = TryGetProperty(module, "Name")?.ToString();
        var path = TryGetProperty(module, "Path")?.ToString() ??
            TryGetProperty(module, "FileName")?.ToString();
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        modules.Add(new DebugModuleInfo(name, path));
    }

    private static object? TryGetProperty(object? source, string propertyName)
    {
        if (source is null)
        {
            return null;
        }

        try
        {
            return source.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                source,
                null);
        }
        catch
        {
            return null;
        }
    }

    private static dbgHitCountType ResolveHitCountType(string? hitCountType, int hitCount)
    {
        if (hitCount <= 0)
        {
            return dbgHitCountType.dbgHitCountTypeNone;
        }

        if (string.IsNullOrWhiteSpace(hitCountType))
        {
            return dbgHitCountType.dbgHitCountTypeEqual;
        }

        var normalizedHitCountType = hitCountType!.Trim().ToLowerInvariant();
        return normalizedHitCountType switch
        {
            "equal" or "equals" or "exact" or "==" => dbgHitCountType.dbgHitCountTypeEqual,
            "multiple" or "multipleof" or "multiple_of" => dbgHitCountType.dbgHitCountTypeMultiple,
            "greaterthanorequal" or "greater_than_or_equal" or "greater-or-equal" or ">=" => dbgHitCountType.dbgHitCountTypeGreaterOrEqual,
            _ => throw new ArgumentException("Hit count type must be one of: equals, multiple, greaterThanOrEqual.", nameof(hitCountType))
        };
    }
}
