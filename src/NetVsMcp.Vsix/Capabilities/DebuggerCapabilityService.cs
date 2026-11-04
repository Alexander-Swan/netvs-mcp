using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal enum DebugStepKind
{
    Into,
    Over,
    Out
}

internal sealed partial class DebuggerCapabilityService : IDebuggerCapabilityService, IDisposable
{
    private const int DefaultCallStackFrameLimit = 2;
    private readonly Func<Task<DTE?>> getDte;
    private readonly Func<CancellationToken, Task<IHotReloadInitializationNotification?>> subscribeHotReload;
    private long? unconfirmedHotReloadGeneration;
    private readonly List<string> watchExpressions = new();
    private readonly DebugExecutionTracker execution = new();
    internal NetVsMcp.Contracts.SessionEventBuffer Events { get; } = new(SessionIdentity.CurrentProcessSessionId());
    private DebuggerEvents? executionEvents;
    private Debugger? eventDebugger;

    public DebuggerCapabilityService(AsyncPackage package)
        : this(async () =>
        {
            var service = await package.GetServiceAsync(typeof(DTE));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return service as DTE;
        })
    {
    }

    internal DebuggerCapabilityService(Func<Task<DTE?>> getDte,
        Func<CancellationToken, Task<IHotReloadInitializationNotification?>>? subscribeHotReload = null)
    {
        this.getDte = getDte;
        this.subscribeHotReload = subscribeHotReload ?? (ct => HotReloadInitializationNotification.TrySubscribeAsync(ct,
            reason => Events.Publish("hotReload.notificationUnavailable." + reason)));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        await GetDebuggerAsync();
    }

    public async Task<DebuggerStateInfo> StartAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var newSession = debugger.CurrentMode == dbgDebugMode.dbgDesignMode;
        var startupDte = await GetDteAsync();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var initialGeneration = execution.ObservedState().Generation;
        var initialSolution = startupDte.Solution?.FullName;
        using var preparedNotification = newSession ? await subscribeHotReload(cancellationToken) : null;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (newSession && (debugger.CurrentMode != dbgDebugMode.dbgDesignMode ||
            execution.ObservedState().Generation != initialGeneration ||
            !string.Equals(initialSolution, startupDte.Solution?.FullName, StringComparison.OrdinalIgnoreCase)))
            throw new StaleDebugContextException("The selected debugger or solution changed during startup preparation; no command was issued.");
        var state = await ExecuteAsync(debugger, () => { ThreadHelper.ThrowIfNotOnUIThread(); debugger.Go(WaitForBreakOrEnd: false); },
            waitForStop: false, newSession: newSession, cancellationToken);
        if (newSession) unconfirmedHotReloadGeneration = state.Generation;
        using var activeNotification = newSession && preparedNotification is null ? await subscribeHotReload(cancellationToken) : null;
        var notification = preparedNotification ?? activeNotification;
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (newSession && debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
        {
            await InitializeHotReloadBaselineAsync(cancellationToken, () => notification?.Arm());
            if (notification is null) unconfirmedHotReloadGeneration = null;
            if (notification is not null)
            {
                var generation = state.Generation;
                var dte = await GetDteAsync();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                var solution = dte.Solution?.FullName;
                var processId = debugger.CurrentProcess?.ProcessID;
                unconfirmedHotReloadGeneration = generation;
                var started = await HotReloadBaseline.WaitForInitializationAsync(notification.Completed, TimeSpan.FromSeconds(10), cancellationToken);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                var sameSession = execution.ObservedState().Generation == generation && debugger.CurrentMode != dbgDebugMode.dbgDesignMode &&
                    string.Equals(solution, dte.Solution?.FullName, StringComparison.OrdinalIgnoreCase) &&
                    (!processId.HasValue || debugger.CurrentProcess?.ProcessID == processId);
                unconfirmedHotReloadGeneration = started && sameSession ? null : generation;
                Events.Publish(started && sameSession ? "hotReload.initialized" : "hotReload.unconfirmed");
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            return GetDebuggerState(debugger);
        }
        return state;
    }

    private async Task InitializeHotReloadBaselineAsync(CancellationToken cancellationToken, Action? beforeIssue = null)
    {
        // VS can initialize its Hot Reload baseline lazily on the first apply request.
        // Request it before returning a new session to the caller: if the first request
        // happens after an edit, it can accept that edit as the baseline and emit no delta.
        // Unsupported engines must still be able to start normally.
        var dte = await GetDteAsync();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var generation = execution.ObservedState().Generation;
        var solution = dte.Solution?.FullName;
        var processId = dte.Debugger.CurrentProcess?.ProcessID;
        await HotReloadBaseline.InitializeAsync(async ct =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            return HotReloadBaseline.Attempt(
                () => { ThreadHelper.ThrowIfNotOnUIThread(); return dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode &&
                    execution.ObservedState().Generation == generation &&
                    (!processId.HasValue || dte.Debugger.CurrentProcess?.ProcessID == processId) &&
                    string.Equals(dte.Solution?.FullName, solution, StringComparison.OrdinalIgnoreCase); },
                () => { ThreadHelper.ThrowIfNotOnUIThread(); return dte.Commands.Item("Debug.ApplyCodeChanges", 0).IsAvailable; },
                () => { ThreadHelper.ThrowIfNotOnUIThread(); beforeIssue?.Invoke(); dte.ExecuteCommand("Debug.ApplyCodeChanges"); });
        }, cancellationToken);
    }

    public async Task<DebuggerStateInfo> StartWithoutDebuggingAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var dte = await GetDteAsync();
        await GetDebuggerAsync();
        var solutionBuild = dte.Solution?.SolutionBuild
            ?? throw new InvalidOperationException("Visual Studio solution build service is unavailable.");
        var targets = NonDebugLaunchObserver.StartupExecutables(dte);
        var before = NonDebugLaunchObserver.ProcessIds();
        var started = DateTime.UtcNow;
        solutionBuild.Run();
        var processIds = await NonDebugLaunchObserver.ObserveAsync(targets, before, started, cancellationToken);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var state = GetDebuggerState(dte.Debugger);
        return new(state.Mode, state.BreakReason, state.Outcome, state.Generation, state.StopId,
            state.ExecutionSequence, processIds.Count > 0 ? processIds : null);
    }

    public async Task<DebuggerStateInfo> RestartAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();

        if (debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
        {
            debugger.Stop(WaitForDesignMode: true);
        }

        return await StartAsync(cancellationToken);
    }

    public async Task<DebuggerStateInfo> StopAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
            throw new InvalidOperationException("The debugger is not running.");
        return await ExecuteAsync(debugger, () => { ThreadHelper.ThrowIfNotOnUIThread(); debugger.Stop(WaitForDesignMode: false); },
            waitForStop: true, newSession: false, cancellationToken);
    }

    public async Task<DebuggerStateInfo> ContinueAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            throw new InvalidOperationException("The debugger must be paused before continuing.");
        return await ExecuteAsync(debugger, () => { ThreadHelper.ThrowIfNotOnUIThread(); debugger.Go(WaitForBreakOrEnd: false); },
            waitForStop: false, newSession: false, cancellationToken);
    }

    private async Task ContinueWhenPausedAsync(Debugger debugger, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            throw new InvalidOperationException("The debugger must be paused before continuing.");
        await ExecuteAsync(debugger, () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            debugger.Go(WaitForBreakOrEnd: false);
        }, waitForStop: false, newSession: false, cancellationToken);
    }
    public async Task<HotReloadApplyResult> ApplyHotReloadAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var dte = await GetDteAsync();
        var debugger = dte.Debugger
            ?? throw new InvalidOperationException("Visual Studio debugger service is unavailable.");

        if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
        {
            return new HotReloadApplyResult(
                false,
                "The debugger is not running; Hot Reload requires an active debug session.",
                Array.Empty<ErrorListItemInfo>());
        }

        if (unconfirmedHotReloadGeneration == execution.ObservedState().Generation)
            return new HotReloadApplyResult(false,
                "Hot Reload session initialization was not confirmed before editing was allowed. Restart debugging before editing source and applying changes.",
                Array.Empty<ErrorListItemInfo>());

        try
        {
            // Debug.ApplyCodeChanges is the underlying command for both classic Edit and
            // Continue and Hot Reload (bound to Alt+F10) - VS has no separate public API
            // for triggering Hot Reload specifically.
            dte.ExecuteCommand("Debug.ApplyCodeChanges");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new HotReloadApplyResult(false, $"Failed to apply code changes: {ex.Message}", Array.Empty<ErrorListItemInfo>());
        }

        // EnvDTE exposes no "did Hot Reload succeed" property, so success is reported as
        // "the command ran without throwing", and any resulting compile errors are surfaced
        // from the Error List so a failed apply doesn't look like a silent no-op.
        var errors = ReadCompileErrors(dte as DTE2);
        var success = errors.Count == 0;
        var message = success
            ? "Requested code changes; no compile errors were reported. Runtime behavior has not been verified. If paused, step or continue to a fresh stop before checking updated values."
            : $"Requested code changes, but {errors.Count} compile error(s) were reported.";
        return new HotReloadApplyResult(success, message, errors);
    }

    private static IReadOnlyCollection<ErrorListItemInfo> ReadCompileErrors(DTE2? dte2)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var errorItems = dte2?.ToolWindows?.ErrorList?.ErrorItems;
        if (errorItems is null)
        {
            return Array.Empty<ErrorListItemInfo>();
        }

        var items = new List<ErrorListItemInfo>();
        for (var index = 1; index <= errorItems.Count; index++)
        {
            var item = errorItems.Item(index);
            if (item is null || item.ErrorLevel != vsBuildErrorLevel.vsBuildErrorLevelHigh)
            {
                continue;
            }

            items.Add(ErrorListItemInfo.FromErrorItem(item));
        }

        return items;
    }

    public async Task<DebuggerStateInfo> BreakAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode != dbgDebugMode.dbgRunMode)
            throw new InvalidOperationException("The debugger must be running before pausing.");
        return await ExecuteAsync(debugger, () => { ThreadHelper.ThrowIfNotOnUIThread(); debugger.Break(WaitForBreakMode: false); },
            waitForStop: true, newSession: false, cancellationToken);
    }

    public async Task<DebuggerStateInfo> StepAsync(DebugStepKind stepKind, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();

        if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            throw new InvalidOperationException("The debugger must be paused before stepping.");
        if (!Enum.IsDefined(typeof(DebugStepKind), stepKind))
            throw new ArgumentOutOfRangeException(nameof(stepKind));
        return await ExecuteAsync(debugger, () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            switch (stepKind)
            {
                case DebugStepKind.Into: debugger.StepInto(WaitForBreakOrEnd: false); break;
                case DebugStepKind.Over: debugger.StepOver(WaitForBreakOrEnd: false); break;
                case DebugStepKind.Out: debugger.StepOut(WaitForBreakOrEnd: false); break;
            }
        }, waitForStop: true, newSession: false, cancellationToken);
    }

    public async Task<DebuggerStateInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        return GetDebuggerState(await GetDebuggerAsync(), refresh: true);
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

        try
        {
            var breakpoints = debugger.Breakpoints.Add(
                Function: string.Empty,
                File: file,
                Line: request.Line,
                Column: column,
                Condition: request.Condition ?? string.Empty,
                ConditionType: dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue,
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
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            throw WrapComFailure($"setting a breakpoint at '{file}:{request.Line}'", ex);
        }
    }

    public async Task<BreakpointListResult> ListBreakpointsAsync(CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var breakpoints = new List<BreakpointInfo>();

        try
        {
            foreach (Breakpoint breakpoint in debugger.Breakpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                breakpoints.Add(BreakpointInfo.FromBreakpoint(breakpoint));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            throw WrapComFailure("listing breakpoints", ex);
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

        try
        {
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
                var info = BreakpointInfo.FromBreakpoint(breakpoint);
                breakpoint.Delete();
                removed++;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            throw WrapComFailure("removing breakpoints", ex);
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
            var info = BreakpointInfo.FromBreakpoint(breakpoint);
            breakpoints.Add(info);
        }

        return new BreakpointEnableResult(updated, breakpoints);
    }

    public async Task<CallStackResult> GetCallStackAsync(CallStackRequest request, CancellationToken cancellationToken)
    {
        var maximum = request.MaxFrames ?? DefaultCallStackFrameLimit;
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(request.MaxFrames));
        return (await InspectFrameAsync(new NetVsMcp.Contracts.DebugFrameInspectionRequest
        { CallStack = new NetVsMcp.Contracts.CallStackRequest { MaxFrames = maximum } }, cancellationToken)).CallStack!;
    }

    private static uint? GetFrameDepth(StackFrame frame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            return (frame as EnvDTE90a.StackFrame2)?.Depth;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or NotSupportedException or NotImplementedException)
        {
            // Optional metadata must not prevent reading a debugger's call stack.
            return null;
        }
    }

    public async Task<DebugSelectFrameResult> SelectFrameAsync(DebugSelectFrameRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        StackFrames? frames = null;
        try
        {
            var selected = DebuggerFrameSelector.Select(debugger.CurrentMode == dbgDebugMode.dbgBreakMode, request.FrameIndex,
                () =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    frames = debugger.CurrentThread?.StackFrames;
                    return frames?.Count ?? 0;
                },
                index =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    return frames!.Item(index);
                },
                frame =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    debugger.CurrentStackFrame = frame;
                }, cancellationToken);
            return new DebugSelectFrameResult(GetDebuggerState(debugger), request.FrameIndex,
                CallStackFrameInfo.FromStackFrame(selected, request.FrameIndex, true));
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            throw WrapComFailure("selecting a stack frame", ex);
        }
    }

    public async Task<DebugFrameInspectionResult> InspectFrameAsync(DebugFrameInspectionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        var observedState = GetDebuggerState(debugger);
        var pinnedStopId = request.ExpectedStopId ?? observedState.StopId;
        var pinnedGeneration = request.ExpectedGeneration ?? observedState.Generation;
        request.Expansion?.Validate();
        if (request.TimeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.TimeoutMilliseconds));
        cancellationToken.ThrowIfCancellationRequested();
        // State-only guards (including snapshot action preconditions) must work in native
        // pauses with no inspectable current frame. Do not touch DTE's thread/frame objects.
        if ((request.ExpectedStopId is not null || request.ExpectedGeneration.HasValue) &&
            !request.IncludeLocals && request.CallStack is null && request.Watch is null &&
            request.AddWatchExpression is null && request.Expressions.Length == 0 && request.Expansion is null)
            return new DebugFrameInspectionResult(GetDebuggerState(debugger), null, [], null);
        // Resolve objects directly: inspecting a frame never changes the IDE selection.
        var thread = DebuggerFrameSelector.ResolveThread(debugger.CurrentMode == dbgDebugMode.dbgBreakMode,
            request.ThreadId, () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return debugger.CurrentThread;
            }, id => FindThread(debugger, id));
        var availableFrames = DebuggerFrameSelector.RequireAvailable(thread.StackFrames, "stack-frame collection");
        var frame = request.StackFrame == 0 && request.ThreadId is null
            ? DebuggerFrameSelector.RequireAvailable(debugger.CurrentStackFrame, "selected stack frame")
            : DebuggerFrameSelector.GetFrame(request.StackFrame == 0 ? 1 : request.StackFrame,
            () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return availableFrames.Count;
            }, index =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return availableFrames.Item(index);
            });
        var state = GetDebuggerState(debugger);
        var context = CaptureInspectionContext(state, thread, frame, availableFrames,
            request.StackFrame > 0 ? request.StackFrame : request.ThreadId.HasValue ? 1 : 0);
        DebugExpansionResult? expansion = null;
        if (request.Expansion is { } expand)
        {
            if (!expand.AllowFunctionEvaluation && !InspectionExpressionPolicy.IsInspectionOnly(expand.Expression))
                throw new ArgumentException("With function evaluation disabled, use a simple identifier, member path, or literal numeric index. Explicit calls, assignments, and complex expressions require allowFunctionEvaluation=true.");
            if (debugger is not EnvDTE100.Debugger5 evaluator)
                throw new NotSupportedException("Bounded expansion requires frame-specific Debugger5 evaluation.");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var expression = evaluator.GetExpression3(expand.Expression, frame, UseAutoExpandRules: false,
                TreatAsStatement: false, AllowAutoFuncEval: expand.AllowFunctionEvaluation, Timeout: expand.BudgetMilliseconds);
            IDebugValue value = new DteDebugValue(expression);
            foreach (var index in expand.MemberPath)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (elapsed.ElapsedMilliseconds >= expand.BudgetMilliseconds)
                    throw new TimeoutException("Cooperative expansion budget expired while resolving memberPath. Retry a narrower request.");
                if (index > value.ChildCount) throw new ArgumentException("memberPath no longer resolves in this pause.");
                value = value.Child(index);
            }
            expansion = BoundedDebugExpansion.Expand(value, expand, cancellationToken, elapsed) with { Context = context };
            execution.Validate(debugger.CurrentMode.ToString(), pinnedStopId, pinnedGeneration);
        }
        LocalsResult? locals = null;
        if (request.IncludeLocals)
        {
            var values = new List<DebugExpressionInfo>();
            if (frame.Locals is Expressions expressions)
                foreach (Expression expression in expressions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    values.Add(DebugExpressionInfo.FromExpression(expression));
                }
            locals = new LocalsResult(state, values, context);
        }
        EvaluateExpressionResult Evaluate(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Expression is required.");
            if (debugger is not EnvDTE100.Debugger5 debugger5)
                throw new NotSupportedException("Frame-specific evaluation requires EnvDTE100.Debugger5.");
            var value = debugger5.GetExpression3(text, frame, UseAutoExpandRules: true,
                TreatAsStatement: false, AllowAutoFuncEval: true, Timeout: request.TimeoutMilliseconds);
            return new EvaluateExpressionResult(GetDebuggerState(debugger), DebugExpressionInfo.FromExpression(value, text), context);
        }
        var results = request.Expressions.Select(Evaluate).ToList();
        string? watchToAdd = null;
        if (request.AddWatchExpression is not null)
        {
            var text = request.AddWatchExpression.Trim();
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Watch expression is required.");
            results.Add(Evaluate(text));
            if (!watchExpressions.Contains(text, StringComparer.OrdinalIgnoreCase)) watchToAdd = text;
        }
        WatchListResult? watch = null;
        if (request.Watch is not null)
        {
            var watched = new List<DebugExpressionInfo>();
            var configured = watchToAdd is null ? watchExpressions.ToArray() : watchExpressions.Concat(new[] { watchToAdd }).ToArray();
            foreach (var text in NormalizeWatchExpressions(request.Watch.Expressions) ?? configured)
            {
                try { watched.Add(Evaluate(text).Expression); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { watched.Add(new DebugExpressionInfo(text, ex.Message, null, false)); }
            }
            watch = new WatchListResult(true, null, watched, context);
        }
        CallStackResult? callStack = null;
        if (request.CallStack is not null)
        {
            var limit = request.CallStack.MaxFrames ?? 2;
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.CallStack.MaxFrames));
            var frames = new List<CallStackFrameInfo>();
            var currentFrame = debugger.CurrentStackFrame;
            var isCurrentThread = debugger.CurrentThread is { } currentThread && thread.ID == currentThread.ID;
            foreach (StackFrame stackFrame in availableFrames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (frames.Count == limit) break;
                frames.Add(CallStackFrameInfo.FromStackFrame(DebuggerFrameSelector.RequireAvailable(stackFrame, "enumerated stack frame"), frames.Count + 1,
                    DebuggerFrameSelector.IsCurrentFrame(stackFrame, currentFrame, isCurrentThread, GetFrameDepth)));
            }
            if (frames.Count == 0)
                throw new InspectionUnavailableException("Visual Studio reports a pause but stack enumeration is not ready. Wait for frame readiness without repeating execution.");
            callStack = new CallStackResult(state, frames, availableFrames.Count > frames.Count, context);
        }
        execution.Validate(debugger.CurrentMode.ToString(), pinnedStopId, pinnedGeneration);
        cancellationToken.ThrowIfCancellationRequested();
        if (watchToAdd is not null) watchExpressions.Add(watchToAdd);
        return new DebugFrameInspectionResult(state, locals, results, watch, callStack, expansion, context);
    }

    private sealed class DteDebugValue(Expression expression) : IDebugValue
    {
        public string? Name { get { ThreadHelper.ThrowIfNotOnUIThread(); return expression.Name; } }
        public string? Type { get { ThreadHelper.ThrowIfNotOnUIThread(); return expression.Type; } }
        public string? Value { get { ThreadHelper.ThrowIfNotOnUIThread(); return expression.Value; } }
        public bool IsValid { get { ThreadHelper.ThrowIfNotOnUIThread(); return expression.IsValidValue; } }
        public int ChildCount { get { ThreadHelper.ThrowIfNotOnUIThread(); return expression.DataMembers?.Count ?? 0; } }
        public IDebugValue Child(int index) { ThreadHelper.ThrowIfNotOnUIThread(); return new DteDebugValue(expression.DataMembers.Item(index)); }
    }

    public async Task<LocalsResult> GetLocalsAsync(CancellationToken cancellationToken) =>
        (await InspectFrameAsync(new NetVsMcp.Contracts.DebugFrameInspectionRequest { IncludeLocals = true }, cancellationToken)).Locals!;

    public async Task<EvaluateExpressionResult> EvaluateAsync(EvaluateExpressionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            throw new ArgumentException("Expression is required.", nameof(request));
        }

        var debugger = await GetDebuggerAsync();

        // Debugger2.GetExpression's Timeout parameter only bounds slow evaluation against a
        // live debuggee; in dbgDesignMode (no debuggee at all) it does not reliably return
        // within that window and can block far longer than the caller's own RPC timeout. Fail
        // fast here instead, matching the guard ApplyHotReloadAsync/ExecuteImmediateAsync
        // already use.
        if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
        {
            return new EvaluateExpressionResult(
                GetDebuggerState(debugger),
                new DebugExpressionInfo(request.Expression, "The debugger is not running; expression evaluation requires an active debug session.", null, false));
        }

        var timeout = request.TimeoutMilliseconds <= 0 ? 5000 : request.TimeoutMilliseconds;
        return (await InspectFrameAsync(new NetVsMcp.Contracts.DebugFrameInspectionRequest
        { Expressions = new[] { request.Expression }, TimeoutMilliseconds = timeout }, cancellationToken)).Results.Single();
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
        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
        {
            var inspection = await InspectFrameAsync(new NetVsMcp.Contracts.DebugFrameInspectionRequest
            { AddWatchExpression = expressionText }, cancellationToken);
            return new WatchOperationResult(true, true, null, inspection.Results.Single().Expression, inspection.Context);
        }
        if (!watchExpressions.Contains(expressionText, StringComparer.OrdinalIgnoreCase))
        {
            watchExpressions.Add(expressionText);
        }

        return new WatchOperationResult(true, true, "Watch saved; pause the debugger before evaluating it.",
            new DebugExpressionInfo(expressionText, null, null, false));
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

    public async Task<WatchListResult> ListWatchesAsync(WatchListRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            return new WatchListResult(false, "Pause the debugger before evaluating watches.", Array.Empty<DebugExpressionInfo>());
        return (await InspectFrameAsync(new NetVsMcp.Contracts.DebugFrameInspectionRequest
        { Watch = new NetVsMcp.Contracts.WatchListRequest { Expressions = request.Expressions } }, cancellationToken)).Watch!;
    }

    private static string[]? NormalizeWatchExpressions(string[]? expressions)
    {
        var normalized = expressions?
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Select(expression => expression.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized is { Length: > 0 } ? normalized : null;
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

        if (!string.IsNullOrWhiteSpace(request.Transport))
        {
            return await AttachRemoteAsync(debugger, request, cancellationToken);
        }

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
        try
        {
            var state = await ExecuteAsync(debugger, () => { ThreadHelper.ThrowIfNotOnUIThread(); process.Attach(); },
                waitForStop: false, newSession: true, cancellationToken);
            return new DebugAttachResult(true, null, DebuggedProcessInfo.FromProcess(process), state);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // Local attach commonly fails with a COMException (access denied, engine/bitness
            // mismatch, already attached to another debugger, etc.) - report it the same
            // structured way as the no-match/ambiguous-match cases above, matching AttachRemote's
            // existing try/catch below.
            return new DebugAttachResult(false, $"Failed to attach to process '{process.Name}' (id {process.ProcessID}): {ex.Message}", null);
        }

    }

    // Handles attaching over a non-local debugger transport (SSH/WSL/Docker/etc.), which Visual
    // Studio exposes via Debugger3.Transports rather than debugger.LocalProcesses. Support for a
    // given transport depends on which VS workloads (Linux, Container Tools, ...) are installed.
    private async Task<DebugAttachResult> AttachRemoteAsync(Debugger debugger, DebugAttachRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (debugger is not EnvDTE90.Debugger3 debugger3)
        {
            return new DebugAttachResult(false, "Remote debugger transports are not available in this Visual Studio version.", null);
        }

        EnvDTE80.Transport? transport = null;
        var availableTransports = new List<string>();
        foreach (EnvDTE80.Transport candidate in debugger3.Transports)
        {
            availableTransports.Add(candidate.Name);
            if (transport is null && AttachSelectors.MatchesTransportName(candidate.Name, request.Transport!))
            {
                transport = candidate;
            }
        }

        if (transport is null)
        {
            return new DebugAttachResult(
                false,
                $"No debugger transport matching '{request.Transport}' was found. Available transports: {string.Join(", ", availableTransports)}.",
                null);
        }

        EnvDTE.Processes remoteProcesses;
        try
        {
            remoteProcesses = debugger3.GetProcesses(transport, request.TransportQualifier ?? string.Empty);
        }
        catch (Exception ex)
        {
            return new DebugAttachResult(false, $"Failed to enumerate processes for transport '{transport.Name}': {ex.Message}", null);
        }

        var matches = new List<EnvDTE90.Process3>();
        foreach (EnvDTE90.Process3 candidate in remoteProcesses)
        {
            if (!AttachSelectors.MatchesProcessSelector(candidate.ProcessID, candidate.Name, request.ProcessId, request.ProcessName))
            {
                continue;
            }

            matches.Add(candidate);
        }

        if (matches.Count == 0)
        {
            return new DebugAttachResult(false, $"No matching process was found on transport '{transport.Name}'.", null);
        }

        if (matches.Count > 1)
        {
            return new DebugAttachResult(false, $"Process selector matched {matches.Count} processes on transport '{transport.Name}'; use processId.", null);
        }

        var process = matches[0];
        var state = await ExecuteAsync(debugger, () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!string.IsNullOrWhiteSpace(request.Engine)) process.Attach2(request.Engine);
            else process.Attach2(Type.Missing);
        }, waitForStop: false, newSession: true, cancellationToken);

        return new DebugAttachResult(true, null, DebuggedProcessInfo.FromProcess(process, transport.Name), state);
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
        var watches = await ListWatchesAsync(new WatchListRequest(), cancellationToken);
        return new ParallelWatchResult(watches.Supported, watches.Message, watches.Watches);
    }

    private async Task<DTE> GetDteAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        return await getDte()
            ?? throw new InvalidOperationException("Visual Studio DTE service is unavailable.");
    }

    private async Task<Debugger> GetDebuggerAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var dte = await GetDteAsync();
        var debugger = dte.Debugger
            ?? throw new InvalidOperationException("Visual Studio debugger service is unavailable.");
        if (executionEvents is null)
        {
            execution.Initialize(debugger.CurrentMode.ToString());
            eventDebugger = debugger;
            executionEvents = dte.Events.DebuggerEvents;
            executionEvents.OnEnterBreakMode += OnExecutionBreak;
            executionEvents.OnEnterRunMode += OnExecutionRun;
            executionEvents.OnEnterDesignMode += OnExecutionEnd;
            exceptionDebugger = Package.GetGlobalService(typeof(Microsoft.VisualStudio.Shell.Interop.SVsShellDebugger)) as Microsoft.VisualStudio.Shell.Interop.IVsDebugger;
            if (exceptionDebugger is not null)
            {
                exceptionObserver = new DebugExceptionEventObserver(execution, exceptions, breakpointErrors);
                try { Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(exceptionDebugger.AdviseDebugEventCallback(exceptionObserver)); }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    System.Diagnostics.Trace.WriteLine("NetVsMcp: exception observation unavailable: " + ex.Message);
                    exceptionObserver = null;
                }
            }
            PublishExecutionState(debugger.CurrentMode.ToString());
        }
        return debugger;
    }

    private void OnExecutionBreak(dbgEventReason reason, ref dbgExecutionAction action)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        RecordExecutionEvent("dbgBreakMode", DebuggerBreakReason.Read(dbgDebugMode.dbgBreakMode, () => reason));
        exceptions.Bind(execution.State(eventDebugger?.CurrentMode.ToString() ?? "dbgBreakMode"));
    }

    private void OnExecutionRun(dbgEventReason reason)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        exceptions.Clear();
        RecordExecutionEvent("dbgRunMode");
    }
    private void OnExecutionEnd(dbgEventReason reason)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        exceptions.Clear();
        RecordExecutionEvent("dbgDesignMode");
    }

    private void RecordExecutionEvent(string mode, string? reason = null)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var liveMode = eventDebugger?.CurrentMode.ToString() ?? mode;
            execution.Record(mode, reason, liveMode);
            PublishExecutionState(liveMode);
        }
        catch (System.Runtime.InteropServices.COMException) { /* Next status can still describe the live mode. */ }
    }

    private void PublishExecutionState(string liveMode)
    {
        var state = execution.State(liveMode);
        Events.Publish("debugger", new NetVsMcp.Contracts.DebuggerStateInfo(state.Mode,
            BreakReason: state.BreakReason, Outcome: state.Outcome, Generation: state.Generation,
            StopId: state.StopId, ExecutionSequence: state.ExecutionSequence));
    }

    internal VsSessionEventSnapshot CaptureEventSnapshot()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var debugger = eventDebugger ?? throw new InvalidOperationException("Debugger observation is not initialized.");
        var state = GetDebuggerState(debugger);
        var current = new NetVsMcp.Contracts.DebuggerStateInfo(state.Mode,
            BreakReason: state.BreakReason, Outcome: state.Outcome, Generation: state.Generation,
            StopId: state.StopId, ExecutionSequence: state.ExecutionSequence);
        // No asynchronous boundary between reading state and publishing its watermark.
        Events.Publish("snapshot", current);
        var batch = Events.Snapshot();
        return new(batch.SessionId, current, batch);
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (executionEvents is null) return;
        try
        {
            executionEvents.OnEnterBreakMode -= OnExecutionBreak;
            executionEvents.OnEnterRunMode -= OnExecutionRun;
            executionEvents.OnEnterDesignMode -= OnExecutionEnd;
        }
        catch (System.Runtime.InteropServices.COMException) { /* VS may already have disconnected the event source. */ }
        finally
        {
            try { if (exceptionObserver is not null) exceptionDebugger?.UnadviseDebugEventCallback(exceptionObserver); }
            catch (System.Runtime.InteropServices.COMException) { /* The debugger service may already be disconnected. */ }
            exceptions.Clear();
            exceptionObserver = null;
            exceptionDebugger = null;
            executionEvents = null;
            eventDebugger = null;
        }
    }

    private async Task<DebuggerStateInfo> ExecuteAsync(Debugger debugger, Action issue,
        bool waitForStop, bool newSession, CancellationToken cancellationToken)
    {
        long before;
        try
        {
            before = await IssueCommandAsync(debugger, issue, newSession, cancellationToken);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            throw new InvalidOperationException($"Debugger command was rejected (HRESULT 0x{ex.ErrorCode:X8}): {ex.Message}", ex);
        }
        var observed = await execution.WaitAsync(before, waitForStop, TimeSpan.FromSeconds(10), cancellationToken);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var state = GetDebuggerState(debugger);
        return observed ? state : execution.State(debugger.CurrentMode.ToString(), outcome: "timedOut");
    }

    private Task<long> IssueCommandAsync(Debugger debugger, Action issue, bool newSession,
        CancellationToken cancellationToken, Action? validateContext = null) =>
        DebugCommandExecution.IssueAsync(execution,
            async (ct, action) =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                action();
            },
            () => { ThreadHelper.ThrowIfNotOnUIThread(); return debugger.CurrentMode.ToString(); },
            issue, newSession, cancellationToken, validateContext);

    private DebuggerStateInfo GetDebuggerState(Debugger debugger, bool refresh = false)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var mode = debugger.CurrentMode;
        return execution.State(mode.ToString(), DebuggerBreakReason.Read(mode, () =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return debugger.LastBreakReason;
        }), refresh: refresh);
    }

    // Breakpoint CRUD and call-stack/locals access (DBG-2) return plain data DTOs with no
    // Success/Message field to report a failure through (unlike DebugAttachResult,
    // ImmediateExecuteResult, etc.), so - unlike ApplyHotReloadAsync/ExecuteImmediateAsync, which
    // swallow the COM exception into a result object - these instead catch it, add operation
    // context, and rethrow. The RPC dispatch boundary turns that into a clean, logged,
    // structured error instead of a raw COM fault crossing the wire.
    private static InvalidOperationException WrapComFailure(string operation, Exception ex) =>
        new($"Visual Studio debugger COM call failed while {operation}: {ex.Message}", ex);

    private static string ResolveDocumentPath(DTE dte, string documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return DocumentPathResolver.Resolve(dte, documentPath);
    }

    private static string? ResolveOptionalDocumentPath(DTE dte, string? documentPath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return DocumentPathResolver.ResolveOptional(dte, documentPath);
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
            if (!AttachSelectors.MatchesProcessSelector(process.ProcessID, process.Name, processId, processName))
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
