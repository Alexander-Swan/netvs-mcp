using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal interface INavigationCapabilityService
{
    Task GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task ListDocumentSymbolsAsync(string documentPath, CancellationToken cancellationToken);
}

internal sealed class NavigationCapabilityService : INavigationCapabilityService
{
    private readonly AsyncPackage package;

    public NavigationCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public Task GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        _ = package;
        _ = documentPath;
        _ = line;
        _ = column;
        _ = cancellationToken;
        throw new System.NotImplementedException("Use Visual Studio's live workspace/language services instead of standalone Roslyn parsing.");
    }

    public Task FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken)
    {
        _ = documentPath;
        _ = line;
        _ = column;
        _ = cancellationToken;
        throw new System.NotImplementedException("Use VS Find All References APIs or Roslyn from the VisualStudioWorkspace.");
    }

    public Task ListDocumentSymbolsAsync(string documentPath, CancellationToken cancellationToken)
    {
        _ = documentPath;
        _ = cancellationToken;
        throw new System.NotImplementedException("Return live document symbols from the VS workspace once shared DTOs exist.");
    }
}
