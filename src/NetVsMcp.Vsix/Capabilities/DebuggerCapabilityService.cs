using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface IDebuggerCapabilityService
{
    Task<DebuggerStateInfo> StartAsync(CancellationToken cancellationToken);
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
    Task<WatchOperationResult> AddWatchAsync(WatchAddRequest request, CancellationToken cancellationToken);
    Task<WatchOperationResult> RemoveWatchAsync(WatchRemoveRequest request, CancellationToken cancellationToken);
    Task<WatchListResult> ListWatchesAsync(CancellationToken cancellationToken);
    Task<DebugThreadListResult> GetThreadsAsync(CancellationToken cancellationToken);
    Task<ThreadSwitchResult> SwitchThreadAsync(ThreadSwitchRequest request, CancellationToken cancellationToken);
    Task<ModuleListResult> ListModulesAsync(CancellationToken cancellationToken);
    Task<ImmediateExecuteResult> ExecuteImmediateAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken);
    Task<ExceptionSettingsResult> GetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken);
    Task<ExceptionSettingsResult> SetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken);
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
            HitCount: 0,
            HitCountType: dbgHitCountType.dbgHitCountTypeNone);

        var breakpoint = breakpoints.Item(1);
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

    public async Task<WatchOperationResult> AddWatchAsync(WatchAddRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return new WatchOperationResult(true, false, "Watch expression is required.", null);
        }

        return new WatchOperationResult(
            false,
            false,
            "Watch expressions are not exposed through this VSIX skeleton yet.",
            null);
    }

    public async Task<WatchOperationResult> RemoveWatchAsync(WatchRemoveRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return new WatchOperationResult(true, false, "Watch expression is required.", null);
        }

        return new WatchOperationResult(
            false,
            false,
            "Watch expressions are not exposed through this VSIX skeleton yet.",
            null);
    }

    public async Task<WatchListResult> ListWatchesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        return new WatchListResult(
            false,
            "Watch expressions are not exposed through this VSIX skeleton yet.",
            Array.Empty<DebugExpressionInfo>());
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

    public async Task<ModuleListResult> ListModulesAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        return new ModuleListResult(
            false,
            "Module listing is not exposed through this VSIX skeleton yet.",
            Array.Empty<DebugModuleInfo>());
    }

    public Task<ImmediateExecuteResult> ExecuteImmediateAsync(ImmediateExecuteRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ImmediateExecuteResult(
            false,
            false,
            "Immediate window execution is not wired yet; EnvDTE command routing needs runtime validation to avoid sending text to the wrong window.",
            null));
    }

    public Task<ExceptionSettingsResult> GetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ExceptionSettingsResult(
            false,
            false,
            "Exception settings are not exposed through this VSIX skeleton yet; use Visual Studio debugger exception settings APIs in a later slice."));
    }

    public Task<ExceptionSettingsResult> SetExceptionSettingsAsync(ExceptionSettingsRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ExceptionSettingsResult(
            false,
            false,
            "Exception settings mutation is not exposed through this VSIX skeleton yet; use Visual Studio debugger exception settings APIs in a later slice."));
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

        if (!string.IsNullOrWhiteSpace(name)
            && string.Equals(breakpoint.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(resolvedDocumentPath)
            && line > 0
            && !string.IsNullOrWhiteSpace(breakpoint.File)
            && string.Equals(Path.GetFullPath(breakpoint.File), resolvedDocumentPath, StringComparison.OrdinalIgnoreCase)
            && breakpoint.FileLine == line)
        {
            return true;
        }

        return false;
    }
}
