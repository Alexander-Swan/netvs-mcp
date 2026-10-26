using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    private readonly BreakpointErrorTracker breakpointErrors = new();

    public async Task<BreakpointDiagnosticsResult> GetBreakpointDiagnosticsAsync(BreakpointDiagnosticsRequest request, CancellationToken cancellationToken)
    {
        if (request.Offset < 0 || request.MaxItems is < 1 or > 100) throw new ArgumentException("offset must be nonnegative and maxItems must be 1–100.");
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var state = ReadRunToState(debugger);
        if (state.Mode == "unknown") throw new NotSupportedException("Current debugger state is unavailable; no binding assertion can be made.");
        var roots = debugger.Breakpoints;
        var total = roots.Count;
        if (request.Offset > total) throw new ArgumentException("offset exceeds the current breakpoint count.");
        var result = new List<BreakpointBindingEvidence>();
        var budget = 200;
        for (var i = request.Offset + 1; i <= total && result.Count < request.MaxItems; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { result.Add(ReadBreakpointBinding(roots.Item(i), state.Mode != "dbgDesignMode", ref budget)); }
            catch (COMException ex) { result.Add(new(null, null, 0, null, "unknown", null, null, Array.Empty<BoundBreakpointLocation>(), false, ex.Message)); }
        }
        var after = ReadRunToState(debugger);
        if (after.Generation != state.Generation || after.Mode != state.Mode || after.StopId != state.StopId)
            throw new StaleDebugContextException("Debugger context changed during breakpoint inspection. Read fresh diagnostics; no breakpoint was changed.");
        var errors = breakpointErrors.Read(state.Generation);
        return new(state, request.Offset, total, request.Offset + result.Count < total ? request.Offset + result.Count : null, result,
            exceptionObserver is not null, errors.Items, errors.Truncated,
            "Binding evidence is a current EnvDTE snapshot, not a promise of a future hit. Error notifications are bounded historical engine events from this generation; they are not correlated to individual roots or proof that a current breakpoint still has that error. Missing notifications do not prove error-free binding. Paging has no retained snapshot; refresh after breakpoint changes.");
    }

    private static BreakpointBindingEvidence ReadBreakpointBinding(Breakpoint root, bool debugging, ref int budget)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var enabled = root.Enabled;
        var type = root.Type;
        var locations = new List<BoundBreakpointLocation>();
        int? count = null;
        string? message = null;
        try
        {
            if (debugging)
            {
                var children = root.Children;
                count = type == dbgBreakpointType.dbgBreakpointTypeBound ? 1 : children?.Count ?? 0;
                var limit = Math.Min(20, budget);
                for (var i = 0; i < Math.Min(count.Value, limit); i++)
                {
                    var bound = type == dbgBreakpointType.dbgBreakpointTypeBound ? root : children!.Item(i + 1);
                    if (bound.Type != dbgBreakpointType.dbgBreakpointTypeBound)
                    { count = null; message = "A child no longer reports bound state; refresh binding evidence."; break; }
                    int? processId = null;
                    try { processId = (bound as EnvDTE90a.Breakpoint3)?.Process?.ProcessID; }
                    catch (COMException) { }
                    locations.Add(new(bound.File, bound.FileLine, bound.FileColumn, bound.FunctionName, processId));
                }
                budget -= locations.Count;
            }
        }
        catch (Exception ex) when (ex is COMException or NotImplementedException or NotSupportedException) { count = null; message = ex.Message; }
        var status = !debugging ? "notDebugging" : !enabled ? "disabled" : count is > 0 ? "bound" : count == 0 ? "pending" : "unknown";
        return new(root.Name, root.File, root.FileLine, enabled, status, type.ToString(), count, locations,
            count > locations.Count, message);
    }
}
