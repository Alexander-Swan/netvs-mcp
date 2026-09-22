using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix.Interfaces;

internal interface IBrokerProcessLauncher
{
    bool IsAvailable { get; }

    Task<bool> TryLaunchAsync(CancellationToken cancellationToken);
}
