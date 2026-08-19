using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class CodeActionsRpcTarget
{
    private readonly ICodeActionsCapabilityService codeActions;

    public CodeActionsRpcTarget(ICodeActionsCapabilityService codeActions)
    {
        this.codeActions = codeActions;
    }

    public Task<CodeActionsListResult> CodeActionsListAsync(CodeActionsListRequest request, CancellationToken cancellationToken)
    {
        return codeActions.ListCodeActionsAsync(request, cancellationToken);
    }

    public Task<CodeActionsApplyResult> CodeActionsApplyAsync(CodeActionsApplyRequest request, CancellationToken cancellationToken)
    {
        return codeActions.ApplyCodeActionAsync(request, cancellationToken);
    }
}
