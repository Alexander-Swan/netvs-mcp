using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NetVsMcp.Contracts;

namespace NetVsMcp.Vsix;

internal sealed partial class DebuggerCapabilityService
{
    public async Task<BreakpointGroupOperationResult> MutateBreakpointGroupAsync(
        BreakpointGroupMutationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName)) throw new ArgumentException("Group name is required.");
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var debugger = await GetDebuggerAsync();
        var group = request.GroupName.Trim();
        var handles = new BreakpointGroupHandles<Breakpoint>();
        foreach (Breakpoint breakpoint in debugger.Breakpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(BreakpointMetadata.FromBreakpoint(breakpoint).GroupName, group, StringComparison.OrdinalIgnoreCase))
                handles.Add(breakpoint, BreakpointMetadata.ReadOwnershipTag(breakpoint));
        }
        var updated = new List<NetVsMcp.Contracts.BreakpointInfo>();
        // No awaits or location/name re-query between membership validation and mutation.
        // A colocated caller breakpoint is never selected through an owned handle.
        var count = handles.Mutate(BreakpointMetadata.ReadOwnershipTag, breakpoint =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (request.Enabled is { } enabled)
            {
                breakpoint.Enabled = enabled;
                var row = BreakpointInfo.FromBreakpoint(breakpoint);
                updated.Add(new(row.Name, row.File, row.Line, row.Column, row.FunctionName, row.Condition, row.Enabled,
                    row.Action, row.ActionMessage, row.ContinueAfterAction, row.HitCount, row.HitCountType, row.DependsOnBreakpointName, row.GroupName));
            }
            else breakpoint.Delete();
        }, cancellationToken);
        return new(group, handles.Count, count, updated);
    }
}
