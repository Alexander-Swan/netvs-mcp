using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    public async Task<DebugRunToResult> RunToAsync(DebugRunToRequest request, CancellationToken cancellationToken)
    {
        request.Validate();
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        var dte = await GetDteAsync();
        var solution = dte.Solution?.FullName;
        var file = ResolveDocumentPath(dte, request.DocumentPath);
        if (!File.Exists(file)) throw new ArgumentException("The run-to source file does not exist.");
        if (debugger.DebuggedProcesses.Count != 1 || debugger.CurrentProcess?.ProcessID != request.ExpectedProcessId)
            throw new ArgumentException("Run-to currently requires exactly one debugged process matching expectedProcessId.");
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        var owner = Guid.NewGuid().ToString("N");
        var group = "run-to-" + owner;
        try
        {
            var current = OptionalDebuggerProbe.Read(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return ReadThreadSourcePosition(request.ExpectedProcessId, debugger.CurrentThread?.ID);
            }).Value;
            if (current.HasValue && MatchesRunToLocation(current.Value.File, current.Value.Line, current.Value.Column, file, request))
            {
                execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
                Events.Publish("runTo.accepted", operationId: owner);
                Events.Publish("runTo.completed", operationId: owner);
                return new(ReadRunToState(debugger), "alreadyAtLocation", owner, group, file, request.Line, request.Column,
                    current.Value.File, current.Value.Line, current.Value.Column, new(0, 0, 0, Array.Empty<string>(), true),
                    "The debugger is already at the requested source location; no execution command was sent.",
                    request.ExpectedProcessId, request.ExpectedGeneration, request.ExpectedStopId, BreakpointMode: "none");
            }
        }
        catch (COMException) { /* Current source is optional; a fresh owned hit can still verify the target. */ }
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        if (debugger.Breakpoints.Count > 1000) throw new NotSupportedException("Run-to ownership verification is limited to 1000 existing breakpoint roots.");
        // Keep old roots alive so an IUnknown identity cannot be recycled during setup.
        var existingRoots = debugger.Breakpoints.Cast<Breakpoint>().ToArray();
        var existing = new HashSet<IntPtr>(existingRoots.Select(BreakpointIdentity));
        var atTarget = existingRoots.Where(bp =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return MatchesRunToLocation(bp.File, bp.FileLine, bp.FileColumn, file, request);
        }).ToArray();
        var borrowed = atTarget.FirstOrDefault(bp =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return CanBorrowForRunTo(bp, request.ExpectedProcessId);
        });
        if (atTarget.Length > 0 && borrowed is null)
            throw new NotSupportedException("The target already has a disabled, conditional, counted, or differently filtered breakpoint. Existing settings will not be changed; choose another location or inspect that breakpoint first.");
        var tag = "NetVsMcp:" + JsonSerializer.Serialize(new BreakpointMetadata { GroupName = group });
        var scope = new OwnedBreakpointScope<Breakpoint>();
        var roots = new HashSet<IntPtr>();
        var createdRoots = new List<Breakpoint>();
        var fullyObserved = true;
        var issued = false;
        var outcome = "setupFailed";
        string? message = null, hitFile = null;
        int? hitLine = null, hitColumn = null;
        TemporaryBreakpointCleanup cleanup;
        Events.Publish("runTo.accepted", operationId: owner);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (borrowed is null)
            {
                fullyObserved = false;
                var added = debugger.Breakpoints.Add(File: file, Line: request.Line, Column: request.Column);
                for (var i = 1; i <= added.Count; i++)
                {
                    var root = added.Item(i);
                    var identity = BreakpointIdentity(root);
                    if (existing.Contains(identity)) throw new InvalidOperationException("VS returned an existing breakpoint; it will not be changed or deleted.");
                    if (!roots.Add(identity)) continue;
                    createdRoots.Add(root);
                    scope.Add(root, root.Tag, tag);
                    root.Tag = tag;
                    if (root.Tag != tag) throw new NotSupportedException("VS did not preserve the temporary breakpoint's ownership tag.");
                    scope.ConfirmTag(root);
                    if (root is not EnvDTE80.Breakpoint2 filtered) throw new NotSupportedException("Run-to requires native process-filtered breakpoints.");
                    var filter = "ProcessID == " + request.ExpectedProcessId;
                    filtered.FilterBy = filter;
                    if (!string.Equals(RemoveWhitespace(filtered.FilterBy), RemoveWhitespace(filter), StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException("The engine did not preserve the exact process filter; execution was not resumed.");
                }
                fullyObserved = true;
                if (roots.Count == 0) throw new InvalidOperationException("VS did not create a temporary breakpoint.");
            }
            else if (!CanBorrowForRunTo(borrowed, request.ExpectedProcessId))
                throw new StaleDebugContextException("The existing target breakpoint changed before execution.");
            execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
            var before = await IssueCommandAsync(debugger, () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                issued = true; // A throwing COM call may still have resumed the target.
                debugger.Go(WaitForBreakOrEnd: false);
            }, newSession: false, cancellationToken, validateContext: () =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (!string.Equals(solution, dte.Solution?.FullName, StringComparison.OrdinalIgnoreCase) ||
                    debugger.DebuggedProcesses.Count != 1 || debugger.CurrentProcess?.ProcessID != request.ExpectedProcessId)
                    throw new StaleDebugContextException("The selected solution or process changed before run-to execution.");
                if (borrowed is not null && (!CanBorrowForRunTo(borrowed, request.ExpectedProcessId) ||
                    !MatchesRunToLocation(borrowed.File, borrowed.FileLine, borrowed.FileColumn, file, request)))
                    throw new StaleDebugContextException("The existing target breakpoint changed before execution.");
                foreach (var root in createdRoots)
                    if (root.Tag != tag || !CanBorrowForRunTo(root, request.ExpectedProcessId) ||
                        !MatchesRunToLocation(root.File, root.FileLine, root.FileColumn, file, request))
                        throw new StaleDebugContextException("A temporary run-to breakpoint changed before execution.");
            });
            var observed = await execution.WaitAsync(before, true, TimeSpan.FromSeconds(request.TimeoutSeconds), cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
            var state = GetDebuggerState(debugger);
            if (state.Generation != request.ExpectedGeneration || !string.Equals(solution, dte.Solution?.FullName, StringComparison.OrdinalIgnoreCase) ||
                (state.Mode != "dbgDesignMode" && debugger.DebuggedProcesses.Count != 1)) outcome = "contextChanged";
            else if (state.Mode == "dbgDesignMode") outcome = "exited";
            else if (!observed || state.Mode != "dbgBreakMode") outcome = "timedOut";
            else
            {
                outcome = "stoppedElsewhere";
                if (debugger.CurrentProcess?.ProcessID == request.ExpectedProcessId && state.BreakReason == "breakpoint")
                {
                    if (borrowed is not null)
                    {
                        var position = OptionalDebuggerProbe.Read(() =>
                        {
                            ThreadHelper.ThrowIfNotOnUIThread();
                            return ReadThreadSourcePosition(request.ExpectedProcessId, debugger.CurrentThread?.ID);
                        });
                        if (position.Value.HasValue)
                        {
                            hitFile = position.Value.Value.File; hitLine = position.Value.Value.Line; hitColumn = position.Value.Value.Column;
                            outcome = MatchesRunToLocation(hitFile, hitLine.Value, hitColumn.Value, file, request) ? "reachedLocation" : "stoppedElsewhere";
                        }
                        else { outcome = "outcomeUnknown"; message = position.Reason ?? "The existing breakpoint was reused without modification, but the actual stopped source position is unavailable."; }
                    }
                    else
                    {
                        foreach (Breakpoint hit in debugger.AllBreakpointsLastHit)
                        {
                            if (!IsOwnedBreakpointHit(hit, roots)) continue;
                            hitFile = hit.File; hitLine = hit.FileLine; hitColumn = hit.FileColumn;
                            outcome = !string.IsNullOrWhiteSpace(hitFile) && string.Equals(Path.GetFullPath(hitFile), file, StringComparison.OrdinalIgnoreCase) && hitLine == request.Line &&
                                (request.Column == 1 || hitColumn == request.Column)
                                ? "reachedLocation" : "boundAtDifferentLocation";
                            break;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { outcome = "waitCanceled"; message = "The wait was canceled; the target was not stopped. Reconcile current debugger state before another execution command."; }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or ArgumentException or NotSupportedException)
        { outcome = issued ? "outcomeUnknown" : "setupFailed"; message = ex.Message; }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
            cleanup = scope.Cleanup(bp => { ThreadHelper.ThrowIfNotOnUIThread(); return bp.Tag; },
                bp => { ThreadHelper.ThrowIfNotOnUIThread(); bp.Delete(); }, fullyObserved);
            GC.KeepAlive(existingRoots);
            Events.Publish("runTo.completed", operationId: owner);
        }
        if (!cleanup.Complete && message is null) message = "Temporary breakpoint cleanup is incomplete. Inspect the cleanup evidence and preserve developer changes; do not repeat execution to clean up artifacts.";
        return new(ReadRunToState(debugger),
            outcome, owner, group, file, request.Line, request.Column, hitFile, hitLine, hitColumn, cleanup, message,
            request.ExpectedProcessId, request.ExpectedGeneration, request.ExpectedStopId,
            BreakpointMode: borrowed is null ? "temporaryOwned" : "existingCallerBreakpoint");
    }

    private static string RemoveWhitespace(string value) => new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
    private static bool MatchesRunToLocation(string? hitFile, int hitLine, int hitColumn, string file, DebugRunToRequest request) =>
        !string.IsNullOrWhiteSpace(hitFile) && string.Equals(Path.GetFullPath(hitFile), file, StringComparison.OrdinalIgnoreCase) &&
        hitLine == request.Line && (request.Column == 1 || hitColumn == request.Column);

    private static bool CanBorrowForRunTo(Breakpoint breakpoint, int processId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return breakpoint is EnvDTE80.Breakpoint2 stopping && ExistingRunToBreakpointPolicy.CanReuse(breakpoint.Enabled,
            stopping.BreakWhenHit, !string.IsNullOrWhiteSpace(breakpoint.Condition),
            breakpoint.HitCountType != dbgHitCountType.dbgHitCountTypeNone, stopping.FilterBy, processId);
    }
    private static IntPtr BreakpointIdentity(Breakpoint breakpoint)
    {
        var identity = Marshal.GetIUnknownForObject(breakpoint);
        try { return identity; } finally { Marshal.Release(identity); }
    }
    private static bool IsOwnedBreakpointHit(Breakpoint hit, HashSet<IntPtr> roots)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var seen = new HashSet<IntPtr>();
        for (var depth = 0; hit is not null && depth < 8; depth++, hit = hit.Parent)
        {
            var identity = BreakpointIdentity(hit);
            if (!seen.Add(identity)) break;
            if (roots.Contains(identity)) return true;
        }
        return false;
    }

    private NetVsMcp.Contracts.DebuggerStateInfo ReadRunToState(Debugger debugger)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var state = GetDebuggerState(debugger);
            return new(state.Mode, BreakReason: state.BreakReason, Outcome: state.Outcome,
                Generation: state.Generation, StopId: state.StopId, ExecutionSequence: state.ExecutionSequence);
        }
        catch (COMException) { return new("unknown", UnderlyingModeIsStale: true, Outcome: "unknown"); }
    }
}
