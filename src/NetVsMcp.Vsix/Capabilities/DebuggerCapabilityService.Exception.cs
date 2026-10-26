using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Symbols;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    private readonly DebugExceptionTracker exceptions = new();
    private Microsoft.VisualStudio.Shell.Interop.IVsDebugger? exceptionDebugger;
    private DebugExceptionEventObserver? exceptionObserver;

    public async Task<DebugExceptionResult> GetExceptionAsync(DebugExceptionRequest request, CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.ExpectedStopId) || request.ExpectedGeneration < 0)
            throw new ArgumentException("Exception inspection requires the current stopId and generation.");
        var debugger = await GetDebuggerAsync();
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        var state = GetDebuggerState(debugger);
        var wireState = new NetVsMcp.Contracts.DebuggerStateInfo(state.Mode, BreakReason: state.BreakReason,
            Outcome: state.Outcome, Generation: state.Generation, StopId: state.StopId, ExecutionSequence: state.ExecutionSequence);
        if (state.BreakReason != "exception") return new(wireState, "noException", null, null, "The current pause is not an exception stop.");
        var evidence = exceptions.Read(state);
        if (evidence is null) return new(wireState, "unavailable", null, null,
            "The engine supplied no exception notification bound to this pause. Exception observation may have started after the stop; no previous exception or evaluated property is substituted.");
        cancellationToken.ThrowIfCancellationRequested();
        (bool Supported, DebugExceptionLocation? Value, string? Reason) location;
        try { location = OptionalDebuggerProbe.Read(() => ReadExceptionLocation(evidence)); }
        catch (System.Runtime.InteropServices.COMException ex)
        { location = (false, null, $"Source query failed (0x{ex.HResult:X8}): {ex.Message}"); }
        execution.Validate(debugger.CurrentMode.ToString(), request.ExpectedStopId, request.ExpectedGeneration);
        return new(wireState, "captured", evidence, location.Value,
            location.Reason ?? (location.Value is null ? "Source location is unavailable for the throwing thread's top frame." : null));
    }

    private static DebugExceptionLocation? ReadExceptionLocation(DebugExceptionEvidence exception)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (exception.ProcessId is null || exception.ThreadId is null) return null;
        var process = DkmProcess.GetProcesses().SingleOrDefault(p => !p.IsUnloaded && p.LivePart?.Id == exception.ProcessId);
        var thread = process?.GetThreads().SingleOrDefault(t => t.SystemPart?.Id == exception.ThreadId);
        var frame = thread?.GetTopStackFrame();
        var source = frame?.GetInstructionSymbol()?.GetSourcePosition(DkmSourcePositionFlags.None, null, out _);
        return source is null || string.IsNullOrWhiteSpace(source.DocumentName) ? null :
            new(source.DocumentName, checked((int)source.TextSpan.StartLine), checked((int)source.TextSpan.StartColumn));
    }

}
