using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class GeneralIdeRpcTarget
{
    private readonly IGeneralIdeCapabilityService generalIde;

    public GeneralIdeRpcTarget(IGeneralIdeCapabilityService generalIde)
    {
        this.generalIde = generalIde;
    }

    public Task<ExecuteCommandResult> ExecuteCommandAsync(ExecuteCommandRequest request, CancellationToken cancellationToken)
    {
        return generalIde.ExecuteCommandAsync(request, cancellationToken);
    }

    public Task<WindowListResult> WindowListAsync(CancellationToken cancellationToken)
    {
        return generalIde.WindowListAsync(cancellationToken);
    }

    public Task<WindowActivateResult> WindowActivateAsync(WindowActivateRequest request, CancellationToken cancellationToken)
    {
        return generalIde.WindowActivateAsync(request, cancellationToken);
    }

    public Task<ToolWindowResult> ToolWindowShowAsync(ToolWindowRequest request, CancellationToken cancellationToken)
    {
        return generalIde.ToolWindowShowAsync(request, cancellationToken);
    }

    public Task<ToolWindowResult> ToolWindowHideAsync(ToolWindowRequest request, CancellationToken cancellationToken)
    {
        return generalIde.ToolWindowHideAsync(request, cancellationToken);
    }
}
