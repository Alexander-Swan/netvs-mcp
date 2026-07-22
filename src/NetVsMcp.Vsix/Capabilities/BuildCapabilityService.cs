using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace NetVsMcp.Vsix;

internal interface IBuildCapabilityService
{
    Task BuildSolutionAsync(CancellationToken cancellationToken);
    Task BuildProjectAsync(string projectName, CancellationToken cancellationToken);
    Task CancelBuildAsync(CancellationToken cancellationToken);
}

internal sealed class BuildCapabilityService : IBuildCapabilityService
{
    private readonly AsyncPackage package;

    public BuildCapabilityService(AsyncPackage package)
    {
        this.package = package;
    }

    public Task BuildSolutionAsync(CancellationToken cancellationToken)
    {
        _ = package;
        _ = cancellationToken;
        throw new System.NotImplementedException("Invoke VS build services and stream build status/output to the broker.");
    }

    public Task BuildProjectAsync(string projectName, CancellationToken cancellationToken)
    {
        _ = projectName;
        _ = cancellationToken;
        throw new System.NotImplementedException("Resolve the project in the active solution before invoking VS build services.");
    }

    public Task CancelBuildAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        throw new System.NotImplementedException("Cancel the active Visual Studio build operation.");
    }
}
