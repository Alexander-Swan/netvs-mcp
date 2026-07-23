using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

internal sealed class BuildRpcTarget
{
    private readonly IBuildCapabilityService build;

    public BuildRpcTarget(IBuildCapabilityService build)
    {
        this.build = build;
    }

    public Task<BuildSolutionResult> BuildSolutionAsync(BuildSolutionRequest request, CancellationToken cancellationToken)
    {
        return build.BuildSolutionAsync(request, cancellationToken);
    }

    public Task<BuildStatusInfo> BuildStatusAsync(CancellationToken cancellationToken)
    {
        return build.GetBuildStatusAsync(cancellationToken);
    }

    public Task<BuildConfigurationInfo> BuildConfigurationGetAsync(CancellationToken cancellationToken)
    {
        return build.GetBuildConfigurationAsync(cancellationToken);
    }

    public Task<ErrorListResult> ErrorsListAsync(ErrorListRequest request, CancellationToken cancellationToken)
    {
        return build.ListErrorsAsync(request, cancellationToken);
    }

    public Task<OutputReadResult> OutputReadAsync(OutputReadRequest request, CancellationToken cancellationToken)
    {
        return build.ReadOutputAsync(request, cancellationToken);
    }

    public Task<OutputPaneListResult> OutputListPanesAsync(CancellationToken cancellationToken)
    {
        return build.ListOutputPanesAsync(cancellationToken);
    }

    public Task<OutputReadResult> OutputClearAsync(OutputPaneRequest request, CancellationToken cancellationToken)
    {
        return build.ClearOutputAsync(request, cancellationToken);
    }
}
