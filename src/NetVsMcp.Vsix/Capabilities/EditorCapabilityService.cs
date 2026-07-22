using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal interface IEditorCapabilityService
{
    Task<string?> GetActiveDocumentAsync(CancellationToken cancellationToken);
    Task<string> ReadDocumentAsync(string path, CancellationToken cancellationToken);
    Task OpenDocumentAsync(string path, CancellationToken cancellationToken);
    Task<string?> GetSelectionAsync(CancellationToken cancellationToken);
}

internal sealed class EditorCapabilityService : IEditorCapabilityService
{
    private readonly AsyncPackage package;

    public EditorCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public Task<string?> GetActiveDocumentAsync(CancellationToken cancellationToken)
    {
        _ = package;
        _ = cancellationToken;
        throw new System.NotImplementedException("VS editor service implementation belongs behind the broker RPC command handlers.");
    }

    public Task<string> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        _ = path;
        _ = cancellationToken;
        throw new System.NotImplementedException("Read from live VS text buffers when available, falling back to disk only when the document is closed.");
    }

    public Task OpenDocumentAsync(string path, CancellationToken cancellationToken)
    {
        _ = path;
        _ = cancellationToken;
        throw new System.NotImplementedException("Open via IVsUIShellOpenDocument or DTE once broker command contracts land.");
    }

    public Task<string?> GetSelectionAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Selection should be read from the active IWpfTextView/TextSelection.");
    }
}
