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
}
