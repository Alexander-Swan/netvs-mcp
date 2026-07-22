using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class NavigationRpcTarget
{
    private readonly INavigationCapabilityService navigation;

    public NavigationRpcTarget(INavigationCapabilityService navigation)
    {
        this.navigation = navigation;
    }

    public Task<DocumentSymbolsResult> CodeDocumentSymbolsAsync(DocumentSymbolsRequest request, CancellationToken cancellationToken)
    {
        return navigation.ListDocumentSymbolsAsync(request.DocumentPath, cancellationToken);
    }
}
