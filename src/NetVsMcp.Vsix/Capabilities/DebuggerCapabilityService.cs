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
    Task<BreakpointInfo> SetBreakpointAsync(BreakpointSetRequest request, CancellationToken cancellationToken);
    Task<BreakpointListResult> ListBreakpointsAsync(CancellationToken cancellationToken);
    Task<BreakpointRemoveResult> RemoveBreakpointAsync(BreakpointRemoveRequest request, CancellationToken cancellationToken);
    Task<CallStackResult> GetCallStackAsync(CancellationToken cancellationToken);
    Task<LocalsResult> GetLocalsAsync(CancellationToken cancellationToken);
    Task<EvaluateExpressionResult> EvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken);
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
        var debugger = await GetDebuggerAsync();
        var removed = 0;

        foreach (Breakpoint breakpoint in debugger.Breakpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MatchesBreakpoint(breakpoint, request))
            {
                continue;
            }

            breakpoint.Delete();
            removed++;
        }

        return new BreakpointRemoveResult(removed);
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

    private static bool MatchesBreakpoint(Breakpoint breakpoint, BreakpointRemoveRequest request)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!string.IsNullOrWhiteSpace(request.Name)
            && string.Equals(breakpoint.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentPath)
            && request.Line > 0
            && string.Equals(Path.GetFullPath(breakpoint.File), Path.GetFullPath(request.DocumentPath), StringComparison.OrdinalIgnoreCase)
            && breakpoint.FileLine == request.Line)
        {
            return true;
        }

        return false;
    }
}
