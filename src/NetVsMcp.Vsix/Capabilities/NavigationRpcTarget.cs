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

    public Task<GoToDefinitionResult> CodeGoToDefinitionAsync(CodePositionRequest request, CancellationToken cancellationToken)
    {
        return navigation.GoToDefinitionAsync(request.DocumentPath, request.Line, request.Column, cancellationToken);
    }

    public Task<FindReferencesResult> CodeFindReferencesAsync(CodePositionRequest request, CancellationToken cancellationToken)
    {
        return navigation.FindReferencesAsync(request.DocumentPath, request.Line, request.Column, cancellationToken);
    }

    public Task<FindImplementationsResult> CodeFindImplementationsAsync(CodePositionRequest request, CancellationToken cancellationToken)
    {
        return navigation.FindImplementationsAsync(request.DocumentPath, request.Line, request.Column, cancellationToken);
    }

    public Task<RenameSymbolPreviewResult> CodeRenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken)
    {
        return navigation.RenameSymbolPreviewAsync(request, cancellationToken);
    }
}
